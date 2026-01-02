using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataIngestion.QuoteService.Services;

public sealed class QuoteServiceHealthCheck : IHealthCheck
{
    private readonly IQuoteProcessor _processor;
    private readonly QuoteMetrics _metrics;

    public QuoteServiceHealthCheck(IQuoteProcessor processor, QuoteMetrics metrics)
    {
        _processor = processor;
        _metrics = metrics;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var stats = _processor.GetStats();
        var data = new Dictionary<string, object>
        {
            ["quotesProcessed"] = stats.Processed,
            ["queueDepth"] = stats.QueueDepth,
            ["activeSymbols"] = stats.ActiveSymbols,
            ["crossedQuotes"] = _metrics.CrossedQuotes,
            ["lockedQuotes"] = _metrics.LockedQuotes
        };

        if (stats.QueueDepth > 80000)
            return Task.FromResult(HealthCheckResult.Unhealthy("Queue critically high", data: data));
        if (stats.QueueDepth > 50000)
            return Task.FromResult(HealthCheckResult.Degraded("Queue depth elevated", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Quote service is healthy", data: data));
    }
}
