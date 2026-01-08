namespace MarketDataCollector.Messaging.Contracts;

/// <summary>
/// Published when the connection status to a data provider changes.
/// </summary>
public interface IConnectionStatusChanged : IMarketEventMessage
{
    /// <summary>
    /// Connection status: "Connected", "Disconnected", "Reconnecting", "Error".
    /// </summary>
    string Status { get; }

    /// <summary>
    /// Provider name (e.g., "IB", "ALPACA", "POLYGON").
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Optional error message if status is "Error".
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Reconnection attempt number (0 if not reconnecting).
    /// </summary>
    int ReconnectAttempt { get; }
}
