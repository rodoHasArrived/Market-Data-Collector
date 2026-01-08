using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Application.EventBus;

public interface IMarketEventSubscription : IAsyncDisposable, IDisposable
{
    IAsyncEnumerable<MarketEvent> Stream { get; }
}
