/// Derived computations — model outputs with full input provenance.
///
/// A DerivedComputation differs fundamentally from a MeasuredObservation:
///   - It is the result of a CALCULATION applied to measured inputs.
///   - The inputs that produced it are stored alongside the result.
///   - Given the same inputs, the same model must reproduce the same output.
///   - Historical results remain meaningful even after the market moves.
///
/// This separation matters for:
///   - Audit trails: why did the system compute delta = 0.47 at 14:30?
///   - Backtesting: replaying a strategy must use the Greeks available *then*.
///   - Debugging: a mis-priced option can be diagnosed by inspecting stored inputs.
///   - Regulatory: portfolio P&L attribution requires consistent Greeks over time.
///
/// Examples of DERIVED data:
///   - Option delta (Black-Scholes, inputs: spot, strike, vol, rate, time)
///   - Option gamma, vega, theta, rho
///   - VWAP over a window (inputs: individual trade prices and sizes)
///   - Implied interest rate of a box spread (inputs: four option prices + strikes)
///   - Portfolio beta (inputs: individual position betas + weights)
module Meridian.FSharp.Domain.DerivedData

open System
open Meridian.Contracts.SecurityMaster

// ── Core derivation type ──────────────────────────────────────────────────────

/// The inputs to a specific computation run.
/// Represented as a key-value map so that any model's input set can be stored
/// without creating separate types for every possible computation.
type ComputationInputs = Map<string, string>

/// A single derived computation result with full input provenance.
///
/// 'Inputs is the type-safe inputs record for a specific computation family.
/// 'Result is the computed output type.
type DerivedComputation<'Inputs, 'Result> =
    {
        /// The primary instrument this computation is about.
        InstrumentId  : InstrumentId

        /// When this computation was produced.
        ComputedAt    : DateTimeOffset

        /// The model or algorithm that produced this result (e.g., "BlackScholes76").
        ModelName     : string

        /// Version of the model/algorithm, enabling reproducibility audits.
        ModelVersion  : string

        /// The typed inputs snapshot — stored so the computation is fully reproducible.
        Inputs        : 'Inputs

        /// The computed result value.
        Result        : 'Result
    }

// ── Greek computation types ───────────────────────────────────────────────────

/// Inputs to a standard option pricing model (Black-Scholes/76).
type OptionModelInputs =
    {
        /// Current price of the underlying instrument.
        SpotPrice         : decimal

        /// Option strike price (as registered in Security Master).
        Strike            : decimal

        /// Time to expiry in years (e.g., 30 days = 30/365.0).
        TimeToExpiryYears : decimal

        /// Annualised implied or historical volatility (e.g., 0.20 for 20%).
        Volatility        : decimal

        /// Annualised risk-free rate (e.g., 0.05 for 5%).
        RiskFreeRate      : decimal

        /// Continuous dividend yield (0.0 for non-dividend-paying assets).
        DividendYield     : decimal

        /// Call or Put (from InstrumentKind taxonomy).
        OptionSide        : Meridian.Contracts.SecurityMaster.OptionSide

        /// American or European exercise (affects pricing model selection).
        ExerciseStyle     : Meridian.Contracts.SecurityMaster.ExerciseStyle

        /// Source of the volatility estimate (e.g., "MARKET_IV", "HV30D").
        VolatilitySource  : string
    }

/// The output of a full option Greek computation.
type OptionGreeks =
    {
        /// Option price under this model.
        TheoreticalPrice  : decimal

        /// Rate of change of option price with respect to underlying price.
        Delta             : decimal

        /// Rate of change of delta with respect to underlying price.
        Gamma             : decimal

        /// Rate of change of option price with respect to time (per day, negative).
        Theta             : decimal

        /// Rate of change of option price with respect to volatility (per 1%).
        Vega              : decimal

        /// Rate of change of option price with respect to interest rate (per 1%).
        Rho               : decimal

        /// Annualised implied volatility back-solved from the market price (if available).
        ImpliedVolatility : decimal option
    }

/// Convenience alias for Greek computations.
type GreekComputation = DerivedComputation<OptionModelInputs, OptionGreeks>

// ── VWAP computation types ────────────────────────────────────────────────────

/// Inputs to a VWAP computation over a window.
type VwapInputs =
    {
        /// Window start (inclusive).
        WindowStart   : DateTimeOffset

        /// Window end (inclusive).
        WindowEnd     : DateTimeOffset

        /// Number of trades included in the calculation.
        TradeCount    : int

        /// Total volume traded in the window.
        TotalVolume   : int64
    }

/// Output of a VWAP computation.
type VwapResult =
    {
        /// Volume-weighted average price over the window.
        Vwap          : decimal

        /// Simple average price (unweighted) for comparison.
        Twap          : decimal
    }

/// Convenience alias for VWAP computations.
type VwapComputation = DerivedComputation<VwapInputs, VwapResult>

// ── Synthetic implied rate types ──────────────────────────────────────────────

/// Inputs to an implied interest rate calculation from a box spread.
/// A box spread is: long call(K1) + short put(K1) + short call(K2) + long put(K2)
/// where K1 < K2. The box always settles for (K2 - K1) at expiry.
type BoxSpreadInputs =
    {
        /// Underlying instrument (index or equity).
        UnderlyingId       : InstrumentId

        /// Lower strike options.
        LowerStrike        : decimal

        /// Upper strike options.
        UpperStrike        : decimal

        /// Expiry date (same for all four legs).
        ExpiryDate         : DateOnly

        /// Time to expiry in years.
        TimeToExpiryYears  : decimal

        /// Net debit paid for the box (positive = net debit to open).
        NetPremium         : decimal

        /// Prices of all four legs at trade time.
        LowerCallPrice     : decimal
        LowerPutPrice      : decimal
        UpperCallPrice     : decimal
        UpperPutPrice      : decimal
    }

/// Result of an implied rate calculation from a box spread.
type ImpliedRateResult =
    {
        /// Annualised implied borrowing/lending rate from the box.
        ImpliedAnnualRate  : decimal

        /// The face value of the box at expiry: UpperStrike - LowerStrike.
        BoxWidth           : decimal

        /// The present value of the box at the implied rate.
        PresentValue       : decimal
    }

/// Convenience alias for box spread implied rate computations.
type BoxSpreadImpliedRateComputation = DerivedComputation<BoxSpreadInputs, ImpliedRateResult>

// ── Factory and query helpers ─────────────────────────────────────────────────

/// Create a computation with the current timestamp.
[<CompiledName("Create")>]
let create
    (instrumentId : InstrumentId)
    (modelName    : string)
    (modelVersion : string)
    (inputs       : 'Inputs)
    (result       : 'Result) : DerivedComputation<'Inputs, 'Result> =
    { InstrumentId  = instrumentId
      ComputedAt    = DateTimeOffset.UtcNow
      ModelName     = modelName
      ModelVersion  = modelVersion
      Inputs        = inputs
      Result        = result }

/// True if this computation is no older than the given staleness threshold.
[<CompiledName("IsFresh")>]
let isFresh (threshold: TimeSpan) (asOf: DateTimeOffset) (c: DerivedComputation<'I,'R>) : bool =
    (asOf - c.ComputedAt) <= threshold
