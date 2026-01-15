using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Integrity event specific to market depth streams.
/// </summary>
public sealed record DepthIntegrityEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    DepthIntegrityKind Kind,
    string Description,
    int Position,
    DepthOperation Operation,
    OrderBookSide Side,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
