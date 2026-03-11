namespace MarketDataCollector.Domain.Events;

/// <summary>
/// Detailed result of a <see cref="IMarketEventPublisher.TryPublish"/> call.
/// Provides richer information than a plain <see langword="bool"/> so that producers
/// can make informed throttling decisions rather than silently dropping events.
/// </summary>
/// <remarks>
/// Use <c>EventPipeline.TryPublishWithResult</c> when you need the detailed result.
/// The original <see cref="IMarketEventPublisher.TryPublish"/> continues to return
/// <see langword="bool"/> for backward compatibility.
/// </remarks>
public enum PublishResult
{
    /// <summary>
    /// The event was accepted and enqueued for processing.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// The event was accepted but the pipeline queue is approaching capacity (≥ 80 %).
    /// Producers should consider slowing down or reducing the rate of publication.
    /// </summary>
    AcceptedUnderPressure = 1,

    /// <summary>
    /// The event was dropped because the pipeline queue is full and backpressure policy
    /// discarded it (DropWrite or DropOldest/DropNewest modes).
    /// The publisher has received a <see langword="false"/> from the underlying channel.
    /// </summary>
    Dropped = 2,
}
