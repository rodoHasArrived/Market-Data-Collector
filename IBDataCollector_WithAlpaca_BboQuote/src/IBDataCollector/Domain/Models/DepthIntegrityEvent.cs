using IBDataCollector.Domain.Events;

namespace IBDataCollector.Domain.Models;

public enum DepthIntegrityKind
{
    Unknown = 0,
    Gap = 1,
    OutOfOrder = 2,
    InvalidPosition = 3,
    Stale = 4
}

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
