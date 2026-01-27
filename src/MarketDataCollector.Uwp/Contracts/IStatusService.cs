using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for retrieving system status from the collector.
/// Enables testability and dependency injection.
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// Gets the current service URL.
    /// </summary>
    string ServiceUrl { get; }

    /// <summary>
    /// Gets the status of the market data collector service.
    /// </summary>
    Task<StatusResponse?> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the status with full response details.
    /// </summary>
    Task<ApiResponse<StatusResponse>> GetStatusWithResponseAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if the service is healthy and reachable.
    /// </summary>
    Task<ServiceHealthResult> CheckHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of a service health check.
/// </summary>
public sealed class ServiceHealthResult
{
    public bool IsReachable { get; init; }
    public bool IsConnected { get; init; }
    public double LatencyMs { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Generic API response wrapper.
/// </summary>
/// <typeparam name="T">The type of the response data.</typeparam>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public int StatusCode { get; init; }
}
