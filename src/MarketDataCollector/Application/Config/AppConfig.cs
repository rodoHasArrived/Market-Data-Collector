using System.Text.Json.Serialization;
using MarketDataCollector.Storage;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// Root configuration model loaded from appsettings.json.
/// </summary>
/// <param name="DataRoot">Output directory root for storage sinks.</param>
/// <param name="Compress">Whether JSONL sinks should gzip.</param>
/// <param name="DataSource">
/// Market data provider selector:
/// - <see cref="DataSourceKind.IB"/> (default) uses Interactive Brokers via IMarketDataClient/IBMarketDataClient.
/// - <see cref="DataSourceKind.Alpaca"/> uses Alpaca market data via WebSocket (trades; quotes optional in future).
/// </param>
/// <param name="Alpaca">Alpaca provider options (required if DataSource == DataSourceKind.Alpaca).</param>
/// <param name="StockSharp">StockSharp connector configuration (required if DataSource == DataSourceKind.StockSharp).</param>
/// <param name="Storage">Storage configuration options (naming convention, partitioning, etc.).</param>
/// <param name="Symbols">Symbol subscriptions.</param>
/// <param name="Backfill">Optional historical backfill defaults.</param>
/// <param name="Tiering">Tiered storage configuration.</param>
/// <param name="Quotas">Storage quota configuration.</param>
/// <param name="Maintenance">Maintenance and operational scheduling.</param>
/// <param name="Sources">Data source registry configuration.</param>
/// <param name="DataSources">Multiple data source configurations for real-time and historical data.</param>
public sealed record AppConfig(
    string DataRoot = "data",
    bool Compress = false,
    [property: JsonConverter(typeof(DataSourceKindConverter))] DataSourceKind DataSource = DataSourceKind.IB,
    AlpacaOptions? Alpaca = null,
    IBOptions? IB = null,
    PolygonOptions? Polygon = null,
    StockSharpConfig? StockSharp = null,
    StorageConfig? Storage = null,
    SymbolConfig[]? Symbols = null,
    BackfillConfig? Backfill = null,
    TieringConfig? Tiering = null,
    QuotaConfig? Quotas = null,
    MaintenanceConfig? Maintenance = null,
    SourceRegistryConfig? Sources = null,
    DataSourcesConfig? DataSources = null
);

/// <summary>
/// Storage configuration for file naming and organization.
/// </summary>
public sealed record StorageConfig(
    /// <summary>
    /// File naming convention: Flat, BySymbol, ByDate, ByType.
    /// </summary>
    string NamingConvention = "BySymbol",

    /// <summary>
    /// Date partitioning: None, Daily, Hourly, Monthly.
    /// </summary>
    string DatePartition = "Daily",

    /// <summary>
    /// Whether to include provider name in file path.
    /// </summary>
    bool IncludeProvider = false,

    /// <summary>
    /// Optional file name prefix.
    /// </summary>
    string? FilePrefix = null,

    /// <summary>
    /// Optional retention window (days). Files older than this are deleted during writes.
    /// </summary>
    int? RetentionDays = null,

    /// <summary>
    /// Optional cap on total bytes (across all files). Oldest files are removed first when exceeded.
    /// Value is expressed in megabytes for readability.
    /// </summary>
    long? MaxTotalMegabytes = null
)
{
    /// <summary>
    /// Converts to StorageOptions for use by storage components.
    /// </summary>
    public StorageOptions ToStorageOptions(string rootPath, bool compress)
    {
        return new StorageOptions
        {
            RootPath = rootPath,
            Compress = compress,
            NamingConvention = ParseNamingConvention(NamingConvention),
            DatePartition = ParseDatePartition(DatePartition),
            IncludeProvider = IncludeProvider,
            FilePrefix = FilePrefix,
            RetentionDays = RetentionDays,
            MaxTotalBytes = MaxTotalMegabytes is null ? null : MaxTotalMegabytes * 1024L * 1024L
        };
    }

    private static FileNamingConvention ParseNamingConvention(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FileNamingConvention.BySymbol;

        return value.ToLowerInvariant() switch
        {
            "flat" => FileNamingConvention.Flat,
            "bysymbol" => FileNamingConvention.BySymbol,
            "bydate" => FileNamingConvention.ByDate,
            "bytype" => FileNamingConvention.ByType,
            "bysource" => FileNamingConvention.BySource,
            "byassetclass" => FileNamingConvention.ByAssetClass,
            "hierarchical" => FileNamingConvention.Hierarchical,
            "canonical" => FileNamingConvention.Canonical,
            _ => FileNamingConvention.BySymbol
        };
    }

    private static DatePartition ParseDatePartition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Storage.DatePartition.Daily;

        return value.ToLowerInvariant() switch
        {
            "none" => Storage.DatePartition.None,
            "daily" => Storage.DatePartition.Daily,
            "hourly" => Storage.DatePartition.Hourly,
            "monthly" => Storage.DatePartition.Monthly,
            _ => Storage.DatePartition.Daily
        };
    }
}

/// <summary>
/// Tiered storage configuration.
/// </summary>
public sealed record TieringConfig(
    bool Enabled = false,
    TierDefinition[]? Tiers = null,
    string? MigrationSchedule = null,
    int ParallelMigrations = 4
);

/// <summary>
/// Definition of a storage tier.
/// </summary>
public sealed record TierDefinition(
    string Name,
    string Path,
    int? MaxAgeDays = null,
    long? MaxSizeGb = null,
    string Format = "jsonl",
    string? Compression = null,
    string? StorageClass = null
);

/// <summary>
/// Storage quota configuration.
/// </summary>
public sealed record QuotaConfig(
    GlobalQuotaConfig? Global = null,
    Dictionary<string, PerSourceQuotaConfig>? PerSource = null,
    Dictionary<string, PerSymbolQuotaConfig>? PerSymbol = null,
    DynamicQuotaSettings? Dynamic = null
);

/// <summary>
/// Global quota limits.
/// </summary>
public sealed record GlobalQuotaConfig(
    long MaxBytes = 107_374_182_400L, // 100GB default
    long? MaxFiles = null,
    long? MaxEventsPerDay = null,
    string Enforcement = "SoftLimit"
);

/// <summary>
/// Per-source quota configuration.
/// </summary>
public sealed record PerSourceQuotaConfig(
    long MaxBytes,
    long? MaxFiles = null,
    string Enforcement = "SoftLimit"
);

/// <summary>
/// Per-symbol quota configuration.
/// </summary>
public sealed record PerSymbolQuotaConfig(
    long MaxBytes,
    long? MaxFiles = null,
    string Enforcement = "SoftLimit"
);

/// <summary>
/// Dynamic quota rebalancing settings.
/// </summary>
public sealed record DynamicQuotaSettings(
    bool Enabled = false,
    int EvaluationPeriodMinutes = 60,
    double MinReservePct = 10,
    double OverprovisionFactor = 1.1,
    bool StealFromInactive = false
);

/// <summary>
/// Maintenance and operational scheduling configuration.
/// </summary>
public sealed record MaintenanceConfig(
    bool Enabled = true,
    string Timezone = "America/New_York",
    TradingSessionConfig[]? TradingSessions = null,
    MaintenanceWindowConfig[]? Windows = null,
    string[]? Holidays = null
);

/// <summary>
/// Trading session definition.
/// </summary>
public sealed record TradingSessionConfig(
    string Name,
    string[] ActiveDays,
    string PreMarketStart = "04:00",
    string RegularStart = "09:30",
    string RegularEnd = "16:00",
    string AfterHoursEnd = "20:00",
    bool IncludesPreMarket = true,
    bool IncludesAfterHours = true
);

/// <summary>
/// Maintenance window definition.
/// </summary>
public sealed record MaintenanceWindowConfig(
    string Name,
    string Start,
    string End,
    string[] Days,
    string[]? AllowedOperations = null,
    int MaxConcurrentJobs = 4,
    int MaxCpuPct = 80,
    int MaxMemoryPct = 70,
    int MaxDiskIoMbps = 500
);

/// <summary>
/// Source registry configuration.
/// </summary>
public sealed record SourceRegistryConfig(
    string? PersistencePath = null,
    SourceDefinition[]? Sources = null,
    string[]? PriorityOrder = null,
    string DefaultConflictStrategy = "HighestPriority"
);

/// <summary>
/// Data source definition.
/// </summary>
public sealed record SourceDefinition(
    string Id,
    string Name,
    string Type = "Live",
    int Priority = 1,
    string[]? AssetClasses = null,
    string[]? DataTypes = null,
    double? LatencyMs = null,
    double? Reliability = null,
    decimal? CostPerEvent = null,
    bool Enabled = true
);
