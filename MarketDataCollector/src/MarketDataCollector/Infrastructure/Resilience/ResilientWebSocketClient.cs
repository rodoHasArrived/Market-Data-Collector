using System.Net.WebSockets;
using System.Text;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Performance;
using Polly;
using Polly.CircuitBreaker;
using Serilog;

namespace MarketDataCollector.Infrastructure.Resilience;

/// <summary>
/// Configuration options for the resilient WebSocket client.
/// </summary>
public sealed record ResilientWebSocketOptions
{
    /// <summary>Maximum number of connection retry attempts (-1 for unlimited).</summary>
    public int MaxConnectionRetries { get; init; } = 5;

    /// <summary>Initial delay for exponential backoff.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum delay between retries.</summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Timeout for individual connection attempts.</summary>
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Interval between heartbeat checks.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for heartbeat responses.</summary>
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Number of consecutive failures before triggering reconnection.</summary>
    public int ConsecutiveFailuresBeforeReconnect { get; init; } = 3;

    /// <summary>Enable automatic reconnection on connection loss.</summary>
    public bool EnableAutoReconnect { get; init; } = true;

    /// <summary>Enable heartbeat monitoring.</summary>
    public bool EnableHeartbeat { get; init; } = true;

    /// <summary>Failure ratio threshold for circuit breaker (0.0-1.0).</summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    /// <summary>Minimum throughput before circuit breaker considers opening.</summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 5;

    /// <summary>Duration the circuit breaker stays open.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Jitter factor for retry delays (0.0-0.5).</summary>
    public double JitterFactor { get; init; } = 0.1;

    /// <summary>Default configuration for production use.</summary>
    public static ResilientWebSocketOptions Default => new();

    /// <summary>Aggressive reconnection for high-availability scenarios.</summary>
    public static ResilientWebSocketOptions HighAvailability => new()
    {
        MaxConnectionRetries = -1,
        InitialRetryDelay = TimeSpan.FromMilliseconds(500),
        MaxRetryDelay = TimeSpan.FromSeconds(30),
        HeartbeatInterval = TimeSpan.FromSeconds(15),
        ConsecutiveFailuresBeforeReconnect = 2
    };
}

/// <summary>
/// Connection state for the resilient WebSocket client.
/// </summary>
public enum ResilientConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    CircuitOpen,
    Faulted
}

/// <summary>
/// Event arguments for connection state changes.
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public ResilientConnectionState OldState { get; }
    public ResilientConnectionState NewState { get; }
    public Exception? Exception { get; }
    public int ReconnectAttempt { get; }

    public ConnectionStateChangedEventArgs(
        ResilientConnectionState oldState,
        ResilientConnectionState newState,
        Exception? exception = null,
        int reconnectAttempt = 0)
    {
        OldState = oldState;
        NewState = newState;
        Exception = exception;
        ReconnectAttempt = reconnectAttempt;
    }
}

/// <summary>
/// A resilient WebSocket client with automatic reconnection, heartbeat monitoring,
/// and circuit breaker protection.
///
/// Features:
/// - Connection retry with exponential backoff and jitter
/// - Automatic reconnection on connection loss
/// - Heartbeat/keep-alive mechanism to detect stale connections
/// - Circuit breaker to prevent cascading failures
/// - Thread-safe state management
/// </summary>
public sealed class ResilientWebSocketClient : IAsyncDisposable
{
    private readonly ILogger _log;
    private readonly ResilientWebSocketOptions _options;
    private readonly Uri _uri;
    private readonly Func<ClientWebSocket, CancellationToken, Task>? _onConnected;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, Task>? _onMessage;
    private readonly ExponentialBackoffRetry _reconnectBackoff;
    private readonly object _stateLock = new();

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private ResiliencePipeline? _circuitBreaker;

    private ResilientConnectionState _state = ResilientConnectionState.Disconnected;
    private int _reconnectAttempts;
    private DateTimeOffset _lastMessageReceived = DateTimeOffset.MinValue;
    private DateTimeOffset _lastPingSent = DateTimeOffset.MinValue;
    private bool _disposed;

    /// <summary>Current connection state.</summary>
    public ResilientConnectionState State
    {
        get { lock (_stateLock) return _state; }
    }

    /// <summary>Whether the client is currently connected and healthy.</summary>
    public bool IsConnected => State == ResilientConnectionState.Connected && _ws?.State == WebSocketState.Open;

    /// <summary>Number of reconnection attempts since last successful connection.</summary>
    public int ReconnectAttempts => Volatile.Read(ref _reconnectAttempts);

    /// <summary>Time since last message was received.</summary>
    public TimeSpan TimeSinceLastMessage => DateTimeOffset.UtcNow - _lastMessageReceived;

    /// <summary>Event fired when connection state changes.</summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>Event fired when connection is lost.</summary>
    public event EventHandler<Exception?>? ConnectionLost;

    /// <summary>Event fired when connection is restored after reconnection.</summary>
    public event EventHandler? ConnectionRestored;

    /// <summary>Event fired when a message is received.</summary>
    public event EventHandler<ReadOnlyMemory<byte>>? MessageReceived;

    /// <summary>
    /// Creates a new resilient WebSocket client.
    /// </summary>
    /// <param name="uri">WebSocket server URI</param>
    /// <param name="options">Resilience configuration options</param>
    /// <param name="onConnected">Optional callback executed after successful connection (e.g., for authentication)</param>
    /// <param name="onMessage">Optional callback for processing received messages</param>
    public ResilientWebSocketClient(
        Uri uri,
        ResilientWebSocketOptions? options = null,
        Func<ClientWebSocket, CancellationToken, Task>? onConnected = null,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task>? onMessage = null)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _options = options ?? ResilientWebSocketOptions.Default;
        _onConnected = onConnected;
        _onMessage = onMessage;
        _log = LoggingSetup.ForContext<ResilientWebSocketClient>();

        _reconnectBackoff = new ExponentialBackoffRetry(
            initialDelay: _options.InitialRetryDelay,
            maxDelay: _options.MaxRetryDelay,
            maxRetries: _options.MaxConnectionRetries,
            multiplier: 2.0,
            jitterFactor: _options.JitterFactor);

        InitializeCircuitBreaker();
    }

    private void InitializeCircuitBreaker()
    {
        _circuitBreaker = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _options.CircuitBreakerFailureRatio,
                MinimumThroughput = _options.CircuitBreakerMinimumThroughput,
                BreakDuration = _options.CircuitBreakerBreakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<WebSocketException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<OperationCanceledException>(),
                OnOpened = args =>
                {
                    _log.Error(
                        "Circuit breaker OPENED after repeated failures. " +
                        "Connection attempts will be blocked for {BreakDuration}s",
                        _options.CircuitBreakerBreakDuration.TotalSeconds);
                    SetState(ResilientConnectionState.CircuitOpen);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _log.Information("Circuit breaker CLOSED. Normal operation resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _log.Information("Circuit breaker HALF-OPEN. Testing connection...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Connects to the WebSocket server with automatic retry on failure.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ResilientWebSocketClient));

        if (State == ResilientConnectionState.Connected)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        SetState(ResilientConnectionState.Connecting);

        try
        {
            await ConnectWithRetryAsync(_cts.Token).ConfigureAwait(false);
            _reconnectBackoff.Reset();
            Interlocked.Exchange(ref _reconnectAttempts, 0);
            SetState(ResilientConnectionState.Connected);

            // Start background tasks
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            if (_options.EnableHeartbeat)
            {
                _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token), _cts.Token);
            }
        }
        catch (Exception ex)
        {
            SetState(ResilientConnectionState.Faulted, ex);
            throw;
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        Exception? lastException = null;

        while (_reconnectBackoff.CanRetry && !ct.IsCancellationRequested)
        {
            try
            {
                await _circuitBreaker!.ExecuteAsync(async token =>
                {
                    await AttemptConnectionAsync(token).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);

                return; // Success
            }
            catch (BrokenCircuitException ex)
            {
                _log.Warning("Connection blocked by circuit breaker: {Message}", ex.Message);
                SetState(ResilientConnectionState.CircuitOpen, ex);
                throw;
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or TimeoutException)
            {
                lastException = ex;
                var attempt = Interlocked.Increment(ref _reconnectAttempts);
                var delay = _reconnectBackoff.GetNextDelay();

                _log.Warning(
                    ex,
                    "Connection attempt {Attempt} failed. Retrying in {Delay}ms...",
                    attempt,
                    delay.TotalMilliseconds);

                if (!_reconnectBackoff.CanRetry)
                {
                    _log.Error("Max connection retries ({MaxRetries}) exceeded", _options.MaxConnectionRetries);
                    break;
                }

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        throw lastException ?? new InvalidOperationException("Connection failed");
    }

    private async Task AttemptConnectionAsync(CancellationToken ct)
    {
        _ws?.Dispose();
        _ws = new ClientWebSocket();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.ConnectionTimeout);

        _log.Debug("Attempting WebSocket connection to {Uri}", _uri);

        await _ws.ConnectAsync(_uri, timeoutCts.Token).ConfigureAwait(false);

        _log.Information("Successfully connected to WebSocket at {Uri}", _uri);

        // Execute post-connection callback (e.g., authentication)
        if (_onConnected != null)
        {
            await _onConnected(_ws, ct).ConfigureAwait(false);
        }

        _lastMessageReceived = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Disconnects from the WebSocket server.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_disposed) return;

        _log.Information("Disconnecting from WebSocket");

        var cts = _cts;
        _cts = null;

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
        }

        // Wait for background tasks to complete
        var tasks = new List<Task>();
        if (_receiveTask != null) tasks.Add(_receiveTask);
        if (_heartbeatTask != null) tasks.Add(_heartbeatTask);

        if (tasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch { }
        }

        // Close WebSocket gracefully
        var ws = _ws;
        _ws = null;

        if (ws != null)
        {
            try
            {
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", ct)
                        .ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                ws.Dispose();
            }
        }

        cts?.Dispose();
        _receiveTask = null;
        _heartbeatTask = null;

        SetState(ResilientConnectionState.Disconnected);
        _log.Information("Disconnected from WebSocket");
    }

    /// <summary>
    /// Sends a text message over the WebSocket.
    /// </summary>
    public async Task SendTextAsync(string message, CancellationToken ct = default)
    {
        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected");

        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends binary data over the WebSocket.
    /// </summary>
    public async Task SendBinaryAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected");

        await ws.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var messageBuffer = new List<byte>(128 * 1024);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ws = _ws;
                if (ws == null || ws.State != WebSocketState.Open)
                {
                    if (_options.EnableAutoReconnect && !ct.IsCancellationRequested)
                    {
                        await TriggerReconnectAsync(ct).ConfigureAwait(false);
                        continue;
                    }
                    break;
                }

                messageBuffer.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _log.Information("WebSocket close message received");
                        if (_options.EnableAutoReconnect && !ct.IsCancellationRequested)
                        {
                            await TriggerReconnectAsync(ct).ConfigureAwait(false);
                        }
                        return;
                    }

                    messageBuffer.AddRange(buffer.Take(result.Count));
                }
                while (!result.EndOfMessage);

                _lastMessageReceived = DateTimeOffset.UtcNow;

                var message = messageBuffer.ToArray();
                MessageReceived?.Invoke(this, message);

                if (_onMessage != null)
                {
                    await _onMessage(message, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _log.Warning(ex, "WebSocket error in receive loop");
                if (_options.EnableAutoReconnect && !ct.IsCancellationRequested)
                {
                    await TriggerReconnectAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected error in receive loop");
                if (_options.EnableAutoReconnect && !ct.IsCancellationRequested)
                {
                    await TriggerReconnectAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    break;
                }
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var consecutiveFailures = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.HeartbeatInterval, ct).ConfigureAwait(false);

                var ws = _ws;
                if (ws == null || ws.State != WebSocketState.Open)
                {
                    consecutiveFailures++;
                    continue;
                }

                // Check if we've received any messages recently
                var timeSinceLastMessage = DateTimeOffset.UtcNow - _lastMessageReceived;
                if (timeSinceLastMessage > _options.HeartbeatInterval + _options.HeartbeatTimeout)
                {
                    consecutiveFailures++;
                    _log.Warning(
                        "No messages received for {Duration}s (consecutive failures: {Failures})",
                        timeSinceLastMessage.TotalSeconds,
                        consecutiveFailures);

                    if (consecutiveFailures >= _options.ConsecutiveFailuresBeforeReconnect)
                    {
                        _log.Warning("Connection appears stale. Triggering reconnection...");
                        await TriggerReconnectAsync(ct).ConfigureAwait(false);
                        consecutiveFailures = 0;
                    }
                }
                else
                {
                    consecutiveFailures = 0;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error in heartbeat loop");
            }
        }
    }

    private async Task TriggerReconnectAsync(CancellationToken ct)
    {
        if (_disposed || ct.IsCancellationRequested) return;

        var oldState = State;
        SetState(ResilientConnectionState.Reconnecting);
        ConnectionLost?.Invoke(this, null);

        _log.Information("Attempting to reconnect...");

        // Close existing connection
        var ws = _ws;
        _ws = null;
        if (ws != null)
        {
            try { ws.Dispose(); } catch { }
        }

        try
        {
            await ConnectWithRetryAsync(ct).ConfigureAwait(false);
            SetState(ResilientConnectionState.Connected);
            ConnectionRestored?.Invoke(this, EventArgs.Empty);
            _log.Information("Successfully reconnected to WebSocket");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to reconnect after {Attempts} attempts", ReconnectAttempts);
            SetState(ResilientConnectionState.Faulted, ex);
        }
    }

    private void SetState(ResilientConnectionState newState, Exception? exception = null)
    {
        ResilientConnectionState oldState;
        lock (_stateLock)
        {
            oldState = _state;
            _state = newState;
        }

        if (oldState != newState)
        {
            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                oldState, newState, exception, ReconnectAttempts));
        }
    }

    /// <summary>
    /// Gets current connection statistics.
    /// </summary>
    public ResilientConnectionStats GetStats()
    {
        return new ResilientConnectionStats(
            State: State,
            ReconnectAttempts: ReconnectAttempts,
            TimeSinceLastMessage: TimeSinceLastMessage,
            LastMessageReceived: _lastMessageReceived,
            IsCircuitBreakerOpen: State == ResilientConnectionState.CircuitOpen
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await DisconnectAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Connection statistics for monitoring.
/// </summary>
public readonly record struct ResilientConnectionStats(
    ResilientConnectionState State,
    int ReconnectAttempts,
    TimeSpan TimeSinceLastMessage,
    DateTimeOffset LastMessageReceived,
    bool IsCircuitBreakerOpen
);
