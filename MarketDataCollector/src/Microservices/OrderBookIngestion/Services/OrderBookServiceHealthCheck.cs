using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;

namespace DataIngestion.OrderBookService.Services;

public sealed class OrderBookServiceHealthCheck : IHealthCheck
{
    private readonly IOrderBookManager _manager;
    private readonly OrderBookMetrics _metrics;

    public OrderBookServiceHealthCheck(IOrderBookManager manager, OrderBookMetrics metrics)
    {
        _manager = manager;
        _metrics = metrics;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["activeBooks"] = _manager.GetActiveBookCount(),
            ["snapshotsProcessed"] = _metrics.SnapshotsProcessed,
            ["updatesProcessed"] = _metrics.UpdatesProcessed,
            ["integrityErrors"] = _metrics.IntegrityErrors
        };

        var frozenBooks = _manager.GetAllOrderBooks().Count(b => b.IsFrozen);
        data["frozenBooks"] = frozenBooks;

        if (frozenBooks > _manager.GetActiveBookCount() / 2)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Too many frozen order books", data: data));
        }

        if (frozenBooks > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"{frozenBooks} frozen order books", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "OrderBook service is healthy", data: data));
    }
}
