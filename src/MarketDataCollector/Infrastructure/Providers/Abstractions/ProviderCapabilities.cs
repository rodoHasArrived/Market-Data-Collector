namespace MarketDataCollector.Infrastructure.Providers.Abstractions;

/// <summary>
/// Declarative capability flags for provider feature discovery.
/// Enables runtime capability checking without provider-specific code.
/// Use bitwise operations to combine capabilities.
/// </summary>
[Flags]
public enum ProviderCapabilities : long
{
    None = 0,

    // ─── Real-time Streaming Capabilities ───────────────────────────────
    /// <summary>Real-time trade data (tick-by-tick).</summary>
    RealTimeTrades = 1L << 0,

    /// <summary>Real-time BBO quotes.</summary>
    RealTimeQuotes = 1L << 1,

    /// <summary>Level 2 market depth (order book).</summary>
    Level2Depth = 1L << 2,

    /// <summary>Full order book (Level 3).</summary>
    Level3FullBook = 1L << 3,

    /// <summary>Opening/closing imbalance data.</summary>
    ImbalanceData = 1L << 4,

    /// <summary>Real-time bar aggregates.</summary>
    RealTimeAggregates = 1L << 5,

    // ─── Historical Data Capabilities ───────────────────────────────────
    /// <summary>End-of-day (daily) historical bars.</summary>
    HistoricalDailyBars = 1L << 10,

    /// <summary>Intraday historical bars (minute/hourly).</summary>
    HistoricalIntradayBars = 1L << 11,

    /// <summary>Historical tick data.</summary>
    HistoricalTicks = 1L << 12,

    /// <summary>Historical aggregate data.</summary>
    HistoricalAggregates = 1L << 13,

    /// <summary>Split/dividend adjusted prices.</summary>
    AdjustedPrices = 1L << 14,

    /// <summary>Corporate actions data.</summary>
    CorporateActions = 1L << 15,

    /// <summary>Dividend data.</summary>
    Dividends = 1L << 16,

    /// <summary>Stock split data.</summary>
    Splits = 1L << 17,

    /// <summary>Free equity/cash data from broker.</summary>
    FreeEquity = 1L << 18,

    // ─── Asset Class Support ────────────────────────────────────────────
    /// <summary>Equities/stocks.</summary>
    Equities = 1L << 20,

    /// <summary>Stock options.</summary>
    Options = 1L << 21,

    /// <summary>Futures contracts.</summary>
    Futures = 1L << 22,

    /// <summary>Foreign exchange.</summary>
    Forex = 1L << 23,

    /// <summary>Cryptocurrencies.</summary>
    Crypto = 1L << 24,

    /// <summary>Indices.</summary>
    Indices = 1L << 25,

    /// <summary>Exchange-traded funds.</summary>
    ETFs = 1L << 26,

    /// <summary>Bonds/fixed income.</summary>
    Bonds = 1L << 27,

    // ─── Market Coverage ────────────────────────────────────────────────
    /// <summary>US markets (NYSE, NASDAQ, etc.).</summary>
    USMarkets = 1L << 30,

    /// <summary>European markets.</summary>
    EuropeanMarkets = 1L << 31,

    /// <summary>Asian markets.</summary>
    AsianMarkets = 1L << 32,

    /// <summary>Global market coverage.</summary>
    GlobalMarkets = 1L << 33,

    /// <summary>Extended hours trading data.</summary>
    ExtendedHours = 1L << 34,

    /// <summary>Pre-market data.</summary>
    PreMarket = 1L << 35,

    /// <summary>After-hours data.</summary>
    AfterHours = 1L << 36,

    // ─── Data Quality Features ──────────────────────────────────────────
    /// <summary>Sequence numbers for ordering.</summary>
    SequenceNumbers = 1L << 40,

    /// <summary>Exchange-level timestamps.</summary>
    ExchangeTimestamps = 1L << 41,

    /// <summary>Trade condition codes.</summary>
    TradeConditions = 1L << 42,

    /// <summary>Venue/exchange identification.</summary>
    VenueIdentification = 1L << 43,

    // ─── Connection Features ────────────────────────────────────────────
    /// <summary>WebSocket streaming support.</summary>
    WebSocketStreaming = 1L << 50,

    /// <summary>REST API support.</summary>
    RestApi = 1L << 51,

    /// <summary>FIX protocol support.</summary>
    FixProtocol = 1L << 52,

    /// <summary>Automatic reconnection.</summary>
    AutoReconnect = 1L << 53,

    /// <summary>Heartbeat/keep-alive support.</summary>
    Heartbeat = 1L << 54,

    /// <summary>Sandbox/paper trading mode.</summary>
    SandboxMode = 1L << 55,

    // ─── Common Capability Groups ───────────────────────────────────────
    /// <summary>All real-time streaming capabilities.</summary>
    AllRealTime = RealTimeTrades | RealTimeQuotes | Level2Depth | Level3FullBook | ImbalanceData | RealTimeAggregates,

    /// <summary>All historical capabilities.</summary>
    AllHistorical = HistoricalDailyBars | HistoricalIntradayBars | HistoricalTicks | HistoricalAggregates | AdjustedPrices,

    /// <summary>Standard equity provider capabilities.</summary>
    StandardEquity = Equities | ETFs | RealTimeTrades | RealTimeQuotes | HistoricalDailyBars | USMarkets,
}

/// <summary>
/// Extension methods for ProviderCapabilities.
/// </summary>
public static class ProviderCapabilitiesExtensions
{
    /// <summary>
    /// Check if the capability set includes all of the required capabilities.
    /// </summary>
    public static bool HasAll(this ProviderCapabilities capabilities, ProviderCapabilities required)
        => (capabilities & required) == required;

    /// <summary>
    /// Check if the capability set includes any of the specified capabilities.
    /// </summary>
    public static bool HasAny(this ProviderCapabilities capabilities, ProviderCapabilities any)
        => (capabilities & any) != 0;

    /// <summary>
    /// Get a human-readable list of capabilities.
    /// </summary>
    public static IEnumerable<string> ToStringList(this ProviderCapabilities capabilities)
    {
        foreach (ProviderCapabilities value in Enum.GetValues<ProviderCapabilities>())
        {
            if (value != ProviderCapabilities.None &&
                !value.ToString().StartsWith("All") &&
                capabilities.HasAll(value))
            {
                yield return value.ToString();
            }
        }
    }

    /// <summary>
    /// Check if this is a streaming provider.
    /// </summary>
    public static bool IsStreamingProvider(this ProviderCapabilities capabilities)
        => capabilities.HasAny(ProviderCapabilities.RealTimeTrades | ProviderCapabilities.RealTimeQuotes | ProviderCapabilities.Level2Depth);

    /// <summary>
    /// Check if this is a historical data provider.
    /// </summary>
    public static bool IsHistoricalProvider(this ProviderCapabilities capabilities)
        => capabilities.HasAny(ProviderCapabilities.HistoricalDailyBars | ProviderCapabilities.HistoricalIntradayBars | HistoricalTicks);

    private const ProviderCapabilities HistoricalTicks = ProviderCapabilities.HistoricalTicks;
}
