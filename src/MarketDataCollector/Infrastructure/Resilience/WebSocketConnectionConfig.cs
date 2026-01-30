using System.Threading;

namespace MarketDataCollector.Infrastructure.Resilience;

/// <summary>
/// Centralized configuration for WebSocket connections.
/// Provides sensible defaults used by all streaming providers (Alpaca, Polygon, etc.)
/// to eliminate duplicate configuration across providers.
/// </summary>
/// <remarks>
/// Default values are based on production best practices:
/// - 5 retries with 2s base delay provides ~62s total retry time
/// - 30s circuit breaker allows service recovery
/// - 30s heartbeat interval with 10s timeout for stale connection detection
/// </remarks>
public sealed record WebSocketConnectionConfig
{
    /// <summary>
    /// Maximum number of connection retry attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 5;

    /// <summary>
    /// Base delay for exponential backoff between retries.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum delay between retries (caps exponential backoff).
    /// </summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of failures before circuit breaker opens.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// Duration circuit breaker stays open before allowing retry.
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for individual connection operations.
    /// </summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Interval between heartbeat pings.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout waiting for heartbeat response before considering connection stale.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of reconnection attempts after connection loss.
    /// </summary>
    public int MaxReconnectAttempts { get; init; } = 10;

    /// <summary>
    /// Default configuration used by all streaming providers.
    /// These values match the previously duplicated settings in Alpaca and Polygon clients.
    /// </summary>
    public static WebSocketConnectionConfig Default { get; } = new();

    /// <summary>
    /// Configuration optimized for high-frequency data (shorter timeouts).
    /// </summary>
    public static WebSocketConnectionConfig HighFrequency { get; } = new()
    {
        RetryBaseDelay = TimeSpan.FromSeconds(1),
        MaxRetryDelay = TimeSpan.FromSeconds(15),
        OperationTimeout = TimeSpan.FromSeconds(15),
        HeartbeatInterval = TimeSpan.FromSeconds(15),
        HeartbeatTimeout = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Configuration optimized for unreliable networks (more retries, longer timeouts).
    /// </summary>
    public static WebSocketConnectionConfig Resilient { get; } = new()
    {
        MaxRetries = 10,
        RetryBaseDelay = TimeSpan.FromSeconds(3),
        MaxRetryDelay = TimeSpan.FromSeconds(60),
        CircuitBreakerDuration = TimeSpan.FromSeconds(60),
        OperationTimeout = TimeSpan.FromSeconds(60),
        MaxReconnectAttempts = 20
    };
}
