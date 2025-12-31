using IBDataCollector.Domain.Events;

namespace IBDataCollector.Application.EventBus;

public interface IMarketEventSubscription : IAsyncDisposable, IDisposable
{
    IAsyncEnumerable<MarketEvent> Stream { get; }
}
