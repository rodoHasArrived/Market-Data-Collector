namespace MarketDataCollector.Storage;

/// <summary>
/// Storage configuration options for market data persistence.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Root directory path for all stored data files.
    /// Can be absolute or relative to the application directory.
    /// </summary>
    public string RootPath { get; init; } = "data";

    /// <summary>
    /// Whether to compress output files using gzip.
    /// </summary>
    public bool Compress { get; init; } = false;

    /// <summary>
    /// File naming convention for organizing stored data.
    /// </summary>
    public FileNamingConvention NamingConvention { get; init; } = FileNamingConvention.BySymbol;

    /// <summary>
    /// Date partitioning strategy for files.
    /// </summary>
    public DatePartition DatePartition { get; init; } = DatePartition.Daily;

    /// <summary>
    /// Whether to include the data source/provider name in the file path.
    /// </summary>
    public bool IncludeProvider { get; init; } = false;

    /// <summary>
    /// Custom file name prefix (optional).
    /// </summary>
    public string? FilePrefix { get; init; }
}

/// <summary>
/// File naming and directory structure conventions.
/// </summary>
public enum FileNamingConvention
{
    /// <summary>
    /// Flat structure: {root}/{symbol}_{type}_{date}.jsonl
    /// All files in root directory, good for small datasets.
    /// Example: data/AAPL_Trade_2024-01-15.jsonl
    /// </summary>
    Flat,

    /// <summary>
    /// Organize by symbol first: {root}/{symbol}/{type}/{date}.jsonl
    /// Best when analyzing individual symbols over time.
    /// Example: data/AAPL/Trade/2024-01-15.jsonl
    /// </summary>
    BySymbol,

    /// <summary>
    /// Organize by date first: {root}/{date}/{symbol}/{type}.jsonl
    /// Best for daily batch processing and archival.
    /// Example: data/2024-01-15/AAPL/Trade.jsonl
    /// </summary>
    ByDate,

    /// <summary>
    /// Organize by event type first: {root}/{type}/{symbol}/{date}.jsonl
    /// Best when analyzing specific event types across symbols.
    /// Example: data/Trade/AAPL/2024-01-15.jsonl
    /// </summary>
    ByType
}

/// <summary>
/// Date-based file partitioning strategy.
/// </summary>
public enum DatePartition
{
    /// <summary>
    /// No date partitioning - all data in single file per symbol/type.
    /// File name: {symbol}_{type}.jsonl
    /// </summary>
    None,

    /// <summary>
    /// Partition by day: {date:yyyy-MM-dd}
    /// </summary>
    Daily,

    /// <summary>
    /// Partition by hour: {date:yyyy-MM-dd_HH}
    /// Good for high-volume data.
    /// </summary>
    Hourly,

    /// <summary>
    /// Partition by month: {date:yyyy-MM}
    /// Good for long-term storage with less granularity.
    /// </summary>
    Monthly
}
