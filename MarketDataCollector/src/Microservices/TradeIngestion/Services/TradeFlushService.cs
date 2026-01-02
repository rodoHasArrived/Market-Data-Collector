using DataIngestion.TradeService.Configuration;
using Serilog;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Background service to periodically flush trade storage.
/// </summary>
public sealed class TradeFlushService : BackgroundService
{
    private readonly ITradeStorage _storage;
    private readonly ITradeProcessor _processor;
    private readonly TradeServiceConfig _config;
    private readonly ILogger _log = Log.ForContext<TradeFlushService>();

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
        _log.Information("Trade flush service started");

        // Start the processor
        await _processor.StartAsync(stoppingToken);

        var flushInterval = TimeSpan.FromMilliseconds(_config.Processing.FlushIntervalMs);

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

        // Final cleanup
        await _processor.StopAsync();
        await _storage.FlushAsync();

        _log.Information("Trade flush service stopped");
    }
}
