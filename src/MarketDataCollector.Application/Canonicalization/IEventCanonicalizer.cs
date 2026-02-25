namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Resolves symbols, maps condition codes, and normalizes venue identifiers
/// on a <see cref="MarketEvent"/> before it enters the <c>EventPipeline</c>.
/// </summary>
public interface IEventCanonicalizer
{
    /// <summary>
    /// Canonicalizes a raw market event by resolving its symbol to the canonical identity,
    /// mapping condition codes to provider-agnostic values, and normalizing venue to ISO 10383 MIC.
    /// Returns an enriched copy via <c>with</c> expression; the original event is not mutated.
    /// </summary>
    MarketEvent Canonicalize(MarketEvent raw, CancellationToken ct = default);
}
