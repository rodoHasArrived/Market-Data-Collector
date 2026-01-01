namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Base contract for all market event messages published via MassTransit.
/// </summary>
public interface IMarketEventMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    Guid MessageId { get; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Trading symbol (e.g., "SPY", "AAPL").
    /// </summary>
    string Symbol { get; }

    /// <summary>
    /// Event type identifier.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Sequence number for ordering.
    /// </summary>
    long Sequence { get; }

    /// <summary>
    /// Data source provider (e.g., "IB", "ALPACA", "POLYGON").
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Schema version for backward compatibility.
    /// </summary>
    int SchemaVersion { get; }
}
