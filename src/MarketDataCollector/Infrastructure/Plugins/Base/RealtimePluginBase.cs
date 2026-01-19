using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MarketDataCollector.Infrastructure.Plugins.Core;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins.Base;

/// <summary>
/// Base class for real-time streaming plugins (WebSocket, etc.).
/// Provides connection management, reconnection logic, and heartbeat handling.
/// </summary>
public abstract class RealtimePluginBase : MarketDataPluginBase
{
    private readonly Channel<MarketDataEvent> _eventChannel;
    private CancellationTokenSource? _connectionCts;
    private Task? _connectionTask;

    /// <summary>
    /// Whether currently connected to the data source.
    /// </summary>
    protected bool IsConnected { get; private set; }

    /// <summary>
    /// Reconnection policy configuration.
    /// </summary>
    protected ReconnectionPolicy ReconnectionPolicy { get; set; } = ReconnectionPolicy.Default;

    protected RealtimePluginBase()
    {
        _eventChannel = Channel.CreateBounded<MarketDataEvent>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });
    }

    #region Abstract Methods

    /// <summary>
    /// Connects to the real-time data source.
    /// </summary>
    protected abstract Task ConnectAsync(CancellationToken ct);

    /// <summary>
    /// Disconnects from the real-time data source.
    /// </summary>
    protected abstract Task DisconnectAsync();

    /// <summary>
    /// Subscribes to symbols and starts receiving data.
    /// Called after connection is established.
    /// </summary>
    protected abstract Task SubscribeAsync(IReadOnlyList<string> symbols, CancellationToken ct);

    /// <summary>
    /// Unsubscribes from symbols.
    /// </summary>
    protected abstract Task UnsubscribeAsync(IReadOnlyList<string> symbols, CancellationToken ct);

    #endregion

    #region IMarketDataPlugin Implementation

    public override async IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!request.IsRealtime)
        {
            throw new ArgumentException(
                $"RealtimePluginBase {Id} only supports real-time requests. " +
                "Use a historical plugin for date-range requests.",
                nameof(request));
        }

        await using var cleanup = new AsyncCleanup(async () =>
        {
            await UnsubscribeAsync(request.Symbols, CancellationToken.None).ConfigureAwait(false);
        });

        // Ensure connected
        await EnsureConnectedAsync(ct).ConfigureAwait(false);

        // Subscribe to symbols
        await SubscribeAsync(request.Symbols, ct).ConfigureAwait(false);

        State = PluginState.Streaming;

        // Stream events from channel
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            // Filter to requested symbols
            if (request.Symbols.Contains(evt.Symbol) ||
                evt is HeartbeatEvent ||
                evt is ErrorEvent)
            {
                // Filter to requested data types
                if (request.DataTypes.Count == 0 ||
                    request.DataTypes.Contains(evt.EventType) ||
                    evt is HeartbeatEvent or ErrorEvent)
                {
                    yield return evt;
                }
            }
        }
    }

    #endregion

    #region Connection Management

    /// <summary>
    /// Ensures the plugin is connected, establishing connection if needed.
    /// </summary>
    protected async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (IsConnected)
            return;

        _connectionCts?.Cancel();
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await ConnectWithRetryAsync(_connectionCts.Token).ConfigureAwait(false);
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        int attempt = 0;
        var delay = ReconnectionPolicy.InitialDelay;

        while (!ct.IsCancellationRequested)
        {
            attempt++;
            State = PluginState.Reconnecting;

            try
            {
                Logger.LogInformation("Connecting to {PluginId} (attempt {Attempt})", Id, attempt);

                await ConnectAsync(ct).ConfigureAwait(false);

                IsConnected = true;
                State = PluginState.Ready;
                RecordSuccess();

                Logger.LogInformation("Connected to {PluginId}", Id);

                // Start heartbeat monitoring
                _ = MonitorConnectionAsync(_connectionCts!.Token);

                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure($"Connection failed: {ex.Message}");
                Logger.LogWarning(ex, "Connection attempt {Attempt} failed for {PluginId}", attempt, Id);

                if (attempt >= ReconnectionPolicy.MaxRetries)
                {
                    State = PluginState.Error;
                    throw new InvalidOperationException(
                        $"Failed to connect to {Id} after {attempt} attempts", ex);
                }

                Logger.LogInformation("Retrying connection to {PluginId} in {Delay}ms", Id, delay.TotalMilliseconds);
                await Task.Delay(delay, ct).ConfigureAwait(false);

                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * ReconnectionPolicy.BackoffMultiplier,
                             ReconnectionPolicy.MaxDelay.TotalMilliseconds));
            }
        }
    }

    private async Task MonitorConnectionAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(ReconnectionPolicy.HeartbeatInterval, ct).ConfigureAwait(false);

                // Emit heartbeat event
                await EnqueueEventAsync(HeartbeatEvent.Create(Id), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    #endregion

    #region Event Handling

    /// <summary>
    /// Enqueues an event to be delivered to subscribers.
    /// Call this from your data-receiving code.
    /// </summary>
    protected ValueTask EnqueueEventAsync(MarketDataEvent evt, CancellationToken ct = default)
    {
        return _eventChannel.Writer.WriteAsync(evt, ct);
    }

    /// <summary>
    /// Tries to enqueue an event without waiting.
    /// Returns false if the channel is full.
    /// </summary>
    protected bool TryEnqueueEvent(MarketDataEvent evt)
    {
        return _eventChannel.Writer.TryWrite(evt);
    }

    #endregion

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _connectionCts?.Cancel();

        if (IsConnected)
        {
            try
            {
                await DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error disconnecting {PluginId}", Id);
            }
            IsConnected = false;
        }

        _eventChannel.Writer.Complete();
        _connectionCts?.Dispose();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}

/// <summary>
/// Reconnection policy for real-time plugins.
/// </summary>
public sealed record ReconnectionPolicy
{
    /// <summary>
    /// Initial delay before first reconnection attempt.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between reconnection attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Maximum number of reconnection attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 10;

    /// <summary>
    /// Interval for heartbeat/keepalive checks.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default reconnection policy.
    /// </summary>
    public static ReconnectionPolicy Default => new();

    /// <summary>
    /// Aggressive reconnection for low-latency requirements.
    /// </summary>
    public static ReconnectionPolicy Aggressive => new()
    {
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 1.5,
        MaxRetries = 20,
        HeartbeatInterval = TimeSpan.FromSeconds(10)
    };
}

/// <summary>
/// Helper for async cleanup in yield return scenarios.
/// </summary>
internal sealed class AsyncCleanup : IAsyncDisposable
{
    private readonly Func<Task> _cleanup;

    public AsyncCleanup(Func<Task> cleanup) => _cleanup = cleanup;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cleanup().ConfigureAwait(false);
        }
        catch
        {
            // Swallow cleanup errors
        }
    }
}
