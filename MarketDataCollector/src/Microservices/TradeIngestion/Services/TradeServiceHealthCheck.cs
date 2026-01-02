using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Health check for the trade service.
/// </summary>
public sealed class TradeServiceHealthCheck : IHealthCheck
{
    private readonly ITradeProcessor _processor;
    private readonly ITradeStorage _storage;

    public TradeServiceHealthCheck(ITradeProcessor processor, ITradeStorage storage)
    {
        _processor = processor;
        _storage = storage;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var procStats = _processor.GetStatistics();
        var storageStats = _storage.GetStatistics();

        var data = new Dictionary<string, object>
        {
            ["tradesProcessed"] = procStats.Processed,
            ["queueDepth"] = procStats.QueueDepth,
            ["duplicates"] = procStats.Duplicates,
            ["validationErrors"] = procStats.ValidationErrors,
            ["tradesWritten"] = storageStats.TradesWritten,
            ["bytesWritten"] = storageStats.BytesWritten,
            ["avgLatencyMs"] = procStats.AverageLatencyMs
        };

        // Check queue depth
        if (procStats.QueueDepth > 40000)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Queue depth critically high",
                data: data
            ));
        }

        if (procStats.QueueDepth > 25000)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Queue depth elevated",
                data: data
            ));
        }

        // Check error rate
        var total = procStats.Processed + procStats.ValidationErrors;
        var errorRate = total > 0 ? (double)procStats.ValidationErrors / total : 0;

        if (errorRate > 0.1)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "High validation error rate",
                data: data
            ));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Trade service is healthy",
            data: data
        ));
    }
}
