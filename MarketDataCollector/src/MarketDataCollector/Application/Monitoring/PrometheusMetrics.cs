using Prometheus;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Prometheus metrics exporter for MarketDataCollector.
/// Implements best practices from prometheus-net documentation and
/// production patterns from Grafana dashboard templates.
///
/// Naming conventions follow Prometheus best practices:
/// - snake_case for metric names
/// - _total suffix for counters
/// - Descriptive help text
/// - Appropriate metric types (Counter, Gauge, Histogram)
/// </summary>
public static class PrometheusMetrics
{
    // Event counters
    private static readonly Counter PublishedEvents = Prometheus.Metrics.CreateCounter(
        "mdc_events_published_total",
        "Total number of market events published to the event pipeline");

    private static readonly Counter DroppedEvents = Prometheus.Metrics.CreateCounter(
        "mdc_events_dropped_total",
        "Total number of events dropped due to backpressure or pipeline capacity");

    private static readonly Counter IntegrityEvents = Prometheus.Metrics.CreateCounter(
        "mdc_integrity_events_total",
        "Total number of data integrity validation events (gaps, out-of-order, etc.)");

    private static readonly Counter TradeEvents = Prometheus.Metrics.CreateCounter(
        "mdc_trade_events_total",
        "Total number of trade events processed");

    private static readonly Counter DepthUpdateEvents = Prometheus.Metrics.CreateCounter(
        "mdc_depth_update_events_total",
        "Total number of market depth update events processed");

    private static readonly Counter QuoteEvents = Prometheus.Metrics.CreateCounter(
        "mdc_quote_events_total",
        "Total number of quote events processed");

    // Gauges for current state
    private static readonly Gauge EventsPerSecond = Prometheus.Metrics.CreateGauge(
        "mdc_events_per_second",
        "Current rate of events published per second");

    private static readonly Gauge TradesPerSecond = Prometheus.Metrics.CreateGauge(
        "mdc_trades_per_second",
        "Current rate of trades processed per second");

    private static readonly Gauge DepthUpdatesPerSecond = Prometheus.Metrics.CreateGauge(
        "mdc_depth_updates_per_second",
        "Current rate of depth updates processed per second");

    private static readonly Gauge DropRatePercent = Prometheus.Metrics.CreateGauge(
        "mdc_drop_rate_percent",
        "Percentage of events dropped (0-100)");

    // Latency metrics (using Histogram for percentile calculations)
    private static readonly Histogram ProcessingLatency = Prometheus.Metrics.CreateHistogram(
        "mdc_processing_latency_microseconds",
        "Event processing latency in microseconds",
        new HistogramConfiguration
        {
            // Buckets optimized for microsecond-level latency (1µs to 10ms)
            Buckets = new[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000 }
        });

    private static readonly Gauge AverageLatencyUs = Prometheus.Metrics.CreateGauge(
        "mdc_average_latency_microseconds",
        "Average event processing latency in microseconds");

    private static readonly Gauge MinLatencyUs = Prometheus.Metrics.CreateGauge(
        "mdc_min_latency_microseconds",
        "Minimum event processing latency in microseconds");

    private static readonly Gauge MaxLatencyUs = Prometheus.Metrics.CreateGauge(
        "mdc_max_latency_microseconds",
        "Maximum event processing latency in microseconds");

    // GC and memory metrics
    private static readonly Counter Gc0Collections = Prometheus.Metrics.CreateCounter(
        "mdc_gc_gen0_collections_total",
        "Total number of Generation 0 garbage collections");

    private static readonly Counter Gc1Collections = Prometheus.Metrics.CreateCounter(
        "mdc_gc_gen1_collections_total",
        "Total number of Generation 1 garbage collections");

    private static readonly Counter Gc2Collections = Prometheus.Metrics.CreateCounter(
        "mdc_gc_gen2_collections_total",
        "Total number of Generation 2 garbage collections");

    private static readonly Gauge MemoryUsageMb = Prometheus.Metrics.CreateGauge(
        "mdc_memory_usage_megabytes",
        "Current memory usage in megabytes");

    private static readonly Gauge HeapSizeMb = Prometheus.Metrics.CreateGauge(
        "mdc_heap_size_megabytes",
        "Current GC heap size in megabytes");

    // Symbol-level metrics (with labels)
    private static readonly Counter TradesBySymbol = Prometheus.Metrics.CreateCounter(
        "mdc_trades_by_symbol_total",
        "Total number of trades per symbol",
        new CounterConfiguration
        {
            LabelNames = new[] { "symbol", "venue" }
        });

    private static readonly Gauge LastTradePrice = Prometheus.Metrics.CreateGauge(
        "mdc_last_trade_price",
        "Last trade price per symbol",
        new GaugeConfiguration
        {
            LabelNames = new[] { "symbol" }
        });

    private static readonly Histogram TradeSizeDistribution = Prometheus.Metrics.CreateHistogram(
        "mdc_trade_size",
        "Distribution of trade sizes",
        new HistogramConfiguration
        {
            LabelNames = new[] { "symbol" },
            Buckets = new[] { 1, 10, 50, 100, 500, 1000, 5000, 10000, 50000 }
        });

    /// <summary>
    /// Updates all Prometheus metrics from the current Metrics snapshot.
    /// Should be called periodically (e.g., every 1-5 seconds) to keep metrics current.
    /// </summary>
    public static void UpdateFromSnapshot()
    {
        var snapshot = Metrics.GetSnapshot();

        // Update counters (Prometheus counters only increase, so we set to current value)
        PublishedEvents.IncTo(snapshot.Published);
        DroppedEvents.IncTo(snapshot.Dropped);
        IntegrityEvents.IncTo(snapshot.Integrity);
        TradeEvents.IncTo(snapshot.Trades);
        DepthUpdateEvents.IncTo(snapshot.DepthUpdates);
        QuoteEvents.IncTo(snapshot.Quotes);

        // Update rate gauges
        EventsPerSecond.Set(snapshot.EventsPerSecond);
        TradesPerSecond.Set(snapshot.TradesPerSecond);
        DepthUpdatesPerSecond.Set(snapshot.DepthUpdatesPerSecond);
        DropRatePercent.Set(snapshot.DropRate);

        // Update latency gauges
        AverageLatencyUs.Set(snapshot.AverageLatencyUs);
        MinLatencyUs.Set(snapshot.MinLatencyUs);
        MaxLatencyUs.Set(snapshot.MaxLatencyUs);

        // Update GC counters
        Gc0Collections.IncTo(snapshot.Gc0Collections);
        Gc1Collections.IncTo(snapshot.Gc1Collections);
        Gc2Collections.IncTo(snapshot.Gc2Collections);

        // Update memory gauges
        MemoryUsageMb.Set(snapshot.MemoryUsageMb);
        HeapSizeMb.Set(snapshot.HeapSizeMb);
    }

    /// <summary>
    /// Records a trade event with symbol and venue labels.
    /// </summary>
    public static void RecordTrade(string symbol, string venue, decimal price, int size)
    {
        TradesBySymbol.WithLabels(symbol, venue).Inc();
        LastTradePrice.WithLabels(symbol).Set((double)price);
        TradeSizeDistribution.WithLabels(symbol).Observe(size);
    }

    /// <summary>
    /// Records event processing latency in microseconds.
    /// </summary>
    public static void RecordProcessingLatency(double latencyMicroseconds)
    {
        ProcessingLatency.Observe(latencyMicroseconds);
    }

    /// <summary>
    /// Gets the Prometheus metrics registry for HTTP export.
    /// Use this with Prometheus.MetricServer or ASP.NET Core middleware.
    /// </summary>
    public static CollectorRegistry Registry => Prometheus.Metrics.DefaultRegistry;
}

/// <summary>
/// Background service that periodically updates Prometheus metrics.
/// </summary>
public class PrometheusMetricsUpdater : IAsyncDisposable
{
    private readonly PeriodicTimer _timer;
    private readonly Task _updateTask;
    private readonly CancellationTokenSource _cts = new();

    public PrometheusMetricsUpdater(TimeSpan updateInterval)
    {
        _timer = new PeriodicTimer(updateInterval);
        _updateTask = UpdateLoopAsync();
    }

    private async Task UpdateLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                PrometheusMetrics.UpdateFromSnapshot();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _timer.Dispose();
        try
        {
            await _updateTask;
        }
        catch
        {
            // Ignore
        }
        _cts.Dispose();
    }
}
