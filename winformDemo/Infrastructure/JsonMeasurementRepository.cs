using System.Text.Json;
using EnterpriseDemo.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseDemo.Infrastructure;

/// <summary>
/// 仓储实现：把记录存到输出目录的 samples.json。
/// 生产环境换 SQLite/EF Core 时，只要新类实现 IMeasurementRepository，
/// 组合根改一行注册，上层代码零改动。
/// </summary>
public sealed class JsonMeasurementRepository : IMeasurementRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ILogger<JsonMeasurementRepository> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<SampleRecord> _items = new();

    public JsonMeasurementRepository(
        IOptions<AppOptions> options,
        ILogger<JsonMeasurementRepository> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(AppContext.BaseDirectory, options.Value.StoragePath);

        if (File.Exists(_filePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<SampleRecord>>(
                    File.ReadAllText(_filePath), SerializerOptions);
                if (loaded is not null)
                {
                    _items.AddRange(loaded);
                }
            }
            catch (Exception ex)
            {
                // 配置/数据损坏时不要让程序崩溃，明确记录并降级为空集合。
                _logger.LogError(ex, "历史数据读取失败，将以空集合启动: {Path}", _filePath);
            }
        }
    }

    public async Task AddAsync(SampleRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _items.Add(record);
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SampleRecord>> QueryAsync(
        DateTime? from,
        DateTime? to,
        string? project,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _items
                .Where(x => (!from.HasValue || x.Time >= from.Value)
                         && (!to.HasValue || x.Time <= to.Value)
                         && (string.IsNullOrWhiteSpace(project) || x.ProjectName == project))
                .OrderBy(x => x.Time)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(_items, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        _logger.LogDebug("已持久化 {Count} 条记录", _items.Count);
    }
}
