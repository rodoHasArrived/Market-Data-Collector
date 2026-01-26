namespace MarketDataCollector.Infrastructure.Plugins.Core;

/// <summary>
/// Describes what a plugin can do.
/// Capabilities drive routing, fallback, and UI decisions.
/// </summary>
/// <remarks>
/// Design note: This replaces the complex DataSourceCapabilities flags enum
/// with a simpler, queryable record type. Plugin authors set only what they support.
/// </remarks>
public sealed record PluginCapabilities
{
    #region Core Capabilities

    /// <summary>
    /// Supports real-time data streaming (WebSocket/push).
    /// </summary>
    public bool SupportsRealtime { get; init; }

    /// <summary>
    /// Supports historical data backfill.
    /// </summary>
    public bool SupportsHistorical { get; init; }

    /// <summary>
    /// Supports trade execution data (tick-by-tick).
    /// </summary>
    public bool SupportsTrades { get; init; }

    /// <summary>
    /// Supports quote data (BBO).
    /// </summary>
    public bool SupportsQuotes { get; init; }

    /// <summary>
    /// Supports order book depth (L2/L3).
    /// </summary>
    public bool SupportsDepth { get; init; }

    /// <summary>
    /// Supports OHLCV bar aggregates.
    /// </summary>
    public bool SupportsBars { get; init; }

    #endregion

    #region Historical Data Specifics

    /// <summary>
    /// Supports adjusted prices (split/dividend adjusted).
    /// </summary>
    public bool SupportsAdjustedPrices { get; init; }

    /// <summary>
    /// Supports dividend data.
    /// </summary>
    public bool SupportsDividends { get; init; }

    /// <summary>
    /// Supports stock split data.
    /// </summary>
    public bool SupportsSplits { get; init; }

    /// <summary>
    /// Earliest date available for historical data.
    /// </summary>
    public DateOnly? MinHistoricalDate { get; init; }

    /// <summary>
    /// Maximum lookback window from today.
    /// </summary>
    public TimeSpan? MaxHistoricalLookback { get; init; }

    /// <summary>
    /// Supported bar intervals (e.g., ["1min", "5min", "1hour", "1day"]).
    /// </summary>
    public IReadOnlyList<string> SupportedBarIntervals { get; init; } = ["1day"];

    #endregion

    #region Asset Classes

    /// <summary>
    /// Asset classes supported by this plugin.
    /// </summary>
    public IReadOnlySet<AssetClass> SupportedAssetClasses { get; init; } =
        new HashSet<AssetClass> { AssetClass.Equity };

    /// <summary>
    /// Market regions supported (e.g., "US", "UK", "DE").
    /// </summary>
    public IReadOnlySet<string> SupportedMarkets { get; init; } =
        new HashSet<string> { "US" };

    #endregion

    #region Operational Limits

    /// <summary>
    /// Maximum symbols per subscription/request.
    /// </summary>
    public int MaxSymbolsPerRequest { get; init; } = 100;

    /// <summary>
    /// Rate limit policy for this plugin.
    /// </summary>
    public RateLimitPolicy RateLimit { get; init; } = RateLimitPolicy.Unlimited;

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates capabilities for a real-time streaming plugin.
    /// </summary>
    public static PluginCapabilities Realtime(
        bool trades = true,
        bool quotes = true,
        bool depth = false) => new()
    {
        SupportsRealtime = true,
        SupportsHistorical = false,
        SupportsTrades = trades,
        SupportsQuotes = quotes,
        SupportsDepth = depth,
        SupportsBars = false
    };

    /// <summary>
    /// Creates capabilities for a historical data plugin.
    /// </summary>
    public static PluginCapabilities Historical(
        bool bars = true,
        bool adjustedPrices = true,
        TimeSpan? maxLookback = null,
        params string[] barIntervals) => new()
    {
        SupportsRealtime = false,
        SupportsHistorical = true,
        SupportsTrades = false,
        SupportsQuotes = false,
        SupportsDepth = false,
        SupportsBars = bars,
        SupportsAdjustedPrices = adjustedPrices,
        MaxHistoricalLookback = maxLookback,
        SupportedBarIntervals = barIntervals.Length > 0 ? barIntervals : ["1day"]
    };

    /// <summary>
    /// Creates capabilities for a hybrid plugin (real-time + historical).
    /// </summary>
    public static PluginCapabilities Hybrid(
        bool trades = true,
        bool quotes = true,
        bool depth = false,
        bool bars = true,
        bool adjustedPrices = true) => new()
    {
        SupportsRealtime = true,
        SupportsHistorical = true,
        SupportsTrades = trades,
        SupportsQuotes = quotes,
        SupportsDepth = depth,
        SupportsBars = bars,
        SupportsAdjustedPrices = adjustedPrices
    };

    #endregion

    #region Query Methods

    /// <summary>
    /// Checks if this plugin can fulfill the given request.
    /// </summary>
    public bool CanFulfill(DataStreamRequest request)
    {
        // Check real-time vs historical
        if (request.IsRealtime && !SupportsRealtime)
            return false;
        if (request.IsHistorical && !SupportsHistorical)
            return false;

        // Check data types
        if (!request.DataTypes.All(dataType => dataType switch
        {
            DataType.Trade => SupportsTrades,
            DataType.Quote => SupportsQuotes,
            DataType.Depth => SupportsDepth,
            DataType.Bar => SupportsBars,
            DataType.Dividend => SupportsDividends,
            DataType.Split => SupportsSplits,
            _ => false
        }))
            return false;

        // Check symbol count
        if (request.Symbols.Count > MaxSymbolsPerRequest)
            return false;

        // Check bar interval
        if (request.BarInterval is not null &&
            !SupportedBarIntervals.Contains(request.BarInterval))
            return false;

        // Check historical date range
        if (request.From.HasValue)
        {
            if (MinHistoricalDate.HasValue && request.From.Value < MinHistoricalDate.Value)
                return false;

            if (MaxHistoricalLookback.HasValue)
            {
                var earliestAllowed = DateOnly.FromDateTime(
                    DateTime.UtcNow - MaxHistoricalLookback.Value);
                if (request.From.Value < earliestAllowed)
                    return false;
            }
        }

        return true;
    }

    #endregion
}

/// <summary>
/// Rate limit policy for a plugin.
/// </summary>
public sealed record RateLimitPolicy
{
    /// <summary>
    /// Maximum requests per time window.
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Time window for rate limiting.
    /// </summary>
    public TimeSpan Window { get; init; }

    /// <summary>
    /// Burst allowance above the normal rate.
    /// </summary>
    public int BurstAllowance { get; init; }

    /// <summary>
    /// No rate limiting.
    /// </summary>
    public static RateLimitPolicy Unlimited => new()
    {
        MaxRequests = int.MaxValue,
        Window = TimeSpan.FromMinutes(1),
        BurstAllowance = 0
    };

    /// <summary>
    /// Creates a rate limit policy with requests per minute.
    /// </summary>
    public static RateLimitPolicy PerMinute(int requests, int burst = 0) => new()
    {
        MaxRequests = requests,
        Window = TimeSpan.FromMinutes(1),
        BurstAllowance = burst
    };

    /// <summary>
    /// Creates a rate limit policy with requests per hour.
    /// </summary>
    public static RateLimitPolicy PerHour(int requests, int burst = 0) => new()
    {
        MaxRequests = requests,
        Window = TimeSpan.FromHours(1),
        BurstAllowance = burst
    };

    /// <summary>
    /// Creates a rate limit policy with requests per day.
    /// </summary>
    public static RateLimitPolicy PerDay(int requests, int burst = 0) => new()
    {
        MaxRequests = requests,
        Window = TimeSpan.FromDays(1),
        BurstAllowance = burst
    };
}

/// <summary>
/// Asset class enumeration.
/// </summary>
public enum AssetClass
{
    Equity,
    Option,
    Future,
    Forex,
    Crypto,
    Index,
    ETF,
    Bond
}
