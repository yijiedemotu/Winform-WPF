using EnterpriseDemo.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseDemo.Infrastructure;

/// <summary>
/// 两点线性标定：把传感器原始计数 [RawLow, RawHigh] 映射到真实尺寸 [ValueLow, ValueHigh]。
/// y = ValueLow + (x - RawLow) * (ValueHigh - ValueLow) / (RawHigh - RawLow)
/// </summary>
public sealed class CalibrationService : ICalibrationService
{
    private readonly IReadOnlyList<ChannelConfig> _channels;

    public CalibrationService(
        IOptions<AppOptions> options,
        ILogger<CalibrationService> logger)
    {
        var cfg = options.Value;
        if (cfg.Channels is null || cfg.Channels.Count != cfg.ChannelCount)
        {
            throw new InvalidOperationException(
                $"配置中的通道数 {cfg.Channels?.Count ?? 0} 与 ChannelCount {cfg.ChannelCount} 不一致");
        }

        foreach (var (channel, index) in cfg.Channels.Select((c, i) => (c, i)))
        {
            Validate(channel, index + 1);
        }

        _channels = cfg.Channels.ToArray();
        logger.LogInformation("标定参数加载完成，共 {Count} 个通道", _channels.Count);
    }

    public int ChannelCount => _channels.Count;

    public double Convert(int channel, ushort raw)
    {
        var c = _channels[channel];
        double slope = (c.ValueHigh - c.ValueLow) / (c.RawHigh - c.RawLow);
        return c.ValueLow + (raw - c.RawLow) * slope;
    }

    public (double Low, double High) LimitsOf(int channel)
        => (_channels[channel].LimitLow, _channels[channel].LimitHigh);

    private static void Validate(ChannelConfig c, int number)
    {
        if (c.RawHigh <= c.RawLow)
            throw new InvalidOperationException($"通道{number}: RawHigh 必须大于 RawLow");
        if (c.ValueHigh <= c.ValueLow)
            throw new InvalidOperationException($"通道{number}: ValueHigh 必须大于 ValueLow");
        if (c.LimitHigh < c.LimitLow)
            throw new InvalidOperationException($"通道{number}: LimitHigh 不能小于 LimitLow");
    }
}
