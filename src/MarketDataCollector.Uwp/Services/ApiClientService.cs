using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Centralized HTTP client service for communicating with the Market Data Collector core service.
/// Provides configurable service URL, retry logic, and health monitoring.
/// </summary>
public sealed class ApiClientService : IDisposable
{
    private static ApiClientService? _instance;
    private static readonly object _lock = new();

    private HttpClient _httpClient;
    private HttpClient? _backfillHttpClient;
    private string _baseUrl;
    private int _timeoutSeconds;
    private int _backfillTimeoutMinutes;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the singleton instance of the ApiClientService.
    /// </summary>
    public static ApiClientService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ApiClientService();
                }
            }
            return _instance;
        }
    }

    private ApiClientService()
    {
        _baseUrl = "http://localhost:8080";
        _timeoutSeconds = 30;
        _backfillTimeoutMinutes = 60;
        _httpClient = CreateHttpClient(_timeoutSeconds);
    }

    /// <summary>
    /// Gets the current base URL for the service.
    /// </summary>
    public string BaseUrl => _baseUrl;

    /// <summary>
    /// Gets whether the client is configured with a non-default URL.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl);

    /// <summary>
    /// Event raised when the service URL changes.
    /// </summary>
    public event EventHandler<ServiceUrlChangedEventArgs>? ServiceUrlChanged;

    /// <summary>
    /// Configures the API client with settings from the app configuration.
    /// </summary>
    public void Configure(AppSettings? settings)
    {
        if (settings == null) return;

        var newUrl = settings.ServiceUrl ?? "http://localhost:8080";
        var newTimeout = settings.ServiceTimeoutSeconds > 0 ? settings.ServiceTimeoutSeconds : 30;
        var newBackfillTimeout = settings.BackfillTimeoutMinutes > 0 ? settings.BackfillTimeoutMinutes : 60;

        var urlChanged = !string.Equals(_baseUrl, newUrl, StringComparison.OrdinalIgnoreCase);
        var timeoutChanged = _timeoutSeconds != newTimeout;

        if (urlChanged || timeoutChanged)
        {
            var oldUrl = _baseUrl;
            _baseUrl = newUrl.TrimEnd('/');
            _timeoutSeconds = newTimeout;
            _backfillTimeoutMinutes = newBackfillTimeout;

            // Recreate HTTP client with new timeout
            var oldClient = _httpClient;
            _httpClient = CreateHttpClient(_timeoutSeconds);
            oldClient.Dispose();

            if (urlChanged)
            {
                ServiceUrlChanged?.Invoke(this, new ServiceUrlChangedEventArgs
                {
                    OldUrl = oldUrl,
                    NewUrl = _baseUrl
                });
            }
        }
    }

    /// <summary>
    /// Configures the API client with a specific URL.
    /// </summary>
    public void Configure(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60)
    {
        Configure(new AppSettings
        {
            ServiceUrl = serviceUrl,
            ServiceTimeoutSeconds = timeoutSeconds,
            BackfillTimeoutMinutes = backfillTimeoutMinutes
        });
    }

    private static HttpClient CreateHttpClient(int timeoutSeconds)
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    /// <summary>
    /// Gets a shared HTTP client configured for long-running backfill operations.
    /// The client is lazily created and reused to avoid socket exhaustion.
    /// </summary>
    public HttpClient GetBackfillClient()
    {
        if (_backfillHttpClient == null)
        {
            lock (_lock)
            {
                _backfillHttpClient ??= new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(_backfillTimeoutMinutes)
                };
            }
        }
        return _backfillHttpClient;
    }

    /// <summary>
    /// Creates an HTTP client configured for long-running backfill operations.
    /// </summary>
    /// <remarks>
    /// Prefer using <see cref="GetBackfillClient"/> instead to reuse the shared client.
    /// Only use this method when you need a client with a custom timeout that will be properly disposed.
    /// </remarks>
    [Obsolete("Prefer GetBackfillClient() to avoid socket exhaustion. Only use when custom timeout is required.")]
    public HttpClient CreateBackfillClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(_backfillTimeoutMinutes)
        };
    }

    /// <summary>
    /// Performs a GET request to the specified endpoint.
    /// </summary>
    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default) where T : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Performs a GET request and returns the raw response.
    /// </summary>
    public async Task<ApiResponse<T>> GetWithResponseAsync<T>(string endpoint, CancellationToken ct = default) where T : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = json
                };
            }

            var data = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Data = data
            };
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = $"Connection failed: {ex.Message}",
                IsConnectionError = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Performs a POST request with JSON body.
    /// </summary>
    public async Task<T?> PostAsync<T>(string endpoint, object? body = null, CancellationToken ct = default) where T : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
                : null;

            var response = await _httpClient.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Performs a POST request and returns the full response.
    /// </summary>
    public async Task<ApiResponse<T>> PostWithResponseAsync<T>(
        string endpoint,
        object? body = null,
        CancellationToken ct = default,
        HttpClient? customClient = null) where T : class
    {
        var url = BuildUrl(endpoint);
        var client = customClient ?? _httpClient;

        try
        {
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
                : null;

            var response = await client.PostAsync(url, content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = json
                };
            }

            var data = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Data = data
            };
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = $"Connection failed: {ex.Message}",
                IsConnectionError = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Checks if the service is reachable.
    /// </summary>
    public async Task<ServiceHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var response = await GetWithResponseAsync<StatusResponse>("/api/status", ct);
            var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new ServiceHealthResult
            {
                IsReachable = response.Success,
                IsConnected = response.Data?.IsConnected ?? false,
                LatencyMs = latencyMs,
                StatusCode = response.StatusCode,
                ErrorMessage = response.ErrorMessage
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceHealthResult
            {
                IsReachable = false,
                IsConnected = false,
                LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private string BuildUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return _baseUrl;

        var path = endpoint.StartsWith('/') ? endpoint : $"/{endpoint}";
        return $"{_baseUrl}{path}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _backfillHttpClient?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Generic API response wrapper.
/// </summary>
public class ApiResponse<T> where T : class
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsConnectionError { get; set; }
}

/// <summary>
/// Service health check result.
/// </summary>
public class ServiceHealthResult
{
    public bool IsReachable { get; set; }
    public bool IsConnected { get; set; }
    public double LatencyMs { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event args for service URL changes.
/// </summary>
public class ServiceUrlChangedEventArgs : EventArgs
{
    public string OldUrl { get; set; } = string.Empty;
    public string NewUrl { get; set; } = string.Empty;
}
