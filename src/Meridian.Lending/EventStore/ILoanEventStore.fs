/// Event store abstraction for the Loan Contract aggregate.
/// Follows the same structural guarantees as Sharpino:
///   - Optimistic concurrency via sequence numbers
///   - Snapshot support for fast state retrieval
///   - Two concrete implementations: in-memory (dev/test) and PostgreSQL (production)
module Meridian.Lending.EventStore.ILoanEventStore

open System

/// A single persisted event row.
type StoredEvent = {
    AggregateId: Guid
    /// 1-based monotonically increasing sequence number within the aggregate's stream.
    SequenceNumber: int64
    EventJson: string
    CreatedAt: DateTimeOffset
}

/// A persisted snapshot row.
type StoredSnapshot = {
    AggregateId: Guid
    /// The sequence number of the last event that was folded into this snapshot.
    SequenceNumber: int64
    StateJson: string
    CreatedAt: DateTimeOffset
}

/// Optimistic concurrency error: the expected sequence number did not match.
[<Struct>]
type ConcurrencyError = ConcurrencyError of expected: int64 * actual: int64

/// Result of attempting to append events to a stream.
type AppendResult =
    | Appended of newSequence: int64
    | ConcurrencyConflict of ConcurrencyError

/// Abstraction over event storage for the Loan Contract aggregate.
/// Both the in-memory and PostgreSQL implementations satisfy this interface.
type ILoanEventStore =
    /// Returns all events for the aggregate, ordered by sequence number.
    abstract GetEvents: aggregateId: Guid -> StoredEvent list

    /// Returns events for the aggregate starting after the given sequence number.
    abstract GetEventsAfter: aggregateId: Guid * fromSequenceExclusive: int64 -> StoredEvent list

    /// Returns the most recent snapshot for the aggregate, if any.
    abstract GetSnapshot: aggregateId: Guid -> StoredSnapshot option

    /// Atomically appends a list of events to the aggregate stream.
    /// <paramref name="expectedSequence"/> is the highest known sequence number
    /// before these events; pass 0L to signal "stream must be empty".
    /// Returns <see cref="Appended"/> with the new highest sequence on success, or
    /// <see cref="ConcurrencyConflict"/> if another writer has already appended events.
    abstract AppendEvents:
        aggregateId: Guid *
        expectedSequence: int64 *
        events: string list ->
            AppendResult

    /// Persists a snapshot.
    abstract SaveSnapshot: aggregateId: Guid * sequenceNumber: int64 * stateJson: string -> unit

    /// Returns the current highest sequence number for the aggregate (0 if the stream is empty).
    abstract GetCurrentSequence: aggregateId: Guid -> int64
