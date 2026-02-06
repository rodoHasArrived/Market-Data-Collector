namespace MarketDataCollector.ProviderSdk.Providers;

/// <summary>
/// Unified capability record that consolidates capabilities across all provider types:
/// streaming, historical/backfill, and symbol search. Providers declare their capabilities
/// once and consumers inspect this record to determine feature support.
/// </summary>
public sealed record ProviderCapabilities
{
    #region Provider Type Flags

    /// <summary>Supports real-time streaming data.</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>Supports historical data backfill.</summary>
    public bool SupportsBackfill { get; init; }

    /// <summary>Supports symbol search/lookup.</summary>
    public bool SupportsSymbolSearch { get; init; }

    #endregion

    #region Streaming Capabilities

    /// <summary>Supports real-time trade data.</summary>
    public bool SupportsRealtimeTrades { get; init; }

    /// <summary>Supports real-time quote data.</summary>
    public bool SupportsRealtimeQuotes { get; init; }

    /// <summary>Supports market depth/order book data.</summary>
    public bool SupportsMarketDepth { get; init; }

    /// <summary>Maximum depth levels supported (null = unlimited).</summary>
    public int? MaxDepthLevels { get; init; }

    #endregion

    #region Backfill Capabilities

    /// <summary>Returns split/dividend adjusted prices.</summary>
    public bool SupportsAdjustedPrices { get; init; }

    /// <summary>Supports intraday bar data.</summary>
    public bool SupportsIntraday { get; init; }

    /// <summary>Includes dividend data.</summary>
    public bool SupportsDividends { get; init; }

    /// <summary>Includes split data.</summary>
    public bool SupportsSplits { get; init; }

    /// <summary>Supports historical quote (NBBO) data.</summary>
    public bool SupportsHistoricalQuotes { get; init; }

    /// <summary>Supports historical trade data.</summary>
    public bool SupportsHistoricalTrades { get; init; }

    /// <summary>Supports historical auction data.</summary>
    public bool SupportsHistoricalAuctions { get; init; }

    #endregion

    #region Symbol Search Capabilities

    /// <summary>Supports filtering by asset type.</summary>
    public bool SupportsAssetTypeFilter { get; init; }

    /// <summary>Supports filtering by exchange.</summary>
    public bool SupportsExchangeFilter { get; init; }

    #endregion

    #region Market Coverage

    /// <summary>Supported market regions (e.g., "US", "UK", "DE").</summary>
    public IReadOnlyList<string> SupportedMarkets { get; init; } = new[] { "US" };

    #endregion

    #region Rate Limiting

    /// <summary>Maximum requests per time window.</summary>
    public int? MaxRequestsPerWindow { get; init; }

    /// <summary>Rate limit time window.</summary>
    public TimeSpan? RateLimitWindow { get; init; }

    /// <summary>Minimum delay between requests.</summary>
    public TimeSpan? MinRequestDelay { get; init; }

    #endregion

    #region Factory Methods

    /// <summary>Default empty capabilities.</summary>
    public static ProviderCapabilities None { get; } = new();

    /// <summary>Basic streaming provider capabilities.</summary>
    public static ProviderCapabilities Streaming(
        bool trades = true,
        bool quotes = true,
        bool depth = false,
        int? maxDepthLevels = null) => new()
    {
        SupportsStreaming = true,
        SupportsRealtimeTrades = trades,
        SupportsRealtimeQuotes = quotes,
        SupportsMarketDepth = depth,
        MaxDepthLevels = maxDepthLevels
    };

    /// <summary>Basic backfill provider with daily bars only.</summary>
    public static ProviderCapabilities BackfillBarsOnly { get; } = new()
    {
        SupportsBackfill = true,
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true
    };

    /// <summary>Full-featured backfill provider.</summary>
    public static ProviderCapabilities BackfillFullFeatured { get; } = new()
    {
        SupportsBackfill = true,
        SupportsAdjustedPrices = true,
        SupportsIntraday = true,
        SupportsDividends = true,
        SupportsSplits = true,
        SupportsHistoricalQuotes = true,
        SupportsHistoricalTrades = true,
        SupportsHistoricalAuctions = true
    };

    /// <summary>Basic symbol search provider.</summary>
    public static ProviderCapabilities SymbolSearchOnly { get; } = new()
    {
        SupportsSymbolSearch = true
    };

    /// <summary>Hybrid provider supporting both streaming and backfill.</summary>
    public static ProviderCapabilities Hybrid(
        bool trades = true,
        bool quotes = true,
        bool depth = false,
        bool adjustedPrices = true,
        bool intraday = true) => new()
    {
        SupportsStreaming = true,
        SupportsBackfill = true,
        SupportsRealtimeTrades = trades,
        SupportsRealtimeQuotes = quotes,
        SupportsMarketDepth = depth,
        SupportsAdjustedPrices = adjustedPrices,
        SupportsIntraday = intraday,
        SupportsDividends = true,
        SupportsSplits = true
    };

    #endregion

    #region Computed Properties

    /// <summary>Whether the provider has any tick-level historical data.</summary>
    public bool HasTickData => SupportsHistoricalQuotes || SupportsHistoricalTrades || SupportsHistoricalAuctions;

    /// <summary>Whether the provider has corporate action data.</summary>
    public bool HasCorporateActions => SupportsDividends || SupportsSplits;

    /// <summary>Whether the provider supports a specific market.</summary>
    public bool SupportsMarket(string market) =>
        SupportedMarkets.Contains(market, StringComparer.OrdinalIgnoreCase);

    #endregion
}
