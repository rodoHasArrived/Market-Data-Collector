/// High-level repository for Loan Contract aggregates.
/// Combines the event store with the aggregate wrapper to provide simple
/// load/save operations and optional snapshot-based acceleration.
///
/// Snapshot policy: a new snapshot is written after every 25 events.
module Meridian.Lending.LoanContractRepository

open System
open Meridian.FSharp.Domain.Lending
open Meridian.Lending.EventStore.ILoanEventStore
open Meridian.Lending.LoanContractAggregate

/// Number of events between automatic snapshot writes.
[<Literal>]
let private SnapshotEvery = 25L

/// Result of executing a command against an aggregate.
[<RequireQualifiedAccess>]
type CommandResult =
    | Success of events: LoanEvent list * newSequence: int64
    | DomainError of message: string
    | ConflictError of ConcurrencyError

/// Loads the current aggregate state for a loan.
/// Uses the most recent snapshot (if any) and replays only the events that
/// follow it, mirroring Sharpino's StateViewer pattern.
let load (store: ILoanEventStore) (loanId: Guid) : LoanContractAggregate * int64 =
    match store.GetSnapshot loanId with
    | Some snap ->
        let baseState = deserializeState snap.StateJson
        let baseAgg = { Id = loanId; State = Some baseState }
        let laterEvents = store.GetEventsAfter(loanId, snap.SequenceNumber)
        let finalAgg =
            laterEvents
            |> List.fold (fun (agg: LoanContractAggregate) ev ->
                agg.Evolve(deserializeEvent ev.EventJson)) baseAgg
        let currentSeq =
            if laterEvents.IsEmpty then snap.SequenceNumber
            else (List.last laterEvents).SequenceNumber
        finalAgg, currentSeq
    | None ->
        let allEvents = store.GetEvents loanId
        let agg =
            allEvents
            |> List.fold (fun (agg: LoanContractAggregate) ev ->
                agg.Evolve(deserializeEvent ev.EventJson))
                         (LoanContractAggregate.Zero loanId)
        let currentSeq =
            if allEvents.IsEmpty then 0L
            else (List.last allEvents).SequenceNumber
        agg, currentSeq

/// Executes a command against the aggregate identified by loanId.
///
/// 1. Loads current state (snapshot + tail events).
/// 2. Delegates to the domain command handler.
/// 3. Appends new events with optimistic concurrency.
/// 4. Writes a snapshot when the sequence number crosses a SnapshotEvery boundary.
let execute (store: ILoanEventStore) (loanId: Guid) (command: LoanCommand) : CommandResult =
    let agg, currentSeq = load store loanId
    match agg.Execute command with
    | Error msg -> CommandResult.DomainError msg
    | Ok [] -> CommandResult.Success([], currentSeq)
    | Ok newEvents ->
        let eventJsons = newEvents |> List.map serializeEvent
        match store.AppendEvents(loanId, currentSeq, eventJsons) with
        | ConcurrencyConflict err -> CommandResult.ConflictError err
        | Appended newSeq ->
            // Snapshot policy: write a snapshot after every SnapshotEvery events.
            let prevBucket = currentSeq / SnapshotEvery
            let newBucket = newSeq / SnapshotEvery
            if newBucket > prevBucket then
                let updatedAgg =
                    newEvents |> List.fold (fun (a: LoanContractAggregate) e -> a.Evolve e) agg
                match updatedAgg.State with
                | Some state ->
                    let stateJson = serializeState state
                    store.SaveSnapshot(loanId, newSeq, stateJson)
                | None -> ()
            CommandResult.Success(newEvents, newSeq)

/// Returns the current visible state of a loan, or None if it has never been created.
let getState (store: ILoanEventStore) (loanId: Guid) : LoanState option =
    let agg, _ = load store loanId
    agg.State

/// Returns all raw events for a loan, in order.
let getEvents (store: ILoanEventStore) (loanId: Guid) : LoanEvent list =
    store.GetEvents loanId
    |> List.map (fun ev -> deserializeEvent ev.EventJson)

