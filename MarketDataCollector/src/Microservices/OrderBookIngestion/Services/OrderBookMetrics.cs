using Prometheus;

namespace DataIngestion.OrderBookService.Services;

public sealed class OrderBookMetrics
{
    private readonly Counter _snapshotsProcessed;
    private readonly Counter _updatesProcessed;
    private readonly Counter _integrityErrors;
    private readonly Gauge _activeBooks;

    private long _totalSnapshots;
    private long _totalUpdates;
    private long _totalErrors;

    public long SnapshotsProcessed => _totalSnapshots;
    public long UpdatesProcessed => _totalUpdates;
    public long IntegrityErrors => _totalErrors;

    public OrderBookMetrics()
    {
        _snapshotsProcessed = Metrics.CreateCounter(
            "orderbook_snapshots_total", "Total snapshots processed",
            new CounterConfiguration { LabelNames = ["symbol"] });

        _updatesProcessed = Metrics.CreateCounter(
            "orderbook_updates_total", "Total updates processed",
            new CounterConfiguration { LabelNames = ["symbol"] });

        _integrityErrors = Metrics.CreateCounter(
            "orderbook_integrity_errors_total", "Total integrity errors",
            new CounterConfiguration { LabelNames = ["symbol", "type"] });

        _activeBooks = Metrics.CreateGauge(
            "orderbook_active_count", "Number of active order books");
    }

    public void RecordSnapshot(string symbol)
    {
        Interlocked.Increment(ref _totalSnapshots);
        _snapshotsProcessed.WithLabels(symbol).Inc();
    }

    public void RecordUpdate(string symbol)
    {
        Interlocked.Increment(ref _totalUpdates);
        _updatesProcessed.WithLabels(symbol).Inc();
    }

    public void RecordIntegrityError(string symbol, string errorType)
    {
        Interlocked.Increment(ref _totalErrors);
        _integrityErrors.WithLabels(symbol, errorType).Inc();
    }

    public void SetActiveBooks(int count) => _activeBooks.Set(count);
}
