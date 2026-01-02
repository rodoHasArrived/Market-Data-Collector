using DataIngestion.OrderBookService.Configuration;
using Serilog;

namespace DataIngestion.OrderBookService.Services;

/// <summary>
/// Background service for periodic order book snapshots.
/// </summary>
public sealed class OrderBookSnapshotService : BackgroundService
{
    private readonly IOrderBookManager _manager;
    private readonly IOrderBookStorage _storage;
    private readonly OrderBookServiceConfig _config;
    private readonly OrderBookMetrics _metrics;
    private readonly ILogger _log = Log.ForContext<OrderBookSnapshotService>();

    public OrderBookSnapshotService(
        IOrderBookManager manager,
        IOrderBookStorage storage,
        OrderBookServiceConfig config,
        OrderBookMetrics metrics)
    {
        _manager = manager;
        _storage = storage;
        _config = config;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Snapshot.EnableStorage)
        {
            _log.Information("Order book snapshot storage is disabled");
            return;
        }

        _log.Information("Order book snapshot service started with {Interval}ms interval",
            _config.Snapshot.IntervalMs);

        var interval = TimeSpan.FromMilliseconds(_config.Snapshot.IntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                var booksToSnapshot = _config.Snapshot.OnlyIfChanged
                    ? _manager.GetChangedBooks().ToList()
                    : _manager.GetAllOrderBooks().ToList();

                if (booksToSnapshot.Count > 0)
                {
                    await _storage.WriteBatchAsync(booksToSnapshot);
                    _manager.MarkSnapshotted(booksToSnapshot.Select(b => b.Symbol));

                    _log.Debug("Snapshotted {Count} order books", booksToSnapshot.Count);
                }

                // Periodic status logging
                if (DateTime.UtcNow.Second % 30 == 0)
                {
                    _log.Information(
                        "OrderBook stats: Active={Active}, Snapshots={Snapshots}, Updates={Updates}",
                        _manager.GetActiveBookCount(),
                        _metrics.SnapshotsProcessed,
                        _metrics.UpdatesProcessed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error during order book snapshot");
            }
        }

        await _storage.FlushAsync();
        _log.Information("Order book snapshot service stopped");
    }
}
