using System.Net.WebSockets;
using System.Text;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Resilience;
using Polly;
using Serilog;

namespace MarketDataCollector.Infrastructure.Shared;

/// <summary>
/// Base class for WebSocket-based market data providers that eliminates common
/// boilerplate code across streaming provider implementations.
/// </summary>
/// <remarks>
/// Features:
/// - WebSocket connection lifecycle management (connect, disconnect, reconnection)
/// - Heartbeat monitoring for stale connection detection
/// - Resilience pipeline with retry and circuit breaker
/// - Centralized subscription management via SubscriptionManager
/// - Message receive loop with buffering
/// - Graceful shutdown handling
///
/// Derived classes should implement:
/// - ConnectionUri property for the WebSocket endpoint
/// - AuthenticateAsync for provider-specific authentication
/// - HandleMessageAsync for message processing
/// - BuildSubscriptionMessage for subscription updates
/// </remarks>
[ImplementsAdr("ADR-001", "Base WebSocket streaming data provider implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public abstract class WebSocketProviderBase : IMarketDataClient
{
    protected readonly ILogger Log;
    protected readonly SubscriptionManager Subscriptions;

    private ClientWebSocket? _ws;
    private Task? _recvLoop;
    private CancellationTokenSource? _cts;
    private WebSocketHeartbeat? _heartbeat;

    // Resilience pipeline for connection retry with exponential backoff
    private readonly ResiliencePipeline _connectionPipeline;

    // Reconnection state
    private volatile bool _isReconnecting;
    private readonly SemaphoreSlim _reconnectGate = new(1, 1);

    #region Abstract Properties (Must be implemented by derived classes)

    /// <summary>
    /// WebSocket connection URI for this provider.
    /// </summary>
    protected abstract Uri ConnectionUri { get; }

    /// <summary>
    /// Provider name for logging.
    /// </summary>
    protected abstract string ProviderName { get; }

    /// <summary>
    /// Starting ID for subscription allocation (should be unique per provider).
    /// </summary>
    protected virtual int SubscriptionStartingId => 100_000;

    #endregion

    #region Virtual Properties (Can be overridden)

    /// <summary>
    /// Maximum retries for connection attempts.
    /// </summary>
    protected virtual int MaxConnectionRetries => 5;

    /// <summary>
    /// Base delay for retry backoff.
    /// </summary>
    protected virtual TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(2);

    /// <summary>
    /// Circuit breaker failure threshold.
    /// </summary>
    protected virtual int CircuitBreakerFailureThreshold => 5;

    /// <summary>
    /// Circuit breaker open duration.
    /// </summary>
    protected virtual TimeSpan CircuitBreakerDuration => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Operation timeout for WebSocket operations.
    /// </summary>
    protected virtual TimeSpan OperationTimeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Heartbeat interval for stale connection detection.
    /// </summary>
    protected virtual TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Heartbeat timeout before considering connection stale.
    /// </summary>
    protected virtual TimeSpan HeartbeatTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Receive buffer size in bytes.
    /// </summary>
    protected virtual int ReceiveBufferSize => 64 * 1024;

    /// <summary>
    /// String builder capacity for message assembly.
    /// </summary>
    protected virtual int MessageBufferCapacity => 128 * 1024;

    /// <summary>
    /// Whether to enable heartbeat monitoring.
    /// </summary>
    protected virtual bool EnableHeartbeat => true;

    #endregion

    public virtual bool IsEnabled => true;

    /// <summary>
    /// Gets the current WebSocket state.
    /// </summary>
    protected WebSocketState? WebSocketState => _ws?.State;

    /// <summary>
    /// Gets whether the WebSocket is currently connected.
    /// </summary>
    protected bool IsConnected => _ws?.State == System.Net.WebSockets.WebSocketState.Open;

    /// <summary>
    /// Creates a new WebSocket provider base instance.
    /// </summary>
    /// <param name="log">Optional logger.</param>
    protected WebSocketProviderBase(ILogger? log = null)
    {
        Log = log ?? LoggingSetup.ForContext(GetType());
        Subscriptions = new SubscriptionManager(SubscriptionStartingId);

        // Initialize resilience pipeline with exponential backoff
        _connectionPipeline = WebSocketResiliencePolicy.CreateComprehensivePipeline(
            maxRetries: MaxConnectionRetries,
            retryBaseDelay: RetryBaseDelay,
            circuitBreakerFailureThreshold: CircuitBreakerFailureThreshold,
            circuitBreakerDuration: CircuitBreakerDuration,
            operationTimeout: OperationTimeout);
    }

    #region IMarketDataClient Implementation

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_ws != null) return;

        var uri = ConnectionUri;
        Log.Information("Connecting to {Provider} WebSocket at {Uri} with retry policy", ProviderName, uri);

        await _connectionPipeline.ExecuteAsync(async token =>
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ws = new ClientWebSocket();

            try
            {
                ConfigureWebSocket(_ws);
                await _ws.ConnectAsync(uri, token).ConfigureAwait(false);
                Log.Information("Successfully connected to {Provider} WebSocket", ProviderName);

                await AuthenticateAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Connection attempt to {Provider} WebSocket failed at {Uri}. Will retry per policy.",
                    ProviderName, uri);
                await CleanupWebSocketAsync();
                throw;
            }
        }, ct).ConfigureAwait(false);

        // Start heartbeat monitoring for stale connection detection
        if (_ws != null && EnableHeartbeat)
        {
            _heartbeat = new WebSocketHeartbeat(_ws, HeartbeatInterval, HeartbeatTimeout);
            _heartbeat.ConnectionLost += OnConnectionLostAsync;
        }

        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts!.Token), _cts!.Token);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        Log.Information("Disconnecting from {Provider} WebSocket", ProviderName);

        var ws = _ws;
        var cts = _cts;
        var heartbeat = _heartbeat;

        _ws = null;
        _cts = null;
        _heartbeat = null;

        // Dispose heartbeat first to prevent reconnection attempts
        if (heartbeat != null)
        {
            heartbeat.ConnectionLost -= OnConnectionLostAsync;
            await heartbeat.DisposeAsync();
        }

        if (cts != null)
        {
            try { cts.Cancel(); }
            catch (Exception ex) { Log.Debug(ex, "CancellationTokenSource.Cancel failed during disconnect"); }
            try { cts.Dispose(); }
            catch (Exception ex) { Log.Debug(ex, "CancellationTokenSource.Dispose failed during disconnect"); }
        }

        if (ws != null)
        {
            try
            {
                if (ws.State is System.Net.WebSockets.WebSocketState.Open or System.Net.WebSockets.WebSocketState.CloseReceived)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", ct).ConfigureAwait(false);
            }
            catch (Exception ex) { Log.Warning(ex, "Error during WebSocket close, connection may have been lost"); }
            try { ws.Dispose(); }
            catch (Exception ex) { Log.Debug(ex, "WebSocket disposal failed during disconnect"); }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); }
            catch (Exception ex) { Log.Debug(ex, "Receive loop task completion error during disconnect"); }
        }
        _recvLoop = null;

        Log.Information("Disconnected from {Provider} WebSocket", ProviderName);
    }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        var id = Subscriptions.Subscribe(cfg.Symbol, "trades");
        if (id >= 0)
        {
            TrySendSubscriptionUpdateAsync()
                .ObserveException(Log, $"{ProviderName} subscription update after trades subscribe");
        }
        return id;
    }

    public void UnsubscribeTrades(int subscriptionId)
    {
        var removed = Subscriptions.Unsubscribe(subscriptionId);
        if (removed != null && removed.Kind == "trades")
        {
            TrySendSubscriptionUpdateAsync()
                .ObserveException(Log, $"{ProviderName} subscription update after trades unsubscribe");
        }
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        var id = Subscriptions.Subscribe(cfg.Symbol, "depth");
        if (id >= 0)
        {
            TrySendSubscriptionUpdateAsync()
                .ObserveException(Log, $"{ProviderName} subscription update after depth subscribe");
        }
        return id;
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
    {
        var removed = Subscriptions.Unsubscribe(subscriptionId);
        if (removed != null && removed.Kind == "depth")
        {
            TrySendSubscriptionUpdateAsync()
                .ObserveException(Log, $"{ProviderName} subscription update after depth unsubscribe");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        Subscriptions.Dispose();
        _reconnectGate.Dispose();
    }

    #endregion

    #region Abstract Methods (Must be implemented by derived classes)

    /// <summary>
    /// Perform provider-specific authentication after connection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    protected abstract Task AuthenticateAsync(CancellationToken ct);

    /// <summary>
    /// Process a received message.
    /// </summary>
    /// <param name="message">Raw message string.</param>
    /// <param name="ct">Cancellation token.</param>
    protected abstract Task HandleMessageAsync(string message, CancellationToken ct);

    /// <summary>
    /// Build the subscription update message to send to the provider.
    /// </summary>
    /// <param name="tradeSymbols">Symbols subscribed for trades.</param>
    /// <param name="depthSymbols">Symbols subscribed for depth/quotes.</param>
    /// <returns>Serialized subscription message or null if no update needed.</returns>
    protected abstract string? BuildSubscriptionMessage(string[] tradeSymbols, string[] depthSymbols);

    #endregion

    #region Virtual Methods (Can be overridden)

    /// <summary>
    /// Configure the WebSocket before connection. Override to set headers, subprotocols, etc.
    /// </summary>
    /// <param name="ws">WebSocket instance to configure.</param>
    protected virtual void ConfigureWebSocket(ClientWebSocket ws)
    {
        // Default: no additional configuration
    }

    /// <summary>
    /// Called when the connection is lost. Override for custom recovery logic.
    /// </summary>
    protected virtual async Task OnConnectionLostAsync()
    {
        if (_isReconnecting) return;

        if (!await _reconnectGate.WaitAsync(0))
        {
            Log.Debug("Reconnection already in progress, skipping duplicate attempt");
            return;
        }

        try
        {
            _isReconnecting = true;
            Log.Warning("{Provider} WebSocket connection lost, initiating automatic reconnection", ProviderName);

            await CleanupConnectionAsync();
            await ConnectAsync(CancellationToken.None);

            if (_ws?.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await TrySendSubscriptionUpdateAsync();
                Log.Information("Successfully reconnected and resubscribed to {Provider} WebSocket", ProviderName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reconnect to {Provider} WebSocket after connection loss. " +
                "Manual intervention may be required.", ProviderName);
        }
        finally
        {
            _isReconnecting = false;
            _reconnectGate.Release();
        }
    }

    #endregion

    #region Protected Helper Methods

    /// <summary>
    /// Send a text message over the WebSocket.
    /// </summary>
    /// <param name="text">Message text to send.</param>
    /// <param name="ct">Cancellation token.</param>
    protected async Task SendTextAsync(string text, CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("Not connected.");
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send subscription update to the provider.
    /// </summary>
    protected async Task TrySendSubscriptionUpdateAsync()
    {
        try
        {
            var ws = _ws;
            if (ws == null || ws.State != System.Net.WebSockets.WebSocketState.Open) return;

            var tradeSymbols = Subscriptions.GetSymbolsByKind("trades");
            var depthSymbols = Subscriptions.GetSymbolsByKind("depth");

            var message = BuildSubscriptionMessage(tradeSymbols, depthSymbols);
            if (!string.IsNullOrEmpty(message))
            {
                await SendTextAsync(message, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send subscription update to {Provider} WebSocket. " +
                "This may indicate a connection issue.", ProviderName);
        }
    }

    /// <summary>
    /// Clean up the current connection without triggering reconnection.
    /// </summary>
    protected async Task CleanupConnectionAsync()
    {
        var ws = _ws;
        var cts = _cts;
        var heartbeat = _heartbeat;

        _ws = null;
        _cts = null;
        _heartbeat = null;

        if (heartbeat != null)
        {
            heartbeat.ConnectionLost -= OnConnectionLostAsync;
            await heartbeat.DisposeAsync();
        }

        if (cts != null)
        {
            try { cts.Cancel(); }
            catch (Exception ex) { Log.Debug(ex, "CancellationTokenSource.Cancel failed during cleanup"); }
            try { cts.Dispose(); }
            catch (Exception ex) { Log.Debug(ex, "CancellationTokenSource.Dispose failed during cleanup"); }
        }

        if (ws != null)
        {
            try { ws.Dispose(); }
            catch (Exception ex) { Log.Debug(ex, "WebSocket disposal failed during cleanup"); }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop.ConfigureAwait(false); }
            catch (Exception ex) { Log.Debug(ex, "Receive loop task completion error during cleanup"); }
            _recvLoop = null;
        }
    }

    #endregion

    #region Private Methods

    private async Task CleanupWebSocketAsync()
    {
        try { _ws?.Dispose(); }
        catch (Exception ex) { Log.Debug(ex, "WebSocket disposal failed during connection cleanup"); }
        _ws = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_ws == null) return;

        var ws = _ws;
        var buf = new byte[ReceiveBufferSize];
        var sb = new StringBuilder(MessageBufferCapacity);

        while (!ct.IsCancellationRequested && ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            sb.Clear();

            WebSocketReceiveResult? res;
            try
            {
                do
                {
                    res = await ws.ReceiveAsync(buf, ct).ConfigureAwait(false);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
                }
                while (!res.EndOfMessage);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (WebSocketException ex)
            {
                Log.Warning(ex, "{Provider} WebSocket receive error", ProviderName);
                return;
            }

            var message = sb.ToString();
            if (string.IsNullOrWhiteSpace(message)) continue;

            try
            {
                await HandleMessageAsync(message, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing {Provider} WebSocket message", ProviderName);
            }
        }
    }

    #endregion
}
