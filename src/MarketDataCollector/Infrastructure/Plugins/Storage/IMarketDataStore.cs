using MarketDataCollector.Infrastructure.Plugins.Core;

namespace MarketDataCollector.Infrastructure.Plugins.Storage;

/// <summary>
/// Simplified interface for storing and retrieving market data.
/// </summary>
/// <remarks>
/// Design philosophy: Start simple, scale when needed.
///
/// This replaces the complex storage architecture with nested options:
/// - 8 FileNamingConventions
/// - 4 DatePartition options
/// - 5 CompressionCodecs
/// - 5 StorageTiers
/// - Quotas, WAL, archival policies
///
/// With a simple interface that:
/// - Just works with reasonable defaults
/// - Can be swapped out for more complex implementations when needed
/// - Keeps the plugin code clean
///
/// Implementations:
/// - FileSystemStore: Simple JSONL files (default)
/// - CompressedStore: JSONL with gzip (wraps FileSystemStore)
/// - SqliteStore: For high-frequency queries
/// - CloudStore: S3/Azure Blob storage
/// </remarks>
public interface IMarketDataStore : IAsyncDisposable
{
    /// <summary>
    /// Appends a market data event to storage.
    /// </summary>
    ValueTask AppendAsync(MarketDataEvent data, CancellationToken ct = default);

    /// <summary>
    /// Appends multiple events to storage (batch write).
    /// </summary>
    ValueTask AppendManyAsync(IEnumerable<MarketDataEvent> data, CancellationToken ct = default);

    /// <summary>
    /// Queries stored data for a symbol and date range.
    /// </summary>
    IAsyncEnumerable<MarketDataEvent> QueryAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        DataType? dataType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the date range of stored data for a symbol.
    /// </summary>
    Task<(DateOnly? First, DateOnly? Last)> GetDateRangeAsync(
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Flushes any buffered data to persistent storage.
    /// </summary>
    Task FlushAsync(CancellationToken ct = default);
}

/// <summary>
/// Configuration for the market data store.
/// </summary>
public sealed record StoreOptions
{
    /// <summary>
    /// Root directory for data storage.
    /// Default: ./data
    /// </summary>
    public string DataPath { get; init; } = "./data";

    /// <summary>
    /// Whether to compress data (gzip).
    /// Default: true for historical, false for real-time
    /// </summary>
    public bool Compress { get; init; }

    /// <summary>
    /// Buffer size for batch writes.
    /// </summary>
    public int BufferSize { get; init; } = 1000;

    /// <summary>
    /// Flush interval for buffered writes.
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates default options for real-time data.
    /// </summary>
    public static StoreOptions Realtime => new()
    {
        Compress = false,
        BufferSize = 100,
        FlushInterval = TimeSpan.FromSeconds(1)
    };

    /// <summary>
    /// Creates default options for historical data.
    /// </summary>
    public static StoreOptions Historical => new()
    {
        Compress = true,
        BufferSize = 10000,
        FlushInterval = TimeSpan.FromSeconds(30)
    };
}
