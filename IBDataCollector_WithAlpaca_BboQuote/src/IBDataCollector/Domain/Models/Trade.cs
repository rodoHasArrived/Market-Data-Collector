using IBDataCollector.Domain.Events;

namespace IBDataCollector.Domain.Models;

// TODO: Add validation for Price (must be > 0) and Size (must be >= 0)
// Consider adding a factory method or constructor validation to enforce business rules
// Invalid data should be rejected at the boundary to prevent corrupt datasets

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
