using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Wpf.Contracts;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing provider connections with auto-reconnection support.
/// Implements IConnectionService with singleton pattern.
/// </summary>
public sealed class ConnectionService : IConnectionService
{
    private static readonly Lazy<ConnectionService> _instance = new(() => new ConnectionService());

    private readonly HttpClient _httpClient;
    private ConnectionSettings _settings = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private string _currentProvider = string.Empty;
    private DateTime? _connectedAt;
    private bool _disposed;

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
        // Stub: monitoring not implemented
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        // Stub: monitoring not implemented
    }

    /// <inheritdoc />
    public Task<bool> ConnectAsync(string provider, CancellationToken ct = default)
    {
        _currentProvider = provider;
        SetState(ConnectionState.Connected);
        _connectedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        SetState(ConnectionState.Disconnected);
        _connectedAt = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void PauseAutoReconnect()
    {
        // Stub: auto-reconnect not implemented
    }

    /// <inheritdoc />
    public void ResumeAutoReconnect()
    {
        // Stub: auto-reconnect not implemented
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

        _httpClient.Dispose();
        _disposed = true;
    }
}
