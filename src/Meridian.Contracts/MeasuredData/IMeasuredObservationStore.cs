using Meridian.Contracts.SecurityMaster;

namespace Meridian.Contracts.MeasuredData;

/// <summary>
/// Storage and retrieval of time-series measured observations.
/// <para>
/// A MeasuredObservation is a raw fact captured directly from a data source —
/// never the output of a calculation. This interface provides the read and
/// write surface for such facts.
/// </para>
/// <para>
/// Implementations are expected to support efficient point-in-time and range
/// queries so that backtesting and derived computation engines can retrieve
/// the observations that were available at any historical moment.
/// </para>
/// </summary>
public interface IMeasuredObservationStore
{
    /// <summary>
    /// Persist a single observation.
    /// Idempotent: if an identical observation (same instrument, source, sequence, timestamp)
    /// already exists, the call is a no-op.
    /// </summary>
    ValueTask WriteAsync(
        InstrumentId instrumentId,
        string observationKind,
        decimal value,
        DateTimeOffset observedAt,
        string source,
        long sequenceNumber = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve the most recent observation of the given kind for an instrument as of a
    /// specific point in time.
    /// Returns null if no observation exists at or before <paramref name="asOf"/>.
    /// </summary>
    ValueTask<ObservationPoint?> GetLatestAsync(
        InstrumentId instrumentId,
        string observationKind,
        DateTimeOffset asOf,
        string? source = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve all observations of the given kind for an instrument within a time range.
    /// </summary>
    IAsyncEnumerable<ObservationPoint> GetRangeAsync(
        InstrumentId instrumentId,
        string observationKind,
        DateTimeOffset from,
        DateTimeOffset to,
        string? source = null,
        CancellationToken ct = default);

    /// <summary>
    /// Delete all observations older than the given cutoff for a specific instrument and kind.
    /// Used for hot-tier data expiry.
    /// </summary>
    ValueTask PurgeAsync(
        InstrumentId instrumentId,
        string observationKind,
        DateTimeOffset olderThan,
        CancellationToken ct = default);
}

/// <summary>
/// A single time-stamped observation point retrieved from the store.
/// </summary>
public sealed record ObservationPoint
{
    /// <summary>The instrument this observation belongs to.</summary>
    public InstrumentId InstrumentId { get; init; }

    /// <summary>The observation category key (e.g., "last_trade_price").</summary>
    public string ObservationKind { get; init; } = "";

    /// <summary>When the exchange or data source recorded this value.</summary>
    public DateTimeOffset ObservedAt { get; init; }

    /// <summary>When Meridian received and stored this record.</summary>
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>The measured value.</summary>
    public decimal Value { get; init; }

    /// <summary>Which data provider produced this observation.</summary>
    public string Source { get; init; } = "";

    /// <summary>Monotonically increasing number within a source stream. Zero = unknown.</summary>
    public long SequenceNumber { get; init; }
}
