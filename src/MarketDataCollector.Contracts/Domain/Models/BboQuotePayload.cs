using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Best-Bid/Offer snapshot payload.
/// </summary>
public sealed record BboQuotePayload(
    DateTimeOffset Timestamp,
    string Symbol,
    decimal BidPrice,
    long BidSize,
    decimal AskPrice,
    long AskSize,
    decimal? MidPrice,
    decimal? Spread,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload
{
    /// <summary>
    /// Creates a BboQuotePayload from a MarketQuoteUpdate.
    /// </summary>
    public static BboQuotePayload FromUpdate(MarketDataCollector.Domain.Models.MarketQuoteUpdate update, long sequenceNumber)
    {
        var midPrice = (update.BidPrice + update.AskPrice) / 2m;
        var spread = update.AskPrice - update.BidPrice;

        return new BboQuotePayload(
            update.Timestamp,
            update.Symbol,
            update.BidPrice,
            update.BidSize,
            update.AskPrice,
            update.AskSize,
            midPrice,
            spread,
            sequenceNumber,
            update.StreamId,
            update.Venue
        );
    }
}
