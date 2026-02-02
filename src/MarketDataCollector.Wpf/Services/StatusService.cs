using System;
using System.Net.Http;
using System.Text.Json;
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
    /// <returns>A task containing the status information.</returns>
    public async Task<SimpleStatus?> GetStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/status");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
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
                        Provider = null // TODO: Add provider info if available
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
    protected void OnStatusChanged(StatusChangedEventArgs e)
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
