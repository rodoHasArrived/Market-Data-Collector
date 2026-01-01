namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published periodically to indicate system health.
/// </summary>
public interface IHeartbeatReceived : IMarketEventMessage
{
    /// <summary>
    /// Total events published since start.
    /// </summary>
    long TotalEventsPublished { get; }

    /// <summary>
    /// Total events dropped due to backpressure.
    /// </summary>
    long TotalEventsDropped { get; }

    /// <summary>
    /// Current events per second rate.
    /// </summary>
    double EventsPerSecond { get; }

    /// <summary>
    /// Number of active subscriptions.
    /// </summary>
    int ActiveSubscriptions { get; }
}
