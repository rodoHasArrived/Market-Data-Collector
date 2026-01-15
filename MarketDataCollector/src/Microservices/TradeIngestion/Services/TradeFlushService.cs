using DataIngestion.TradeService.Configuration;
using Serilog;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Background service that periodically flushes trade data to storage.
/// Implements graceful shutdown to prevent data loss.
/// </summary>
public sealed class TradeFlushService : BackgroundService
{
    private readonly ITradeStorage _storage;
    private readonly ITradeProcessor _processor;
    private readonly TradeServiceConfig _config;
    private readonly Serilog.ILogger _log = Log.ForContext<TradeFlushService>();
    private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);

    public TradeFlushService(
        ITradeStorage storage,
        ITradeProcessor processor,
        TradeServiceConfig config)
    {
        _storage = storage;
        _processor = processor;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Information("Trade flush service starting...");

        // Start the processor
        await _processor.StartAsync(stoppingToken);

        var flushInterval = TimeSpan.FromMilliseconds(_config.Processing.FlushIntervalMs);
        _log.Information("Trade flush service started with {Interval}ms flush interval", _config.Processing.FlushIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(flushInterval, stoppingToken);
                await _storage.FlushAsync();

                // Log statistics periodically
                var procStats = _processor.GetStatistics();
                var storageStats = _storage.GetStatistics();

                _log.Debug(
                    "Trade service stats: Submitted={Submitted}, Processed={Processed}, " +
                    "Queue={Queue}, Written={Written}, AvgLatency={Latency:F2}ms",
                    procStats.Submitted,
                    procStats.Processed,
                    procStats.QueueDepth,
                    storageStats.TradesWritten,
                    procStats.AverageLatencyMs);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error during trade flush");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Information("Trade flush service stopping - flushing remaining buffers...");

        using var timeoutCts = new CancellationTokenSource(_shutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            // Stop the processor first (which drains its channel)
            await _processor.StopAsync().WaitAsync(linkedCts.Token);
            _log.Debug("Processor stopped successfully");

            // Final storage flush to ensure all data is persisted
            await _storage.FlushAsync().WaitAsync(linkedCts.Token);
            _log.Information("Trade flush service stopped - all buffers flushed successfully");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _log.Warning("Shutdown timeout ({Timeout}s) reached - some trade data may be lost",
                _shutdownTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during graceful shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
