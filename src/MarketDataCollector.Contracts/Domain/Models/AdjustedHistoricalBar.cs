using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Contracts.Domain.Models;

/// <summary>
/// Extended historical bar with adjustment factors and corporate action data.
/// </summary>
public sealed record AdjustedHistoricalBar(
    string Symbol,
    DateOnly SessionDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    string Source = "unknown",
    long SequenceNumber = 0,
    decimal? AdjustedOpen = null,
    decimal? AdjustedHigh = null,
    decimal? AdjustedLow = null,
    decimal? AdjustedClose = null,
    long? AdjustedVolume = null,
    decimal? SplitFactor = null,
    decimal? DividendAmount = null
) : MarketEventPayload
{
    /// <summary>
    /// Convert to standard HistoricalBar (uses adjusted values if available).
    /// </summary>
    public HistoricalBar ToHistoricalBar(bool preferAdjusted = true)
    {
        if (preferAdjusted && AdjustedClose.HasValue)
        {
            var (open, high, low, close) = NormalizeOHLC(
                AdjustedOpen ?? Open,
                AdjustedHigh ?? High,
                AdjustedLow ?? Low,
                AdjustedClose ?? Close
            );

            return new HistoricalBar(
                Symbol,
                SessionDate,
                open,
                high,
                low,
                close,
                AdjustedVolume ?? Volume,
                Source,
                SequenceNumber
            );
        }

        return new HistoricalBar(Symbol, SessionDate, Open, High, Low, Close, Volume, Source, SequenceNumber);
    }

    /// <summary>
    /// Normalizes OHLC values to satisfy HistoricalBar constraints.
    /// Yahoo Finance adjusted prices can violate constraints due to rounding during adjustment factor application.
    /// This method expands High/Low to accommodate Open/Close when needed.
    /// </summary>
    private static (decimal Open, decimal High, decimal Low, decimal Close) NormalizeOHLC(
        decimal open, decimal high, decimal low, decimal close)
    {
        // Ensure High is at least as high as Open and Close
        var normalizedHigh = Math.Max(high, Math.Max(open, close));

        // Ensure Low is at most as low as Open and Close
        var normalizedLow = Math.Min(low, Math.Min(open, close));

        return (open, normalizedHigh, normalizedLow, close);
    }
}
