using IBDataCollector.Domain.Events;

namespace IBDataCollector.Domain.Models;

/// <summary>
/// Wrapper payload for L2 snapshots (requested for explicit serialization type).
/// Prefer using LOBSnapshot directly, but this is supported for compatibility.
/// </summary>
public sealed record L2SnapshotPayload(
    LOBSnapshot Snapshot,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
