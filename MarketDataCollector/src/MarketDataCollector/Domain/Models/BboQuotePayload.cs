using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

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
    public static BboQuotePayload FromUpdate(MarketQuoteUpdate u, long seq)
    {
        decimal? mid = null;
        decimal? spr = null;

        if (u.BidPrice > 0m && u.AskPrice > 0m && u.AskPrice >= u.BidPrice)
        {
            spr = u.AskPrice - u.BidPrice;
            mid = u.BidPrice + (spr.Value / 2m);
        }

        return new BboQuotePayload(
            Timestamp: u.Timestamp,
            Symbol: u.Symbol,
            BidPrice: u.BidPrice,
            BidSize: u.BidSize,
            AskPrice: u.AskPrice,
            AskSize: u.AskSize,
            MidPrice: mid,
            Spread: spr,
            SequenceNumber: seq,
            StreamId: u.StreamId,
            Venue: u.Venue
        );
    }
}
