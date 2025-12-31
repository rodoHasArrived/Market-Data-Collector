namespace IBDataCollector.Domain.Events;

/// <summary>
/// Distinguishes raw events emitted by collectors from derived/enriched events.
/// </summary>
public enum MarketEventTier
{
    Raw,
    Derived
}
