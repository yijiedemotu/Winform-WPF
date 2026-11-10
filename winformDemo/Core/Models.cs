namespace EnterpriseDemo.Core;

/// <summary>
/// 单个通道的标定与判定参数。
/// RawLow/RawHigh 是传感器原始读数区间，ValueLow/ValueHigh 是对应的真实尺寸（mm）。
/// LimitLow/LimitHigh 是合格判定的上下限。
/// </summary>
public sealed class ChannelConfig
{
    public double RawLow { get; init; }
    public double RawHigh { get; init; }
    public double ValueLow { get; init; }
    public double ValueHigh { get; init; }
    public double LimitLow { get; init; }
    public double LimitHigh { get; init; }
}

/// <summary>来自 appsettings.json 的强类型配置对象（Options 模式）。</summary>
public sealed class AppOptions
{
    public int ChannelCount { get; set; } = 8;
    public int SampleIntervalMs { get; set; } = 500;
    public string StoragePath { get; set; } = "samples.json";
    public List<ChannelConfig> Channels { get; set; } = new();
}

/// <summary>一次采集的结果：不可变记录，跨层传递时不会被意外修改。</summary>
public sealed record ChannelReading(
    int Channel,
    ushort Raw,
    double Value,
    bool IsQualified,
    DateTime Timestamp);

/// <summary>一条保存到仓储的检测记录。</summary>
public sealed record SampleRecord(
    DateTime Time,
    string ProjectName,
    double[] Values);
