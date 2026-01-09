namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Represents a position from a broker portfolio.
/// </summary>
public sealed record PortfolioPosition(
    /// <summary>Ticker symbol.</summary>
    string Symbol,

    /// <summary>Quantity held (can be negative for short positions).</summary>
    decimal Quantity,

    /// <summary>Current market value in account currency.</summary>
    decimal? MarketValue,

    /// <summary>Average entry cost.</summary>
    decimal? AverageCost,

    /// <summary>Unrealized profit/loss.</summary>
    decimal? UnrealizedPnL,

    /// <summary>Asset class (stock, etf, option, etc.).</summary>
    string? AssetClass,

    /// <summary>Exchange where the position is held.</summary>
    string? Exchange,

    /// <summary>Currency of the position.</summary>
    string Currency = "USD",

    /// <summary>Side: long or short.</summary>
    string Side = "long"
);

/// <summary>
/// Portfolio summary from a broker.
/// </summary>
public sealed record PortfolioSummary(
    /// <summary>Broker name.</summary>
    string Broker,

    /// <summary>Account identifier.</summary>
    string AccountId,

    /// <summary>Total portfolio value.</summary>
    decimal? TotalValue,

    /// <summary>Cash balance.</summary>
    decimal? CashBalance,

    /// <summary>Buying power available.</summary>
    decimal? BuyingPower,

    /// <summary>All positions in the portfolio.</summary>
    PortfolioPosition[] Positions,

    /// <summary>When the portfolio data was retrieved.</summary>
    DateTimeOffset RetrievedAt,

    /// <summary>Currency of the account.</summary>
    string Currency = "USD"
);

/// <summary>
/// Request to import symbols from a broker portfolio.
/// </summary>
public sealed record PortfolioImportRequest(
    /// <summary>Broker to import from (alpaca, ib, manual).</summary>
    string Broker,

    /// <summary>Import options.</summary>
    PortfolioImportOptions Options
);

/// <summary>
/// Options for portfolio import.
/// </summary>
public sealed record PortfolioImportOptions(
    /// <summary>Only import positions with market value above this threshold.</summary>
    decimal? MinPositionValue = null,

    /// <summary>Only import positions with quantity above this threshold.</summary>
    decimal? MinQuantity = null,

    /// <summary>Asset classes to include (null = all).</summary>
    string[]? AssetClasses = null,

    /// <summary>Exclude specific symbols.</summary>
    string[]? ExcludeSymbols = null,

    /// <summary>Only include long positions.</summary>
    bool LongOnly = false,

    /// <summary>Create a watchlist for imported positions.</summary>
    bool CreateWatchlist = false,

    /// <summary>Name for the created watchlist.</summary>
    string? WatchlistName = null,

    /// <summary>Subscribe to trades for imported symbols.</summary>
    bool SubscribeTrades = true,

    /// <summary>Subscribe to depth for imported symbols.</summary>
    bool SubscribeDepth = true,

    /// <summary>Skip symbols that are already subscribed.</summary>
    bool SkipExisting = true
);

/// <summary>
/// Result of portfolio import.
/// </summary>
public sealed record PortfolioImportResult(
    /// <summary>Whether the import succeeded.</summary>
    bool Success,

    /// <summary>Broker that was imported from.</summary>
    string Broker,

    /// <summary>Number of symbols imported.</summary>
    int ImportedCount,

    /// <summary>Number of symbols skipped.</summary>
    int SkippedCount,

    /// <summary>Number of symbols that failed to import.</summary>
    int FailedCount,

    /// <summary>Symbols that were imported.</summary>
    string[] ImportedSymbols,

    /// <summary>Any errors that occurred.</summary>
    string[] Errors,

    /// <summary>Watchlist ID if one was created.</summary>
    string? WatchlistId = null,

    /// <summary>Portfolio summary if available.</summary>
    PortfolioSummary? Portfolio = null
);

/// <summary>
/// Manual portfolio entry for direct import without broker API.
/// </summary>
public sealed record ManualPortfolioEntry(
    /// <summary>Symbol.</summary>
    string Symbol,

    /// <summary>Quantity (optional).</summary>
    decimal? Quantity = null,

    /// <summary>Asset class (optional).</summary>
    string? AssetClass = null
);

/// <summary>
/// Supported broker types.
/// </summary>
public enum BrokerType
{
    /// <summary>Alpaca Markets</summary>
    Alpaca,

    /// <summary>Interactive Brokers</summary>
    InteractiveBrokers,

    /// <summary>Manual entry (no API)</summary>
    Manual
}
