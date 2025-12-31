namespace MarketDataCollector.Domain.Events;

/// <summary>
/// Minimal publish contract so collectors can emit MarketEvents without knowing transport.
/// Publish must be non-blocking (hot path).
/// </summary>
public interface IMarketEventPublisher
{
    bool TryPublish(MarketEvent evt);
}
