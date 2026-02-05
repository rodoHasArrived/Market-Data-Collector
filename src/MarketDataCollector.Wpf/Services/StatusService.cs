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
/// </summary>
public sealed class StatusService
{
    private static readonly Lazy<StatusService> _instance = new(() => new StatusService());
    private static readonly HttpClient _httpClient = new();

    private string _currentStatus = "Ready";
    private readonly object _lock = new();
    private string _baseUrl = "http://localhost:8080";

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
    /// Occurs when the status is changed.
    /// </summary>
    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    private StatusService()
    {
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
