using System.Text.Json.Serialization;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// Root configuration model loaded from appsettings.json.
/// </summary>
/// <param name="DataRoot">Output directory root for storage sinks.</param>
/// <param name="Compress">Whether JSONL sinks should gzip. Null means use base configuration/default.</param>
/// <param name="DataSource">
/// Market data provider selector:
/// - <see cref="DataSourceKind.IB"/> (default) uses Interactive Brokers via IMarketDataClient/IBMarketDataClient.
/// - <see cref="DataSourceKind.Alpaca"/> uses Alpaca market data via WebSocket (trades; quotes optional in future).
/// - <see cref="DataSourceKind.StockSharp"/> uses StockSharp connectors (Rithmic, IQFeed, CQG, IB, etc.).
/// - <see cref="DataSourceKind.NYSE"/> uses the NYSE market data feed.
/// </param>
/// <param name="Alpaca">Alpaca provider options (required if DataSource == DataSourceKind.Alpaca).</param>
/// <param name="IB">Interactive Brokers provider options (required if DataSource == DataSourceKind.IB).</param>
/// <param name="Polygon">Polygon provider options (required if DataSource == DataSourceKind.Polygon).</param>
/// <param name="StockSharp">StockSharp connector configuration (required if DataSource == DataSourceKind.StockSharp).</param>
/// <param name="Storage">Storage configuration options (naming convention, partitioning, etc.).</param>
/// <param name="Symbols">Symbol subscriptions.</param>
/// <param name="Backfill">Optional historical backfill defaults.</param>
/// <param name="Sources">Source registry persistence path.</param>
/// <param name="DataSources">Multiple data source configurations for real-time and historical data.</param>
/// <param name="Derivatives">Derivatives (options) data collection configuration.</param>
/// <param name="ProviderRegistry">Unified provider registry configuration controlling attribute-based discovery.</param>
public sealed record AppConfig(
    string DataRoot = "data",
    bool? Compress = null,
    [property: JsonConverter(typeof(DataSourceKindConverter))] DataSourceKind DataSource = DataSourceKind.IB,
    AlpacaOptions? Alpaca = null,
    IBOptions? IB = null,
    PolygonOptions? Polygon = null,
    StockSharpConfig? StockSharp = null,
    StorageConfig? Storage = null,
    SymbolConfig[]? Symbols = null,
    BackfillConfig? Backfill = null,
    SourceRegistryConfig? Sources = null,
    DataSourcesConfig? DataSources = null,
    DerivativesConfig? Derivatives = null,
    ProviderRegistryConfig? ProviderRegistry = null,
    CanonicalizationConfig? Canonicalization = null
);

/// <summary>
/// Configuration for the unified provider registry (Phase 1.2).
/// Controls how streaming, backfill, and symbol search providers are discovered and registered.
/// </summary>
/// <param name="UseAttributeDiscovery">
/// When true, <c>DataSourceAttribute</c>-decorated types are discovered via reflection
/// and automatically registered as streaming factories in the <c>ProviderRegistry</c>,
/// replacing manual lambda registration. Default is false (manual registration).
/// </param>
public sealed record ProviderRegistryConfig(
    bool UseAttributeDiscovery = false
);

/// <summary>
/// Configuration for deterministic canonicalization (Phase 2+).
/// Controls how raw market events are transformed into structurally comparable canonical records.
/// </summary>
/// <param name="Enabled">Master switch for canonicalization. When false, events pass through unchanged.</param>
/// <param name="PilotSymbols">
/// Optional list of symbols to canonicalize during dual-write validation (Phase 2).
/// If empty or null, all symbols are canonicalized when <see cref="Enabled"/> is true.
/// </param>
/// <param name="DualWriteRawAndCanonical">
/// When true, persists both raw and enriched events. Used during Phase 2 to validate
/// parity before committing to canonical-only writes.
/// </param>
/// <param name="ConditionCodesPath">Path to condition-codes.json. Defaults to config/condition-codes.json.</param>
/// <param name="VenueMappingPath">Path to venue-mapping.json. Defaults to config/venue-mapping.json.</param>
/// <param name="Version">Canonicalization version stamp applied to enriched events.</param>
public sealed record CanonicalizationConfig(
    bool Enabled = false,
    string[]? PilotSymbols = null,
    bool DualWriteRawAndCanonical = true,
    string ConditionCodesPath = "config/condition-codes.json",
    string VenueMappingPath = "config/venue-mapping.json",
    int Version = 1
);

/// <summary>
/// Storage configuration for file naming and organization.
/// Conversion to StorageOptions is available via extension methods in the Application layer.
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
    /// Optional storage profile preset (Research, LowLatency, Archival).
    /// </summary>
    string? Profile = null,

    /// <summary>
    /// Optional retention window (days). Files older than this are deleted during writes.
    /// </summary>
    int? RetentionDays = null,

    /// <summary>
    /// Optional cap on total bytes (across all files). Oldest files are removed first when exceeded.
    /// Value is expressed in megabytes for readability.
    /// </summary>
    long? MaxTotalMegabytes = null,

    /// <summary>
    /// Whether to enable Parquet storage as an additional sink alongside JSONL.
    /// When enabled, events are written to both JSONL and Parquet via CompositeSink.
    /// </summary>
    bool EnableParquetSink = false
);

/// <summary>
/// Source registry configuration - only PersistencePath is used.
/// </summary>
public sealed record SourceRegistryConfig(
    string? PersistencePath = null
);
