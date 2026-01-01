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
/// <param name="Storage">Storage configuration options (naming convention, partitioning, etc.).</param>
/// <param name="Symbols">Symbol subscriptions.</param>
public sealed record AppConfig(
    string DataRoot = "data",
    bool Compress = false,
    [property: JsonConverter(typeof(DataSourceKindConverter))] DataSourceKind DataSource = DataSourceKind.IB,
    AlpacaOptions? Alpaca = null,
    StorageConfig? Storage = null,
    SymbolConfig[]? Symbols = null
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
