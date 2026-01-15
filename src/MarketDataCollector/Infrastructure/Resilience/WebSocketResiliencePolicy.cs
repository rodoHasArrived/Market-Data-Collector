using MarketDataCollector.Application.Logging;
using Polly;
using Polly.Retry;
using Serilog;
using System.Net.WebSockets;
using System.Threading;

namespace MarketDataCollector.Infrastructure.Resilience;

/// <summary>
/// WebSocket resilience policies using Polly.
/// Implements best practices from:
/// - Marfusios/websocket-client patterns
/// - Microsoft's resilience guidance for .NET
/// - Production-grade WebSocket patterns from fullstackcity.com
/// </summary>
public static class WebSocketResiliencePolicy
{
    /// <summary>
    /// Creates a resilience pipeline for WebSocket connection attempts.
    /// Uses exponential backoff with jitter to avoid thundering herd.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 5)</param>
    /// <param name="baseDelay">Base delay for exponential backoff (default: 2 seconds)</param>
    /// <param name="maxDelay">Maximum delay between retries (default: 30 seconds)</param>
    public static ResiliencePipeline<WebSocketReceiveResult> CreateReceivePipeline(
        int maxRetries = 5,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        var logger = LoggingSetup.ForContext("WebSocketResilience");
        baseDelay ??= TimeSpan.FromSeconds(2);
        maxDelay ??= TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder<WebSocketReceiveResult>()
            .AddRetry(new RetryStrategyOptions<WebSocketReceiveResult>
            {
                MaxRetryAttempts = maxRetries,
                Delay = baseDelay.Value,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = maxDelay.Value,
                ShouldHandle = new PredicateBuilder<WebSocketReceiveResult>()
                    .Handle<WebSocketException>()
                    .Handle<OperationCanceledException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.Warning(
                        "WebSocket operation failed (attempt {AttemptNumber}/{MaxRetryAttempts}). " +
                        "Retrying after {DelayDuration}ms. Error: {Exception}",
                        args.AttemptNumber,
                        maxRetries + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for WebSocket connection establishment.
    /// </summary>
    public static ResiliencePipeline CreateConnectionPipeline(
        int maxRetries = 5,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        var logger = LoggingSetup.ForContext("WebSocketResilience");
        baseDelay ??= TimeSpan.FromSeconds(2);
        maxDelay ??= TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = baseDelay.Value,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = maxDelay.Value,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.Warning(
                        "WebSocket connection failed (attempt {AttemptNumber}/{MaxRetryAttempts}). " +
                        "Retrying after {DelayDuration}ms. Error: {Exception}",
                        args.AttemptNumber,
                        maxRetries + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a circuit breaker policy for WebSocket operations.
    /// Prevents cascading failures by opening the circuit after consecutive failures.
    /// </summary>
    public static ResiliencePipeline CreateCircuitBreakerPipeline(
        int failureThreshold = 5,
        TimeSpan? breakDuration = null)
    {
        var logger = LoggingSetup.ForContext("WebSocketCircuitBreaker");
        breakDuration ??= TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = failureThreshold,
                BreakDuration = breakDuration.Value,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                OnOpened = args =>
                {
                    logger.Error(
                        "Circuit breaker OPENED after {FailureCount} failures. " +
                        "Circuit will remain open for {BreakDuration}s. Last error: {Exception}",
                        failureThreshold,
                        breakDuration.Value.TotalSeconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.Information("Circuit breaker CLOSED. Normal operation resumed.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.Information("Circuit breaker HALF-OPEN. Testing if service has recovered...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a timeout policy for WebSocket operations.
    /// </summary>
    public static ResiliencePipeline CreateTimeoutPipeline(TimeSpan? timeout = null)
    {
        var logger = LoggingSetup.ForContext("WebSocketTimeout");
        timeout ??= TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder()
            .AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = timeout.Value,
                OnTimeout = args =>
                {
                    logger.Warning(
                        "WebSocket operation timed out after {TimeoutDuration}s",
                        timeout.Value.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a comprehensive resilience pipeline combining retry, circuit breaker, and timeout.
    /// This is the recommended pipeline for production WebSocket clients.
    /// </summary>
    public static ResiliencePipeline CreateComprehensivePipeline(
        int maxRetries = 5,
        TimeSpan? retryBaseDelay = null,
        int circuitBreakerFailureThreshold = 5,
        TimeSpan? circuitBreakerDuration = null,
        TimeSpan? operationTimeout = null)
    {
        retryBaseDelay ??= TimeSpan.FromSeconds(2);
        circuitBreakerDuration ??= TimeSpan.FromSeconds(30);
        operationTimeout ??= TimeSpan.FromSeconds(30);

        var logger = LoggingSetup.ForContext("WebSocketResilience");

        return new ResiliencePipelineBuilder()
            // Outermost: Timeout (applies to entire operation including retries)
            .AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMinutes(5), // Total operation timeout
                OnTimeout = args =>
                {
                    logger.Error("WebSocket operation exceeded total timeout of 5 minutes");
                    return ValueTask.CompletedTask;
                }
            })
            // Middle: Circuit Breaker (prevents cascading failures)
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = circuitBreakerFailureThreshold,
                BreakDuration = circuitBreakerDuration.Value,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            // Innermost: Retry with exponential backoff
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = retryBaseDelay.Value,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            .Build();
    }
}

/// <summary>
/// Heartbeat manager for WebSocket connections.
/// Implements ping/pong pattern to detect stale connections.
/// Based on best practices: send ping every 30-60 seconds, expect pong within 10 seconds.
/// </summary>
public class WebSocketHeartbeat : IAsyncDisposable
{
    private readonly ClientWebSocket _ws;
    private readonly TimeSpan _pingInterval;
    private readonly TimeSpan _pongTimeout;
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _heartbeatTask;
    private DateTimeOffset _lastPongReceived = DateTimeOffset.UtcNow;

    public event Func<Task>? ConnectionLost;

    public WebSocketHeartbeat(
        ClientWebSocket ws,
        TimeSpan? pingInterval = null,
        TimeSpan? pongTimeout = null)
    {
        _ws = ws ?? throw new ArgumentNullException(nameof(ws));
        _pingInterval = pingInterval ?? TimeSpan.FromSeconds(30);
        _pongTimeout = pongTimeout ?? TimeSpan.FromSeconds(10);
        _log = LoggingSetup.ForContext<WebSocketHeartbeat>();

        _heartbeatTask = Task.Run(HeartbeatLoopAsync);
    }

    private async Task HeartbeatLoopAsync()
    {
        _log.Debug("WebSocket heartbeat started (ping every {PingInterval}s, pong timeout: {PongTimeout}s)",
            _pingInterval.TotalSeconds, _pongTimeout.TotalSeconds);

        try
        {
            while (!_cts.Token.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                await Task.Delay(_pingInterval, _cts.Token);

                if (_ws.State != WebSocketState.Open)
                    break;

                // Send ping (empty message or specific ping frame)
                // Note: ClientWebSocket doesn't expose ping/pong frames directly
                // This is a simplified version - production code might use custom ping messages
                var timeSinceLastPong = DateTimeOffset.UtcNow - _lastPongReceived;
                if (timeSinceLastPong > _pongTimeout + _pingInterval)
                {
                    _log.Warning("No pong received for {Duration}s. Connection may be stale.",
                        timeSinceLastPong.TotalSeconds);

                    if (ConnectionLost != null)
                        await ConnectionLost.Invoke();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Heartbeat loop failed");
        }

        _log.Debug("WebSocket heartbeat stopped");
    }

    public void RecordPongReceived()
    {
        _lastPongReceived = DateTimeOffset.UtcNow;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _heartbeatTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning(ex, "Error awaiting heartbeat task completion during disposal");
        }
        _cts.Dispose();
    }
}
