namespace MarketDataCollector.Application.Config;

/// <summary>
/// Configuration for StockSharp connector integration.
/// Provides access to 90+ data sources through StockSharp's unified connector framework.
/// Supported connectors: Rithmic, IQFeed, CQG, InteractiveBrokers, Binance, Coinbase, Kraken, and more.
/// </summary>
public sealed record StockSharpConfig(
    /// <summary>Whether StockSharp integration is enabled.</summary>
    bool Enabled = false,

    /// <summary>Primary connector type (e.g., "Rithmic", "IQFeed", "CQG", "InteractiveBrokers", "Binance", "Coinbase", "Kraken", "Custom").</summary>
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
    StockSharpIBConfig? InteractiveBrokers = null,

    /// <summary>Binance crypto exchange configuration.</summary>
    BinanceConfig? Binance = null,

    /// <summary>Coinbase crypto exchange configuration.</summary>
    CoinbaseConfig? Coinbase = null,

    /// <summary>Kraken crypto exchange configuration.</summary>
    KrakenConfig? Kraken = null
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

/// <summary>
/// Binance crypto exchange configuration.
/// Supports spot and futures markets with real-time WebSocket streams.
/// Note: Requires StockSharp crowdfunding membership for crypto connectors.
/// </summary>
public sealed record BinanceConfig(
    /// <summary>Binance API key.</summary>
    string ApiKey = "",

    /// <summary>Binance API secret.</summary>
    string ApiSecret = "",

    /// <summary>Whether to use the testnet environment.</summary>
    bool UseTestnet = false,

    /// <summary>Market type: "Spot", "UsdtFutures", "CoinFutures".</summary>
    string MarketType = "Spot",

    /// <summary>Whether to subscribe to order book updates.</summary>
    bool SubscribeOrderBook = true,

    /// <summary>Order book depth level (5, 10, 20).</summary>
    int OrderBookDepth = 20,

    /// <summary>Whether to subscribe to trades.</summary>
    bool SubscribeTrades = true
);

/// <summary>
/// Coinbase crypto exchange configuration.
/// Supports Coinbase Pro (Advanced Trade) API.
/// </summary>
public sealed record CoinbaseConfig(
    /// <summary>Coinbase API key.</summary>
    string ApiKey = "",

    /// <summary>Coinbase API secret.</summary>
    string ApiSecret = "",

    /// <summary>Coinbase API passphrase.</summary>
    string Passphrase = "",

    /// <summary>Whether to use the sandbox environment.</summary>
    bool UseSandbox = false,

    /// <summary>Whether to subscribe to order book updates.</summary>
    bool SubscribeOrderBook = true,

    /// <summary>Order book subscription level: "level2" or "level3".</summary>
    string OrderBookLevel = "level2",

    /// <summary>Whether to subscribe to trades.</summary>
    bool SubscribeTrades = true
);

/// <summary>
/// Kraken crypto exchange configuration.
/// Supports spot markets with WebSocket streams.
/// </summary>
public sealed record KrakenConfig(
    /// <summary>Kraken API key.</summary>
    string ApiKey = "",

    /// <summary>Kraken API secret (private key).</summary>
    string ApiSecret = "",

    /// <summary>Whether to subscribe to order book updates.</summary>
    bool SubscribeOrderBook = true,

    /// <summary>Order book depth (10, 25, 100, 500, 1000).</summary>
    int OrderBookDepth = 25,

    /// <summary>Whether to subscribe to trades.</summary>
    bool SubscribeTrades = true,

    /// <summary>Whether to subscribe to OHLC candles.</summary>
    bool SubscribeOhlc = false,

    /// <summary>OHLC interval in minutes (1, 5, 15, 30, 60, 240, 1440, 10080, 21600).</summary>
    int OhlcInterval = 1
);
