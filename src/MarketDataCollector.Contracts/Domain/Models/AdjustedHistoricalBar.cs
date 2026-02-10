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
    /// Normalizes OHLC values to ensure they satisfy bar constraints (Open/Close within High/Low).
    /// </summary>
    public HistoricalBar ToHistoricalBar(bool preferAdjusted = true)
    {
        if (preferAdjusted && AdjustedClose.HasValue)
        {
            // Use adjusted values
            var open = AdjustedOpen ?? Open;
            var high = AdjustedHigh ?? High;
            var low = AdjustedLow ?? Low;
            var close = AdjustedClose ?? Close;
            var volume = AdjustedVolume ?? Volume;

            // Normalize OHLC values to satisfy constraints
            // This handles cases where adjustment factors cause Open/Close to fall outside High/Low
            (open, high, low, close) = NormalizeOHLC(open, high, low, close);

            return new HistoricalBar(
                Symbol,
                SessionDate,
                open,
                high,
                low,
                close,
                volume,
                Source,
                SequenceNumber
            );
        }

        return new HistoricalBar(Symbol, SessionDate, Open, High, Low, Close, Volume, Source, SequenceNumber);
    }

    /// <summary>
    /// Normalizes OHLC values to ensure they satisfy bar constraints.
    /// Expands High/Low range if necessary to accommodate Open/Close prices.
    /// </summary>
    private static (decimal open, decimal high, decimal low, decimal close) NormalizeOHLC(
        decimal open, decimal high, decimal low, decimal close)
    {
        // Ensure High is at least as large as Open and Close
        if (open > high)
            high = open;
        if (close > high)
            high = close;

        // Ensure Low is at most as small as Open and Close
        if (open < low)
            low = open;
        if (close < low)
            low = close;

        return (open, high, low, close);
    }
}
