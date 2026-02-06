using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing provider connections with auto-reconnection support.
/// Uses the centralized ApiClientService for configurable service URL.
/// Implements <see cref="IConnectionService"/> for testability.
/// </summary>
public sealed class ConnectionService : IConnectionService
{
    private static ConnectionService? _instance;
    private static readonly object _lock = new();
    private static readonly Random SharedRandom = Random.Shared;

    private readonly ApiClientService _apiClient;
    private readonly NotificationService _notificationService;

    private Contracts.ConnectionState _currentState = Contracts.ConnectionState.Disconnected;
    private string _currentProvider = "Unknown";
    private int _reconnectAttempts;
    private CancellationTokenSource? _reconnectCts;
    private CancellationTokenSource? _healthCheckCts;
    private Timer? _healthCheckTimer;

    private Contracts.ConnectionSettings _settings = new();
    private DateTime? _connectedSince;
    private DateTime? _lastDisconnect;
    private int _totalReconnects;
    private double _lastLatencyMs;
    private bool _disposed;

    public static ConnectionService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ConnectionService();
                }
            }
            return _instance;
        }
    }

    private ConnectionService()
    {
        _apiClient = ApiClientService.Instance;
        _notificationService = NotificationService.Instance;
    }

    /// <summary>
    /// Gets the current service URL being used for connections.
    /// </summary>
    public string ServiceUrl => _apiClient.BaseUrl;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public Contracts.ConnectionState State => _currentState;

    /// <summary>
    /// Gets the current provider name.
    /// </summary>
    public string CurrentProvider => _currentProvider;

    /// <summary>
    /// Gets the connection uptime.
    /// </summary>
    public TimeSpan? Uptime => _connectedSince.HasValue
        ? DateTime.UtcNow - _connectedSince.Value
        : null;

    /// <summary>
    /// Gets the last measured latency.
    /// </summary>
    public double LastLatencyMs => _lastLatencyMs;

    /// <summary>
    /// Gets the total reconnection count.
    /// </summary>
    public int TotalReconnects => _totalReconnects;

    /// <summary>
    /// Updates connection settings and reconfigures the API client if URL changed.
    /// </summary>
    public void UpdateSettings(Contracts.ConnectionSettings settings)
    {
        _settings = settings;

        // Update API client with new URL if specified
        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            _apiClient.Configure(settings.ServiceUrl, settings.ServiceTimeoutSeconds);
        }
    }

    /// <summary>
    /// Configures the service URL directly.
    /// </summary>
    public void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30)
    {
        _settings.ServiceUrl = serviceUrl;
        _settings.ServiceTimeoutSeconds = timeoutSeconds;
        _apiClient.Configure(serviceUrl, timeoutSeconds);
    }

    /// <summary>
    /// Gets current connection settings.
    /// </summary>
    public Contracts.ConnectionSettings GetSettings() => _settings;

    /// <summary>
    /// Starts the connection monitoring and auto-reconnection.
    /// Uses CancellationToken to enable graceful shutdown of async operations.
    /// </summary>
    public void StartMonitoring()
    {
        StopMonitoring();
        _healthCheckCts = new CancellationTokenSource();
        var ct = _healthCheckCts.Token;

        _healthCheckTimer = new Timer(
            async _ =>
            {
                try
                {
                    await CheckConnectionHealthAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown, ignore
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning("Health check failed", ex);
                }
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds));
    }

    /// <summary>
    /// Stops connection monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        // Cancel any pending health check operations
        try
        {
            _healthCheckCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }

        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
        _healthCheckCts?.Dispose();
        _healthCheckCts = null;

        CancelReconnection();
    }

    /// <summary>
    /// Disposes the service and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        StopMonitoring();
        _reconnectCts?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Initiates a connection to the provider.
    /// </summary>
    public async Task<bool> ConnectAsync(string provider, CancellationToken ct = default)
    {
        _currentProvider = provider;
        UpdateState(Contracts.ConnectionState.Connecting);

        try
        {
            ct.ThrowIfCancellationRequested();

            // Attempt to check service health (which validates connection)
            var health = await _apiClient.CheckHealthAsync(ct);

            if (health.IsReachable && health.IsConnected)
            {
                _connectedSince = DateTime.UtcNow;
                _reconnectAttempts = 0;
                _lastLatencyMs = health.LatencyMs;
                UpdateState(Contracts.ConnectionState.Connected);

                await _notificationService.NotifyConnectionStatusAsync(true, provider);
                return true;
            }
            else if (health.IsReachable)
            {
                // Service is reachable but provider not connected
                UpdateState(Contracts.ConnectionState.Disconnected);
                return false;
            }
            else
            {
                UpdateState(Contracts.ConnectionState.Error);
                await _notificationService.NotifyErrorAsync("Connection Failed", health.ErrorMessage ?? "Service unreachable");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UpdateState(Contracts.ConnectionState.Error);
            await _notificationService.NotifyErrorAsync("Connection Failed", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the provider.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        CancelReconnection();
        _connectedSince = null;
        UpdateState(Contracts.ConnectionState.Disconnected);

        await _notificationService.NotifyConnectionStatusAsync(false, _currentProvider, "Manually disconnected");
    }

    /// <summary>
    /// Pauses auto-reconnection temporarily.
    /// </summary>
    public void PauseAutoReconnect()
    {
        _settings.AutoReconnectEnabled = false;
        CancelReconnection();
    }

    /// <summary>
    /// Resumes auto-reconnection.
    /// </summary>
    public void ResumeAutoReconnect()
    {
        _settings.AutoReconnectEnabled = true;
    }

    private async Task CheckConnectionHealthAsync(CancellationToken ct = default)
    {
        if (_currentState == Contracts.ConnectionState.Reconnecting)
            return;

        ct.ThrowIfCancellationRequested();

        try
        {
            var health = await _apiClient.CheckHealthAsync(ct).ConfigureAwait(false);
            RaiseLatencyUpdated(health.LatencyMs);

            if (health.IsReachable && health.IsConnected)
            {
                if (_currentState != Contracts.ConnectionState.Connected)
                {
                    _connectedSince = DateTime.UtcNow;
                    UpdateState(Contracts.ConnectionState.Connected);
                }

                ConnectionHealthUpdated?.Invoke(this, new Contracts.ConnectionHealthEventArgs
                {
                    IsHealthy = true,
                    LatencyMs = _lastLatencyMs,
                    Uptime = Uptime,
                    ReconnectCount = _totalReconnects
                });
            }
            else
            {
                await HandleConnectionLostAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch
        {
            await HandleConnectionLostAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleConnectionLostAsync()
    {
        if (_currentState == Contracts.ConnectionState.Reconnecting)
            return;

        _lastDisconnect = DateTime.UtcNow;
        _connectedSince = null;
        UpdateState(Contracts.ConnectionState.Disconnected);

        await _notificationService.NotifyConnectionStatusAsync(false, _currentProvider);

        if (_settings.AutoReconnectEnabled)
        {
            await StartAutoReconnectAsync();
        }
    }

    private async Task StartAutoReconnectAsync()
    {
        if (_currentState == Contracts.ConnectionState.Reconnecting)
            return;

        UpdateState(Contracts.ConnectionState.Reconnecting);
        _reconnectCts = new CancellationTokenSource();

        try
        {
            await ReconnectWithExponentialBackoffAsync(_reconnectCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Reconnection was cancelled
        }
    }

    private async Task ReconnectWithExponentialBackoffAsync(CancellationToken cancellationToken)
    {
        _reconnectAttempts = 0;
        var baseDelayMs = _settings.InitialReconnectDelayMs;
        var maxDelayMs = _settings.MaxReconnectDelayMs;

        while (_reconnectAttempts < _settings.MaxReconnectAttempts && !cancellationToken.IsCancellationRequested)
        {
            _reconnectAttempts++;

            // Calculate exponential backoff delay
            var delayMs = Math.Min(
                baseDelayMs * Math.Pow(2, _reconnectAttempts - 1),
                maxDelayMs);

            // Add jitter (up to 10% of delay) using shared random to avoid allocation
            var jitter = SharedRandom.NextDouble() * 0.1 * delayMs;
            delayMs += jitter;

            await _notificationService.NotifyReconnectionAttemptAsync(
                _currentProvider,
                _reconnectAttempts,
                _settings.MaxReconnectAttempts);

            ReconnectAttempting?.Invoke(this, new Contracts.ReconnectEventArgs
            {
                Attempt = _reconnectAttempts,
                MaxAttempts = _settings.MaxReconnectAttempts,
                NextRetryMs = (int)delayMs
            });

            // Wait before attempting reconnection
            try
            {
                await Task.Delay((int)delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Attempt reconnection
            try
            {
                var health = await _apiClient.CheckHealthAsync(cancellationToken);

                if (health.IsReachable && health.IsConnected)
                {
                    _totalReconnects++;
                    _connectedSince = DateTime.UtcNow;
                    _lastLatencyMs = health.LatencyMs;
                    var attempts = _reconnectAttempts;
                    _reconnectAttempts = 0;
                    UpdateState(Contracts.ConnectionState.Connected);

                    await _notificationService.NotifyConnectionStatusAsync(
                        true,
                        _currentProvider,
                        $"Reconnected after {attempts} attempts");

                    ReconnectSucceeded?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Attempt failed, continue to next attempt
            }
        }

        // Max attempts reached
        UpdateState(Contracts.ConnectionState.Error);

        await _notificationService.NotifyErrorAsync(
            "Reconnection Failed",
            $"Failed to reconnect to {_currentProvider} after {_reconnectAttempts} attempts");

        ReconnectFailed?.Invoke(this, new Contracts.ReconnectFailedEventArgs
        {
            Attempts = _reconnectAttempts,
            LastError = "Max reconnection attempts reached"
        });
    }

    private void CancelReconnection()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        _reconnectAttempts = 0;
    }

    private void UpdateState(Contracts.ConnectionState newState)
    {
        var oldState = _currentState;
        _currentState = newState;

        if (oldState != newState)
        {
            StateChanged?.Invoke(this, new Contracts.ConnectionStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Provider = _currentProvider
            });

            // Also raise the ViewModel-compatible event
            ConnectionStateChanged?.Invoke(this, new Contracts.ConnectionStateEventArgs
            {
                State = newState,
                Provider = _currentProvider
            });
        }
    }

    private void RaiseLatencyUpdated(double latencyMs)
    {
        _lastLatencyMs = latencyMs;
        LatencyUpdated?.Invoke(this, (int)latencyMs);
    }

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    public event EventHandler<Contracts.ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when connection state changes (alias for StateChanged for ViewModel compatibility).
    /// </summary>
    public event EventHandler<Contracts.ConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Event raised when latency measurement is updated.
    /// </summary>
    public event EventHandler<int>? LatencyUpdated;

    /// <summary>
    /// Event raised when a reconnection attempt is starting.
    /// </summary>
    public event EventHandler<Contracts.ReconnectEventArgs>? ReconnectAttempting;

    /// <summary>
    /// Event raised when reconnection succeeds.
    /// </summary>
    public event EventHandler? ReconnectSucceeded;

    /// <summary>
    /// Event raised when all reconnection attempts fail.
    /// </summary>
    public event EventHandler<Contracts.ReconnectFailedEventArgs>? ReconnectFailed;

    /// <summary>
    /// Event raised when connection health is updated.
    /// </summary>
    public event EventHandler<Contracts.ConnectionHealthEventArgs>? ConnectionHealthUpdated;
}

