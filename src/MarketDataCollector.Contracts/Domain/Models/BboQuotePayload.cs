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
) : MarketEventPayload;
