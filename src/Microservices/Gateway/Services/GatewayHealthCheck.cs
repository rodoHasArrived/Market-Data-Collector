using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;

namespace DataIngestion.Gateway.Services;

/// <summary>
/// Health check for the gateway service.
/// </summary>
public sealed class GatewayHealthCheck : IHealthCheck
{
    private readonly IProviderManager _providerManager;
    private readonly IDataRouter _dataRouter;

    public GatewayHealthCheck(IProviderManager providerManager, IDataRouter dataRouter)
    {
        _providerManager = providerManager;
        _dataRouter = dataRouter;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var providerStatuses = _providerManager.GetProviderStatuses();
        var routingStats = _dataRouter.GetStatistics();

        var data = new Dictionary<string, object>
        {
            ["totalRouted"] = routingStats.TotalRouted,
            ["totalFailed"] = routingStats.TotalFailed,
            ["averageRoutingTimeMs"] = routingStats.AverageRoutingTimeMs,
            ["connectedProviders"] = providerStatuses.Count(p => p.Value.IsConnected)
        };

        // Check failure rate
        var totalAttempts = routingStats.TotalRouted + routingStats.TotalFailed;
        var failureRate = totalAttempts > 0
            ? (double)routingStats.TotalFailed / totalAttempts
            : 0;

        if (failureRate > 0.5)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "High routing failure rate",
                data: data
            ));
        }

        if (failureRate > 0.1)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Elevated routing failure rate",
                data: data
            ));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Gateway is healthy",
            data: data
        ));
    }
}
