using System.Text.Json.Serialization;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Application configuration model.
/// </summary>
public class AppConfig
{
    [JsonPropertyName("dataRoot")]
    public string? DataRoot { get; set; } = "data";

    [JsonPropertyName("compress")]
    public bool Compress { get; set; }

    [JsonPropertyName("dataSource")]
    public string? DataSource { get; set; } = "IB";

    [JsonPropertyName("alpaca")]
    public AlpacaOptions? Alpaca { get; set; }

    [JsonPropertyName("storage")]
    public StorageConfig? Storage { get; set; }

    [JsonPropertyName("symbols")]
    public SymbolConfig[]? Symbols { get; set; }

    [JsonPropertyName("backfill")]
    public BackfillConfig? Backfill { get; set; }

    [JsonPropertyName("dataSources")]
    public DataSourcesConfig? DataSources { get; set; }

    [JsonPropertyName("symbolGroups")]
    public SymbolGroupsConfig? SymbolGroups { get; set; }

    [JsonPropertyName("settings")]
    public AppSettings? Settings { get; set; }
}

/// <summary>
/// Collection of data source configurations.
/// </summary>
public class DataSourcesConfig
{
    [JsonPropertyName("sources")]
    public DataSourceConfig[]? Sources { get; set; }

    [JsonPropertyName("defaultRealTimeSourceId")]
    public string? DefaultRealTimeSourceId { get; set; }

    [JsonPropertyName("defaultHistoricalSourceId")]
    public string? DefaultHistoricalSourceId { get; set; }

    [JsonPropertyName("enableFailover")]
    public bool EnableFailover { get; set; } = true;

    [JsonPropertyName("failoverTimeoutSeconds")]
    public int FailoverTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Configuration for an individual data source.
/// </summary>
public class DataSourceConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "IB";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "RealTime";

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("alpaca")]
    public AlpacaOptions? Alpaca { get; set; }

    [JsonPropertyName("polygon")]
    public PolygonOptions? Polygon { get; set; }

    [JsonPropertyName("ib")]
    public IBOptions? IB { get; set; }

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }
}

/// <summary>
/// Polygon.io API configuration options.
/// </summary>
public class PolygonOptions
{
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("useDelayed")]
    public bool UseDelayed { get; set; }

    [JsonPropertyName("feed")]
    public string Feed { get; set; } = "stocks";

    [JsonPropertyName("subscribeTrades")]
    public bool SubscribeTrades { get; set; } = true;

    [JsonPropertyName("subscribeQuotes")]
    public bool SubscribeQuotes { get; set; }

    [JsonPropertyName("subscribeAggregates")]
    public bool SubscribeAggregates { get; set; }
}

/// <summary>
/// Interactive Brokers connection options.
/// </summary>
public class IBOptions
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 7496;

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("usePaperTrading")]
    public bool UsePaperTrading { get; set; }

    [JsonPropertyName("subscribeDepth")]
    public bool SubscribeDepth { get; set; } = true;

    [JsonPropertyName("depthLevels")]
    public int DepthLevels { get; set; } = 10;

    [JsonPropertyName("tickByTick")]
    public bool TickByTick { get; set; } = true;
}

/// <summary>
/// Alpaca provider configuration.
/// </summary>
public class AlpacaOptions
{
    [JsonPropertyName("keyId")]
    public string? KeyId { get; set; }

    [JsonPropertyName("secretKey")]
    public string? SecretKey { get; set; }

    [JsonPropertyName("feed")]
    public string? Feed { get; set; } = "iex";

    [JsonPropertyName("useSandbox")]
    public bool UseSandbox { get; set; }

    [JsonPropertyName("subscribeQuotes")]
    public bool SubscribeQuotes { get; set; }
}

/// <summary>
/// Storage configuration.
/// </summary>
public class StorageConfig
{
    [JsonPropertyName("namingConvention")]
    public string? NamingConvention { get; set; } = "BySymbol";

    [JsonPropertyName("datePartition")]
    public string? DatePartition { get; set; } = "Daily";

    [JsonPropertyName("includeProvider")]
    public bool IncludeProvider { get; set; }

    [JsonPropertyName("filePrefix")]
    public string? FilePrefix { get; set; }
}

/// <summary>
/// Symbol subscription configuration.
/// </summary>
public class SymbolConfig
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("subscribeTrades")]
    public bool SubscribeTrades { get; set; } = true;

    [JsonPropertyName("subscribeDepth")]
    public bool SubscribeDepth { get; set; }

    [JsonPropertyName("depthLevels")]
    public int DepthLevels { get; set; } = 10;

    [JsonPropertyName("securityType")]
    public string? SecurityType { get; set; } = "STK";

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; } = "SMART";

    [JsonPropertyName("currency")]
    public string? Currency { get; set; } = "USD";

    [JsonPropertyName("primaryExchange")]
    public string? PrimaryExchange { get; set; }

    [JsonPropertyName("localSymbol")]
    public string? LocalSymbol { get; set; }
}

/// <summary>
/// Backfill configuration.
/// </summary>
public class BackfillConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; } = "stooq";

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }
}

/// <summary>
/// Status response model.
/// </summary>
public class StatusResponse
{
    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; set; }

    [JsonPropertyName("metrics")]
    public MetricsData? Metrics { get; set; }
}

/// <summary>
/// Metrics data.
/// </summary>
public class MetricsData
{
    [JsonPropertyName("published")]
    public long Published { get; set; }

    [JsonPropertyName("dropped")]
    public long Dropped { get; set; }

    [JsonPropertyName("integrity")]
    public long Integrity { get; set; }

    [JsonPropertyName("historicalBars")]
    public long HistoricalBars { get; set; }
}

/// <summary>
/// Backfill result model.
/// </summary>
public class BackfillResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("barsWritten")]
    public int BarsWritten { get; set; }

    [JsonPropertyName("startedUtc")]
    public DateTime StartedUtc { get; set; }

    [JsonPropertyName("completedUtc")]
    public DateTime CompletedUtc { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Backfill provider description.
/// </summary>
public class BackfillProviderInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Symbol group configuration for organizing symbols into groups/portfolios.
/// </summary>
public class SymbolGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#0078D4";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "\uE8D2";

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("isExpanded")]
    public bool IsExpanded { get; set; } = true;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("smartCriteria")]
    public SmartGroupCriteria? SmartCriteria { get; set; }
}

/// <summary>
/// Criteria for smart/dynamic symbol groups.
/// </summary>
public class SmartGroupCriteria
{
    [JsonPropertyName("isSmartGroup")]
    public bool IsSmartGroup { get; set; }

    [JsonPropertyName("sectorFilter")]
    public string? SectorFilter { get; set; }

    [JsonPropertyName("industryFilter")]
    public string? IndustryFilter { get; set; }

    [JsonPropertyName("exchangeFilter")]
    public string? ExchangeFilter { get; set; }

    [JsonPropertyName("minPrice")]
    public decimal? MinPrice { get; set; }

    [JsonPropertyName("maxPrice")]
    public decimal? MaxPrice { get; set; }

    [JsonPropertyName("tagsFilter")]
    public string[]? TagsFilter { get; set; }
}

/// <summary>
/// Extended symbol configuration with group membership and status.
/// </summary>
public class ExtendedSymbolConfig : SymbolConfig
{
    [JsonPropertyName("groupIds")]
    public string[]? GroupIds { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("customColor")]
    public string? CustomColor { get; set; }
}

/// <summary>
/// Symbol groups configuration container.
/// </summary>
public class SymbolGroupsConfig
{
    [JsonPropertyName("groups")]
    public SymbolGroup[]? Groups { get; set; }

    [JsonPropertyName("defaultGroupId")]
    public string? DefaultGroupId { get; set; }

    [JsonPropertyName("showUngroupedSymbols")]
    public bool ShowUngroupedSymbols { get; set; } = true;

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "Name"; // Name, SortOrder, SymbolCount

    [JsonPropertyName("viewMode")]
    public string ViewMode { get; set; } = "Tree"; // Tree, Flat, Grid
}

/// <summary>
/// Storage analytics data.
/// </summary>
public class StorageAnalytics
{
    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("tradeSizeBytes")]
    public long TradeSizeBytes { get; set; }

    [JsonPropertyName("depthSizeBytes")]
    public long DepthSizeBytes { get; set; }

    [JsonPropertyName("historicalSizeBytes")]
    public long HistoricalSizeBytes { get; set; }

    [JsonPropertyName("totalFileCount")]
    public int TotalFileCount { get; set; }

    [JsonPropertyName("tradeFileCount")]
    public int TradeFileCount { get; set; }

    [JsonPropertyName("depthFileCount")]
    public int DepthFileCount { get; set; }

    [JsonPropertyName("historicalFileCount")]
    public int HistoricalFileCount { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("symbolBreakdown")]
    public SymbolStorageInfo[]? SymbolBreakdown { get; set; }

    [JsonPropertyName("dailyGrowthBytes")]
    public long DailyGrowthBytes { get; set; }

    [JsonPropertyName("projectedDaysUntilFull")]
    public int? ProjectedDaysUntilFull { get; set; }
}

/// <summary>
/// Per-symbol storage information.
/// </summary>
public class SymbolStorageInfo
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("percentOfTotal")]
    public double PercentOfTotal { get; set; }

    [JsonPropertyName("oldestData")]
    public DateTime? OldestData { get; set; }

    [JsonPropertyName("newestData")]
    public DateTime? NewestData { get; set; }
}

/// <summary>
/// Backfill progress tracking.
/// </summary>
public class BackfillProgress
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending"; // Pending, Running, Paused, Completed, Failed, Cancelled

    [JsonPropertyName("totalSymbols")]
    public int TotalSymbols { get; set; }

    [JsonPropertyName("completedSymbols")]
    public int CompletedSymbols { get; set; }

    [JsonPropertyName("failedSymbols")]
    public int FailedSymbols { get; set; }

    [JsonPropertyName("totalBars")]
    public long TotalBars { get; set; }

    [JsonPropertyName("downloadedBars")]
    public long DownloadedBars { get; set; }

    [JsonPropertyName("barsPerSecond")]
    public double BarsPerSecond { get; set; }

    [JsonPropertyName("estimatedSecondsRemaining")]
    public int? EstimatedSecondsRemaining { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("symbolProgress")]
    public SymbolBackfillProgress[]? SymbolProgress { get; set; }

    [JsonPropertyName("currentProvider")]
    public string? CurrentProvider { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Per-symbol backfill progress.
/// </summary>
public class SymbolBackfillProgress
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending"; // Pending, Downloading, Completed, Failed, Skipped

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("barsDownloaded")]
    public int BarsDownloaded { get; set; }

    [JsonPropertyName("expectedBars")]
    public int ExpectedBars { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }
}

/// <summary>
/// Application settings for UI preferences.
/// </summary>
public class AppSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System"; // System, Light, Dark

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "System";

    [JsonPropertyName("compactMode")]
    public bool CompactMode { get; set; }

    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    [JsonPropertyName("notifyConnectionStatus")]
    public bool NotifyConnectionStatus { get; set; } = true;

    [JsonPropertyName("notifyErrors")]
    public bool NotifyErrors { get; set; } = true;

    [JsonPropertyName("notifyBackfillComplete")]
    public bool NotifyBackfillComplete { get; set; } = true;

    [JsonPropertyName("notifyDataGaps")]
    public bool NotifyDataGaps { get; set; } = true;

    [JsonPropertyName("notifyStorageWarnings")]
    public bool NotifyStorageWarnings { get; set; } = true;

    [JsonPropertyName("quietHoursEnabled")]
    public bool QuietHoursEnabled { get; set; }

    [JsonPropertyName("quietHoursStart")]
    public string QuietHoursStart { get; set; } = "22:00";

    [JsonPropertyName("quietHoursEnd")]
    public string QuietHoursEnd { get; set; } = "07:00";

    [JsonPropertyName("autoReconnectEnabled")]
    public bool AutoReconnectEnabled { get; set; } = true;

    [JsonPropertyName("maxReconnectAttempts")]
    public int MaxReconnectAttempts { get; set; } = 10;

    [JsonPropertyName("statusRefreshIntervalSeconds")]
    public int StatusRefreshIntervalSeconds { get; set; } = 2;
}

/// <summary>
/// Keyboard shortcut configuration.
/// </summary>
public class KeyboardShortcut
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("modifiers")]
    public string[]? Modifiers { get; set; } // Ctrl, Shift, Alt

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

// ============================================================================
// Collection Session Management Models (#65/27) - P0 Critical
// ============================================================================

/// <summary>
/// Represents a discrete data collection session with comprehensive tracking.
/// </summary>
public class CollectionSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending"; // Pending, Active, Paused, Completed, Failed

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("endedAt")]
    public DateTime? EndedAt { get; set; }

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("eventTypes")]
    public string[] EventTypes { get; set; } = Array.Empty<string>(); // Trade, Quote, Depth, Bar

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("statistics")]
    public CollectionSessionStatistics? Statistics { get; set; }

    [JsonPropertyName("qualityScore")]
    public double QualityScore { get; set; }

    [JsonPropertyName("manifestPath")]
    public string? ManifestPath { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Statistics for a collection session.
/// </summary>
public class CollectionSessionStatistics
{
    [JsonPropertyName("totalEvents")]
    public long TotalEvents { get; set; }

    [JsonPropertyName("tradeEvents")]
    public long TradeEvents { get; set; }

    [JsonPropertyName("quoteEvents")]
    public long QuoteEvents { get; set; }

    [JsonPropertyName("depthEvents")]
    public long DepthEvents { get; set; }

    [JsonPropertyName("barEvents")]
    public long BarEvents { get; set; }

    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; set; }

    [JsonPropertyName("compressedBytes")]
    public long CompressedBytes { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("gapsDetected")]
    public int GapsDetected { get; set; }

    [JsonPropertyName("gapsFilled")]
    public int GapsFilled { get; set; }

    [JsonPropertyName("sequenceErrors")]
    public int SequenceErrors { get; set; }

    [JsonPropertyName("eventsPerSecond")]
    public double EventsPerSecond { get; set; }

    [JsonPropertyName("compressionRatio")]
    public double CompressionRatio { get; set; }
}

/// <summary>
/// Configuration for collection sessions.
/// </summary>
public class CollectionSessionsConfig
{
    [JsonPropertyName("sessions")]
    public CollectionSession[]? Sessions { get; set; }

    [JsonPropertyName("activeSessionId")]
    public string? ActiveSessionId { get; set; }

    [JsonPropertyName("autoCreateDailySessions")]
    public bool AutoCreateDailySessions { get; set; } = true;

    [JsonPropertyName("sessionNamingPattern")]
    public string SessionNamingPattern { get; set; } = "{date}-{mode}"; // {date}, {mode}, {symbols}

    [JsonPropertyName("generateManifestOnComplete")]
    public bool GenerateManifestOnComplete { get; set; } = true;

    [JsonPropertyName("retainSessionHistory")]
    public int RetainSessionHistory { get; set; } = 365; // Days to keep session history
}

// ============================================================================
// Offline Data Catalog & Manifest System Models (#56) - P0 Critical
// ============================================================================

/// <summary>
/// Comprehensive manifest for a collection session or archive package.
/// </summary>
public class DataManifest
{
    [JsonPropertyName("manifestVersion")]
    public string ManifestVersion { get; set; } = "1.0";

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("sessionName")]
    public string? SessionName { get; set; }

    [JsonPropertyName("dateRange")]
    public DateRangeInfo? DateRange { get; set; }

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("totalEvents")]
    public long TotalEvents { get; set; }

    [JsonPropertyName("totalBytesRaw")]
    public long TotalBytesRaw { get; set; }

    [JsonPropertyName("totalBytesCompressed")]
    public long TotalBytesCompressed { get; set; }

    [JsonPropertyName("files")]
    public ManifestFileEntry[] Files { get; set; } = Array.Empty<ManifestFileEntry>();

    [JsonPropertyName("schemas")]
    public Dictionary<string, string>? Schemas { get; set; } // EventType -> SchemaVersion

    [JsonPropertyName("qualityMetrics")]
    public DataQualityMetrics? QualityMetrics { get; set; }

    [JsonPropertyName("verificationStatus")]
    public string VerificationStatus { get; set; } = "Pending"; // Pending, Verified, Failed

    [JsonPropertyName("lastVerifiedAt")]
    public DateTime? LastVerifiedAt { get; set; }
}

/// <summary>
/// Date range information.
/// </summary>
public class DateRangeInfo
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("tradingDays")]
    public int TradingDays { get; set; }
}

/// <summary>
/// Individual file entry in a manifest.
/// </summary>
public class ManifestFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("checksumSha256")]
    public string ChecksumSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("compressedSizeBytes")]
    public long? CompressedSizeBytes { get; set; }

    [JsonPropertyName("eventCount")]
    public long EventCount { get; set; }

    [JsonPropertyName("firstTimestamp")]
    public DateTime? FirstTimestamp { get; set; }

    [JsonPropertyName("lastTimestamp")]
    public DateTime? LastTimestamp { get; set; }

    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    [JsonPropertyName("isCompressed")]
    public bool IsCompressed { get; set; }

    [JsonPropertyName("compressionType")]
    public string? CompressionType { get; set; } // gzip, zstd, lz4

    [JsonPropertyName("verificationStatus")]
    public string VerificationStatus { get; set; } = "Pending"; // Pending, Verified, Failed

    [JsonPropertyName("lastVerifiedAt")]
    public DateTime? LastVerifiedAt { get; set; }
}

/// <summary>
/// Data quality metrics for manifests and sessions.
/// </summary>
public class DataQualityMetrics
{
    [JsonPropertyName("completenessScore")]
    public double CompletenessScore { get; set; }

    [JsonPropertyName("integrityScore")]
    public double IntegrityScore { get; set; }

    [JsonPropertyName("overallScore")]
    public double OverallScore { get; set; }

    [JsonPropertyName("gapsDetected")]
    public int GapsDetected { get; set; }

    [JsonPropertyName("sequenceErrors")]
    public int SequenceErrors { get; set; }

    [JsonPropertyName("duplicatesFound")]
    public int DuplicatesFound { get; set; }

    [JsonPropertyName("expectedEvents")]
    public long ExpectedEvents { get; set; }

    [JsonPropertyName("actualEvents")]
    public long ActualEvents { get; set; }

    [JsonPropertyName("missingTradingDays")]
    public string[]? MissingTradingDays { get; set; }

    [JsonPropertyName("outliersDetected")]
    public int OutliersDetected { get; set; }
}

// ============================================================================
// Schema & Data Dictionary Models (#37/72) - P0 High
// ============================================================================

/// <summary>
/// Schema definition for a data event type.
/// </summary>
public class EventSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("introducedAt")]
    public DateTime IntroducedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deprecatedAt")]
    public DateTime? DeprecatedAt { get; set; }

    [JsonPropertyName("fields")]
    public SchemaField[] Fields { get; set; } = Array.Empty<SchemaField>();

    [JsonPropertyName("primaryKey")]
    public string[]? PrimaryKey { get; set; }

    [JsonPropertyName("indexes")]
    public string[][]? Indexes { get; set; }

    [JsonPropertyName("migrationFromVersion")]
    public string? MigrationFromVersion { get; set; }

    [JsonPropertyName("sampleRecord")]
    public Dictionary<string, object>? SampleRecord { get; set; }
}

/// <summary>
/// Schema field definition.
/// </summary>
public class SchemaField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string"; // string, int64, decimal, datetime, bool, enum

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; }

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("validRange")]
    public FieldValidRange? ValidRange { get; set; }

    [JsonPropertyName("enumValues")]
    public string[]? EnumValues { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; } // ISO8601, decimal(18,8), etc.

    [JsonPropertyName("example")]
    public object? Example { get; set; }

    [JsonPropertyName("exchangeSpecific")]
    public bool ExchangeSpecific { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Valid range for a field.
/// </summary>
public class FieldValidRange
{
    [JsonPropertyName("min")]
    public object? Min { get; set; }

    [JsonPropertyName("max")]
    public object? Max { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; } // Regex pattern for strings
}

/// <summary>
/// Data dictionary containing all schemas.
/// </summary>
public class DataDictionary
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("schemas")]
    public Dictionary<string, EventSchema> Schemas { get; set; } = new();

    [JsonPropertyName("exchangeCodes")]
    public Dictionary<string, string>? ExchangeCodes { get; set; }

    [JsonPropertyName("tradeConditions")]
    public Dictionary<string, string>? TradeConditions { get; set; }

    [JsonPropertyName("quoteConditions")]
    public Dictionary<string, string>? QuoteConditions { get; set; }
}

// ============================================================================
// Archive Health & Verification Models (#26/57) - P0 Critical
// ============================================================================

/// <summary>
/// Archive health status and metrics.
/// </summary>
public class ArchiveHealthStatus
{
    [JsonPropertyName("overallHealthScore")]
    public double OverallHealthScore { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Unknown"; // Healthy, Warning, Critical, Unknown

    [JsonPropertyName("lastFullVerificationAt")]
    public DateTime? LastFullVerificationAt { get; set; }

    [JsonPropertyName("lastVerificationDurationMinutes")]
    public int? LastVerificationDurationMinutes { get; set; }

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("verifiedFiles")]
    public int VerifiedFiles { get; set; }

    [JsonPropertyName("pendingFiles")]
    public int PendingFiles { get; set; }

    [JsonPropertyName("failedFiles")]
    public int FailedFiles { get; set; }

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("verifiedSizeBytes")]
    public long VerifiedSizeBytes { get; set; }

    [JsonPropertyName("issues")]
    public ArchiveIssue[]? Issues { get; set; }

    [JsonPropertyName("recommendations")]
    public string[]? Recommendations { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("storageHealthInfo")]
    public StorageHealthInfo? StorageHealthInfo { get; set; }
}

/// <summary>
/// An issue detected in the archive.
/// </summary>
public class ArchiveIssue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Warning"; // Info, Warning, Critical

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty; // Integrity, Completeness, Storage, Performance

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("affectedFiles")]
    public string[]? AffectedFiles { get; set; }

    [JsonPropertyName("affectedSymbols")]
    public string[]? AffectedSymbols { get; set; }

    [JsonPropertyName("suggestedAction")]
    public string? SuggestedAction { get; set; }

    [JsonPropertyName("detectedAt")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }

    [JsonPropertyName("isAutoFixable")]
    public bool IsAutoFixable { get; set; }
}

/// <summary>
/// Storage media health information.
/// </summary>
public class StorageHealthInfo
{
    [JsonPropertyName("driveType")]
    public string DriveType { get; set; } = "Unknown"; // SSD, HDD, Network, Unknown

    [JsonPropertyName("healthStatus")]
    public string HealthStatus { get; set; } = "Unknown"; // Good, Warning, Critical, Unknown

    [JsonPropertyName("totalCapacity")]
    public long TotalCapacity { get; set; }

    [JsonPropertyName("freeSpace")]
    public long FreeSpace { get; set; }

    [JsonPropertyName("usedPercent")]
    public double UsedPercent { get; set; }

    [JsonPropertyName("averageWriteLatencyMs")]
    public double? AverageWriteLatencyMs { get; set; }

    [JsonPropertyName("readSpeedMbps")]
    public double? ReadSpeedMbps { get; set; }

    [JsonPropertyName("writeSpeedMbps")]
    public double? WriteSpeedMbps { get; set; }

    [JsonPropertyName("daysUntilFull")]
    public int? DaysUntilFull { get; set; }
}

/// <summary>
/// Verification job status.
/// </summary>
public class VerificationJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Full"; // Full, Incremental, Selective

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed, Cancelled

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("processedFiles")]
    public int ProcessedFiles { get; set; }

    [JsonPropertyName("failedFiles")]
    public int FailedFiles { get; set; }

    [JsonPropertyName("progressPercent")]
    public double ProgressPercent { get; set; }

    [JsonPropertyName("estimatedTimeRemainingSeconds")]
    public int? EstimatedTimeRemainingSeconds { get; set; }

    [JsonPropertyName("filesPerSecond")]
    public double FilesPerSecond { get; set; }

    [JsonPropertyName("errors")]
    public string[]? Errors { get; set; }
}

/// <summary>
/// Scheduled verification configuration.
/// </summary>
public class VerificationScheduleConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("dailyVerificationTime")]
    public string DailyVerificationTime { get; set; } = "03:00";

    [JsonPropertyName("dailyVerificationScope")]
    public string DailyVerificationScope { get; set; } = "Last7Days"; // Last7Days, Last30Days, All

    [JsonPropertyName("weeklyFullVerificationDay")]
    public int WeeklyFullVerificationDay { get; set; } = 0; // 0 = Sunday

    [JsonPropertyName("monthlyFullVerificationDay")]
    public int MonthlyFullVerificationDay { get; set; } = 1;

    [JsonPropertyName("pauseDuringMarketHours")]
    public bool PauseDuringMarketHours { get; set; } = true;

    [JsonPropertyName("maxConcurrentVerifications")]
    public int MaxConcurrentVerifications { get; set; } = 4;
}
