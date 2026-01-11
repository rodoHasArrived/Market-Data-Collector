using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing provider connections with auto-reconnection support.
/// Uses the centralized ApiClientService for configurable service URL.
/// </summary>
public class ConnectionService
{
    private static ConnectionService? _instance;
    private static readonly object _lock = new();

    private readonly ApiClientService _apiClient;
    private readonly NotificationService _notificationService;

    private ConnectionState _currentState = ConnectionState.Disconnected;
    private string _currentProvider = "Unknown";
    private int _reconnectAttempts;
    private CancellationTokenSource? _reconnectCts;
    private Timer? _healthCheckTimer;

    private ConnectionSettings _settings = new();
    private DateTime? _connectedSince;
    private DateTime? _lastDisconnect;
    private int _totalReconnects;
    private double _lastLatencyMs;

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
    public ConnectionState State => _currentState;

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
    public void UpdateSettings(ConnectionSettings settings)
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
    public ConnectionSettings GetSettings() => _settings;

    /// <summary>
    /// Starts the connection monitoring and auto-reconnection.
    /// </summary>
    public void StartMonitoring()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = new Timer(
            async _ => await CheckConnectionHealthAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_settings.HealthCheckIntervalSeconds));
    }

    /// <summary>
    /// Stops connection monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
        CancelReconnection();
    }

    /// <summary>
    /// Initiates a connection to the provider.
    /// </summary>
    public async Task<bool> ConnectAsync(string provider)
    {
        _currentProvider = provider;
        UpdateState(ConnectionState.Connecting);

        try
        {
            // Attempt to check service health (which validates connection)
            var health = await _apiClient.CheckHealthAsync();

            if (health.IsReachable && health.IsConnected)
            {
                _connectedSince = DateTime.UtcNow;
                _reconnectAttempts = 0;
                _lastLatencyMs = health.LatencyMs;
                UpdateState(ConnectionState.Connected);

                await _notificationService.NotifyConnectionStatusAsync(true, provider);
                return true;
            }
            else if (health.IsReachable)
            {
                // Service is reachable but provider not connected
                UpdateState(ConnectionState.Disconnected);
                return false;
            }
            else
            {
                UpdateState(ConnectionState.Error);
                await _notificationService.NotifyErrorAsync("Connection Failed", health.ErrorMessage ?? "Service unreachable");
                return false;
            }
        }
        catch (Exception ex)
        {
            UpdateState(ConnectionState.Error);
            await _notificationService.NotifyErrorAsync("Connection Failed", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the provider.
    /// </summary>
    public async Task DisconnectAsync()
    {
        CancelReconnection();
        _connectedSince = null;
        UpdateState(ConnectionState.Disconnected);

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

    private async Task CheckConnectionHealthAsync()
    {
        if (_currentState == ConnectionState.Reconnecting)
            return;

        try
        {
            var health = await _apiClient.CheckHealthAsync();
            RaiseLatencyUpdated(health.LatencyMs);

            if (health.IsReachable && health.IsConnected)
            {
                if (_currentState != ConnectionState.Connected)
                {
                    _connectedSince = DateTime.UtcNow;
                    UpdateState(ConnectionState.Connected);
                }

                ConnectionHealthUpdated?.Invoke(this, new ConnectionHealthEventArgs
                {
                    IsHealthy = true,
                    LatencyMs = _lastLatencyMs,
                    Uptime = Uptime,
                    ReconnectCount = _totalReconnects
                });
            }
            else
            {
                await HandleConnectionLostAsync();
            }
        }
        catch
        {
            await HandleConnectionLostAsync();
        }
    }

    private async Task HandleConnectionLostAsync()
    {
        if (_currentState == ConnectionState.Reconnecting)
            return;

        _lastDisconnect = DateTime.UtcNow;
        _connectedSince = null;
        UpdateState(ConnectionState.Disconnected);

        await _notificationService.NotifyConnectionStatusAsync(false, _currentProvider);

        if (_settings.AutoReconnectEnabled)
        {
            await StartAutoReconnectAsync();
        }
    }

    private async Task StartAutoReconnectAsync()
    {
        if (_currentState == ConnectionState.Reconnecting)
            return;

        UpdateState(ConnectionState.Reconnecting);
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

            // Add jitter (up to 10% of delay)
            var jitter = new Random().NextDouble() * 0.1 * delayMs;
            delayMs += jitter;

            await _notificationService.NotifyReconnectionAttemptAsync(
                _currentProvider,
                _reconnectAttempts,
                _settings.MaxReconnectAttempts);

            ReconnectAttempting?.Invoke(this, new ReconnectEventArgs
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
                var health = await _apiClient.CheckHealthAsync();

                if (health.IsReachable && health.IsConnected)
                {
                    _totalReconnects++;
                    _connectedSince = DateTime.UtcNow;
                    _lastLatencyMs = health.LatencyMs;
                    var attempts = _reconnectAttempts;
                    _reconnectAttempts = 0;
                    UpdateState(ConnectionState.Connected);

                    await _notificationService.NotifyConnectionStatusAsync(
                        true,
                        _currentProvider,
                        $"Reconnected after {attempts} attempts");

                    ReconnectSucceeded?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
            catch
            {
                // Attempt failed, continue to next attempt
            }
        }

        // Max attempts reached
        UpdateState(ConnectionState.Error);

        await _notificationService.NotifyErrorAsync(
            "Reconnection Failed",
            $"Failed to reconnect to {_currentProvider} after {_reconnectAttempts} attempts");

        ReconnectFailed?.Invoke(this, new ReconnectFailedEventArgs
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

    private void UpdateState(ConnectionState newState)
    {
        var oldState = _currentState;
        _currentState = newState;

        if (oldState != newState)
        {
            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Provider = _currentProvider
            });

            // Also raise the ViewModel-compatible event
            ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs
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
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when connection state changes (alias for StateChanged for ViewModel compatibility).
    /// </summary>
    public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Event raised when latency measurement is updated.
    /// </summary>
    public event EventHandler<int>? LatencyUpdated;

    /// <summary>
    /// Event raised when a reconnection attempt is starting.
    /// </summary>
    public event EventHandler<ReconnectEventArgs>? ReconnectAttempting;

    /// <summary>
    /// Event raised when reconnection succeeds.
    /// </summary>
    public event EventHandler? ReconnectSucceeded;

    /// <summary>
    /// Event raised when all reconnection attempts fail.
    /// </summary>
    public event EventHandler<ReconnectFailedEventArgs>? ReconnectFailed;

    /// <summary>
    /// Event raised when connection health is updated.
    /// </summary>
    public event EventHandler<ConnectionHealthEventArgs>? ConnectionHealthUpdated;
}

/// <summary>
/// Connection state event args for ViewModel binding.
/// </summary>
public class ConnectionStateEventArgs : EventArgs
{
    public ConnectionState State { get; set; }
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Connection states.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

/// <summary>
/// Connection settings.
/// </summary>
public class ConnectionSettings
{
    public bool AutoReconnectEnabled { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 10;
    public int InitialReconnectDelayMs { get; set; } = 2000; // 2 seconds
    public int MaxReconnectDelayMs { get; set; } = 300000; // 5 minutes
    public int HealthCheckIntervalSeconds { get; set; } = 5;
    public string ServiceUrl { get; set; } = "http://localhost:8080";
    public int ServiceTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Connection state change event args.
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState OldState { get; set; }
    public ConnectionState NewState { get; set; }
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Reconnect attempt event args.
/// </summary>
public class ReconnectEventArgs : EventArgs
{
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; }
    public int NextRetryMs { get; set; }
}

/// <summary>
/// Reconnect failed event args.
/// </summary>
public class ReconnectFailedEventArgs : EventArgs
{
    public int Attempts { get; set; }
    public string LastError { get; set; } = string.Empty;
}

/// <summary>
/// Connection health event args.
/// </summary>
public class ConnectionHealthEventArgs : EventArgs
{
    public bool IsHealthy { get; set; }
    public double LatencyMs { get; set; }
    public TimeSpan? Uptime { get; set; }
    public int ReconnectCount { get; set; }
}
