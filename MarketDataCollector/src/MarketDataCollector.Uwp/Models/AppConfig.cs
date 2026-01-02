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
