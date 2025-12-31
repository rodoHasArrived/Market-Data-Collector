using IBDataCollector.Domain.Events;

namespace IBDataCollector.Domain.Models;

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
}
