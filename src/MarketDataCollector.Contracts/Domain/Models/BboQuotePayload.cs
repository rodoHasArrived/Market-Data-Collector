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
    /// Creates a BboQuotePayload from a MarketQuoteUpdate (adapter input).
    /// Calculates mid-price and spread automatically.
    /// </summary>
    public static BboQuotePayload FromUpdate(
        DateTimeOffset timestamp,
        string symbol,
        decimal bidPrice,
        long bidSize,
        decimal askPrice,
        long askSize,
        long sequenceNumber,
        string? streamId = null,
        string? venue = null)
    {
        var midPrice = (bidPrice + askPrice) / 2m;
        var spread = askPrice - bidPrice;
        
        return new BboQuotePayload(
            Timestamp: timestamp,
            Symbol: symbol,
            BidPrice: bidPrice,
            BidSize: bidSize,
            AskPrice: askPrice,
            AskSize: askSize,
            MidPrice: midPrice,
            Spread: spread,
            SequenceNumber: sequenceNumber,
            StreamId: streamId,
            Venue: venue
        );
    }
}
