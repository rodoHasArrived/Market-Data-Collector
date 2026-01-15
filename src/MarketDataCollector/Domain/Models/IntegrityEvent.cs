using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Domain.Models;

/// <summary>
/// Data integrity / continuity / anomaly event.
/// </summary>
public sealed record IntegrityEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    IntegritySeverity Severity,
    string Description,
    int? ErrorCode,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload
{
    public static IntegrityEvent SequenceGap(
        DateTimeOffset ts,
        string symbol,
        long expectedNext,
        long received,
        string? streamId = null,
        string? venue = null)
        => new(ts, symbol, IntegritySeverity.Error,
            $"Sequence gap: expected {expectedNext} but received {received}.",
            ErrorCode: 1001,
            SequenceNumber: received,
            StreamId: streamId,
            Venue: venue);

    public static IntegrityEvent OutOfOrder(
        DateTimeOffset ts,
        string symbol,
        long last,
        long received,
        string? streamId = null,
        string? venue = null)
        => new(ts, symbol, IntegritySeverity.Warning,
            $"Out-of-order trade: last {last}, received {received}.",
            ErrorCode: 1002,
            SequenceNumber: received,
            StreamId: streamId,
            Venue: venue);

    public static IntegrityEvent InvalidSymbol(
        DateTimeOffset ts,
        string symbol,
        string reason,
        long sequenceNumber,
        string? streamId = null,
        string? venue = null)
        => new(ts, symbol, IntegritySeverity.Error,
            $"Invalid symbol format: {reason}",
            ErrorCode: 1003,
            SequenceNumber: sequenceNumber,
            StreamId: streamId,
            Venue: venue);

    public static IntegrityEvent InvalidSequenceNumber(
        DateTimeOffset ts,
        string symbol,
        long sequenceNumber,
        string reason,
        string? streamId = null,
        string? venue = null)
        => new(ts, symbol, IntegritySeverity.Error,
            $"Invalid sequence number {sequenceNumber}: {reason}",
            ErrorCode: 1004,
            SequenceNumber: sequenceNumber,
            StreamId: streamId,
            Venue: venue);
}
