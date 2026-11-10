using EnterpriseDemo.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseDemo.Infrastructure;

/// <summary>
/// IModbusClient 的教学用实现：不接真串口，用正弦 + 随机噪声生成 8 路原始值。
/// 接真实设备时新增一个 NModbusModbusClient 实现同一个接口，在组合根改一行注册即可。
/// </summary>
public sealed class SimulatedModbusClient : IModbusClient
{
    private readonly ILogger<SimulatedModbusClient> _logger;
    private readonly AppOptions _options;
    private readonly Random _random = new();
    private bool _connected;

    public SimulatedModbusClient(
        IOptions<AppOptions> options,
        ILogger<SimulatedModbusClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConnected => _connected;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _connected = true;
        _logger.LogInformation("模拟 Modbus 设备已连接，通道数 {Count}", _options.ChannelCount);
        return Task.CompletedTask;
    }

    public Task<ushort[]> ReadChannelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_connected)
        {
            throw new InvalidOperationException("设备未连接");
        }

        // 用第一个通道的量程作为模拟信号范围，其余通道共用同一批原始值，
        // 但因各通道标定参数不同，会得到不同的工程值与判定结果。
        var first = _options.Channels.Count > 0
            ? _options.Channels[0]
            : new ChannelConfig { RawLow = 0, RawHigh = 1000 };

        double low = first.RawLow;
        double span = Math.Max(1, first.RawHigh - first.RawLow);
        double mid = low + span / 2;
        double phase = DateTime.Now.TimeOfDay.TotalSeconds;

        var values = new ushort[_options.ChannelCount];
        for (int i = 0; i < values.Length; i++)
        {
            double wave = Math.Sin(phase * 0.6 + i * 0.35) * span * 0.45;
            double noise = (_random.NextDouble() - 0.5) * span * 0.06;
            double raw = mid + wave + noise;
            values[i] = (ushort)Math.Clamp(raw, ushort.MinValue, ushort.MaxValue);
        }

        return Task.FromResult(values);
    }

    public void Dispose()
    {
        if (_connected)
        {
            _connected = false;
            _logger.LogInformation("模拟 Modbus 设备已断开");
        }
    }
}
