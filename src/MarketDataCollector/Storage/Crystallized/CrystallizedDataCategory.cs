namespace MarketDataCollector.Storage.Crystallized;

/// <summary>
/// Categorizes market data into logical groups for storage organization.
/// Each category has standardized column schemas for consistency.
/// </summary>
public enum CrystallizedDataCategory
{
    /// <summary>
    /// OHLCV bar/candle data at various time granularities.
    /// Columns: timestamp, open, high, low, close, volume, [vwap], [trades_count]
    /// </summary>
    Bars,

    /// <summary>
    /// Tick-by-tick trade prints.
    /// Columns: timestamp, price, size, side, sequence, [venue], [conditions]
    /// </summary>
    Trades,

    /// <summary>
    /// Best bid/offer quote data.
    /// Columns: timestamp, bid_price, bid_size, ask_price, ask_size, spread, [sequence]
    /// </summary>
    Quotes,

    /// <summary>
    /// Level 2 order book snapshots (multiple price levels).
    /// Columns: timestamp, level, bid_price, bid_size, ask_price, ask_size, [sequence]
    /// </summary>
    OrderBook,

    /// <summary>
    /// Computed order flow statistics and analytics.
    /// Columns: timestamp, imbalance, vwap, buy_volume, sell_volume, [sequence]
    /// </summary>
    OrderFlow,

    /// <summary>
    /// Auction/opening/closing data.
    /// Columns: timestamp, auction_type, price, volume, imbalance
    /// </summary>
    Auctions,

    /// <summary>
    /// Corporate actions (dividends, splits, etc.).
    /// Columns: timestamp, action_type, factor, ex_date, [description]
    /// </summary>
    CorporateActions,

    /// <summary>
    /// System/operational events (connections, integrity, heartbeats).
    /// Columns: timestamp, event_type, details, [sequence]
    /// </summary>
    System
}

/// <summary>
/// Extension methods for CrystallizedDataCategory.
/// </summary>
public static class CrystallizedDataCategoryExtensions
{
    /// <summary>
    /// Gets the folder name for a data category.
    /// </summary>
    public static string ToFolderName(this CrystallizedDataCategory category) => category switch
    {
        CrystallizedDataCategory.Bars => "bars",
        CrystallizedDataCategory.Trades => "trades",
        CrystallizedDataCategory.Quotes => "quotes",
        CrystallizedDataCategory.OrderBook => "orderbook",
        CrystallizedDataCategory.OrderFlow => "orderflow",
        CrystallizedDataCategory.Auctions => "auctions",
        CrystallizedDataCategory.CorporateActions => "corporate_actions",
        CrystallizedDataCategory.System => "_system",
        _ => "other"
    };

    /// <summary>
    /// Gets the display name for a data category.
    /// </summary>
    public static string ToDisplayName(this CrystallizedDataCategory category) => category switch
    {
        CrystallizedDataCategory.Bars => "Price Bars (OHLCV)",
        CrystallizedDataCategory.Trades => "Trade Prints",
        CrystallizedDataCategory.Quotes => "Best Bid/Offer Quotes",
        CrystallizedDataCategory.OrderBook => "Order Book (Level 2)",
        CrystallizedDataCategory.OrderFlow => "Order Flow Statistics",
        CrystallizedDataCategory.Auctions => "Auction Data",
        CrystallizedDataCategory.CorporateActions => "Corporate Actions",
        CrystallizedDataCategory.System => "System Events",
        _ => "Other"
    };

    /// <summary>
    /// Gets the standard CSV column headers for a data category.
    /// </summary>
    public static string[] GetCsvHeaders(this CrystallizedDataCategory category) => category switch
    {
        CrystallizedDataCategory.Bars => new[]
        {
            "timestamp", "open", "high", "low", "close", "volume", "vwap", "trades_count"
        },
        CrystallizedDataCategory.Trades => new[]
        {
            "timestamp", "price", "size", "side", "sequence", "venue", "conditions"
        },
        CrystallizedDataCategory.Quotes => new[]
        {
            "timestamp", "bid_price", "bid_size", "ask_price", "ask_size", "spread", "mid_price", "sequence"
        },
        CrystallizedDataCategory.OrderBook => new[]
        {
            "timestamp", "level", "bid_price", "bid_size", "ask_price", "ask_size", "sequence"
        },
        CrystallizedDataCategory.OrderFlow => new[]
        {
            "timestamp", "imbalance", "vwap", "buy_volume", "sell_volume", "total_volume", "sequence"
        },
        CrystallizedDataCategory.Auctions => new[]
        {
            "timestamp", "auction_type", "price", "volume", "imbalance"
        },
        CrystallizedDataCategory.CorporateActions => new[]
        {
            "timestamp", "action_type", "factor", "ex_date", "description"
        },
        CrystallizedDataCategory.System => new[]
        {
            "timestamp", "event_type", "details", "sequence"
        },
        _ => new[] { "timestamp", "data" }
    };

    /// <summary>
    /// Gets a description for documentation purposes.
    /// </summary>
    public static string GetDescription(this CrystallizedDataCategory category) => category switch
    {
        CrystallizedDataCategory.Bars =>
            "OHLCV price bars aggregated at various time intervals. " +
            "Use 'daily' for end-of-day analysis, '1m' or '5m' for intraday strategies.",
        CrystallizedDataCategory.Trades =>
            "Individual trade executions (tick data). Each row represents one trade print. " +
            "High volume - use for precise backtest fills and market microstructure analysis.",
        CrystallizedDataCategory.Quotes =>
            "Best bid/offer (BBO) snapshots. Use for spread analysis and quote-based strategies. " +
            "Lower volume than trades but captures liquidity at top of book.",
        CrystallizedDataCategory.OrderBook =>
            "Full Level 2 order book with multiple price levels. " +
            "Use for order book visualization, depth analysis, and advanced market making.",
        CrystallizedDataCategory.OrderFlow =>
            "Pre-computed order flow statistics including volume imbalance and VWAP. " +
            "Derived from trade data - use for order flow trading strategies.",
        CrystallizedDataCategory.Auctions =>
            "Opening/closing auction data including indicative prices and imbalances. " +
            "Critical for MOO/MOC order strategies.",
        CrystallizedDataCategory.CorporateActions =>
            "Dividend announcements, stock splits, and other corporate actions. " +
            "Essential for adjusting historical prices correctly.",
        CrystallizedDataCategory.System =>
            "System-level events including connection status, data quality alerts, and heartbeats. " +
            "Use for monitoring and debugging data collection.",
        _ => "Unknown data category."
    };
}
