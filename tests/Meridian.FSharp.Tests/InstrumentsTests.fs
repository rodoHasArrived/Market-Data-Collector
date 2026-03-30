/// Unit tests for F# Instruments domain types.
module Meridian.FSharp.Tests.InstrumentsTests

open System
open Xunit
open FsUnit.Xunit
open Meridian.Contracts.SecurityMaster
open Meridian.FSharp.Domain.Instruments

// ── Helpers ───────────────────────────────────────────────────────────────────

let private equityRecord () : InstrumentRecord =
    InstrumentRecord(
        Id = InstrumentId.New(),
        Kind = InstrumentKind.Equity,
        Currency = "USD",
        DisplaySymbol = "AAPL",
        ContractMultiplier = 1m,
        SettlementType = SettlementType.PhysicalDelivery,
        IsActive = true,
        RegisteredAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    )

let private indexOptionRecord (underlyingId: InstrumentId) : InstrumentRecord =
    InstrumentRecord(
        Id = InstrumentId.New(),
        Kind = InstrumentKind.IndexOption,
        Currency = "USD",
        DisplaySymbol = "SPX   240119C04500000",
        ContractMultiplier = 100m,
        SettlementType = SettlementType.CashSettled,
        ExerciseStyle = Nullable ExerciseStyle.European,
        OptionSide = Nullable OptionSide.Call,
        Strike = Nullable 4500m,
        Expiry = Nullable (DateOnly(2024, 1, 19)),
        UnderlyingId = Nullable underlyingId,
        IsActive = true,
        RegisteredAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    )

// ── ofRecord: Equity ──────────────────────────────────────────────────────────

[<Fact>]
let ``ofRecord Equity returns Instrument.Equity case`` () =
    let record = equityRecord ()
    let result = ofRecord record
    match result with
    | Ok (Instrument.Equity(id, currency, None, None)) ->
        id |> should equal record.Id
        currency |> should equal "USD"
    | _ -> failwith $"Expected Equity instrument, got {result}"

// ── ofRecord: IndexOption ─────────────────────────────────────────────────────

[<Fact>]
let ``ofRecord IndexOption returns Instrument.IndexOption case`` () =
    let underId = InstrumentId.New()
    let record = indexOptionRecord underId
    let result = ofRecord record
    match result with
    | Ok (Instrument.IndexOption(id, currency, opt)) ->
        id |> should equal record.Id
        currency |> should equal "USD"
        opt.Strike |> should equal 4500m
        opt.Side |> should equal OptionSide.Call
        opt.Style |> should equal ExerciseStyle.European
        opt.Underlying |> should equal underId
    | _ -> failwith $"Expected IndexOption, got {result}"

[<Fact>]
let ``ofRecord IndexOption missing Strike returns Error`` () =
    let underId = InstrumentId.New()
    let record =
        InstrumentRecord(
            Id = InstrumentId.New(),
            Kind = InstrumentKind.IndexOption,
            Currency = "USD",
            DisplaySymbol = "SPX bad",
            ContractMultiplier = 100m,
            SettlementType = SettlementType.CashSettled,
            ExerciseStyle = Nullable ExerciseStyle.European,
            OptionSide = Nullable OptionSide.Call,
            // Strike deliberately absent
            Expiry = Nullable (DateOnly(2024, 1, 19)),
            UnderlyingId = Nullable underId,
            IsActive = true,
            RegisteredAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        )
    let result = ofRecord record
    match result with
    | Error _ -> ()
    | Ok _ -> failwith "Expected Error, got Ok"

// ── Core accessors ────────────────────────────────────────────────────────────

[<Fact>]
let ``instrumentId extracts correct id from Equity`` () =
    let record = equityRecord ()
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    instrumentId instrument |> should equal record.Id

[<Fact>]
let ``isOption returns false for Equity`` () =
    let record = equityRecord ()
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    isOption instrument |> should be False

[<Fact>]
let ``isOption returns true for IndexOption`` () =
    let underId = InstrumentId.New()
    let record = indexOptionRecord underId
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    isOption instrument |> should be True

[<Fact>]
let ``isDerivative returns true for Future`` () =
    let underId = InstrumentId.New()
    let record =
        InstrumentRecord(
            Id = InstrumentId.New(),
            Kind = InstrumentKind.Future,
            Currency = "USD",
            DisplaySymbol = "ES_F",
            ContractMultiplier = 50m,
            SettlementType = SettlementType.CashSettled,
            Expiry = Nullable (DateOnly(2024, 3, 15)),
            UnderlyingId = Nullable underId,
            IsActive = true,
            RegisteredAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        )
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    isDerivative instrument |> should be True

[<Fact>]
let ``underlyingId returns Some for IndexOption`` () =
    let underId = InstrumentId.New()
    let record = indexOptionRecord underId
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    underlyingId instrument |> should equal (Some underId)

[<Fact>]
let ``underlyingId returns None for Equity`` () =
    let record = equityRecord ()
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    underlyingId instrument |> should equal None

[<Fact>]
let ``multiplier returns 100 for standard index option`` () =
    let underId = InstrumentId.New()
    let record = indexOptionRecord underId
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    multiplier instrument |> should equal 100m

[<Fact>]
let ``multiplier returns 1 for equity`` () =
    let record = equityRecord ()
    let instrument = ofRecord record |> Result.defaultWith (fun e -> failwith e)
    multiplier instrument |> should equal 1m

// ── FxPair ────────────────────────────────────────────────────────────────────

[<Fact>]
let ``ofRecord FxPair succeeds with base and quote currency`` () =
    let record =
        InstrumentRecord(
            Id = InstrumentId.New(),
            Kind = InstrumentKind.FxPair,
            Currency = "USD",
            DisplaySymbol = "EUR/USD",
            ContractMultiplier = 1m,
            SettlementType = SettlementType.PhysicalDelivery,
            BaseCurrency = "EUR",
            QuoteCurrency = "USD",
            IsActive = true,
            RegisteredAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        )
    let result = ofRecord record
    match result with
    | Ok (Instrument.FxPair(_, "EUR", "USD", None)) -> ()
    | _ -> failwith $"Expected FxPair, got {result}"
