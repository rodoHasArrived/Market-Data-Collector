using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using Serilog;

namespace MarketDataCollector.Messaging.Publishers;

/// <summary>
/// Composite publisher that broadcasts to multiple downstream publishers.
/// Used to publish events to both local storage and MassTransit simultaneously.
/// </summary>
public sealed class CompositePublisher : IMarketEventPublisher
{
    private readonly IReadOnlyList<IMarketEventPublisher> _publishers;
    private readonly ILogger _log;

    public CompositePublisher(params IMarketEventPublisher[] publishers)
        : this((IReadOnlyList<IMarketEventPublisher>)publishers)
    {
    }

    public CompositePublisher(IReadOnlyList<IMarketEventPublisher> publishers)
    {
        _publishers = publishers ?? throw new ArgumentNullException(nameof(publishers));
        _log = LoggingSetup.ForContext<CompositePublisher>();
        _log.Debug("CompositePublisher initialized with {Count} downstream publishers", publishers.Count);
    }

    /// <summary>
    /// Publishes to all downstream publishers. Returns true if at least one succeeds.
    /// </summary>
    public bool TryPublish(in MarketEvent evt)
    {
        var anySuccess = false;
        var failCount = 0;

        foreach (var publisher in _publishers)
        {
            try
            {
                if (publisher.TryPublish(evt))
                    anySuccess = true;
                else
                    failCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                _log.Warning(ex, "Publisher {PublisherType} failed for event {Symbol} {Type}",
                    publisher.GetType().Name, evt.Symbol, evt.Type);
            }
        }

        if (failCount > 0)
        {
            _log.Debug("Event published with {FailCount}/{TotalCount} publisher failures: {Symbol} {Type}",
                failCount, _publishers.Count, evt.Symbol, evt.Type);
        }

        return anySuccess;
    }
}
