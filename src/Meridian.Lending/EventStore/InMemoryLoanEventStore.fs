/// Thread-safe in-memory implementation of ILoanEventStore.
/// Suitable for unit tests and single-process development use.
/// Functionally equivalent to Sharpino's MemoryEventStore.
module Meridian.Lending.EventStore.InMemoryLoanEventStore

open System
open System.Collections.Concurrent
open ILoanEventStore

/// Per-aggregate stream held in memory.
type private Stream = {
    mutable Events: ResizeArray<StoredEvent>
    mutable Snapshot: StoredSnapshot option
}

/// Thread-safe, in-memory implementation of ILoanEventStore.
/// Suitable for unit tests and in-process development environments.
type InMemoryLoanEventStore() =
    let streams = ConcurrentDictionary<Guid, Stream>()
    let streamLock = obj()

    let getOrCreate (id: Guid) =
        streams.GetOrAdd(id, fun _ -> { Events = ResizeArray(); Snapshot = None })

    interface ILoanEventStore with

        member _.GetEvents(aggregateId) =
            match streams.TryGetValue(aggregateId) with
            | false, _ -> []
            | true, s ->
                lock streamLock (fun () -> s.Events |> Seq.toList)

        member _.GetEventsAfter(aggregateId, fromSequenceExclusive) =
            match streams.TryGetValue(aggregateId) with
            | false, _ -> []
            | true, s ->
                lock streamLock (fun () ->
                    s.Events
                    |> Seq.filter (fun e -> e.SequenceNumber > fromSequenceExclusive)
                    |> Seq.toList)

        member _.GetSnapshot(aggregateId) =
            match streams.TryGetValue(aggregateId) with
            | false, _ -> None
            | true, s -> lock streamLock (fun () -> s.Snapshot)

        member _.AppendEvents(aggregateId, expectedSequence, events) =
            let stream = getOrCreate aggregateId
            lock streamLock (fun () ->
                let currentSeq =
                    if stream.Events.Count = 0 then 0L
                    else stream.Events.[stream.Events.Count - 1].SequenceNumber

                if currentSeq <> expectedSequence then
                    ConcurrencyConflict(ConcurrencyError(expectedSequence, currentSeq))
                else
                    let mutable seq = currentSeq
                    for json in events do
                        seq <- seq + 1L
                        stream.Events.Add({
                            AggregateId = aggregateId
                            SequenceNumber = seq
                            EventJson = json
                            CreatedAt = DateTimeOffset.UtcNow
                        })
                    Appended seq)

        member _.SaveSnapshot(aggregateId, sequenceNumber, stateJson) =
            let stream = getOrCreate aggregateId
            lock streamLock (fun () ->
                stream.Snapshot <- Some {
                    AggregateId = aggregateId
                    SequenceNumber = sequenceNumber
                    StateJson = stateJson
                    CreatedAt = DateTimeOffset.UtcNow
                })

        member _.GetCurrentSequence(aggregateId) =
            match streams.TryGetValue(aggregateId) with
            | false, _ -> 0L
            | true, s ->
                lock streamLock (fun () ->
                    if s.Events.Count = 0 then 0L
                    else s.Events.[s.Events.Count - 1].SequenceNumber)
