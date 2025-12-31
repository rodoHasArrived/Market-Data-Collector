using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Immutable Level-2 order book snapshot.
/// Bids/Asks should be sorted best-to-worst (Level 0 = best).
/// </summary>
public sealed record LOBSnapshot(
    DateTimeOffset Timestamp,
    string Symbol,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks,
    double? MidPrice = null,
    double? MicroPrice = null,
    double? Imbalance = null,
    MarketState MarketState = MarketState.Normal,
    long SequenceNumber = 0,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
