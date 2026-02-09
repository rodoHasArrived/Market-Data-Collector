using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Api;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for retrieving system status from the collector.
/// Uses the centralized ApiClientService for configurable URL support.
/// Implements <see cref="IStatusService"/> for testability.
/// </summary>
public sealed class StatusService : IStatusService
{
    private static StatusService? _instance;
    private static readonly object _lock = new();

    private readonly ApiClientService _apiClient;

    public static StatusService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new StatusService();
                }
            }
            return _instance;
        }
    }

    private StatusService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Gets the current service URL.
    /// </summary>
    public string ServiceUrl => _apiClient.BaseUrl;

    /// <summary>
    /// Gets the status of the market data collector service.
    /// </summary>
    public async Task<StatusResponse?> GetStatusAsync(CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<StatusResponse>(UiApiRoutes.Status, ct);
    }

    /// <summary>
    /// Gets the status with full response details.
    /// </summary>
    public async Task<ApiResponse<StatusResponse>> GetStatusWithResponseAsync(CancellationToken ct = default)
    {
        // ApiClientService returns ApiResponse<T> from Contracts.Api
        return await _apiClient.GetWithResponseAsync<StatusResponse>(UiApiRoutes.Status, ct);
    }

    /// <summary>
    /// Checks if the service is healthy and reachable.
    /// </summary>
    public async Task<ServiceHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        // ApiClientService returns ServiceHealthResult from Contracts.Api
        return await _apiClient.CheckHealthAsync(ct);
    }
}
