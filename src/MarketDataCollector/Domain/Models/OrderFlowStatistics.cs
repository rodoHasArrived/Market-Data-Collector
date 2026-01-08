using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Rolling order-flow statistics derived from recent trades.
/// </summary>
public sealed record OrderFlowStatistics(
    DateTimeOffset Timestamp,
    string Symbol,
    long BuyVolume,
    long SellVolume,
    long UnknownVolume,
    decimal VWAP,
    decimal Imbalance,
    int TradeCount,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
