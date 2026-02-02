using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Tests.TestHelpers;

/// <summary>
/// Fake implementation of IMarketEventPublisher for testing.
/// Captures all published events in a list.
/// </summary>
public sealed class FakeMarketEventPublisher : IMarketEventPublisher
{
    private readonly List<MarketEvent> _events;
    private readonly Func<MarketEvent, bool>? _publishFunc;

    public FakeMarketEventPublisher(List<MarketEvent> events, Func<MarketEvent, bool>? publishFunc = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _publishFunc = publishFunc;
    }

    public bool TryPublish(in MarketEvent evt)
    {
        if (_publishFunc != null)
        {
            return _publishFunc(evt);
        }
        
        _events.Add(evt);
        return true;
    }
}
