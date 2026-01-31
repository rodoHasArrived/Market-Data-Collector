namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Represents an alert for stale data detection.
/// Used for webhook notifications and daily summaries.
/// </summary>
public sealed record StaleDataAlert(
    string Symbol,
    DateTimeOffset LastEventTime,
    TimeSpan TimeSinceLastEvent,
    int ThresholdSeconds)
{
    /// <summary>
    /// Creates a stale data alert from a SymbolSlaStatus.
    /// </summary>
    public static StaleDataAlert FromSlaStatus(DataQuality.SymbolSlaStatus status)
    {
        return new StaleDataAlert(
            status.Symbol,
            status.LastEventTime,
            status.TimeSinceLastEvent,
            status.ThresholdSeconds);
    }
}
