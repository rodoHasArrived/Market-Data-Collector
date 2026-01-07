using MarketDataCollector.Application.Filters;
using System.Threading;
using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Application.EventBus;

/// <summary>
/// Application-level bus. Publish via IMarketEventPublisher; subscribe via this bus.
/// </summary>
public interface IMarketEventBus : IMarketEventPublisher
{
    IMarketEventSubscription Subscribe(MarketEventFilter filter, CancellationToken ct = default);
}
