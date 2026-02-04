using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MarketDataCollector.Wpf.Contracts;
using Timer = System.Timers.Timer;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing provider connections with auto-reconnection support.
/// Implements IConnectionService with singleton pattern.
/// </summary>
public sealed class ConnectionService : IConnectionService
{
    private static readonly Lazy<ConnectionService> _instance = new(() => new ConnectionService());

    private readonly HttpClient _httpClient;
    private readonly object _lock = new();
    private ConnectionSettings _settings = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private string _currentProvider = string.Empty;
    private DateTime? _connectedAt;
    private bool _disposed;

    // Monitoring fields
    private Timer? _monitoringTimer;
    private bool _isMonitoring;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailuresBeforeReconnect = 3;

    // Auto-reconnect fields
    private bool _autoReconnectEnabled = true;
    private bool _autoReconnectPaused;
    private int _reconnectAttempts;
    private Timer? _reconnectTimer;
    private readonly int[] _reconnectDelaysMs = { 1000, 2000, 5000, 10000, 30000 };

    /// <summary>
    /// Gets the singleton instance of the ConnectionService.
    /// </summary>
    public static ConnectionService Instance => _instance.Value;

    /// <inheritdoc />
    public string ServiceUrl => _settings.ServiceUrl;

    /// <inheritdoc />
    public ConnectionState State => _state;

    /// <inheritdoc />
    public string CurrentProvider => _currentProvider;

    /// <inheritdoc />
    public TimeSpan? Uptime => _connectedAt.HasValue ? DateTime.UtcNow - _connectedAt.Value : null;

    /// <inheritdoc />
    public double LastLatencyMs { get; private set; }

    /// <inheritdoc />
    public int TotalReconnects { get; private set; }

    /// <summary>
    /// Gets whether monitoring is currently active.
    /// </summary>
    public bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// Gets whether auto-reconnect is currently paused.
    /// </summary>
    public bool IsAutoReconnectPaused => _autoReconnectPaused;

    /// <inheritdoc />
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler<int>? LatencyUpdated;

    /// <inheritdoc />
    public event EventHandler<ReconnectEventArgs>? ReconnectAttempting;

    /// <inheritdoc />
    public event EventHandler? ReconnectSucceeded;

    /// <inheritdoc />
    public event EventHandler<ReconnectFailedEventArgs>? ReconnectFailed;

    /// <inheritdoc />
    public event EventHandler<ConnectionHealthEventArgs>? ConnectionHealthUpdated;

    private ConnectionService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_settings.ServiceTimeoutSeconds)
        };
    }

    /// <inheritdoc />
    public void UpdateSettings(ConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.ServiceTimeoutSeconds);
    }

    /// <inheritdoc />
    public void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30)
    {
        _settings.ServiceUrl = serviceUrl;
        _settings.ServiceTimeoutSeconds = timeoutSeconds;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    /// <inheritdoc />
    public ConnectionSettings GetSettings() => _settings;

    /// <inheritdoc />
    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _consecutiveFailures = 0;

            _monitoringTimer = new Timer(_settings.HealthCheckIntervalSeconds > 0 ? _settings.HealthCheckIntervalSeconds * 1000 : 10000);
            _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;
            _monitoringTimer.AutoReset = true;
            _monitoringTimer.Start();

            LoggingService.Instance.LogInfo("Connection monitoring started",
                ("Interval", _monitoringTimer.Interval.ToString()));
        }

        // Perform initial health check
        _ = PerformHealthCheckAsync();
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;

            if (_monitoringTimer != null)
            {
                _monitoringTimer.Stop();
                _monitoringTimer.Elapsed -= OnMonitoringTimerElapsed;
                _monitoringTimer.Dispose();
                _monitoringTimer = null;
            }

            LoggingService.Instance.LogInfo("Connection monitoring stopped");
        }
    }

    private async void OnMonitoringTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await PerformHealthCheckAsync();
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync($"{_settings.ServiceUrl}/healthz", cts.Token);
            isHealthy = response.IsSuccessStatusCode;
            stopwatch.Stop();

            LastLatencyMs = stopwatch.Elapsed.TotalMilliseconds;
            LatencyUpdated?.Invoke(this, (int)LastLatencyMs);

            if (isHealthy)
            {
                _consecutiveFailures = 0;

                // If we were disconnected, we're now connected
                if (_state == ConnectionState.Disconnected || _state == ConnectionState.Reconnecting)
                {
                    SetState(ConnectionState.Connected);
                    _connectedAt = DateTime.UtcNow;
                }
            }
            else
            {
                errorMessage = $"Health check returned {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            isHealthy = false;
            errorMessage = ex.Message;
            LoggingService.Instance.LogWarning("Health check failed", ("Error", ex.Message));
        }

        // Raise health event
        ConnectionHealthUpdated?.Invoke(this, new ConnectionHealthEventArgs
        {
            IsHealthy = isHealthy,
            LatencyMs = LastLatencyMs,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        });

        // Handle failure
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

                // Trigger auto-reconnect if enabled
                if (_autoReconnectEnabled && !_autoReconnectPaused)
                {
                    StartAutoReconnect();
                }
            }
        }
    }

    /// <inheritdoc />
    public Task<bool> ConnectAsync(string provider, CancellationToken ct = default)
    {
        _currentProvider = provider;
        SetState(ConnectionState.Connected);
        _connectedAt = DateTime.UtcNow;
        _consecutiveFailures = 0;
        _reconnectAttempts = 0;

        // Start monitoring if not already running
        if (!_isMonitoring)
        {
            StartMonitoring();
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        StopAutoReconnect();
        SetState(ConnectionState.Disconnected);
        _connectedAt = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void PauseAutoReconnect()
    {
        lock (_lock)
        {
            _autoReconnectPaused = true;
            StopAutoReconnect();

            LoggingService.Instance.LogInfo("Auto-reconnect paused");
        }
    }

    /// <inheritdoc />
    public void ResumeAutoReconnect()
    {
        lock (_lock)
        {
            _autoReconnectPaused = false;

            LoggingService.Instance.LogInfo("Auto-reconnect resumed");

            // If we're disconnected, start reconnecting
            if (_state == ConnectionState.Disconnected && _autoReconnectEnabled)
            {
                StartAutoReconnect();
            }
        }
    }

    private void StartAutoReconnect()
    {
        lock (_lock)
        {
            if (_autoReconnectPaused || !_autoReconnectEnabled)
                return;

            if (_reconnectTimer != null)
                return; // Already reconnecting

            SetState(ConnectionState.Reconnecting);

            var delayIndex = Math.Min(_reconnectAttempts, _reconnectDelaysMs.Length - 1);
            var delayMs = _reconnectDelaysMs[delayIndex];

            _reconnectTimer = new Timer(delayMs);
            _reconnectTimer.Elapsed += OnReconnectTimerElapsed;
            _reconnectTimer.AutoReset = false;
            _reconnectTimer.Start();

            _reconnectAttempts++;

            ReconnectAttempting?.Invoke(this, new ReconnectEventArgs
            {
                AttemptNumber = _reconnectAttempts,
                DelayMs = delayMs,
                Provider = _currentProvider
            });

            LoggingService.Instance.LogInfo("Scheduling reconnect attempt",
                ("Attempt", _reconnectAttempts.ToString()),
                ("DelayMs", delayMs.ToString()));
        }
    }

    private void StopAutoReconnect()
    {
        lock (_lock)
        {
            if (_reconnectTimer != null)
            {
                _reconnectTimer.Stop();
                _reconnectTimer.Elapsed -= OnReconnectTimerElapsed;
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
            }
        }
    }

    private async void OnReconnectTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        StopAutoReconnect();

        try
        {
            // Attempt to reconnect by checking health
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync($"{_settings.ServiceUrl}/healthz", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                // Reconnection successful
                SetState(ConnectionState.Connected);
                _connectedAt = DateTime.UtcNow;
                _consecutiveFailures = 0;
                TotalReconnects++;

                ReconnectSucceeded?.Invoke(this, EventArgs.Empty);

                LoggingService.Instance.LogInfo("Reconnection successful",
                    ("TotalReconnects", TotalReconnects.ToString()));

                _reconnectAttempts = 0;
            }
            else
            {
                throw new HttpRequestException($"Health check returned {response.StatusCode}");
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

            LoggingService.Instance.LogWarning("Reconnect attempt failed",
                ("Attempt", _reconnectAttempts.ToString()),
                ("Error", ex.Message));

            // Schedule another attempt if enabled
            if (_autoReconnectEnabled && !_autoReconnectPaused)
            {
                StartAutoReconnect();
            }
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopMonitoring();
        StopAutoReconnect();
        _httpClient.Dispose();
        _disposed = true;
    }
}
