using System.ComponentModel;
using EnterpriseDemo.Application;
using EnterpriseDemo.Core;
using Microsoft.Extensions.Logging;

namespace WinformDemo.Presentation;

/// <summary>
/// 表现层：只做「把服务给的数据画出来」，不含业务规则、不直接碰硬件、不直接读写文件。
/// 精简骨架用代码构建界面，避免 Designer 文件淹没重点。
/// </summary>
public sealed class MainForm : Form
{
    private readonly AcquisitionService _acquisition;
    private readonly IMeasurementRepository _repository;
    private readonly ILogger<MainForm> _logger;
    private readonly SynchronizationContext _uiContext;

    private readonly BindingList<ChannelRow> _liveRows = new();
    private readonly BindingList<SampleRow> _historyRows = new();
    private readonly TextBox _projectBox = new() { Width = 160, Text = "DEMO-001" };
    private readonly Label _status = new() { AutoSize = true, Text = "采集中…" };

    private IReadOnlyList<ChannelReading> _latest = Array.Empty<ChannelReading>();

    public MainForm(
        AcquisitionService acquisition,
        IMeasurementRepository repository,
        ILogger<MainForm> logger)
    {
        _acquisition = acquisition;
        _repository = repository;
        _logger = logger;

        BuildUi();

        // 控件创建后 WinForms 已安装 WindowsFormsSynchronizationContext。
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _acquisition.BatchArrived += OnBatchArrived;

        _ = LoadHistoryAsync();
    }

    private void BuildUi()
    {
        Text = "WinForms 企业级骨架 · 八通道测量";
        Width = 900;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;

        var saveButton = new Button { Text = "保存数据", Width = 100 };
        saveButton.Click += OnSaveClick;

        var refreshButton = new Button { Text = "刷新历史", Width = 100 };
        refreshButton.Click += (_, _) => _ = LoadHistoryAsync();

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 10, 8, 0)
        };
        toolbar.Controls.AddRange(new Control[]
        {
            new Label { Text = "产品名称：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
            _projectBox,
            saveButton,
            refreshButton,
            new Label { Text = "  ", AutoSize = true },
            _status
        });

        var liveGrid = CreateGrid(_liveRows);
        var historyGrid = CreateGrid(_historyRows);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(liveGrid, 0, 1);
        layout.Controls.Add(historyGrid, 0, 2);

        Controls.Add(layout);
    }

    private static DataGridView CreateGrid<T>(BindingList<T> source) => new()
    {
        Dock = DockStyle.Fill,
        DataSource = source,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false,
        ReadOnly = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };

    /// <summary>在采集线程被调用：只做封送，不碰控件。</summary>
    private void OnBatchArrived(IReadOnlyList<ChannelReading> batch)
    {
        _latest = batch.ToArray();
        _uiContext.Post(_ => RenderBatch(batch), null);
    }

    /// <summary>只运行在 UI 线程。</summary>
    private void RenderBatch(IReadOnlyList<ChannelReading> batch)
    {
        if (_liveRows.Count != batch.Count)
        {
            _liveRows.Clear();
            foreach (var _ in batch)
            {
                _liveRows.Add(new ChannelRow());
            }
        }

        for (int i = 0; i < batch.Count; i++)
        {
            var row = _liveRows[i];
            var reading = batch[i];
            row.Channel = reading.Channel;
            row.Raw = reading.Raw;
            row.Value = Math.Round(reading.Value, 3);
            row.Result = reading.IsQualified ? "合格" : "不合格";
        }

        _liveRows.ResetBindings();
        _status.Text = $"最近刷新 {DateTime.Now:HH:mm:ss} · 共 {batch.Count} 通道";
    }

    private async void OnSaveClick(object? sender, EventArgs e)
    {
        if (_latest.Count == 0)
        {
            _status.Text = "暂无数据可保存";
            return;
        }

        try
        {
            var record = new SampleRecord(
                DateTime.Now,
                _projectBox.Text.Trim(),
                _latest.Select(x => x.Value).ToArray());

            await _repository.AddAsync(record);
            await LoadHistoryAsync();
            _status.Text = $"已保存 {record.Time:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存检测记录失败");
            _status.Text = "保存失败：" + ex.Message;
        }
    }

    private async Task LoadHistoryAsync()
    {
        var records = await _repository.QueryAsync(null, null, null);
        _historyRows.Clear();
        foreach (var record in records)
        {
            var row = new SampleRow
            {
                Time = record.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Project = record.ProjectName,
                Ch1 = record.Values.Length > 0 ? Math.Round(record.Values[0], 3) : 0,
                Ch2 = record.Values.Length > 1 ? Math.Round(record.Values[1], 3) : 0,
                Average = record.Values.Length > 0 ? Math.Round(record.Values.Average(), 3) : 0
            };
            _historyRows.Add(row);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _acquisition.BatchArrived -= OnBatchArrived;
        base.OnFormClosed(e);
    }

    public sealed class ChannelRow
    {
        [DisplayName("通道")] public int Channel { get; set; }
        [DisplayName("原始值")] public ushort Raw { get; set; }
        [DisplayName("工程值 / mm")] public double Value { get; set; }
        [DisplayName("判定")] public string Result { get; set; } = "";
    }

    public sealed class SampleRow
    {
        [DisplayName("检测时间")] public string Time { get; set; } = "";
        [DisplayName("产品名称")] public string Project { get; set; } = "";
        [DisplayName("通道1")] public double Ch1 { get; set; }
        [DisplayName("通道2")] public double Ch2 { get; set; }
        [DisplayName("平均值")] public double Average { get; set; }
    }
}
