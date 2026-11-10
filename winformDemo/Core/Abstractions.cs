namespace EnterpriseDemo.Core;

/// <summary>
/// 硬件访问抽象。真实项目里换成基于 NModbus 的实现即可，业务层一行都不用改。
/// </summary>
public interface IModbusClient : IDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task<ushort[]> ReadChannelsAsync(CancellationToken cancellationToken);
}

/// <summary>标定换算与阈值查询的抽象。</summary>
public interface ICalibrationService
{
    int ChannelCount { get; }
    double Convert(int channel, ushort raw);
    (double Low, double High) LimitsOf(int channel);
}

/// <summary>
/// 仓储抽象。当前实现是 JSON 文件，换成 SQLite / EF Core 只需替换实现类。
/// </summary>
public interface IMeasurementRepository
{
    Task AddAsync(SampleRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SampleRecord>> QueryAsync(
        DateTime? from,
        DateTime? to,
        string? project,
        CancellationToken cancellationToken = default);
}
