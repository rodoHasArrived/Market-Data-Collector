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
    /// Creates a BboQuotePayload from a MarketQuoteUpdate and sequence number.
    /// </summary>
    public static BboQuotePayload FromUpdate(object update, long sequenceNumber)
    {
        // Use reflection to get properties from update object
        var type = update.GetType();
        var timestamp = (DateTimeOffset)type.GetProperty("Timestamp")?.GetValue(update)!;
        var symbol = (string)type.GetProperty("Symbol")?.GetValue(update)!;
        var bidPrice = (decimal)type.GetProperty("BidPrice")?.GetValue(update)!;
        var bidSize = (long)type.GetProperty("BidSize")?.GetValue(update)!;
        var askPrice = (decimal)type.GetProperty("AskPrice")?.GetValue(update)!;
        var askSize = (long)type.GetProperty("AskSize")?.GetValue(update)!;
        var streamId = (string?)type.GetProperty("StreamId")?.GetValue(update);
        var venue = (string?)type.GetProperty("Venue")?.GetValue(update);
        
        var midPrice = (bidPrice + askPrice) / 2m;
        var spread = askPrice - bidPrice;
        
        return new BboQuotePayload(
            timestamp,
            symbol,
            bidPrice,
            bidSize,
            askPrice,
            askSize,
            midPrice,
            spread,
            sequenceNumber,
            streamId,
            venue
        );
    }
};
