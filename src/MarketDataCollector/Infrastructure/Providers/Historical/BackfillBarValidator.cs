using MarketDataCollector.Application.Logging;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Validation codes describing specific issues detected in historical bar data.
/// </summary>
public static class BarValidationCodes
{
    public const string NegativePrice = "NEGATIVE_PRICE";
    public const string PriceExceedsMax = "PRICE_EXCEEDS_MAX";
    public const string PriceBelowMin = "PRICE_BELOW_MIN";
    public const string OhlcInconsistency = "OHLC_INCONSISTENCY";
    public const string ZeroVolume = "ZERO_VOLUME";
    public const string NegativeVolume = "NEGATIVE_VOLUME";
    public const string VolumeExceedsMax = "VOLUME_EXCEEDS_MAX";
    public const string FutureDate = "FUTURE_DATE";
    public const string DuplicateDate = "DUPLICATE_DATE";
    public const string PriceSpike = "PRICE_SPIKE";
    public const string PriceGap = "PRICE_GAP";
    public const string StaleData = "STALE_DATA";
    public const string EmptySymbol = "EMPTY_SYMBOL";
    public const string EmptySource = "EMPTY_SOURCE";
}

/// <summary>
/// A non-fatal validation issue detected in a historical bar.
/// Warnings do not prevent storage but signal data quality concerns.
/// </summary>
/// <param name="Symbol">The ticker symbol associated with the warning.</param>
/// <param name="SessionDate">The session date of the bar that triggered the warning.</param>
/// <param name="Code">A machine-readable code identifying the type of warning (e.g., "PRICE_SPIKE").</param>
/// <param name="Message">A human-readable description of the issue.</param>
public sealed record BarValidationWarning(
    string Symbol,
    DateOnly SessionDate,
    string Code,
    string Message);

/// <summary>
/// A fatal validation issue detected in a historical bar.
/// Errors prevent the bar from being persisted to storage.
/// </summary>
/// <param name="Symbol">The ticker symbol associated with the error.</param>
/// <param name="SessionDate">The session date of the bar that triggered the error.</param>
/// <param name="Code">A machine-readable code identifying the type of error (e.g., "NEGATIVE_PRICE").</param>
/// <param name="Message">A human-readable description of the issue.</param>
public sealed record BarValidationError(
    string Symbol,
    DateOnly SessionDate,
    string Code,
    string Message);

/// <summary>
/// A bar that was rejected during validation, paired with the reasons for rejection.
/// </summary>
/// <param name="Bar">The rejected historical bar.</param>
/// <param name="Errors">The validation errors that caused rejection.</param>
public sealed record RejectedBar(
    HistoricalBar Bar,
    IReadOnlyList<BarValidationError> Errors);

/// <summary>
/// The result of validating a batch of historical bars.
/// Contains the filtered valid bars, rejected bars with reasons, and accumulated warnings.
/// </summary>
public sealed record BarValidationResult
{
    /// <summary>
    /// Gets whether the entire batch passed validation without any errors.
    /// True when there are zero rejected bars.
    /// </summary>
    public bool IsValid => RejectedBars.Count == 0;

    /// <summary>
    /// Non-fatal issues detected across the batch.
    /// </summary>
    public IReadOnlyList<BarValidationWarning> Warnings { get; }

    /// <summary>
    /// Fatal issues detected across the batch.
    /// </summary>
    public IReadOnlyList<BarValidationError> Errors { get; }

    /// <summary>
    /// Bars that passed validation and are safe to persist.
    /// </summary>
    public IReadOnlyList<HistoricalBar> ValidBars { get; }

    /// <summary>
    /// Bars that failed validation with the reasons for rejection.
    /// </summary>
    public IReadOnlyList<RejectedBar> RejectedBars { get; }

    public BarValidationResult(
        IReadOnlyList<BarValidationWarning> warnings,
        IReadOnlyList<BarValidationError> errors,
        IReadOnlyList<HistoricalBar> validBars,
        IReadOnlyList<RejectedBar> rejectedBars)
    {
        Warnings = warnings;
        Errors = errors;
        ValidBars = validBars;
        RejectedBars = rejectedBars;
    }
}

/// <summary>
/// Configuration for historical bar validation thresholds.
/// Use the static factory methods to create preset configurations.
/// </summary>
public sealed record BarValidationConfig
{
    /// <summary>
    /// Maximum allowed price for any OHLC field. Bars exceeding this are rejected.
    /// </summary>
    public decimal MaxPrice { get; init; } = 1_000_000m;

    /// <summary>
    /// Minimum allowed price for any OHLC field. Bars below this are rejected.
    /// </summary>
    public decimal MinPrice { get; init; } = 0.0001m;

    /// <summary>
    /// Maximum allowed volume. Bars exceeding this are rejected.
    /// </summary>
    public long MaxVolume { get; init; } = 50_000_000_000L;

    /// <summary>
    /// Maximum daily change percent (|close - open| / open * 100).
    /// Bars exceeding this threshold generate a warning.
    /// </summary>
    public decimal MaxDailyChangePercent { get; init; } = 100m;

    /// <summary>
    /// Maximum gap percent between consecutive bars (|current.open - previous.close| / previous.close * 100).
    /// Gaps exceeding this threshold generate a warning.
    /// </summary>
    public decimal MaxGapPercent { get; init; } = 50m;

    /// <summary>
    /// Whether to allow zero-volume bars. Historical data often has legitimate zero-volume bars
    /// for thinly traded securities or non-trading days included in some feeds.
    /// </summary>
    public bool AllowZeroVolume { get; init; } = true;

    /// <summary>
    /// Whether to allow bars with session dates in the future.
    /// </summary>
    public bool AllowFutureDate { get; init; } = false;

    /// <summary>
    /// Number of consecutive bars with identical OHLC values before flagging as stale data.
    /// </summary>
    public int StaleDataThreshold { get; init; } = 5;

    /// <summary>
    /// Creates a default configuration suitable for most historical backfill scenarios.
    /// </summary>
    public static BarValidationConfig CreateDefault() => new();

    /// <summary>
    /// Creates a strict configuration for high-quality data sources where anomalies
    /// are more likely to indicate real errors.
    /// </summary>
    public static BarValidationConfig CreateStrict() => new()
    {
        MaxPrice = 500_000m,
        MinPrice = 0.01m,
        MaxVolume = 10_000_000_000L,
        MaxDailyChangePercent = 50m,
        MaxGapPercent = 25m,
        AllowZeroVolume = false,
        AllowFutureDate = false,
        StaleDataThreshold = 3
    };

    /// <summary>
    /// Creates a lenient configuration for data sources known to have quirks,
    /// such as penny stocks, historical data with splits not yet adjusted, or
    /// international markets with different conventions.
    /// </summary>
    public static BarValidationConfig CreateLenient() => new()
    {
        MaxPrice = 10_000_000m,
        MinPrice = 0.000001m,
        MaxVolume = 100_000_000_000L,
        MaxDailyChangePercent = 500m,
        MaxGapPercent = 200m,
        AllowZeroVolume = true,
        AllowFutureDate = false,
        StaleDataThreshold = 10
    };
}

/// <summary>
/// Validates historical bars before they are persisted to storage.
/// Checks for OHLC consistency, price and volume reasonableness, duplicate dates,
/// price spikes, inter-bar gaps, stale data patterns, and future dates.
/// </summary>
/// <remarks>
/// Follows the validation patterns established in the F# validators
/// (TradeValidator.fs, QuoteValidator.fs) but adapted for batch-oriented
/// historical bar validation. Issues are classified as warnings (non-fatal,
/// bar is still stored) or errors (fatal, bar is rejected).
/// </remarks>
[ImplementsAdr("ADR-004", "Historical bar validation with configurable thresholds")]
public sealed class BackfillBarValidator
{
    private readonly ILogger _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackfillBarValidator"/> class.
    /// </summary>
    /// <param name="log">Optional Serilog logger. Falls back to a contextual logger if not provided.</param>
    public BackfillBarValidator(ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<BackfillBarValidator>();
    }

    /// <summary>
    /// Validates a batch of historical bars, returning valid bars, rejected bars,
    /// and accumulated warnings and errors.
    /// </summary>
    /// <param name="bars">The bars to validate.</param>
    /// <param name="config">
    /// Optional validation configuration. If null, <see cref="BarValidationConfig.CreateDefault"/> is used.
    /// </param>
    /// <returns>A <see cref="BarValidationResult"/> containing filtered results and diagnostics.</returns>
    public BarValidationResult ValidateBars(IReadOnlyList<HistoricalBar> bars, BarValidationConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(bars);

        var cfg = config ?? BarValidationConfig.CreateDefault();
        var allWarnings = new List<BarValidationWarning>();
        var allErrors = new List<BarValidationError>();
        var validBars = new List<HistoricalBar>();
        var rejectedBars = new List<RejectedBar>();

        if (bars.Count == 0)
        {
            _log.Debug("No bars to validate");
            return new BarValidationResult(allWarnings, allErrors, validBars, rejectedBars);
        }

        // Sort bars by symbol then date for sequential analysis
        var sortedBars = bars
            .OrderBy(b => b.Symbol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.SessionDate)
            .ToList();

        // Detect duplicates across the batch (same symbol + date)
        var duplicateSet = DetectDuplicates(sortedBars);

        // Track previous bar per symbol for gap and spike detection
        var previousBarBySymbol = new Dictionary<string, HistoricalBar>(StringComparer.OrdinalIgnoreCase);

        // Track consecutive identical OHLC counts per symbol for stale detection
        var staleCountBySymbol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var staleStartBySymbol = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);

        foreach (var bar in sortedBars)
        {
            previousBarBySymbol.TryGetValue(bar.Symbol, out var previousBar);

            var (isValid, warnings, errors) = ValidateBar(bar, previousBar, cfg);

            // Check for duplicate date
            var duplicateKey = (bar.Symbol.ToUpperInvariant(), bar.SessionDate);
            if (duplicateSet.Contains(duplicateKey))
            {
                warnings.Add(new BarValidationWarning(
                    bar.Symbol,
                    bar.SessionDate,
                    BarValidationCodes.DuplicateDate,
                    $"Duplicate bar for {bar.Symbol} on {bar.SessionDate:yyyy-MM-dd}"));
            }

            // Stale data detection: track consecutive bars with identical OHLC
            DetectStaleData(bar, previousBar, cfg, staleCountBySymbol, staleStartBySymbol, warnings);

            allWarnings.AddRange(warnings);
            allErrors.AddRange(errors);

            if (isValid)
            {
                validBars.Add(bar);
            }
            else
            {
                rejectedBars.Add(new RejectedBar(bar, errors));
            }

            previousBarBySymbol[bar.Symbol] = bar;
        }

        // Log summary
        _log.Information(
            "Bar validation complete: {Total} total, {Valid} valid, {Rejected} rejected, {Warnings} warnings",
            bars.Count, validBars.Count, rejectedBars.Count, allWarnings.Count);

        if (rejectedBars.Count > 0)
        {
            _log.Warning(
                "Rejected {RejectedCount} bars with {ErrorCount} errors",
                rejectedBars.Count, allErrors.Count);
        }

        return new BarValidationResult(allWarnings, allErrors, validBars, rejectedBars);
    }

    /// <summary>
    /// Validates a single historical bar against the specified configuration.
    /// Optionally uses the previous bar for inter-bar checks (gap detection, spike detection).
    /// </summary>
    /// <param name="bar">The bar to validate.</param>
    /// <param name="previousBar">The preceding bar for the same symbol, or null if this is the first bar.</param>
    /// <param name="config">The validation configuration.</param>
    /// <returns>
    /// A tuple containing whether the bar is valid, a list of warnings, and a list of errors.
    /// </returns>
    public (bool isValid, List<BarValidationWarning> warnings, List<BarValidationError> errors) ValidateBar(
        HistoricalBar bar,
        HistoricalBar? previousBar,
        BarValidationConfig config)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(config);

        var warnings = new List<BarValidationWarning>();
        var errors = new List<BarValidationError>();

        ValidateSymbolAndSource(bar, errors);
        ValidateOhlcConsistency(bar, errors);
        ValidatePriceReasonableness(bar, config, errors);
        ValidateVolume(bar, config, warnings, errors);
        ValidateFutureDate(bar, config, errors);
        ValidateDailyChange(bar, config, warnings);
        ValidateInterBarGap(bar, previousBar, config, warnings);

        return (errors.Count == 0, warnings, errors);
    }

    #region Individual Validation Checks

    /// <summary>
    /// Validates that the symbol and source fields are non-empty.
    /// </summary>
    private static void ValidateSymbolAndSource(HistoricalBar bar, List<BarValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(bar.Symbol))
        {
            errors.Add(new BarValidationError(
                bar.Symbol ?? string.Empty,
                bar.SessionDate,
                BarValidationCodes.EmptySymbol,
                "Bar has empty or whitespace-only symbol"));
        }

        if (string.IsNullOrWhiteSpace(bar.Source))
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.EmptySource,
                "Bar has empty or whitespace-only source"));
        }
    }

    /// <summary>
    /// Validates OHLC consistency: High >= Low, Open and Close within [Low, High].
    /// The HistoricalBar constructor already enforces this, but we double-check here
    /// in case bars are constructed via deserialization or other paths.
    /// </summary>
    private static void ValidateOhlcConsistency(HistoricalBar bar, List<BarValidationError> errors)
    {
        if (bar.Low > bar.High)
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.OhlcInconsistency,
                $"Low ({bar.Low}) exceeds High ({bar.High})"));
        }

        if (bar.Open > bar.High || bar.Open < bar.Low)
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.OhlcInconsistency,
                $"Open ({bar.Open}) outside High-Low range [{bar.Low}, {bar.High}]"));
        }

        if (bar.Close > bar.High || bar.Close < bar.Low)
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.OhlcInconsistency,
                $"Close ({bar.Close}) outside High-Low range [{bar.Low}, {bar.High}]"));
        }
    }

    /// <summary>
    /// Validates that all OHLC prices are within the configured bounds.
    /// Negative prices and prices exceeding MaxPrice are errors.
    /// Prices below MinPrice are also errors.
    /// </summary>
    private static void ValidatePriceReasonableness(
        HistoricalBar bar,
        BarValidationConfig config,
        List<BarValidationError> errors)
    {
        var prices = new (string Name, decimal Value)[]
        {
            ("Open", bar.Open),
            ("High", bar.High),
            ("Low", bar.Low),
            ("Close", bar.Close)
        };

        foreach (var (name, value) in prices)
        {
            if (value < 0)
            {
                errors.Add(new BarValidationError(
                    bar.Symbol,
                    bar.SessionDate,
                    BarValidationCodes.NegativePrice,
                    $"{name} price is negative ({value})"));
            }
            else if (value > config.MaxPrice)
            {
                errors.Add(new BarValidationError(
                    bar.Symbol,
                    bar.SessionDate,
                    BarValidationCodes.PriceExceedsMax,
                    $"{name} price ({value}) exceeds maximum ({config.MaxPrice})"));
            }
            else if (value > 0 && value < config.MinPrice)
            {
                errors.Add(new BarValidationError(
                    bar.Symbol,
                    bar.SessionDate,
                    BarValidationCodes.PriceBelowMin,
                    $"{name} price ({value}) is below minimum ({config.MinPrice})"));
            }
        }
    }

    /// <summary>
    /// Validates volume against configured bounds.
    /// Negative volume is always an error. Zero volume is a warning unless allowed by config.
    /// Volume exceeding MaxVolume is an error.
    /// </summary>
    private static void ValidateVolume(
        HistoricalBar bar,
        BarValidationConfig config,
        List<BarValidationWarning> warnings,
        List<BarValidationError> errors)
    {
        if (bar.Volume < 0)
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.NegativeVolume,
                $"Volume is negative ({bar.Volume})"));
        }
        else if (bar.Volume == 0 && !config.AllowZeroVolume)
        {
            warnings.Add(new BarValidationWarning(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.ZeroVolume,
                "Volume is zero"));
        }
        else if (bar.Volume == 0 && config.AllowZeroVolume)
        {
            // Zero volume is allowed; no action needed
        }

        if (bar.Volume > config.MaxVolume)
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.VolumeExceedsMax,
                $"Volume ({bar.Volume}) exceeds maximum ({config.MaxVolume})"));
        }
    }

    /// <summary>
    /// Validates that the bar's session date is not in the future.
    /// </summary>
    private static void ValidateFutureDate(
        HistoricalBar bar,
        BarValidationConfig config,
        List<BarValidationError> errors)
    {
        if (config.AllowFutureDate)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (bar.SessionDate > today)
        {
            errors.Add(new BarValidationError(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.FutureDate,
                $"Session date {bar.SessionDate:yyyy-MM-dd} is in the future (today is {today:yyyy-MM-dd})"));
        }
    }

    /// <summary>
    /// Checks for excessive intra-bar price changes (|close - open| / open).
    /// This is a warning, not an error, because legitimate large moves can occur.
    /// </summary>
    private static void ValidateDailyChange(
        HistoricalBar bar,
        BarValidationConfig config,
        List<BarValidationWarning> warnings)
    {
        if (bar.Open <= 0)
            return;

        var changePercent = Math.Abs(bar.Close - bar.Open) / bar.Open * 100m;
        if (changePercent > config.MaxDailyChangePercent)
        {
            warnings.Add(new BarValidationWarning(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.PriceSpike,
                $"Daily change of {changePercent:F2}% (open {bar.Open} to close {bar.Close}) exceeds threshold of {config.MaxDailyChangePercent}%"));
        }
    }

    /// <summary>
    /// Checks for excessive price gaps between consecutive bars for the same symbol.
    /// The gap is measured as |current.Open - previous.Close| / previous.Close.
    /// </summary>
    private static void ValidateInterBarGap(
        HistoricalBar bar,
        HistoricalBar? previousBar,
        BarValidationConfig config,
        List<BarValidationWarning> warnings)
    {
        if (previousBar == null || previousBar.Close <= 0)
            return;

        // Only check gap for bars of the same symbol
        if (!string.Equals(bar.Symbol, previousBar.Symbol, StringComparison.OrdinalIgnoreCase))
            return;

        var gapPercent = Math.Abs(bar.Open - previousBar.Close) / previousBar.Close * 100m;
        if (gapPercent > config.MaxGapPercent)
        {
            warnings.Add(new BarValidationWarning(
                bar.Symbol,
                bar.SessionDate,
                BarValidationCodes.PriceGap,
                $"Price gap of {gapPercent:F2}% between previous close ({previousBar.Close} on {previousBar.SessionDate:yyyy-MM-dd}) and current open ({bar.Open})"));
        }
    }

    #endregion

    #region Batch-Level Checks

    /// <summary>
    /// Identifies duplicate (symbol, date) pairs in the batch.
    /// Returns a set of all keys that appear more than once.
    /// </summary>
    private static HashSet<(string Symbol, DateOnly Date)> DetectDuplicates(IReadOnlyList<HistoricalBar> sortedBars)
    {
        var seen = new HashSet<(string, DateOnly)>();
        var duplicates = new HashSet<(string, DateOnly)>();

        foreach (var bar in sortedBars)
        {
            var key = (bar.Symbol.ToUpperInvariant(), bar.SessionDate);
            if (!seen.Add(key))
            {
                duplicates.Add(key);
            }
        }

        return duplicates;
    }

    /// <summary>
    /// Tracks consecutive bars with identical OHLC values per symbol.
    /// When the count reaches the configured threshold, a stale data warning is emitted.
    /// </summary>
    private static void DetectStaleData(
        HistoricalBar bar,
        HistoricalBar? previousBar,
        BarValidationConfig config,
        Dictionary<string, int> staleCountBySymbol,
        Dictionary<string, DateOnly> staleStartBySymbol,
        List<BarValidationWarning> warnings)
    {
        if (previousBar == null
            || !string.Equals(bar.Symbol, previousBar.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            staleCountBySymbol[bar.Symbol] = 1;
            staleStartBySymbol[bar.Symbol] = bar.SessionDate;
            return;
        }

        var isIdentical = bar.Open == previousBar.Open
                          && bar.High == previousBar.High
                          && bar.Low == previousBar.Low
                          && bar.Close == previousBar.Close;

        if (isIdentical)
        {
            staleCountBySymbol.TryGetValue(bar.Symbol, out var count);
            count++;
            staleCountBySymbol[bar.Symbol] = count;

            if (count == config.StaleDataThreshold)
            {
                staleStartBySymbol.TryGetValue(bar.Symbol, out var startDate);
                warnings.Add(new BarValidationWarning(
                    bar.Symbol,
                    bar.SessionDate,
                    BarValidationCodes.StaleData,
                    $"Identical OHLC values for {count} consecutive bars from {startDate:yyyy-MM-dd} to {bar.SessionDate:yyyy-MM-dd} "
                    + $"(O={bar.Open}, H={bar.High}, L={bar.Low}, C={bar.Close})"));
            }
        }
        else
        {
            staleCountBySymbol[bar.Symbol] = 1;
            staleStartBySymbol[bar.Symbol] = bar.SessionDate;
        }
    }

    #endregion
}
