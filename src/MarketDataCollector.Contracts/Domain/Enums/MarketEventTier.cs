namespace MarketDataCollector.Contracts.Domain.Enums;

/// <summary>
/// Processing tier for market events.
/// </summary>
public enum MarketEventTier
{
    /// <summary>
    /// Raw event from data provider.
    /// </summary>
    Raw = 0,

    /// <summary>
    /// Derived event from processing pipeline.
    /// </summary>
    Derived = 1
}
