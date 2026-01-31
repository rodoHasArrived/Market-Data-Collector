using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

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
    /// <summary>
    /// Creates a sequence gap integrity event.
    /// </summary>
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

    /// <summary>
    /// Creates an out-of-order integrity event.
    /// </summary>
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

    /// <summary>
    /// Creates an invalid symbol integrity event.
    /// </summary>
    public static IntegrityEvent InvalidSymbol(
        DateTimeOffset ts,
        string symbol,
        string message,
        long seq = 0,
        string? streamId = null,
        string? venue = null)
        => new(ts, symbol, IntegritySeverity.Error,
            message,
            ErrorCode: 1003,
            SequenceNumber: seq,
            StreamId: streamId,
            Venue: venue);

    /// <summary>
    /// Creates an invalid sequence number integrity event.
    /// </summary>
    public static IntegrityEvent InvalidSequenceNumber(
        DateTimeOffset ts,
        string symbol,
        string message,
        long seq = 0,
        string? streamId = null,
        string? venue = null)
        => new(ts, symbol, IntegritySeverity.Error,
            message,
            ErrorCode: 1004,
            SequenceNumber: seq,
            StreamId: streamId,
            Venue: venue);
}
