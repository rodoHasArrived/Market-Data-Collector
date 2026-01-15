namespace MarketDataCollector.Storage.Crystallized;

/// <summary>
/// Configuration options for the crystallized storage format.
/// </summary>
public sealed class CrystallizedStorageOptions
{
    /// <summary>
    /// Root directory for all stored data.
    /// Default: "data"
    /// </summary>
    public string RootPath { get; init; } = "data";

    /// <summary>
    /// Whether to use self-documenting file names that include symbol, provider, and granularity.
    /// When true: AAPL_alpaca_bars_daily_2024-01-15.jsonl
    /// When false: 2024-01-15.jsonl (within appropriate directory structure)
    /// Default: true (recommended for portability)
    /// </summary>
    public bool SelfDocumentingFileNames { get; init; } = true;

    /// <summary>
    /// Prefer CSV format over JSONL for better Excel compatibility.
    /// CSV is easier to open in Excel but less flexible for nested data.
    /// Default: false (JSONL)
    /// </summary>
    public bool PreferCsv { get; init; } = false;

    /// <summary>
    /// Whether to compress output files.
    /// Default: false
    /// </summary>
    public bool Compress { get; init; } = false;

    /// <summary>
    /// Compression codec to use when Compress is true.
    /// Default: Gzip (best compatibility)
    /// </summary>
    public CompressionCodec CompressionCodec { get; init; } = CompressionCodec.Gzip;

    /// <summary>
    /// Whether to generate manifest files in each symbol directory.
    /// Manifests describe available data categories, date ranges, and file counts.
    /// Default: true
    /// </summary>
    public bool GenerateManifests { get; init; } = true;

    /// <summary>
    /// Whether to maintain a root catalog file listing all symbols and providers.
    /// Default: true
    /// </summary>
    public bool GenerateCatalog { get; init; } = true;

    /// <summary>
    /// Default time granularity for bar data when not specified.
    /// Default: Daily
    /// </summary>
    public TimeGranularity DefaultBarGranularity { get; init; } = TimeGranularity.Daily;

    /// <summary>
    /// Retention days for hot tier data (most recent data).
    /// Null means no automatic cleanup.
    /// Default: null
    /// </summary>
    public int? HotTierRetentionDays { get; init; }

    /// <summary>
    /// Maximum storage in megabytes before oldest files are cleaned up.
    /// Null means no size limit.
    /// Default: null
    /// </summary>
    public long? MaxStorageMegabytes { get; init; }

    /// <summary>
    /// Creates options optimized for Excel users.
    /// Uses CSV format, self-documenting names, no compression.
    /// </summary>
    public static CrystallizedStorageOptions ForExcel() => new()
    {
        PreferCsv = true,
        SelfDocumentingFileNames = true,
        Compress = false,
        GenerateManifests = true,
        GenerateCatalog = true
    };

    /// <summary>
    /// Creates options optimized for machine learning workloads.
    /// Uses JSONL format with compression, organized by granularity.
    /// </summary>
    public static CrystallizedStorageOptions ForMachineLearning() => new()
    {
        PreferCsv = false,
        SelfDocumentingFileNames = false, // Shorter paths, rely on directory structure
        Compress = true,
        CompressionCodec = CompressionCodec.Zstd,
        GenerateManifests = true,
        GenerateCatalog = true
    };

    /// <summary>
    /// Creates options optimized for high-throughput real-time collection.
    /// Uses JSONL with LZ4 compression for speed.
    /// </summary>
    public static CrystallizedStorageOptions ForRealTimeCollection() => new()
    {
        PreferCsv = false,
        SelfDocumentingFileNames = false,
        Compress = true,
        CompressionCodec = CompressionCodec.LZ4,
        GenerateManifests = false, // Don't slow down writes
        GenerateCatalog = false
    };

    /// <summary>
    /// Creates options optimized for long-term archival.
    /// Uses high compression and self-documenting names for future access.
    /// </summary>
    public static CrystallizedStorageOptions ForArchival() => new()
    {
        PreferCsv = false,
        SelfDocumentingFileNames = true,
        Compress = true,
        CompressionCodec = CompressionCodec.Zstd,
        GenerateManifests = true,
        GenerateCatalog = true
    };
}
