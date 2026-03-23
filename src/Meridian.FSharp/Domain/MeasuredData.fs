/// Measured observations — immutable facts about market reality.
///
/// A MeasuredObservation is a single datum captured directly from a data source
/// at a specific point in time. It is NEVER the result of a calculation.
///
/// Examples of MEASURED data:
///   - Last trade price (from an exchange print)
///   - Bid/ask spread (from a quote feed)
///   - Open interest (reported by exchange)
///   - Implied volatility surface point (if the broker sends it as a feed)
///
/// Examples that are NOT measured (they are Derived — see DerivedData.fs):
///   - Option delta, gamma, theta, vega (Black-Scholes or other model outputs)
///   - VWAP (a weighted average calculation)
///   - Implied interest rate from a box spread (a no-arbitrage calculation)
///   - Any smoothed, averaged, or model-dependent value
module Meridian.FSharp.Domain.MeasuredData

open System
open Meridian.Contracts.SecurityMaster

// ── Core observation type ─────────────────────────────────────────────────────

/// A single measured fact about a financial instrument at a specific time.
///
/// 'T is the type of the observed value (e.g., decimal for price, int64 for volume).
/// Source identifies the data provider that produced the observation.
/// SequenceNumber enables gap detection across a stream.
[<Struct>]
type MeasuredObservation<'T> =
    {
        /// The instrument this observation belongs to.
        InstrumentId  : InstrumentId

        /// When the exchange or data source recorded this fact.
        ObservedAt    : DateTimeOffset

        /// When this record was received and stored by Meridian.
        ReceivedAt    : DateTimeOffset

        /// The measured value.
        Value         : 'T

        /// Which data provider produced this observation (e.g., "POLYGON", "ALPACA").
        Source        : string

        /// Monotonically increasing number within a source stream. Zero = unknown.
        SequenceNumber : int64
    }

// ── Observation kinds ─────────────────────────────────────────────────────────

/// Tags for well-known measured observation categories.
/// Used as a discriminator in the store — not stored in the struct itself.
[<RequireQualifiedAccess>]
type ObservationKind =
    | LastTradePrice
    | BidPrice
    | AskPrice
    | MidPrice
    | LastTradeSize
    | OpenInterest
    | ImpliedVolatility   // Only if directly measured from a feed (rare)
    | SettlementPrice
    | OpenPrice
    | ClosePrice
    | HighPrice
    | LowPrice
    | Volume
    | Custom of string

    member this.Key =
        match this with
        | LastTradePrice    -> "last_trade_price"
        | BidPrice          -> "bid_price"
        | AskPrice          -> "ask_price"
        | MidPrice          -> "mid_price"
        | LastTradeSize     -> "last_trade_size"
        | OpenInterest      -> "open_interest"
        | ImpliedVolatility -> "implied_volatility"
        | SettlementPrice   -> "settlement_price"
        | OpenPrice         -> "open_price"
        | ClosePrice        -> "close_price"
        | HighPrice         -> "high_price"
        | LowPrice          -> "low_price"
        | Volume            -> "volume"
        | Custom k          -> $"custom:{k}"

// ── Factory helpers ───────────────────────────────────────────────────────────

/// Create a price observation (decimal value).
[<CompiledName("CreatePrice")>]
let createPrice
    (instrumentId : InstrumentId)
    (price        : decimal)
    (observedAt   : DateTimeOffset)
    (source       : string)
    (seq          : int64) : MeasuredObservation<decimal> =
    { InstrumentId   = instrumentId
      ObservedAt     = observedAt
      ReceivedAt     = DateTimeOffset.UtcNow
      Value          = price
      Source         = source
      SequenceNumber = seq }

/// Create a volume observation (int64 value).
[<CompiledName("CreateVolume")>]
let createVolume
    (instrumentId : InstrumentId)
    (volume       : int64)
    (observedAt   : DateTimeOffset)
    (source       : string)
    (seq          : int64) : MeasuredObservation<int64> =
    { InstrumentId   = instrumentId
      ObservedAt     = observedAt
      ReceivedAt     = DateTimeOffset.UtcNow
      Value          = volume
      Source         = source
      SequenceNumber = seq }

// ── Validation helpers ────────────────────────────────────────────────────────

/// True if the observation's price is a finite positive number.
[<CompiledName("IsPriceValid")>]
let isPriceValid (obs: MeasuredObservation<decimal>) : bool =
    obs.Value > 0m

/// True if this observation is no older than the given staleness threshold.
[<CompiledName("IsFresh")>]
let isFresh (threshold: TimeSpan) (asOf: DateTimeOffset) (obs: MeasuredObservation<'T>) : bool =
    (asOf - obs.ObservedAt) <= threshold
