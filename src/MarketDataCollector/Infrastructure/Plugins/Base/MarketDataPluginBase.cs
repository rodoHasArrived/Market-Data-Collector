using System.Runtime.CompilerServices;
using MarketDataCollector.Infrastructure.Plugins.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Infrastructure.Plugins.Base;

/// <summary>
/// Base class for all market data plugins.
/// Provides common infrastructure: logging, health tracking, state management, metrics.
/// </summary>
/// <remarks>
/// Plugin authors should extend this class or one of its specialized subclasses
/// (RealtimePluginBase, HistoricalPluginBase) rather than implementing IMarketDataPlugin directly.
/// </remarks>
public abstract class MarketDataPluginBase : IMarketDataPlugin
{
    private readonly object _stateLock = new();
    private PluginState _state = PluginState.Created;
    private PluginHealth _health = PluginHealth.Unknown;
    private IPluginConfig _config = PluginConfig.Empty;

    /// <summary>
    /// Logger instance for this plugin.
    /// </summary>
    protected ILogger Logger { get; private set; } = NullLogger.Instance;

    #region IMarketDataPlugin - Identity

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual Version Version => new(1, 0, 0);

    #endregion

    #region IMarketDataPlugin - Lifecycle

    public PluginState State
    {
        get { lock (_stateLock) return _state; }
        protected set
        {
            lock (_stateLock)
            {
                if (_state != value)
                {
                    var oldState = _state;
                    _state = value;
                    OnStateChanged(oldState, value);
                }
            }
        }
    }

    public async Task InitializeAsync(IPluginConfig config, CancellationToken ct = default)
    {
        if (State != PluginState.Created)
        {
            throw new InvalidOperationException(
                $"Plugin {Id} cannot be initialized from state {State}. " +
                "Plugins can only be initialized once from the Created state.");
        }

        _config = config;
        State = PluginState.Initializing;

        try
        {
            // Allow subclasses to set up logging
            Logger = CreateLogger();

            Logger.LogInformation("Initializing plugin {PluginId} v{Version}", Id, Version);

            // Validate configuration
            ValidateConfiguration(config);

            // Perform plugin-specific initialization
            await OnInitializeAsync(config, ct).ConfigureAwait(false);

            State = PluginState.Ready;
            UpdateHealth(PluginHealth.Healthy());

            Logger.LogInformation("Plugin {PluginId} initialized successfully", Id);
        }
        catch (Exception ex)
        {
            State = PluginState.Error;
            UpdateHealth(PluginHealth.Unhealthy($"Initialization failed: {ex.Message}"));
            Logger.LogError(ex, "Failed to initialize plugin {PluginId}", Id);
            throw;
        }
    }

    /// <summary>
    /// Override to create a logger for this plugin.
    /// Default returns NullLogger.
    /// </summary>
    protected virtual ILogger CreateLogger() => NullLogger.Instance;

    /// <summary>
    /// Override to validate plugin configuration.
    /// Throw if required configuration is missing.
    /// </summary>
    protected virtual void ValidateConfiguration(IPluginConfig config) { }

    /// <summary>
    /// Override to perform plugin-specific initialization.
    /// Called after configuration validation.
    /// </summary>
    protected virtual Task OnInitializeAsync(IPluginConfig config, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>
    /// Called when the plugin state changes.
    /// Override to react to state transitions.
    /// </summary>
    protected virtual void OnStateChanged(PluginState oldState, PluginState newState)
    {
        Logger.LogDebug("Plugin {PluginId} state changed: {OldState} -> {NewState}",
            Id, oldState, newState);
    }

    #endregion

    #region IMarketDataPlugin - Capabilities

    public abstract PluginCapabilities Capabilities { get; }

    #endregion

    #region IMarketDataPlugin - Data Streaming

    public abstract IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default);

    /// <summary>
    /// Wraps the streaming implementation with state management and error handling.
    /// Call this from StreamAsync implementations.
    /// </summary>
    protected async IAsyncEnumerable<MarketDataEvent> StreamWithManagementAsync(
        DataStreamRequest request,
        Func<DataStreamRequest, CancellationToken, IAsyncEnumerable<MarketDataEvent>> streamFunc,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (State != PluginState.Ready)
        {
            throw new InvalidOperationException(
                $"Plugin {Id} is not ready for streaming. Current state: {State}");
        }

        if (!Capabilities.CanFulfill(request))
        {
            throw new ArgumentException(
                $"Plugin {Id} cannot fulfill request. Check capabilities.", nameof(request));
        }

        State = PluginState.Streaming;
        EmitMetric("stream_started", 1, new Dictionary<string, string>
        {
            ["symbols"] = string.Join(",", request.Symbols.Take(5)),
            ["is_historical"] = request.IsHistorical.ToString()
        });

        int eventCount = 0;
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            await foreach (var evt in streamFunc(request, ct).WithCancellation(ct).ConfigureAwait(false))
            {
                eventCount++;
                if (eventCount % 10000 == 0)
                {
                    EmitMetric("events_streamed", eventCount);
                }
                yield return evt;
            }
        }
        finally
        {
            var elapsed = DateTimeOffset.UtcNow - startTime;
            State = PluginState.Ready;

            EmitMetric("stream_completed", 1, new Dictionary<string, string>
            {
                ["event_count"] = eventCount.ToString(),
                ["duration_seconds"] = elapsed.TotalSeconds.ToString("F1")
            });

            Logger.LogInformation(
                "Stream completed for plugin {PluginId}: {EventCount} events in {Elapsed:F1}s",
                Id, eventCount, elapsed.TotalSeconds);
        }
    }

    #endregion

    #region IMarketDataPlugin - Health & Diagnostics

    public PluginHealth Health
    {
        get { lock (_stateLock) return _health; }
    }

    public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
    public event EventHandler<PluginMetricEventArgs>? MetricEmitted;

    /// <summary>
    /// Updates the plugin health status and raises HealthChanged if significant.
    /// </summary>
    protected void UpdateHealth(PluginHealth newHealth)
    {
        PluginHealth previous;
        lock (_stateLock)
        {
            previous = _health;
            _health = newHealth;
        }

        if (previous.Status != newHealth.Status || previous.Score != newHealth.Score)
        {
            Logger.LogDebug("Plugin {PluginId} health changed: {OldStatus} ({OldScore:P0}) -> {NewStatus} ({NewScore:P0})",
                Id, previous.Status, previous.Score, newHealth.Status, newHealth.Score);

            HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs
            {
                Previous = previous,
                Current = newHealth
            });
        }
    }

    /// <summary>
    /// Records a successful operation, updating health accordingly.
    /// </summary>
    protected void RecordSuccess(TimeSpan? latency = null)
    {
        UpdateHealth(PluginHealth.Healthy(latency));
        EmitMetric("operation_success", 1);
        if (latency.HasValue)
        {
            EmitMetric("operation_latency_ms", latency.Value.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Records a failed operation, updating health accordingly.
    /// </summary>
    protected void RecordFailure(string message, bool isRecoverable = true)
    {
        var currentHealth = Health;
        var failures = currentHealth.ConsecutiveFailures + 1;

        var newHealth = isRecoverable && failures < 3
            ? PluginHealth.Degraded(message, 1.0 - (failures * 0.2), failures)
            : PluginHealth.Unhealthy(message, failures);

        UpdateHealth(newHealth);
        EmitMetric("operation_failure", 1, new Dictionary<string, string>
        {
            ["message"] = message,
            ["recoverable"] = isRecoverable.ToString()
        });
    }

    /// <summary>
    /// Records a rate limit hit, updating health accordingly.
    /// </summary>
    protected void RecordRateLimit(TimeSpan resetIn, int remaining = 0, int limit = 0)
    {
        State = PluginState.RateLimited;
        UpdateHealth(PluginHealth.RateLimited(resetIn, remaining, limit));
        EmitMetric("rate_limit_hit", 1, new Dictionary<string, string>
        {
            ["reset_seconds"] = resetIn.TotalSeconds.ToString("F0")
        });
    }

    /// <summary>
    /// Emits a metric for observability.
    /// </summary>
    protected void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null)
    {
        MetricEmitted?.Invoke(this, new PluginMetricEventArgs
        {
            Name = $"plugin.{Id}.{name}",
            Value = value,
            Tags = tags
        });
    }

    #endregion

    #region IAsyncDisposable

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        Logger.LogDebug("Disposing plugin {PluginId}", Id);

        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            State = PluginState.Disposed;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to perform plugin-specific cleanup.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    /// <summary>
    /// Throws if the plugin has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    #endregion

    #region Configuration Access

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    protected IPluginConfig Config => _config;

    /// <summary>
    /// Gets a required configuration value.
    /// </summary>
    protected string GetRequiredConfig(string key) => _config.GetRequired(key);

    /// <summary>
    /// Gets an optional configuration value.
    /// </summary>
    protected string? GetConfig(string key, string? defaultValue = null) =>
        _config.Get(key, defaultValue);

    /// <summary>
    /// Gets a typed configuration value.
    /// </summary>
    protected T GetConfig<T>(string key, T defaultValue = default!) where T : IParsable<T> =>
        _config.Get(key, defaultValue);

    #endregion
}
