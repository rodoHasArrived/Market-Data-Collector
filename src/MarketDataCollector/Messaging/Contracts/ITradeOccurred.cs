namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published when a trade occurs for a subscribed symbol.
/// </summary>
public interface ITradeOccurred : IMarketEventMessage
{
    /// <summary>
    /// Trade execution price.
    /// </summary>
    decimal Price { get; }

    /// <summary>
    /// Trade size/volume.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Aggressor side: "Buy", "Sell", or "Unknown".
    /// </summary>
    string AggressorSide { get; }

    /// <summary>
    /// Unique trade identifier from the exchange.
    /// </summary>
    string? TradeId { get; }

    /// <summary>
    /// Execution venue (e.g., exchange identifier).
    /// </summary>
    string? Venue { get; }
}
