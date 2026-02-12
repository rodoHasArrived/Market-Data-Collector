using System.Diagnostics;
using MarketDataCollector.Ui.Services.Contracts;

namespace MarketDataCollector.Ui.Services.Services;

/// <summary>
/// Abstract base class for connection management shared between WPF and UWP desktop applications.
/// Provides shared state machine, monitoring orchestration, auto-reconnect logic, and event raising.
/// Platform-specific HTTP calls and timer implementations are delegated to derived classes.
/// Part of Phase 6C.2 service deduplication (ROADMAP item 6C.2).
/// </summary>
public abstract class ConnectionServiceBase : IDisposable
{
    private readonly object _lock = new();
    private ConnectionSettings _settings = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private string _currentProvider = string.Empty;
    private DateTime? _connectedAt;
    private bool _disposed;

    // Monitoring fields
    private bool _isMonitoring;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailuresBeforeReconnect = 3;

    // Auto-reconnect fields
    private bool _autoReconnectEnabled = true;
    private bool _autoReconnectPaused;
    private int _reconnectAttempts;

    /// <summary>Gets the current service URL being used for connections.</summary>
    public string ServiceUrl => _settings.ServiceUrl;

    /// <summary>Gets the current connection state.</summary>
    public ConnectionState State => _state;

    /// <summary>Gets the current provider name.</summary>
    public string CurrentProvider => _currentProvider;

    /// <summary>Gets the connection uptime.</summary>
    public TimeSpan? Uptime => _connectedAt.HasValue ? DateTime.UtcNow - _connectedAt.Value : null;

    /// <summary>Gets the last measured latency in milliseconds.</summary>
    public double LastLatencyMs { get; private set; }

    /// <summary>Gets the total number of reconnections.</summary>
    public int TotalReconnects { get; private set; }

    /// <summary>Gets whether monitoring is currently active.</summary>
    public bool IsMonitoring => _isMonitoring;

    /// <summary>Gets whether auto-reconnect is currently paused.</summary>
    public bool IsAutoReconnectPaused => _autoReconnectPaused;

    /// <summary>Event raised when connection state changes.</summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>Event raised when connection state changes (ViewModel binding).</summary>
    public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>Event raised when latency measurement is updated.</summary>
    public event EventHandler<int>? LatencyUpdated;

    /// <summary>Event raised when a reconnection attempt is starting.</summary>
    public event EventHandler<ReconnectEventArgs>? ReconnectAttempting;

    /// <summary>Event raised when reconnection succeeds.</summary>
    public event EventHandler? ReconnectSucceeded;

    /// <summary>Event raised when a reconnection attempt fails.</summary>
    public event EventHandler<ReconnectFailedEventArgs>? ReconnectFailed;

    /// <summary>Event raised when connection health is updated.</summary>
    public event EventHandler<ConnectionHealthEventArgs>? ConnectionHealthUpdated;

    /// <summary>
    /// Updates connection settings and reconfigures the underlying client.
    /// </summary>
    public void UpdateSettings(ConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        OnSettingsUpdated(settings);
    }

    /// <summary>
    /// Configures the service URL directly.
    /// </summary>
    public void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30)
    {
        _settings.ServiceUrl = serviceUrl;
        _settings.ServiceTimeoutSeconds = timeoutSeconds;
        OnSettingsUpdated(_settings);
    }

    /// <summary>Gets current connection settings.</summary>
    public ConnectionSettings GetSettings() => _settings;

    /// <summary>
    /// Starts the connection monitoring with periodic health checks.
    /// </summary>
    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _consecutiveFailures = 0;

            var intervalMs = _settings.HealthCheckIntervalSeconds > 0
                ? _settings.HealthCheckIntervalSeconds * 1000
                : 10000;

            StartMonitoringTimer(intervalMs);
            LogOperation("Connection monitoring started", ("Interval", intervalMs.ToString()));
        }

        // Perform initial health check
        _ = PerformHealthCheckAsync();
    }

    /// <summary>
    /// Stops connection monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            StopMonitoringTimer();
            LogOperation("Connection monitoring stopped");
        }
    }

    /// <summary>
    /// Initiates a connection to the provider.
    /// </summary>
    public Task<bool> ConnectAsync(string provider, CancellationToken ct = default)
    {
        _currentProvider = provider;
        SetState(ConnectionState.Connected);
        _connectedAt = DateTime.UtcNow;
        _consecutiveFailures = 0;
        _reconnectAttempts = 0;

        if (!_isMonitoring)
        {
            StartMonitoring();
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Disconnects from the provider.
    /// </summary>
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        StopAutoReconnect();
        SetState(ConnectionState.Disconnected);
        _connectedAt = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pauses auto-reconnection temporarily.
    /// </summary>
    public void PauseAutoReconnect()
    {
        lock (_lock)
        {
            _autoReconnectPaused = true;
            StopAutoReconnect();
            LogOperation("Auto-reconnect paused");
        }
    }

    /// <summary>
    /// Resumes auto-reconnection.
    /// </summary>
    public void ResumeAutoReconnect()
    {
        lock (_lock)
        {
            _autoReconnectPaused = false;
            LogOperation("Auto-reconnect resumed");

            if (_state == ConnectionState.Disconnected && _autoReconnectEnabled)
            {
                ScheduleReconnect();
            }
        }
    }

    /// <summary>
    /// Called by derived classes when the monitoring timer fires.
    /// </summary>
    protected async Task OnMonitoringTimerFired()
    {
        await PerformHealthCheckAsync();
    }

    /// <summary>
    /// Called by derived classes when the reconnect timer fires.
    /// </summary>
    protected async Task OnReconnectTimerFired()
    {
        StopAutoReconnect();

        try
        {
            var isHealthy = await PerformHealthCheckCoreAsync(CancellationToken.None);

            if (isHealthy)
            {
                SetState(ConnectionState.Connected);
                _connectedAt = DateTime.UtcNow;
                _consecutiveFailures = 0;
                TotalReconnects++;

                ReconnectSucceeded?.Invoke(this, EventArgs.Empty);
                LogOperation("Reconnection successful", ("TotalReconnects", TotalReconnects.ToString()));
                _reconnectAttempts = 0;
            }
            else
            {
                throw new InvalidOperationException("Health check returned unhealthy status");
            }
        }
        catch (Exception ex)
        {
            ReconnectFailed?.Invoke(this, new ReconnectFailedEventArgs
            {
                AttemptNumber = _reconnectAttempts,
                Error = ex.Message,
                WillRetry = _autoReconnectEnabled && !_autoReconnectPaused
            });

            LogWarning("Reconnect attempt failed",
                ("Attempt", _reconnectAttempts.ToString()),
                ("Error", ex.Message));

            if (_autoReconnectEnabled && !_autoReconnectPaused)
            {
                ScheduleReconnect();
            }
        }
    }

    private async Task PerformHealthCheckAsync()
    {
        if (!_isMonitoring)
            return;

        var stopwatch = Stopwatch.StartNew();
        bool isHealthy = false;
        string? errorMessage = null;

        try
        {
            isHealthy = await PerformHealthCheckCoreAsync(CancellationToken.None);
            stopwatch.Stop();

            LastLatencyMs = stopwatch.Elapsed.TotalMilliseconds;
            LatencyUpdated?.Invoke(this, (int)LastLatencyMs);

            if (isHealthy)
            {
                _consecutiveFailures = 0;

                if (_state == ConnectionState.Disconnected || _state == ConnectionState.Reconnecting)
                {
                    SetState(ConnectionState.Connected);
                    _connectedAt = DateTime.UtcNow;
                }
            }
            else
            {
                errorMessage = "Health check returned unhealthy status";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            isHealthy = false;
            errorMessage = ex.Message;
            LogWarning("Health check failed", ("Error", ex.Message));
        }

        ConnectionHealthUpdated?.Invoke(this, new ConnectionHealthEventArgs
        {
            IsHealthy = isHealthy,
            LatencyMs = LastLatencyMs,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        });

        if (!isHealthy)
        {
            _consecutiveFailures++;

            if (_consecutiveFailures >= MaxConsecutiveFailuresBeforeReconnect)
            {
                if (_state == ConnectionState.Connected)
                {
                    SetState(ConnectionState.Disconnected);
                    _connectedAt = null;
                }

                if (_autoReconnectEnabled && !_autoReconnectPaused)
                {
                    ScheduleReconnect();
                }
            }
        }
    }

    private void ScheduleReconnect()
    {
        lock (_lock)
        {
            if (_autoReconnectPaused || !_autoReconnectEnabled)
                return;

            SetState(ConnectionState.Reconnecting);

            var delayMs = GetReconnectDelayMs(_reconnectAttempts);
            _reconnectAttempts++;

            StartReconnectTimer(delayMs);

            ReconnectAttempting?.Invoke(this, new ReconnectEventArgs
            {
                AttemptNumber = _reconnectAttempts,
                DelayMs = delayMs,
                Provider = _currentProvider
            });

            LogOperation("Scheduling reconnect attempt",
                ("Attempt", _reconnectAttempts.ToString()),
                ("DelayMs", delayMs.ToString()));
        }
    }

    private void SetState(ConnectionState newState)
    {
        var oldState = _state;
        if (oldState == newState)
        {
            return;
        }

        _state = newState;

        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            OldState = oldState,
            NewState = newState,
            Provider = _currentProvider
        });

        ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs
        {
            State = newState,
            Provider = _currentProvider
        });
    }

    /// <summary>
    /// When overridden, performs the actual health check HTTP call.
    /// Returns true if the health check is successful.
    /// </summary>
    protected abstract Task<bool> PerformHealthCheckCoreAsync(CancellationToken ct);

    /// <summary>When overridden, starts the platform-specific monitoring timer.</summary>
    protected abstract void StartMonitoringTimer(int intervalMs);

    /// <summary>When overridden, stops the platform-specific monitoring timer.</summary>
    protected abstract void StopMonitoringTimer();

    /// <summary>When overridden, starts the platform-specific reconnect timer.</summary>
    protected abstract void StartReconnectTimer(int delayMs);

    /// <summary>When overridden, stops the platform-specific reconnect timer.</summary>
    protected abstract void StopAutoReconnect();

    /// <summary>When overridden, handles settings update (e.g., reconfigure HttpClient timeout).</summary>
    protected abstract void OnSettingsUpdated(ConnectionSettings settings);

    /// <summary>
    /// Gets the reconnect delay for the given attempt number.
    /// Default implementation uses a fixed backoff schedule.
    /// </summary>
    protected virtual int GetReconnectDelayMs(int attemptIndex)
    {
        int[] delays = [1000, 2000, 5000, 10000, 30000];
        var index = Math.Min(attemptIndex, delays.Length - 1);
        return delays[index];
    }

    /// <summary>Logs an informational operation message.</summary>
    protected abstract void LogOperation(string message, params (string Key, string Value)[] properties);

    /// <summary>Logs a warning message.</summary>
    protected abstract void LogWarning(string message, params (string Key, string Value)[] properties);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            StopMonitoring();
            StopAutoReconnect();
            DisposePlatformResources();
        }

        _disposed = true;
    }

    /// <summary>When overridden, disposes platform-specific resources (HttpClient, timers).</summary>
    protected abstract void DisposePlatformResources();
}
