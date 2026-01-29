namespace MarketDataCollector.Application.Config;

/// <summary>
/// Configuration for StockSharp connector integration.
/// Provides access to 90+ data sources through StockSharp's unified connector framework.
/// </summary>
public sealed record StockSharpConfig(
    /// <summary>Whether StockSharp integration is enabled.</summary>
    bool Enabled = false,

    /// <summary>Primary connector type (e.g., "Rithmic", "IQFeed", "CQG", "InteractiveBrokers", "Custom").</summary>
    string ConnectorType = "Rithmic",

    /// <summary>Fully qualified StockSharp adapter type for custom connectors.</summary>
    string? AdapterType = null,

    /// <summary>Optional assembly name for custom adapters (if AdapterType is not assembly-qualified).</summary>
    string? AdapterAssembly = null,

    /// <summary>Connection parameters specific to the connector.</summary>
    Dictionary<string, string>? ConnectionParams = null,

    /// <summary>Whether to use StockSharp binary storage format (2 bytes/trade, 7 bytes/order book).</summary>
    bool UseBinaryStorage = false,

    /// <summary>Path to StockSharp storage directory. Supports {connector} placeholder.</summary>
    string StoragePath = "data/stocksharp/{connector}",

    /// <summary>Whether to enable real-time data streaming.</summary>
    bool EnableRealTime = true,

    /// <summary>Whether to enable historical data downloads.</summary>
    bool EnableHistorical = true,

    /// <summary>Rithmic-specific configuration.</summary>
    RithmicConfig? Rithmic = null,

    /// <summary>IQFeed-specific configuration.</summary>
    IQFeedConfig? IQFeed = null,

    /// <summary>CQG-specific configuration.</summary>
    CQGConfig? CQG = null,

    /// <summary>Interactive Brokers-specific configuration for StockSharp connector.</summary>
    StockSharpIBConfig? InteractiveBrokers = null
);

/// <summary>
/// Rithmic-specific configuration.
/// Rithmic provides low-latency futures data for CME, NYMEX, COMEX, etc.
/// </summary>
public sealed record RithmicConfig(
    /// <summary>Rithmic server environment (e.g., "Rithmic Test", "Rithmic Paper Trading", "Rithmic 01").</summary>
    string Server = "Rithmic Test",

    /// <summary>Rithmic account username.</summary>
    string UserName = "",

    /// <summary>Rithmic account password.</summary>
    string Password = "",

    /// <summary>Path to SSL certificate file for Rithmic connection.</summary>
    string CertFile = "",

    /// <summary>Whether to use paper trading environment.</summary>
    bool UsePaperTrading = true
);

/// <summary>
/// IQFeed-specific configuration.
/// IQFeed provides tick-level equities data with historical lookups.
/// </summary>
public sealed record IQFeedConfig(
    /// <summary>IQFeed server host address.</summary>
    string Host = "127.0.0.1",

    /// <summary>Port for Level 1 (quotes) data.</summary>
    int Level1Port = 9100,

    /// <summary>Port for Level 2 (market depth) data.</summary>
    int Level2Port = 9200,

    /// <summary>Port for historical data lookup.</summary>
    int LookupPort = 9300,

    /// <summary>DTN product ID for IQFeed registration.</summary>
    string ProductId = "",

    /// <summary>DTN product version string.</summary>
    string ProductVersion = "1.0"
);

/// <summary>
/// CQG-specific configuration.
/// CQG provides futures/options data with excellent historical coverage.
/// </summary>
public sealed record CQGConfig(
    /// <summary>CQG account username.</summary>
    string UserName = "",

    /// <summary>CQG account password.</summary>
    string Password = "",

    /// <summary>Whether to use demo/paper trading server.</summary>
    bool UseDemoServer = true
);

/// <summary>
/// Interactive Brokers configuration for StockSharp connector.
/// Alternative to native IB TWS API integration.
/// </summary>
public sealed record StockSharpIBConfig(
    /// <summary>TWS/Gateway host address.</summary>
    string Host = "127.0.0.1",

    /// <summary>TWS/Gateway port (7496 for TWS, 4001 for Gateway).</summary>
    int Port = 7496,

    /// <summary>Client ID for IB connection.</summary>
    int ClientId = 1
);
