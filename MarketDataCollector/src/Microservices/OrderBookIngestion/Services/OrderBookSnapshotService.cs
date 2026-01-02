using DataIngestion.OrderBookService.Configuration;
using Serilog;

namespace DataIngestion.OrderBookService.Services;

/// <summary>
/// Background service for periodic order book snapshots.
/// Implements graceful shutdown to prevent data loss.
/// </summary>
public sealed class OrderBookSnapshotService : BackgroundService
{
    private readonly IOrderBookManager _manager;
    private readonly IOrderBookStorage _storage;
    private readonly OrderBookServiceConfig _config;
    private readonly OrderBookMetrics _metrics;
    private readonly ILogger _log = Log.ForContext<OrderBookSnapshotService>();
    private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);

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

        _log.Information("Order book snapshot service starting with {Interval}ms interval",
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
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Information("Order book snapshot service stopping - flushing remaining data...");

        using var timeoutCts = new CancellationTokenSource(_shutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            // Final snapshot of any changed books
            var booksToSnapshot = _manager.GetChangedBooks().ToList();
            if (booksToSnapshot.Count > 0)
            {
                await _storage.WriteBatchAsync(booksToSnapshot).WaitAsync(linkedCts.Token);
                _log.Debug("Final snapshot of {Count} order books", booksToSnapshot.Count);
            }

            // Flush storage
            await _storage.FlushAsync().WaitAsync(linkedCts.Token);
            _log.Information("Order book snapshot service stopped - all data flushed successfully");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _log.Warning("Shutdown timeout ({Timeout}s) reached - some order book data may be lost",
                _shutdownTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during graceful shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
