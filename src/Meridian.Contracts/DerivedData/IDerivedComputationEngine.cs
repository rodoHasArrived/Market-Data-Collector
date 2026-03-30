using Meridian.Contracts.SecurityMaster;

namespace Meridian.Contracts.DerivedData;

/// <summary>
/// Computes derived values (Greeks, VWAP, implied rates) from measured inputs
/// and stores the results with full input provenance.
/// <para>
/// The engine is the single point where measurements become calculations.
/// Every computation stores its inputs so that any result can be replicated
/// or audited independently of current market conditions.
/// </para>
/// <para>
/// Rules:
/// <list type="bullet">
///   <item>Never compute and discard — always persist what was computed and when.</item>
///   <item>Inputs are always stored at computation time, even for cheap calculations.</item>
///   <item>Consumers read results from the store; they do not call the engine directly.</item>
/// </list>
/// </para>
/// </summary>
public interface IDerivedComputationEngine
{
    /// <summary>
    /// Compute option Greeks for the given instrument using the specified inputs,
    /// persist the result, and return it.
    /// </summary>
    /// <param name="instrumentId">The option instrument to compute Greeks for.</param>
    /// <param name="inputs">All inputs required by the option model.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored computation result including the computed Greeks.</returns>
    ValueTask<GreekComputationResult> ComputeGreeksAsync(
        InstrumentId instrumentId,
        GreekComputationInputs inputs,
        CancellationToken ct = default);

    /// <summary>
    /// Compute VWAP for the given instrument over a time window,
    /// persist the result, and return it.
    /// </summary>
    ValueTask<VwapComputationResult> ComputeVwapAsync(
        InstrumentId instrumentId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);

    /// <summary>
    /// Compute the implied annualised interest rate from a box spread,
    /// persist the result, and return it.
    /// </summary>
    ValueTask<BoxSpreadImpliedRateResult> ComputeBoxSpreadImpliedRateAsync(
        BoxSpreadComputationInputs inputs,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve the most recently stored Greek computation for an instrument as of
    /// a specific point in time.
    /// Returns null if no computation exists at or before <paramref name="asOf"/>.
    /// </summary>
    ValueTask<GreekComputationResult?> GetLatestGreeksAsync(
        InstrumentId instrumentId,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve all stored Greek computations for an instrument within a time range.
    /// </summary>
    IAsyncEnumerable<GreekComputationResult> GetGreekHistoryAsync(
        InstrumentId instrumentId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

/// <summary>Inputs for option Greek computation.</summary>
public sealed record GreekComputationInputs
{
    public decimal SpotPrice         { get; init; }
    public decimal Strike            { get; init; }
    public decimal TimeToExpiryYears { get; init; }
    public decimal Volatility        { get; init; }
    public decimal RiskFreeRate      { get; init; }
    public decimal DividendYield     { get; init; }
    public OptionSide OptionSide     { get; init; }
    public ExerciseStyle ExerciseStyle { get; init; }

    /// <summary>Source identifier for the volatility estimate used (e.g., "MARKET_IV", "HV30D").</summary>
    public string VolatilitySource   { get; init; } = "";
}

/// <summary>Stored result of an option Greek computation with input provenance.</summary>
public sealed record GreekComputationResult
{
    public InstrumentId InstrumentId    { get; init; }
    public DateTimeOffset ComputedAt    { get; init; }
    public string ModelName             { get; init; } = "";
    public string ModelVersion          { get; init; } = "";

    // Inputs (stored for reproducibility)
    public GreekComputationInputs Inputs { get; init; } = new();

    // Results
    public decimal TheoreticalPrice     { get; init; }
    public decimal Delta                { get; init; }
    public decimal Gamma                { get; init; }
    public decimal Theta                { get; init; }
    public decimal Vega                 { get; init; }
    public decimal Rho                  { get; init; }
    public decimal? ImpliedVolatility   { get; init; }
}

/// <summary>Stored result of a VWAP computation with input provenance.</summary>
public sealed record VwapComputationResult
{
    public InstrumentId InstrumentId  { get; init; }
    public DateTimeOffset ComputedAt  { get; init; }
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd   { get; init; }
    public int TradeCount             { get; init; }
    public long TotalVolume           { get; init; }
    public decimal Vwap               { get; init; }
    public decimal Twap               { get; init; }
}

/// <summary>Inputs for a box spread implied rate computation.</summary>
public sealed record BoxSpreadComputationInputs
{
    public InstrumentId UnderlyingId  { get; init; }
    public decimal LowerStrike        { get; init; }
    public decimal UpperStrike        { get; init; }
    public DateOnly ExpiryDate        { get; init; }
    public decimal TimeToExpiryYears  { get; init; }
    public decimal LowerCallPrice     { get; init; }
    public decimal LowerPutPrice      { get; init; }
    public decimal UpperCallPrice     { get; init; }
    public decimal UpperPutPrice      { get; init; }
}

/// <summary>Stored result of a box spread implied rate computation.</summary>
public sealed record BoxSpreadImpliedRateResult
{
    public InstrumentId UnderlyingId  { get; init; }
    public DateTimeOffset ComputedAt  { get; init; }
    public BoxSpreadComputationInputs Inputs { get; init; } = new();
    public decimal BoxWidth           { get; init; }
    public decimal NetPremium         { get; init; }
    public decimal PresentValue       { get; init; }
    public decimal ImpliedAnnualRate  { get; init; }
}
