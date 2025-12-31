using IBDataCollector.Domain.Events;

namespace IBDataCollector.Domain.Models;

/// <summary>
/// Immutable tick-by-tick trade record.
/// </summary>
public sealed record Trade(
    DateTimeOffset Timestamp,
    string Symbol,
    decimal Price,
    long Size,
    AggressorSide Aggressor,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
