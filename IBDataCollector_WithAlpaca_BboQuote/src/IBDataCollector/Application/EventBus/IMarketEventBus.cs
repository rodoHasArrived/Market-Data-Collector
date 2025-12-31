using IBDataCollector.Application.Filters;
using IBDataCollector.Domain.Events;

namespace IBDataCollector.Application.EventBus;

/// <summary>
/// Application-level bus. Publish via IMarketEventPublisher; subscribe via this bus.
/// </summary>
public interface IMarketEventBus : IMarketEventPublisher
{
    IMarketEventSubscription Subscribe(MarketEventFilter filter, CancellationToken ct = default);
}
