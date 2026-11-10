using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseDemo.Application;
using EnterpriseDemo.Core;
using Microsoft.Extensions.Logging;

namespace WpfDemo.ViewModels;

/// <summary>
/// MVVM 的 VM：持有状态 + 暴露命令，不引用任何 Window/控件，也不含业务规则。
/// 与 WinForms 版 MainForm 对照：那里用 BindingList + 事件处理，这里用
/// ObservableCollection + ICommand + 数据绑定，职责一一对应。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AcquisitionService _acquisition;
    private readonly IMeasurementRepository _repository;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SynchronizationContext _uiContext;

    [ObservableProperty] private string _projectName = "DEMO-001";
    [ObservableProperty] private string _status = "采集中…";

    public ObservableCollection<ChannelItemViewModel> Channels { get; } = new();
    public ObservableCollection<SampleItemViewModel> History { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    private IReadOnlyList<ChannelReading> _latest = Array.Empty<ChannelReading>();

    public MainViewModel(
        AcquisitionService acquisition,
        IMeasurementRepository repository,
        ILogger<MainViewModel> logger)
    {
        _acquisition = acquisition;
        _repository = repository;
        _logger = logger;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        _acquisition.BatchArrived += OnBatchArrived;
        _ = LoadHistoryAsync();
    }

    /// <summary>采集线程回调：只做封送，绝不直接碰 ObservableCollection。</summary>
    private void OnBatchArrived(IReadOnlyList<ChannelReading> batch)
    {
        _latest = batch.ToArray();
        _uiContext.Post(_ => Render(batch), null);
    }

    private void Render(IReadOnlyList<ChannelReading> batch)
    {
        if (Channels.Count != batch.Count)
        {
            Channels.Clear();
            foreach (var _ in batch)
            {
                Channels.Add(new ChannelItemViewModel());
            }
        }

        for (int i = 0; i < batch.Count; i++)
        {
            var item = Channels[i];
            var reading = batch[i];
            item.Channel = reading.Channel;
            item.Raw = reading.Raw;
            item.Value = Math.Round(reading.Value, 3);
            item.Result = reading.IsQualified ? "合格" : "不合格";
        }

        Status = $"最近刷新 {DateTime.Now:HH:mm:ss} · 共 {batch.Count} 通道";
    }

    private async Task SaveAsync()
    {
        if (_latest.Count == 0)
        {
            Status = "暂无数据可保存";
            return;
        }

        try
        {
            var record = new SampleRecord(
                DateTime.Now,
                ProjectName.Trim(),
                _latest.Select(x => x.Value).ToArray());

            await _repository.AddAsync(record);
            await LoadHistoryAsync();
            Status = $"已保存 {record.Time:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存检测记录失败");
            Status = "保存失败：" + ex.Message;
        }
    }

    private Task RefreshAsync() => LoadHistoryAsync();

    private async Task LoadHistoryAsync()
    {
        var records = await _repository.QueryAsync(null, null, null);
        History.Clear();
        foreach (var record in records)
        {
            History.Add(new SampleItemViewModel
            {
                Time = record.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Project = record.ProjectName,
                Ch1 = record.Values.Length > 0 ? Math.Round(record.Values[0], 3) : 0,
                Ch2 = record.Values.Length > 1 ? Math.Round(record.Values[1], 3) : 0,
                Average = record.Values.Length > 0 ? Math.Round(record.Values.Average(), 3) : 0
            });
        }
    }

    /// <summary>窗口关闭时取消订阅，避免后台服务持有已销毁的 ViewModel。</summary>
    public void Unsubscribe() => _acquisition.BatchArrived -= OnBatchArrived;
}

/// <summary>单行通道数据。ObservableObject 让属性变化自动刷新 DataGrid。</summary>
public sealed partial class ChannelItemViewModel : ObservableObject
{
    [ObservableProperty] private int _channel;
    [ObservableProperty] private ushort _raw;
    [ObservableProperty] private double _value;
    [ObservableProperty] private string _result = "";
}

/// <summary>历史记录行，整体替换集合即可，无需逐属性通知。</summary>
public sealed class SampleItemViewModel
{
    public string Time { get; init; } = "";
    public string Project { get; init; } = "";
    public double Ch1 { get; init; }
    public double Ch2 { get; init; }
    public double Average { get; init; }
}
