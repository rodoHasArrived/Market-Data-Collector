using Prometheus;

namespace DataIngestion.Gateway.Services;

/// <summary>
/// Collects and exposes Prometheus metrics for the gateway.
/// </summary>
public sealed class MetricsCollector
{
    private readonly Counter _routedMessagesTotal;
    private readonly Counter _failedRoutingTotal;
    private readonly Histogram _routingDuration;
    private readonly Counter _rateLimitedRequests;
    private readonly Gauge _activeConnections;
    private readonly Counter _requestsTotal;

    public MetricsCollector()
    {
        _routedMessagesTotal = Metrics.CreateCounter(
            "gateway_routed_messages_total",
            "Total number of messages routed to downstream services",
            new CounterConfiguration
            {
                LabelNames = ["service", "data_type"]
            });

        _failedRoutingTotal = Metrics.CreateCounter(
            "gateway_failed_routing_total",
            "Total number of failed routing attempts",
            new CounterConfiguration
            {
                LabelNames = ["service", "data_type"]
            });

        _routingDuration = Metrics.CreateHistogram(
            "gateway_routing_duration_milliseconds",
            "Duration of routing operations in milliseconds",
            new HistogramConfiguration
            {
                LabelNames = ["service"],
                Buckets = [0.1, 0.5, 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000]
            });

        _rateLimitedRequests = Metrics.CreateCounter(
            "gateway_rate_limited_requests_total",
            "Total number of rate-limited requests",
            new CounterConfiguration
            {
                LabelNames = ["client_id", "endpoint"]
            });

        _activeConnections = Metrics.CreateGauge(
            "gateway_active_connections",
            "Number of active provider connections",
            new GaugeConfiguration
            {
                LabelNames = ["provider"]
            });

        _requestsTotal = Metrics.CreateCounter(
            "gateway_requests_total",
            "Total number of API requests",
            new CounterConfiguration
            {
                LabelNames = ["method", "endpoint", "status"]
            });
    }

    public void RecordRouting(string service, string dataType, bool success, long durationMs)
    {
        if (success)
        {
            _routedMessagesTotal.WithLabels(service, dataType).Inc();
            _routingDuration.WithLabels(service).Observe(durationMs);
        }
        else
        {
            _failedRoutingTotal.WithLabels(service, dataType).Inc();
        }
    }

    public void RecordRateLimited(string clientId, string endpoint)
    {
        _rateLimitedRequests.WithLabels(clientId, endpoint).Inc();
    }

    public void SetActiveConnections(string provider, int count)
    {
        _activeConnections.WithLabels(provider).Set(count);
    }

    public void RecordRequest(string method, string endpoint, int statusCode)
    {
        _requestsTotal.WithLabels(method, endpoint, statusCode.ToString()).Inc();
    }
}
