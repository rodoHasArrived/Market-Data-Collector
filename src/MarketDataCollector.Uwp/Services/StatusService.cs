using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Uwp.Contracts;
using MarketDataCollector.Uwp.Models;

// Type aliases for backwards compatibility with renamed Contracts types
using ProviderHealth = MarketDataCollector.Contracts.Api.BackfillProviderHealth;
using SymbolResolution = MarketDataCollector.Contracts.Api.SymbolResolutionResponse;

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

/// <summary>
/// Service for managing backfill operations via the core API.
/// Replaces simulation with real API integration.
/// </summary>
public sealed class BackfillApiService
{
    private readonly ApiClientService _apiClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public BackfillApiService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Gets the list of available backfill providers.
    /// </summary>
    public async Task<List<BackfillProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<List<BackfillProviderInfo>>(UiApiRoutes.BackfillProviders, ct);
        return result ?? new List<BackfillProviderInfo>();
    }

    /// <summary>
    /// Gets the last backfill operation status.
    /// </summary>
    public async Task<BackfillResult?> GetLastStatusAsync(CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<BackfillResult>(UiApiRoutes.BackfillStatus, ct);
    }

    /// <summary>
    /// Runs a backfill operation for the specified symbols.
    /// </summary>
    public async Task<BackfillResult?> RunBackfillAsync(
        string provider,
        string[] symbols,
        string? from,
        string? to,
        CancellationToken ct = default)
    {
        var request = new BackfillRequest
        {
            Provider = provider,
            Symbols = symbols,
            From = from,
            To = to
        };

        // Use the shared backfill client with longer timeout
        var backfillClient = _apiClient.GetBackfillClient();
        var response = await _apiClient.PostWithResponseAsync<BackfillResult>(
            UiApiRoutes.BackfillRun,
            request,
            ct,
            backfillClient);

        if (response.Success)
        {
            return response.Data;
        }

        return new BackfillResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Backfill request failed"
        };
    }

    /// <summary>
    /// Checks the health of backfill providers.
    /// </summary>
    public async Task<BackfillHealthResponse?> CheckProviderHealthAsync(CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<BackfillHealthResponse>(UiApiRoutes.BackfillHealth, ct);
    }

    /// <summary>
    /// Resolves symbol information for backfill.
    /// </summary>
    public async Task<SymbolResolution?> ResolveSymbolAsync(string symbol, CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<SymbolResolution>(
            UiApiRoutes.WithParam(UiApiRoutes.BackfillResolve, "symbol", symbol), ct);
    }

    /// <summary>
    /// Runs an immediate gap-fill operation.
    /// </summary>
    public async Task<BackfillExecutionResponse?> RunGapFillAsync(
        string[] symbols,
        int lookbackDays = 30,
        string priority = "High",
        CancellationToken ct = default)
    {
        var request = new
        {
            Symbols = symbols,
            LookbackDays = lookbackDays,
            Priority = priority
        };

        // Use the shared backfill client with longer timeout
        var backfillClient = _apiClient.GetBackfillClient();
        var response = await _apiClient.PostWithResponseAsync<BackfillExecutionResponse>(
            UiApiRoutes.BackfillGapFill,
            request,
            ct,
            backfillClient);
        return response.Data;
    }

    /// <summary>
    /// Gets available backfill presets.
    /// </summary>
    public async Task<List<BackfillPreset>> GetPresetsAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<List<BackfillPreset>>(UiApiRoutes.BackfillPresets, ct);
        return result ?? new List<BackfillPreset>();
    }

    /// <summary>
    /// Gets backfill execution history.
    /// </summary>
    public async Task<List<BackfillExecution>> GetExecutionHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<List<BackfillExecution>>(
            UiApiRoutes.WithQuery(UiApiRoutes.BackfillExecutions, $"limit={limit}"), ct);
        return result ?? new List<BackfillExecution>();
    }

    /// <summary>
    /// Gets backfill statistics.
    /// </summary>
    public async Task<BackfillStatistics?> GetStatisticsAsync(int? hours = null, CancellationToken ct = default)
    {
        var endpoint = hours.HasValue
            ? UiApiRoutes.WithQuery(UiApiRoutes.BackfillStatistics, $"hours={hours.Value}")
            : UiApiRoutes.BackfillStatistics;
        return await _apiClient.GetAsync<BackfillStatistics>(endpoint, ct);
    }
}

// Backfill-related models (BackfillRequest, BackfillHealthResponse, BackfillProviderHealth,
// SymbolResolutionResponse, BackfillExecutionResponse, BackfillPreset, BackfillExecution,
// BackfillStatistics) are now defined in MarketDataCollector.Contracts.Api.BackfillApiModels.cs
// and StatusModels.cs. Type aliases at the top of this file maintain backwards compatibility.
