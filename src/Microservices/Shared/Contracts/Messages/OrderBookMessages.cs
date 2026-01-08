namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Message for raw order book snapshot ingestion.
/// </summary>
public interface IRawOrderBookIngested : ISymbolIngestionMessage
{
    IReadOnlyList<OrderBookLevel> Bids { get; }
    IReadOnlyList<OrderBookLevel> Asks { get; }
    int DepthLevels { get; }
    string? Exchange { get; }
}

/// <summary>
/// Message for order book delta/update ingestion.
/// </summary>
public interface IRawOrderBookUpdate : ISymbolIngestionMessage
{
    OrderBookUpdateType UpdateType { get; }
    OrderBookSide Side { get; }
    int Position { get; }
    decimal? Price { get; }
    long? Size { get; }
    string? MarketMaker { get; }
    string? Exchange { get; }
}

/// <summary>
/// Message for validated order book snapshot.
/// </summary>
public interface IValidatedOrderBook : ISymbolIngestionMessage
{
    IReadOnlyList<OrderBookLevel> Bids { get; }
    IReadOnlyList<OrderBookLevel> Asks { get; }
    bool IsValid { get; }
    string[] ValidationErrors { get; }
    decimal? Spread { get; }
    decimal? MidPrice { get; }
    decimal? Imbalance { get; }
}

/// <summary>
/// Order book level data.
/// </summary>
public record OrderBookLevel(
    decimal Price,
    long Size,
    string? MarketMaker = null,
    int? OrderCount = null
);

/// <summary>
/// Order book update types.
/// </summary>
public enum OrderBookUpdateType
{
    Insert,
    Update,
    Delete
}

/// <summary>
/// Order book side.
/// </summary>
public enum OrderBookSide
{
    Bid,
    Ask
}

/// <summary>
/// Command to rebuild order book from updates.
/// </summary>
public interface IRebuildOrderBook : IIngestionMessage
{
    string Symbol { get; }
    string? Exchange { get; }
}
