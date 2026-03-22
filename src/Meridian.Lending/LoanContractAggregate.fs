/// Sharpino-compatible wrapper types that adapt the pure F# Lending domain
/// to the event-store persistence layer.
///
/// The core domain types (LoanState, LoanEvent, LoanCommand) live in Meridian.FSharp
/// and remain free of any I/O or persistence concern. This module adds only what is
/// needed to connect them to the event store:
///   - JSON serialization/deserialization of LoanEvent using FsPickler.Json
///   - A LoanContractAggregate record that exposes Sharpino-standard members
///       Id, StorageName, Version, evolve
///   - An execute function that delegates to LoanAggregate.handle
module Meridian.Lending.LoanContractAggregate

open System
open MBrace.FsPickler.Json
open Meridian.FSharp.Domain.Lending

// ── Serializer (one instance per process, thread-safe) ──────────────────────

/// FsPickler JSON serializer configured for F# discriminated unions.
/// The same serializer is used for both events and snapshots.
let private serializer = JsonSerializer(indent = false, omitHeader = true)

/// Serializes a single LoanEvent to a JSON string.
let serializeEvent (event: LoanEvent) : string =
    use sw = new System.IO.StringWriter()
    serializer.Serialize(sw, event)
    sw.ToString()

/// Deserializes a JSON string back to a LoanEvent.
let deserializeEvent (json: string) : LoanEvent =
    use sr = new System.IO.StringReader(json)
    serializer.Deserialize<LoanEvent>(sr)

/// Serializes a LoanState to a JSON string for snapshot storage.
let serializeState (state: LoanState) : string =
    use sw = new System.IO.StringWriter()
    serializer.Serialize(sw, state)
    sw.ToString()

/// Deserializes a JSON string back to a LoanState snapshot.
let deserializeState (json: string) : LoanState =
    use sr = new System.IO.StringReader(json)
    serializer.Deserialize<LoanState>(sr)

// ── Aggregate wrapper ─────────────────────────────────────────────────────────

/// Sharpino-pattern aggregate for the Loan Contract.
///
/// Naming follows Sharpino conventions:
///   - StorageName  — PostgreSQL table-name suffix used to partition event streams
///   - Version      — schema version string; bump when the event schema changes
///   - evolve       — pure fold function; delegates to LoanAggregate.evolve
type LoanContractAggregate = {
    Id: Guid
    State: LoanState option
} with
    /// Sharpino: table-name suffix for this aggregate type.
    static member StorageName = "_loan_contracts"

    /// Sharpino: event schema version — bump when breaking changes are made to LoanEvent.
    static member Version = "_v1"

    /// Constructs a fresh (empty) aggregate for the given Id.
    static member Zero(id: Guid) : LoanContractAggregate =
        { Id = id; State = None }

    /// Folds a single LoanEvent into the aggregate, returning the updated aggregate.
    member this.Evolve(event: LoanEvent) : LoanContractAggregate =
        let newState = LoanAggregate.evolve this.State event
        { this with State = Some newState }

    /// Handles a command against this aggregate's current state.
    /// Returns Ok(events) on success or Error(message) on domain rejection.
    member this.Execute(command: LoanCommand) : Result<LoanEvent list, string> =
        match LoanAggregate.handle this.State command with
        | Ok events -> Ok events
        | Error msg -> Error msg

    /// Rebuilds the aggregate by folding a sequence of events from scratch.
    static member Rebuild(id: Guid, events: LoanEvent seq) : LoanContractAggregate =
        events
        |> Seq.fold (fun (agg: LoanContractAggregate) e -> agg.Evolve e)
                    (LoanContractAggregate.Zero id)

