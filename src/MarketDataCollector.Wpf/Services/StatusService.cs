using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for tracking and broadcasting application status.
/// Implements singleton pattern for application-wide status management.
/// Supports continuous live monitoring with stale data detection.
/// </summary>
public sealed class StatusService
{
    private static readonly Lazy<StatusService> _instance = new(() => new StatusService());
    private static readonly HttpClient _httpClient = new();

    private string _currentStatus = "Ready";
    private readonly object _lock = new();
    private string _baseUrl = "http://localhost:8080";
    private CancellationTokenSource? _monitoringCts;
    private DateTime? _lastSuccessfulUpdate;
    private bool _isBackendReachable = true;

    /// <summary>
    /// Gets the singleton instance of the StatusService.
    /// </summary>
    public static StatusService Instance => _instance.Value;

    /// <summary>
    /// Gets or sets the base URL for the API.
    /// </summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value;
    }

    /// <summary>
    /// Gets the current application status.
    /// </summary>
    public string CurrentStatus
    {
        get
        {
            lock (_lock)
            {
                return _currentStatus;
            }
        }
    }

    /// <summary>
    /// Gets the timestamp of the last successful API status fetch.
    /// </summary>
    public DateTime? LastSuccessfulUpdate => _lastSuccessfulUpdate;

    /// <summary>
    /// Gets whether the backend service is currently reachable.
    /// </summary>
    public bool IsBackendReachable => _isBackendReachable;

    /// <summary>
    /// Gets how many seconds since the last successful update, or null if never updated.
    /// </summary>
    public double? SecondsSinceLastUpdate =>
        _lastSuccessfulUpdate.HasValue
            ? (DateTime.UtcNow - _lastSuccessfulUpdate.Value).TotalSeconds
            : null;

    /// <summary>
    /// Gets whether the current data is considered stale (older than 10 seconds).
    /// </summary>
    public bool IsDataStale =>
        !_lastSuccessfulUpdate.HasValue || (DateTime.UtcNow - _lastSuccessfulUpdate.Value).TotalSeconds > 10;

    /// <summary>
    /// Whether live monitoring is currently active.
    /// </summary>
    public bool IsMonitoring => _monitoringCts != null && !_monitoringCts.IsCancellationRequested;

    /// <summary>
    /// Occurs when the status is changed.
    /// </summary>
    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Occurs when a live status snapshot is received from the API.
    /// </summary>
    public event EventHandler<LiveStatusEventArgs>? LiveStatusReceived;

    /// <summary>
    /// Occurs when backend reachability changes.
    /// </summary>
    public event EventHandler<bool>? BackendReachabilityChanged;

    private StatusService()
    {
    }

    /// <summary>
    /// Starts continuous live monitoring that polls /api/status every 2 seconds.
    /// Raises LiveStatusReceived on each successful poll.
    /// </summary>
    /// <param name="intervalSeconds">Polling interval in seconds (default 2).</param>
    public void StartLiveMonitoring(int intervalSeconds = 2)
    {
        StopLiveMonitoring();
        _monitoringCts = new CancellationTokenSource();
        _ = RunLiveMonitoringLoopAsync(intervalSeconds, _monitoringCts.Token);
    }

    /// <summary>
    /// Stops live monitoring if currently active.
    /// </summary>
    public void StopLiveMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    private async Task RunLiveMonitoringLoopAsync(int intervalSeconds, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await GetStatusAsync(ct);

                if (status != null)
                {
                    _lastSuccessfulUpdate = DateTime.UtcNow;
                    SetBackendReachable(true);
                    LiveStatusReceived?.Invoke(this, new LiveStatusEventArgs
                    {
                        Status = status,
                        Timestamp = DateTime.UtcNow,
                        IsStale = false
                    });
                }
                else
                {
                    SetBackendReachable(false);
                    LiveStatusReceived?.Invoke(this, new LiveStatusEventArgs
                    {
                        Status = null,
                        Timestamp = DateTime.UtcNow,
                        IsStale = IsDataStale
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                SetBackendReachable(false);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SetBackendReachable(bool reachable)
    {
        if (_isBackendReachable != reachable)
        {
            _isBackendReachable = reachable;
            BackendReachabilityChanged?.Invoke(this, reachable);
        }
    }

    /// <summary>
    /// Gets status from the API endpoint.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the status information.</returns>
    public async Task<SimpleStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var statusTask = _httpClient.GetAsync($"{_baseUrl}/api/status", cts.Token);
            var providerTask = GetProviderStatusAsync(cts.Token);

            await Task.WhenAll(statusTask, providerTask);

            var response = await statusTask;
            var providerInfo = await providerTask;

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cts.Token);
                var apiStatus = JsonSerializer.Deserialize<ApiStatusResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiStatus?.Metrics != null)
                {
                    return new SimpleStatus
                    {
                        Published = apiStatus.Metrics.Published,
                        Dropped = apiStatus.Metrics.Dropped,
                        Integrity = apiStatus.Metrics.Integrity,
                        Historical = apiStatus.Metrics.HistoricalBars,
                        Provider = providerInfo
                    };
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled - return null
        }
        catch
        {
            // Return null on error - caller will handle
        }

        return null;
    }

    /// <summary>
    /// Gets provider status from the API endpoint.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the provider information.</returns>
    public async Task<StatusProviderInfo?> GetProviderStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/providers/status", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var providerStatus = JsonSerializer.Deserialize<ProviderStatusResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (providerStatus != null)
                {
                    return new StatusProviderInfo
                    {
                        ActiveProvider = providerStatus.ActiveProvider,
                        IsConnected = providerStatus.IsConnected,
                        ConnectionCount = providerStatus.ConnectionCount,
                        LastHeartbeat = providerStatus.LastHeartbeat,
                        AvailableProviders = providerStatus.AvailableProviders ?? new List<string>()
                    };
                }
            }
        }
        catch
        {
            // Return null on error - caller will handle
        }

        return null;
    }

    /// <summary>
    /// Gets the list of available streaming providers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the list of available providers.</returns>
    public async Task<IReadOnlyList<ProviderInfo>> GetAvailableProvidersAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/providers/catalog", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var providers = JsonSerializer.Deserialize<List<ProviderInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return providers ?? new List<ProviderInfo>();
            }
        }
        catch
        {
            // Return empty list on error
        }

        return new List<ProviderInfo>();
    }

    /// <summary>
    /// Updates the application status.
    /// </summary>
    /// <param name="status">The new status message.</param>
    /// <exception cref="ArgumentNullException">Thrown when status is null.</exception>
    public void UpdateStatus(string status)
    {
        ArgumentNullException.ThrowIfNull(status);

        string previousStatus;

        lock (_lock)
        {
            if (_currentStatus == status)
            {
                return;
            }

            previousStatus = _currentStatus;
            _currentStatus = status;
        }

        LoggingService.Instance.LogInfo(
            "Status changed",
            ("PreviousStatus", previousStatus),
            ("NewStatus", status));

        OnStatusChanged(new StatusChangedEventArgs(previousStatus, status));
    }

    /// <summary>
    /// Updates the status to indicate an operation is in progress.
    /// </summary>
    /// <param name="operation">The operation description.</param>
    public void SetBusy(string operation)
    {
        UpdateStatus($"Working: {operation}...");
    }

    /// <summary>
    /// Updates the status to indicate the application is ready.
    /// </summary>
    public void SetReady()
    {
        UpdateStatus("Ready");
    }

    /// <summary>
    /// Updates the status to indicate an error occurred.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public void SetError(string errorMessage)
    {
        UpdateStatus($"Error: {errorMessage}");
    }

    /// <summary>
    /// Updates the status to indicate a connection state.
    /// </summary>
    /// <param name="isConnected">Whether the application is connected.</param>
    /// <param name="providerName">Optional provider name.</param>
    public void SetConnectionStatus(bool isConnected, string? providerName = null)
    {
        if (isConnected)
        {
            UpdateStatus(string.IsNullOrEmpty(providerName)
                ? "Connected"
                : $"Connected to {providerName}");
        }
        else
        {
            UpdateStatus(string.IsNullOrEmpty(providerName)
                ? "Disconnected"
                : $"Disconnected from {providerName}");
        }
    }

    /// <summary>
    /// Raises the StatusChanged event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnStatusChanged(StatusChangedEventArgs e)
    {
        StatusChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for status change events.
/// </summary>
public sealed class StatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous status.
    /// </summary>
    public string PreviousStatus { get; }

    /// <summary>
    /// Gets the new status.
    /// </summary>
    public string NewStatus { get; }

    /// <summary>
    /// Gets the timestamp when the status changed.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the StatusChangedEventArgs class.
    /// </summary>
    /// <param name="previousStatus">The previous status.</param>
    /// <param name="newStatus">The new status.</param>
    public StatusChangedEventArgs(string previousStatus, string newStatus)
    {
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Simplified status for display in the WPF UI.
/// </summary>
public sealed class SimpleStatus
{
    /// <summary>Total events published.</summary>
    public long Published { get; set; }

    /// <summary>Total events dropped.</summary>
    public long Dropped { get; set; }

    /// <summary>Total integrity events.</summary>
    public long Integrity { get; set; }

    /// <summary>Total historical bars received.</summary>
    public long Historical { get; set; }

    /// <summary>Current provider status information.</summary>
    public StatusProviderInfo? Provider { get; set; }
}

/// <summary>
/// Provider status information.
/// </summary>
public sealed class StatusProviderInfo
{
    /// <summary>Name of the currently active provider.</summary>
    public string? ActiveProvider { get; set; }

    /// <summary>Whether the provider is connected.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Number of active connections.</summary>
    public int ConnectionCount { get; set; }

    /// <summary>Last heartbeat timestamp.</summary>
    public DateTimeOffset? LastHeartbeat { get; set; }

    /// <summary>List of available providers.</summary>
    public IReadOnlyList<string> AvailableProviders { get; set; } = new List<string>();

    /// <summary>
    /// Gets a display string for the provider status.
    /// </summary>
    public string DisplayStatus => IsConnected
        ? $"Connected to {ActiveProvider ?? "Unknown"}"
        : "Disconnected";
}

/// <summary>
/// Provider information for display.
/// </summary>
public sealed class ProviderInfo
{
    /// <summary>Provider identifier.</summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Display name for the provider.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Description of the provider.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Provider type (Streaming, Backfill, Hybrid).</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>Whether the provider requires credentials.</summary>
    public bool RequiresCredentials { get; set; }
}

/// <summary>
/// API status response model.
/// </summary>
internal sealed class ApiStatusResponse
{
    public ApiMetrics? Metrics { get; set; }
}

/// <summary>
/// API metrics model.
/// </summary>
internal sealed class ApiMetrics
{
    public long Published { get; set; }
    public long Dropped { get; set; }
    public long Integrity { get; set; }
    public long HistoricalBars { get; set; }
}

/// <summary>
/// Provider status API response model.
/// </summary>
internal sealed class ProviderStatusResponse
{
    public string? ActiveProvider { get; set; }
    public bool IsConnected { get; set; }
    public int ConnectionCount { get; set; }
    public DateTimeOffset? LastHeartbeat { get; set; }
    public List<string>? AvailableProviders { get; set; }
}

/// <summary>
/// Event arguments for live status monitoring updates.
/// </summary>
public sealed class LiveStatusEventArgs : EventArgs
{
    /// <summary>The status snapshot (null if backend unreachable).</summary>
    public SimpleStatus? Status { get; init; }

    /// <summary>When this update was received.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Whether the data is considered stale.</summary>
    public bool IsStale { get; init; }
}
