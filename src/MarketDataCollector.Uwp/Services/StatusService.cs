using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for retrieving system status from the collector.
/// Uses the centralized ApiClientService for configurable URL support.
/// </summary>
public class StatusService
{
    private readonly ApiClientService _apiClient;

    public StatusService()
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
        return await _apiClient.GetAsync<StatusResponse>("/api/status", ct);
    }

    /// <summary>
    /// Gets the status with full response details.
    /// </summary>
    public async Task<ApiResponse<StatusResponse>> GetStatusWithResponseAsync(CancellationToken ct = default)
    {
        return await _apiClient.GetWithResponseAsync<StatusResponse>("/api/status", ct);
    }

    /// <summary>
    /// Checks if the service is healthy and reachable.
    /// </summary>
    public async Task<ServiceHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        return await _apiClient.CheckHealthAsync(ct);
    }
}

/// <summary>
/// Service for managing backfill operations via the core API.
/// Replaces simulation with real API integration.
/// </summary>
public class BackfillApiService
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
        var result = await _apiClient.GetAsync<List<BackfillProviderInfo>>("/api/backfill/providers", ct);
        return result ?? new List<BackfillProviderInfo>();
    }

    /// <summary>
    /// Gets the last backfill operation status.
    /// </summary>
    public async Task<BackfillResult?> GetLastStatusAsync(CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<BackfillResult>("/api/backfill/status", ct);
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

        // Use a longer timeout client for backfill operations
        using var backfillClient = _apiClient.CreateBackfillClient();
        var response = await _apiClient.PostWithResponseAsync<BackfillResult>(
            "/api/backfill/run",
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
        return await _apiClient.GetAsync<BackfillHealthResponse>("/api/backfill/health", ct);
    }

    /// <summary>
    /// Resolves symbol information for backfill.
    /// </summary>
    public async Task<SymbolResolution?> ResolveSymbolAsync(string symbol, CancellationToken ct = default)
    {
        return await _apiClient.GetAsync<SymbolResolution>($"/api/backfill/resolve/{Uri.EscapeDataString(symbol)}", ct);
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

        using var backfillClient = _apiClient.CreateBackfillClient();
        return await _apiClient.PostAsync<BackfillExecutionResponse>("/api/backfill/gap-fill", request, ct);
    }

    /// <summary>
    /// Gets available backfill presets.
    /// </summary>
    public async Task<List<BackfillPreset>> GetPresetsAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<List<BackfillPreset>>("/api/backfill/presets", ct);
        return result ?? new List<BackfillPreset>();
    }

    /// <summary>
    /// Gets backfill execution history.
    /// </summary>
    public async Task<List<BackfillExecution>> GetExecutionHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<List<BackfillExecution>>($"/api/backfill/executions?limit={limit}", ct);
        return result ?? new List<BackfillExecution>();
    }

    /// <summary>
    /// Gets backfill statistics.
    /// </summary>
    public async Task<BackfillStatistics?> GetStatisticsAsync(int? hours = null, CancellationToken ct = default)
    {
        var endpoint = hours.HasValue
            ? $"/api/backfill/statistics?hours={hours.Value}"
            : "/api/backfill/statistics";
        return await _apiClient.GetAsync<BackfillStatistics>(endpoint, ct);
    }
}

/// <summary>
/// Request model for running a backfill.
/// </summary>
public class BackfillRequest
{
    public string Provider { get; set; } = "composite";
    public string[] Symbols { get; set; } = Array.Empty<string>();
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>
/// Response model for backfill health check.
/// </summary>
public class BackfillHealthResponse
{
    public bool IsHealthy { get; set; }
    public Dictionary<string, ProviderHealth>? Providers { get; set; }
}

/// <summary>
/// Individual provider health status.
/// </summary>
public class ProviderHealth
{
    public bool IsAvailable { get; set; }
    public double? LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? LastChecked { get; set; }
}

/// <summary>
/// Symbol resolution result.
/// </summary>
public class SymbolResolution
{
    public string Symbol { get; set; } = string.Empty;
    public string? ResolvedSymbol { get; set; }
    public string? Exchange { get; set; }
    public string? Currency { get; set; }
    public string? SecurityType { get; set; }
    public Dictionary<string, string>? ProviderMappings { get; set; }
}

/// <summary>
/// Backfill execution response.
/// </summary>
public class BackfillExecutionResponse
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; }
    public string[] Symbols { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Backfill preset definition.
/// </summary>
public class BackfillPreset
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int LookbackDays { get; set; }
}

/// <summary>
/// Backfill execution record.
/// </summary>
public class BackfillExecution
{
    public string Id { get; set; } = string.Empty;
    public string ScheduleId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int SymbolsProcessed { get; set; }
    public int BarsDownloaded { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Backfill statistics summary.
/// </summary>
public class BackfillStatistics
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public long TotalBarsDownloaded { get; set; }
    public double AverageExecutionTimeSeconds { get; set; }
    public DateTime? LastSuccessfulExecution { get; set; }
}
