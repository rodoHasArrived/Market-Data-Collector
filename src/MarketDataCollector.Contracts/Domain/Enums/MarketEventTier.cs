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
    Derived = 1,

    /// <summary>
    /// Enriched event after canonicalization (symbol resolution, condition code mapping, venue normalization).
    /// </summary>
    Enriched = 2,

    /// <summary>
    /// Fully processed event after all transformations.
    /// </summary>
    Processed = 3
}
