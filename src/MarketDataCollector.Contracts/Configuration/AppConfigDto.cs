using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Configuration;

/// <summary>
/// Application configuration DTO shared between core and UWP applications.
/// </summary>
public class AppConfigDto
{
    [JsonPropertyName("dataRoot")]
    public string DataRoot { get; set; } = "data";

    [JsonPropertyName("compress")]
    public bool Compress { get; set; }

    [JsonPropertyName("dataSource")]
    public string DataSource { get; set; } = "IB";

    [JsonPropertyName("alpaca")]
    public AlpacaOptionsDto? Alpaca { get; set; }

    [JsonPropertyName("stockSharp")]
    public StockSharpOptionsDto? StockSharp { get; set; }

    [JsonPropertyName("storage")]
    public StorageConfigDto? Storage { get; set; }

    [JsonPropertyName("symbols")]
    public SymbolConfigDto[]? Symbols { get; set; }

    [JsonPropertyName("backfill")]
    public BackfillConfigDto? Backfill { get; set; }

    [JsonPropertyName("dataSources")]
    public DataSourcesConfigDto? DataSources { get; set; }

    [JsonPropertyName("symbolGroups")]
    public SymbolGroupsConfigDto? SymbolGroups { get; set; }

    [JsonPropertyName("settings")]
    public AppSettingsDto? Settings { get; set; }
}

/// <summary>
/// Alpaca provider configuration.
/// </summary>
public class AlpacaOptionsDto
{
    [JsonPropertyName("keyId")]
    public string? KeyId { get; set; }

    [JsonPropertyName("secretKey")]
    public string? SecretKey { get; set; }

    [JsonPropertyName("feed")]
    public string Feed { get; set; } = "iex";

    [JsonPropertyName("useSandbox")]
    public bool UseSandbox { get; set; }

    [JsonPropertyName("subscribeQuotes")]
    public bool SubscribeQuotes { get; set; }
}

/// <summary>
/// StockSharp connector configuration.
/// </summary>
public class StockSharpOptionsDto
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("connectorType")]
    public string ConnectorType { get; set; } = "Rithmic";

    [JsonPropertyName("adapterType")]
    public string? AdapterType { get; set; }

    [JsonPropertyName("adapterAssembly")]
    public string? AdapterAssembly { get; set; }

    [JsonPropertyName("connectionParams")]
    public Dictionary<string, string>? ConnectionParams { get; set; }

    [JsonPropertyName("useBinaryStorage")]
    public bool UseBinaryStorage { get; set; }

    [JsonPropertyName("storagePath")]
    public string StoragePath { get; set; } = "data/stocksharp/{connector}";

    [JsonPropertyName("enableRealTime")]
    public bool EnableRealTime { get; set; } = true;

    [JsonPropertyName("enableHistorical")]
    public bool EnableHistorical { get; set; } = true;

    [JsonPropertyName("rithmic")]
    public RithmicOptionsDto? Rithmic { get; set; }

    [JsonPropertyName("iqFeed")]
    public IQFeedOptionsDto? IQFeed { get; set; }

    [JsonPropertyName("cqg")]
    public CQGOptionsDto? CQG { get; set; }

    [JsonPropertyName("interactiveBrokers")]
    public StockSharpIBOptionsDto? InteractiveBrokers { get; set; }
}

public class RithmicOptionsDto
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "Rithmic Test";

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("certFile")]
    public string? CertFile { get; set; }

    [JsonPropertyName("usePaperTrading")]
    public bool UsePaperTrading { get; set; } = true;
}

public class IQFeedOptionsDto
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("level1Port")]
    public int Level1Port { get; set; } = 9100;

    [JsonPropertyName("level2Port")]
    public int Level2Port { get; set; } = 9200;

    [JsonPropertyName("lookupPort")]
    public int LookupPort { get; set; } = 9300;

    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("productVersion")]
    public string ProductVersion { get; set; } = "1.0";
}

public class CQGOptionsDto
{
    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("useDemoServer")]
    public bool UseDemoServer { get; set; } = true;
}

public class StockSharpIBOptionsDto
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 7496;

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; } = 1;
}

/// <summary>
/// Storage configuration.
/// </summary>
public class StorageConfigDto
{
    [JsonPropertyName("namingConvention")]
    public string NamingConvention { get; set; } = "BySymbol";

    [JsonPropertyName("datePartition")]
    public string DatePartition { get; set; } = "Daily";

    [JsonPropertyName("includeProvider")]
    public bool IncludeProvider { get; set; }

    [JsonPropertyName("filePrefix")]
    public string? FilePrefix { get; set; }

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("retentionDays")]
    public int? RetentionDays { get; set; }

    [JsonPropertyName("maxTotalMegabytes")]
    public long? MaxTotalMegabytes { get; set; }
}

/// <summary>
/// Symbol subscription configuration.
/// </summary>
public class SymbolConfigDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("subscribeTrades")]
    public bool SubscribeTrades { get; set; } = true;

    [JsonPropertyName("subscribeDepth")]
    public bool SubscribeDepth { get; set; }

    [JsonPropertyName("depthLevels")]
    public int DepthLevels { get; set; } = 10;

    [JsonPropertyName("securityType")]
    public string SecurityType { get; set; } = "STK";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "SMART";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("primaryExchange")]
    public string? PrimaryExchange { get; set; }

    [JsonPropertyName("localSymbol")]
    public string? LocalSymbol { get; set; }
}

/// <summary>
/// Backfill configuration.
/// </summary>
public class BackfillConfigDto
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "stooq";

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("enableFallback")]
    public bool EnableFallback { get; set; } = true;

    [JsonPropertyName("enableSymbolResolution")]
    public bool EnableSymbolResolution { get; set; } = true;
}

/// <summary>
/// Multiple data source configuration.
/// </summary>
public class DataSourcesConfigDto
{
    [JsonPropertyName("sources")]
    public DataSourceConfigDto[]? Sources { get; set; }

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
/// Individual data source configuration.
/// </summary>
public class DataSourceConfigDto
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

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("alpaca")]
    public AlpacaOptionsDto? Alpaca { get; set; }

    [JsonPropertyName("polygon")]
    public PolygonOptionsDto? Polygon { get; set; }

    [JsonPropertyName("ib")]
    public IBOptionsDto? IB { get; set; }

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }
}

/// <summary>
/// Polygon.io API configuration options.
/// </summary>
public class PolygonOptionsDto
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
public class IBOptionsDto
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
/// Symbol groups configuration.
/// </summary>
public class SymbolGroupsConfigDto
{
    [JsonPropertyName("groups")]
    public SymbolGroupDto[]? Groups { get; set; }

    [JsonPropertyName("defaultGroupId")]
    public string? DefaultGroupId { get; set; }

    [JsonPropertyName("showUngroupedSymbols")]
    public bool ShowUngroupedSymbols { get; set; } = true;

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "Name";

    [JsonPropertyName("viewMode")]
    public string ViewMode { get; set; } = "Tree";
}

/// <summary>
/// Symbol group definition.
/// </summary>
public class SymbolGroupDto
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
    public SmartGroupCriteriaDto? SmartCriteria { get; set; }
}

/// <summary>
/// Criteria for smart/dynamic symbol groups.
/// </summary>
public class SmartGroupCriteriaDto
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
public class ExtendedSymbolConfigDto : SymbolConfigDto
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
/// Application UI settings.
/// </summary>
public class AppSettingsDto
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System";

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "System";

    [JsonPropertyName("compactMode")]
    public bool CompactMode { get; set; }

    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    [JsonPropertyName("autoReconnectEnabled")]
    public bool AutoReconnectEnabled { get; set; } = true;

    [JsonPropertyName("maxReconnectAttempts")]
    public int MaxReconnectAttempts { get; set; } = 10;

    [JsonPropertyName("statusRefreshIntervalSeconds")]
    public int StatusRefreshIntervalSeconds { get; set; } = 2;
}
