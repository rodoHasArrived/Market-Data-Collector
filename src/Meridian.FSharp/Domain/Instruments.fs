/// F# type-safe domain model for financial instruments.
/// Provides an exhaustive discriminated union over InstrumentKind so that
/// every F# function that consumes an instrument must handle every case.
///
/// The C# InstrumentRecord is the persistence/transport type.
/// Instrument (this DU) is the F# computation type — use it for Greeks,
/// margin calculations, synthetic recognition, and ledger routing.
module Meridian.FSharp.Domain.Instruments

open System
open Meridian.Contracts.SecurityMaster

// ── Shared payload records ────────────────────────────────────────────────────

/// Fields common to all option types (equity, index, future).
type OptionFields = {
    Underlying  : InstrumentId
    Side        : OptionSide
    Strike      : decimal
    Expiry      : DateOnly
    Style       : ExerciseStyle
    Multiplier  : decimal
    Settlement  : SettlementType
}

/// Fields common to all futures.
type FutureFields = {
    Underlying  : InstrumentId
    Expiry      : DateOnly
    Multiplier  : decimal
    TickSize    : decimal option
    TickValue   : decimal option
    Settlement  : SettlementType
}

/// Fields for fixed-income instruments.
type FixedIncomeFields = {
    CouponRate         : decimal option
    MaturityDate       : DateOnly option
    FaceValue          : decimal option
    DayCountConvention : string option
}

// ── Instrument discriminated union ────────────────────────────────────────────

/// Type-safe representation of a financial instrument.
/// Every branch carries only the fields relevant to that kind —
/// no nullable properties, no runtime casts.
///
/// Always obtained by calling Instrument.ofRecord on an InstrumentRecord,
/// or constructed directly in tests / factories.
[<RequireQualifiedAccess>]
type Instrument =
    /// Physical or digital currency held as cash (USD, EUR, BTC).
    | CashCurrency of id: InstrumentId * currency: string

    /// Common/preferred stock, ETF, or ADR.
    | Equity of id: InstrumentId * currency: string * exchange: string option * issuer: string option

    /// Listed option on an individual equity or ETF.
    | EquityOption of id: InstrumentId * currency: string * option: OptionFields

    /// Market index used as reference or option underlying.
    | Index of id: InstrumentId * currency: string * displaySymbol: string

    /// Cash-settled option on a market index (SPX, NDX, RUT).
    | IndexOption of id: InstrumentId * currency: string * option: OptionFields

    /// Exchange-traded futures contract.
    | Future of id: InstrumentId * currency: string * future: FutureFields

    /// Option on a futures contract.
    | FutureOption of id: InstrumentId * currency: string * option: OptionFields

    /// Bond, note, bill, or structured credit instrument.
    | FixedIncome of id: InstrumentId * currency: string * fi: FixedIncomeFields

    /// Foreign exchange spot or forward pair.
    | FxPair of id: InstrumentId * baseCurrency: string * quoteCurrency: string * settlementDate: DateOnly option

// ── Core accessors ────────────────────────────────────────────────────────────

/// Extract the stable InstrumentId from any instrument case.
[<CompiledName("GetId")>]
let instrumentId (instrument: Instrument) : InstrumentId =
    match instrument with
    | Instrument.CashCurrency(id, _)          -> id
    | Instrument.Equity(id, _, _, _)          -> id
    | Instrument.EquityOption(id, _, _)       -> id
    | Instrument.Index(id, _, _)              -> id
    | Instrument.IndexOption(id, _, _)        -> id
    | Instrument.Future(id, _, _)             -> id
    | Instrument.FutureOption(id, _, _)       -> id
    | Instrument.FixedIncome(id, _, _)        -> id
    | Instrument.FxPair(id, _, _, _)          -> id

/// True for EquityOption, IndexOption, FutureOption.
[<CompiledName("IsOption")>]
let isOption (instrument: Instrument) : bool =
    match instrument with
    | Instrument.EquityOption _
    | Instrument.IndexOption _
    | Instrument.FutureOption _ -> true
    | _                         -> false

/// True for Future, EquityOption, IndexOption, FutureOption — i.e. any derivative.
[<CompiledName("IsDerivative")>]
let isDerivative (instrument: Instrument) : bool =
    match instrument with
    | Instrument.EquityOption _
    | Instrument.IndexOption _
    | Instrument.Future _
    | Instrument.FutureOption _ -> true
    | _                         -> false

/// Returns the underlying InstrumentId for derivatives; None for non-derivatives.
[<CompiledName("GetUnderlyingId")>]
let underlyingId (instrument: Instrument) : InstrumentId option =
    match instrument with
    | Instrument.EquityOption(_, _, opt)  -> Some opt.Underlying
    | Instrument.IndexOption(_, _, opt)   -> Some opt.Underlying
    | Instrument.Future(_, _, fut)        -> Some fut.Underlying
    | Instrument.FutureOption(_, _, opt)  -> Some opt.Underlying
    | _                                   -> None

/// Returns the expiry date for derivatives; None for non-derivatives.
[<CompiledName("GetExpiry")>]
let expiry (instrument: Instrument) : DateOnly option =
    match instrument with
    | Instrument.EquityOption(_, _, opt)  -> Some opt.Expiry
    | Instrument.IndexOption(_, _, opt)   -> Some opt.Expiry
    | Instrument.Future(_, _, fut)        -> Some fut.Expiry
    | Instrument.FutureOption(_, _, opt)  -> Some opt.Expiry
    | _                                   -> None

/// Returns the option fields for option instruments; None for non-options.
[<CompiledName("GetOptionFields")>]
let optionFields (instrument: Instrument) : OptionFields option =
    match instrument with
    | Instrument.EquityOption(_, _, opt)  -> Some opt
    | Instrument.IndexOption(_, _, opt)   -> Some opt
    | Instrument.FutureOption(_, _, opt)  -> Some opt
    | _                                   -> None

/// Returns the contract multiplier for any instrument (1 for non-derivatives).
[<CompiledName("GetMultiplier")>]
let multiplier (instrument: Instrument) : decimal =
    match instrument with
    | Instrument.EquityOption(_, _, opt)  -> opt.Multiplier
    | Instrument.IndexOption(_, _, opt)   -> opt.Multiplier
    | Instrument.Future(_, _, fut)        -> fut.Multiplier
    | Instrument.FutureOption(_, _, opt)  -> opt.Multiplier
    | _                                   -> 1m

// ── Conversion from InstrumentRecord ──────────────────────────────────────────

/// Map an InstrumentRecord (C# transport type) to the F# Instrument DU.
/// Returns Error if required fields are missing for the given Kind.
[<CompiledName("OfRecord")>]
let ofRecord (r: InstrumentRecord) : Result<Instrument, string> =
    match r.Kind with
    | InstrumentKind.CashCurrency ->
        Ok (Instrument.CashCurrency(r.Id, r.Currency))

    | InstrumentKind.Equity ->
        Ok (Instrument.Equity(r.Id, r.Currency, Option.ofObj r.PrimaryExchangeMic, Option.ofObj r.IssuerName))

    | InstrumentKind.EquityOption ->
        match Option.ofNullable r.UnderlyingId,
              Option.ofNullable r.OptionSide,
              Option.ofNullable r.Strike,
              Option.ofNullable r.Expiry,
              Option.ofNullable r.ExerciseStyle with
        | Some uid, Some side, Some strike, Some exp, Some style ->
            let opt = { Underlying = uid; Side = side; Strike = strike; Expiry = exp;
                        Style = style; Multiplier = r.ContractMultiplier; Settlement = r.SettlementType }
            Ok (Instrument.EquityOption(r.Id, r.Currency, opt))
        | _ ->
            Error $"EquityOption {r.Id} is missing required fields (UnderlyingId, OptionSide, Strike, Expiry, or ExerciseStyle)"

    | InstrumentKind.Index ->
        Ok (Instrument.Index(r.Id, r.Currency, r.DisplaySymbol))

    | InstrumentKind.IndexOption ->
        match Option.ofNullable r.UnderlyingId,
              Option.ofNullable r.OptionSide,
              Option.ofNullable r.Strike,
              Option.ofNullable r.Expiry,
              Option.ofNullable r.ExerciseStyle with
        | Some uid, Some side, Some strike, Some exp, Some style ->
            let opt = { Underlying = uid; Side = side; Strike = strike; Expiry = exp;
                        Style = style; Multiplier = r.ContractMultiplier; Settlement = r.SettlementType }
            Ok (Instrument.IndexOption(r.Id, r.Currency, opt))
        | _ ->
            Error $"IndexOption {r.Id} is missing required fields (UnderlyingId, OptionSide, Strike, Expiry, or ExerciseStyle)"

    | InstrumentKind.Future ->
        match Option.ofNullable r.UnderlyingId, Option.ofNullable r.Expiry with
        | Some uid, Some exp ->
            let fut = { Underlying = uid; Expiry = exp; Multiplier = r.ContractMultiplier;
                        TickSize = Option.ofNullable r.TickSize
                        TickValue = Option.ofNullable r.TickValue
                        Settlement = r.SettlementType }
            Ok (Instrument.Future(r.Id, r.Currency, fut))
        | _ ->
            Error $"Future {r.Id} is missing required fields (UnderlyingId or Expiry)"

    | InstrumentKind.FutureOption ->
        match Option.ofNullable r.UnderlyingId,
              Option.ofNullable r.OptionSide,
              Option.ofNullable r.Strike,
              Option.ofNullable r.Expiry,
              Option.ofNullable r.ExerciseStyle with
        | Some uid, Some side, Some strike, Some exp, Some style ->
            let opt = { Underlying = uid; Side = side; Strike = strike; Expiry = exp;
                        Style = style; Multiplier = r.ContractMultiplier; Settlement = r.SettlementType }
            Ok (Instrument.FutureOption(r.Id, r.Currency, opt))
        | _ ->
            Error $"FutureOption {r.Id} is missing required fields (UnderlyingId, OptionSide, Strike, Expiry, or ExerciseStyle)"

    | InstrumentKind.FixedIncome ->
        let fi = { CouponRate          = Option.ofNullable r.CouponRate
                   MaturityDate        = Option.ofNullable r.MaturityDate
                   FaceValue           = Option.ofNullable r.FaceValue
                   DayCountConvention  = Option.ofObj r.DayCountConvention }
        Ok (Instrument.FixedIncome(r.Id, r.Currency, fi))

    | InstrumentKind.FxPair ->
        match r.BaseCurrency, r.QuoteCurrency with
        | base', quote when not (String.IsNullOrEmpty base') && not (String.IsNullOrEmpty quote) ->
            Ok (Instrument.FxPair(r.Id, base', quote, Option.ofNullable r.FxSettlementDate))
        | _ ->
            Error $"FxPair {r.Id} is missing BaseCurrency or QuoteCurrency"

    | unknown ->
        Error $"Unknown InstrumentKind {unknown} on record {r.Id}"
