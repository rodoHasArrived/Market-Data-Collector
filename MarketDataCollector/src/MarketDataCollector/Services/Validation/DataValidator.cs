using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Services.Validation;

/// <summary>
/// Validates market data integrity and quality.
/// Inspired by StockSharp Hydra's data validation capabilities.
///
/// Checks for:
/// - Sequence gaps and duplicates
/// - Timestamp anomalies (out of order, future dates)
/// - Price anomalies (spikes, zero prices)
/// - Volume anomalies
/// - Order book crossed markets
/// - Data completeness
/// </summary>
public sealed class DataValidator
{
    private readonly ILogger _log = LoggingSetup.ForContext<DataValidator>();
    private readonly ValidationConfig _config;
    private readonly ConcurrentDictionary<string, SymbolValidationState> _symbolStates = new();

    public DataValidator(ValidationConfig? config = null)
    {
        _config = config ?? new ValidationConfig();
    }

    /// <summary>
    /// Validate a trade and return any issues found.
    /// </summary>
    public ValidationResult ValidateTrade(Trade trade)
    {
        var issues = new List<ValidationIssue>();
        var state = _symbolStates.GetOrAdd(trade.Symbol, _ => new SymbolValidationState());

        // Check price
        if (trade.Price <= 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                ValidationIssueType.InvalidPrice,
                $"Non-positive price: {trade.Price}",
                trade.Symbol,
                trade.Timestamp));
        }

        // Check for price spike
        if (state.LastTradePrice.HasValue && _config.MaxPriceChangePercent > 0)
        {
            var changePercent = Math.Abs((trade.Price - state.LastTradePrice.Value) / state.LastTradePrice.Value * 100);
            if (changePercent > (decimal)_config.MaxPriceChangePercent)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.PriceSpike,
                    $"Price spike: {changePercent:F2}% change from {state.LastTradePrice} to {trade.Price}",
                    trade.Symbol,
                    trade.Timestamp));
            }
        }

        // Check size
        if (trade.Size < 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                ValidationIssueType.InvalidSize,
                $"Negative size: {trade.Size}",
                trade.Symbol,
                trade.Timestamp));
        }

        // Check sequence
        if (state.LastTradeSequence.HasValue && _config.CheckSequenceGaps)
        {
            if (trade.SequenceNumber <= state.LastTradeSequence.Value)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.SequenceAnomaly,
                    $"Sequence not increasing: {trade.SequenceNumber} <= {state.LastTradeSequence}",
                    trade.Symbol,
                    trade.Timestamp));
            }
            else if (trade.SequenceNumber > state.LastTradeSequence.Value + 1)
            {
                var gap = trade.SequenceNumber - state.LastTradeSequence.Value - 1;
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.SequenceGap,
                    $"Sequence gap of {gap} detected",
                    trade.Symbol,
                    trade.Timestamp));
            }
        }

        // Check timestamp
        if (_config.CheckTimestampOrder && state.LastTradeTime.HasValue)
        {
            if (trade.Timestamp < state.LastTradeTime.Value)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.TimestampOutOfOrder,
                    $"Timestamp out of order: {trade.Timestamp} < {state.LastTradeTime}",
                    trade.Symbol,
                    trade.Timestamp));
            }
        }

        // Check for future timestamp
        if (_config.MaxFutureTimestamp.HasValue)
        {
            var maxFuture = DateTimeOffset.UtcNow.Add(_config.MaxFutureTimestamp.Value);
            if (trade.Timestamp > maxFuture)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationIssueType.FutureTimestamp,
                    $"Timestamp in future: {trade.Timestamp}",
                    trade.Symbol,
                    trade.Timestamp));
            }
        }

        // Update state
        state.LastTradePrice = trade.Price;
        state.LastTradeSequence = trade.SequenceNumber;
        state.LastTradeTime = trade.Timestamp;
        state.TradeCount++;

        return new ValidationResult
        {
            IsValid = issues.All(i => i.Severity != ValidationSeverity.Error),
            Issues = issues
        };
    }

    /// <summary>
    /// Validate an order book snapshot.
    /// </summary>
    public ValidationResult ValidateDepth(LOBSnapshot snapshot)
    {
        var issues = new List<ValidationIssue>();
        var state = _symbolStates.GetOrAdd(snapshot.Symbol, _ => new SymbolValidationState());

        // Check for crossed market
        if (snapshot.Bids.Count > 0 && snapshot.Asks.Count > 0)
        {
            var bestBid = snapshot.Bids[0].Price;
            var bestAsk = snapshot.Asks[0].Price;

            if (bestBid >= bestAsk)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationIssueType.CrossedMarket,
                    $"Crossed market: bid {bestBid} >= ask {bestAsk}",
                    snapshot.Symbol,
                    snapshot.Timestamp));
            }
        }

        // Check bid ordering (should be descending)
        for (int i = 1; i < snapshot.Bids.Count; i++)
        {
            if (snapshot.Bids[i].Price > snapshot.Bids[i - 1].Price)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.OrderBookMisordering,
                    $"Bids not in descending order at level {i}",
                    snapshot.Symbol,
                    snapshot.Timestamp));
                break;
            }
        }

        // Check ask ordering (should be ascending)
        for (int i = 1; i < snapshot.Asks.Count; i++)
        {
            if (snapshot.Asks[i].Price < snapshot.Asks[i - 1].Price)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.OrderBookMisordering,
                    $"Asks not in ascending order at level {i}",
                    snapshot.Symbol,
                    snapshot.Timestamp));
                break;
            }
        }

        // Check for zero/negative sizes
        foreach (var level in snapshot.Bids.Concat(snapshot.Asks))
        {
            if (level.Size <= 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    ValidationIssueType.InvalidSize,
                    $"Non-positive size at price {level.Price}: {level.Size}",
                    snapshot.Symbol,
                    snapshot.Timestamp));
            }

            if (level.Price <= 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    ValidationIssueType.InvalidPrice,
                    $"Non-positive price in order book: {level.Price}",
                    snapshot.Symbol,
                    snapshot.Timestamp));
            }
        }

        // Update state
        state.LastDepthTime = snapshot.Timestamp;
        state.DepthCount++;

        return new ValidationResult
        {
            IsValid = issues.All(i => i.Severity != ValidationSeverity.Error),
            Issues = issues
        };
    }

    /// <summary>
    /// Validate a stream of trades and return summary.
    /// </summary>
    public async Task<ValidationSummary> ValidateTradesAsync(
        IAsyncEnumerable<Trade> trades,
        CancellationToken ct = default)
    {
        var summary = new ValidationSummary();
        var issuesByType = new Dictionary<ValidationIssueType, int>();

        await foreach (var trade in trades.WithCancellation(ct))
        {
            summary.TotalRecords++;

            var result = ValidateTrade(trade);

            if (!result.IsValid)
                summary.InvalidRecords++;

            foreach (var issue in result.Issues)
            {
                if (!issuesByType.ContainsKey(issue.Type))
                    issuesByType[issue.Type] = 0;
                issuesByType[issue.Type]++;

                if (issue.Severity == ValidationSeverity.Error)
                    summary.ErrorCount++;
                else if (issue.Severity == ValidationSeverity.Warning)
                    summary.WarningCount++;

                // Keep sample of issues
                if (summary.SampleIssues.Count < 100)
                    summary.SampleIssues.Add(issue);
            }
        }

        summary.IssuesByType = issuesByType;
        return summary;
    }

    /// <summary>
    /// Check data completeness for a time range.
    /// </summary>
    public async Task<CompletenessReport> CheckCompletenessAsync(
        IAsyncEnumerable<Trade> trades,
        DateTimeOffset expectedStart,
        DateTimeOffset expectedEnd,
        TimeSpan expectedInterval,
        CancellationToken ct = default)
    {
        var report = new CompletenessReport
        {
            ExpectedStart = expectedStart,
            ExpectedEnd = expectedEnd,
            ExpectedInterval = expectedInterval
        };

        DateTimeOffset? firstTimestamp = null;
        DateTimeOffset? lastTimestamp = null;
        DateTimeOffset? previousTimestamp = null;
        var gaps = new List<DataGap>();
        long count = 0;

        await foreach (var trade in trades.WithCancellation(ct))
        {
            firstTimestamp ??= trade.Timestamp;
            lastTimestamp = trade.Timestamp;
            count++;

            if (previousTimestamp.HasValue)
            {
                var gap = trade.Timestamp - previousTimestamp.Value;
                if (gap > expectedInterval * 2) // Gap is more than 2x expected interval
                {
                    gaps.Add(new DataGap
                    {
                        Start = previousTimestamp.Value,
                        End = trade.Timestamp,
                        Duration = gap
                    });
                }
            }

            previousTimestamp = trade.Timestamp;
        }

        report.ActualStart = firstTimestamp;
        report.ActualEnd = lastTimestamp;
        report.RecordCount = count;
        report.Gaps = gaps;

        // Calculate completeness percentage
        if (firstTimestamp.HasValue && lastTimestamp.HasValue)
        {
            var actualDuration = lastTimestamp.Value - firstTimestamp.Value;
            var expectedDuration = expectedEnd - expectedStart;

            // Estimate expected records
            var expectedRecords = (long)(expectedDuration / expectedInterval);
            report.ExpectedRecords = expectedRecords;

            // Calculate coverage
            report.CoveragePercent = expectedRecords > 0
                ? Math.Min(100, (double)count / expectedRecords * 100)
                : 0;

            // Calculate gap time
            var totalGapTime = gaps.Aggregate(TimeSpan.Zero, (acc, g) => acc + g.Duration);
            report.TotalGapDuration = totalGapTime;
        }

        return report;
    }

    /// <summary>
    /// Get statistics for a symbol.
    /// </summary>
    public SymbolValidationStats? GetSymbolStats(string symbol)
    {
        if (!_symbolStates.TryGetValue(symbol, out var state))
            return null;

        return new SymbolValidationStats
        {
            Symbol = symbol,
            TradeCount = state.TradeCount,
            DepthCount = state.DepthCount,
            LastTradeTime = state.LastTradeTime,
            LastDepthTime = state.LastDepthTime,
            LastTradePrice = state.LastTradePrice
        };
    }

    /// <summary>
    /// Reset validation state for a symbol.
    /// </summary>
    public void ResetSymbol(string symbol)
    {
        _symbolStates.TryRemove(symbol, out _);
    }

    /// <summary>
    /// Reset all validation state.
    /// </summary>
    public void ResetAll()
    {
        _symbolStates.Clear();
    }
}

/// <summary>
/// Validation configuration.
/// </summary>
public sealed record ValidationConfig
{
    /// <summary>Maximum allowed price change percentage (0 to disable).</summary>
    public double MaxPriceChangePercent { get; init; } = 10.0;

    /// <summary>Whether to check for sequence gaps.</summary>
    public bool CheckSequenceGaps { get; init; } = true;

    /// <summary>Whether to check timestamp ordering.</summary>
    public bool CheckTimestampOrder { get; init; } = true;

    /// <summary>Maximum allowed future timestamp offset.</summary>
    public TimeSpan? MaxFutureTimestamp { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Minimum expected trades per minute.</summary>
    public int MinTradesPerMinute { get; init; } = 1;
}

/// <summary>
/// Per-symbol validation state.
/// </summary>
internal sealed class SymbolValidationState
{
    public decimal? LastTradePrice { get; set; }
    public long? LastTradeSequence { get; set; }
    public DateTimeOffset? LastTradeTime { get; set; }
    public DateTimeOffset? LastDepthTime { get; set; }
    public long TradeCount { get; set; }
    public long DepthCount { get; set; }
}

/// <summary>
/// Result of validating a single record.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();
}

/// <summary>
/// A single validation issue.
/// </summary>
public sealed record ValidationIssue(
    ValidationSeverity Severity,
    ValidationIssueType Type,
    string Message,
    string Symbol,
    DateTimeOffset Timestamp
);

/// <summary>
/// Issue severity levels.
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Types of validation issues.
/// </summary>
public enum ValidationIssueType
{
    InvalidPrice,
    InvalidSize,
    PriceSpike,
    SequenceGap,
    SequenceAnomaly,
    TimestampOutOfOrder,
    FutureTimestamp,
    CrossedMarket,
    OrderBookMisordering,
    DataGap,
    DuplicateRecord
}

/// <summary>
/// Summary of validation run.
/// </summary>
public sealed class ValidationSummary
{
    public long TotalRecords { get; set; }
    public long InvalidRecords { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public Dictionary<ValidationIssueType, int> IssuesByType { get; set; } = new();
    public List<ValidationIssue> SampleIssues { get; } = new();

    public double ErrorRate => TotalRecords > 0 ? (double)InvalidRecords / TotalRecords * 100 : 0;
}

/// <summary>
/// Data completeness report.
/// </summary>
public sealed class CompletenessReport
{
    public DateTimeOffset ExpectedStart { get; init; }
    public DateTimeOffset ExpectedEnd { get; init; }
    public TimeSpan ExpectedInterval { get; init; }
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }
    public long RecordCount { get; set; }
    public long ExpectedRecords { get; set; }
    public double CoveragePercent { get; set; }
    public IReadOnlyList<DataGap> Gaps { get; set; } = Array.Empty<DataGap>();
    public TimeSpan TotalGapDuration { get; set; }

    public bool IsComplete => CoveragePercent >= 99.0 && Gaps.Count == 0;
}

/// <summary>
/// Represents a gap in data.
/// </summary>
public sealed record DataGap
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Statistics for a validated symbol.
/// </summary>
public sealed record SymbolValidationStats
{
    public required string Symbol { get; init; }
    public long TradeCount { get; init; }
    public long DepthCount { get; init; }
    public DateTimeOffset? LastTradeTime { get; init; }
    public DateTimeOffset? LastDepthTime { get; init; }
    public decimal? LastTradePrice { get; init; }
}
