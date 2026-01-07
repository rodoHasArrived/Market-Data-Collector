using DataIngestion.Contracts.Messages;
using System.Threading;

namespace DataIngestion.Gateway.Services;

/// <summary>
/// Routes ingestion data to appropriate downstream services.
/// </summary>
public interface IDataRouter
{
    /// <summary>Route data based on type and configuration.</summary>
    Task<RoutingResult> RouteAsync(IngestionDataType dataType, string symbol, object data, CancellationToken ct = default);

    /// <summary>Get routing statistics.</summary>
    RoutingStatistics GetStatistics();
}

/// <summary>
/// Result of a routing operation.
/// </summary>
public record RoutingResult(
    bool Success,
    string TargetService,
    string? MessageId = null,
    TimeSpan? RoutingTime = null,
    string? ErrorMessage = null
);

/// <summary>
/// Routing statistics.
/// </summary>
public record RoutingStatistics(
    long TotalRouted,
    long TotalFailed,
    Dictionary<string, long> RoutedByService,
    Dictionary<string, long> RoutedByDataType,
    double AverageRoutingTimeMs
);
