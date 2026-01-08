namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published when a Best Bid/Offer (BBO) quote is updated.
/// </summary>
public interface IBboQuoteUpdated : IMarketEventMessage
{
    /// <summary>
    /// Best bid price.
    /// </summary>
    decimal BidPrice { get; }

    /// <summary>
    /// Best bid size.
    /// </summary>
    long BidSize { get; }

    /// <summary>
    /// Best ask price.
    /// </summary>
    decimal AskPrice { get; }

    /// <summary>
    /// Best ask size.
    /// </summary>
    long AskSize { get; }

    /// <summary>
    /// Bid-ask spread.
    /// </summary>
    decimal Spread { get; }

    /// <summary>
    /// Exchange providing the quote.
    /// </summary>
    string? Exchange { get; }
}
