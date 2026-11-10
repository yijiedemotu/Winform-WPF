using EnterpriseDemo.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseDemo.Application;

/// <summary>
/// 采集服务：只负责「读设备 → 标定 → 判定 → 发布」，完全不引用任何 UI 框架。
/// 这是本仓库最重要的设计点：WinForms 与 WPF 两套项目共用这个文件的逐字节副本。
/// </summary>
public sealed class AcquisitionService : BackgroundService
{
    private readonly IModbusClient _client;
    private readonly ICalibrationService _calibration;
    private readonly ILogger<AcquisitionService> _logger;
    private readonly TimeSpan _interval;

    /// <summary>每批数据就绪时发布。订阅方负责把数据封送回自己的 UI 线程。</summary>
    public event Action<IReadOnlyList<ChannelReading>>? BatchArrived;

    public AcquisitionService(
        IModbusClient client,
        ICalibrationService calibration,
        IOptions<AppOptions> options,
        ILogger<AcquisitionService> logger)
    {
        _client = client;
        _calibration = calibration;
        _logger = logger;
        _interval = TimeSpan.FromMilliseconds(Math.Max(50, options.Value.SampleIntervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _client.ConnectAsync(stoppingToken);
            using var timer = new PeriodicTimer(_interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var rawValues = await _client.ReadChannelsAsync(stoppingToken);
                    var now = DateTime.Now;
                    var batch = new List<ChannelReading>(_calibration.ChannelCount);

                    for (int i = 0; i < _calibration.ChannelCount && i < rawValues.Length; i++)
                    {
                        double value = _calibration.Convert(i, rawValues[i]);
                        var (low, high) = _calibration.LimitsOf(i);
                        bool qualified = value >= low && value <= high;
                        batch.Add(new ChannelReading(i + 1, rawValues[i], value, qualified, now));
                    }

                    BatchArrived?.Invoke(batch);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 不吞异常：记录后继续下一周期，避免一次抖动让整个服务停摆。
                    _logger.LogError(ex, "本周期采集失败，将在 {Interval} 后重试", _interval);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "采集服务启动失败");
        }
        finally
        {
            _logger.LogInformation("采集服务已停止");
        }
    }
}
