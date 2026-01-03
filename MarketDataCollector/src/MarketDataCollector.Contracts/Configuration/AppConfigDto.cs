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
