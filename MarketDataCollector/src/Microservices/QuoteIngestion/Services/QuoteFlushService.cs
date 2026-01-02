using DataIngestion.QuoteService.Configuration;
using Serilog;

namespace DataIngestion.QuoteService.Services;

/// <summary>
/// Background service that periodically flushes quote data to storage.
/// Implements graceful shutdown to prevent data loss.
/// </summary>
public sealed class QuoteFlushService : BackgroundService
{
    private readonly IQuoteProcessor _processor;
    private readonly IQuoteStorage _storage;
    private readonly QuoteServiceConfig _config;
    private readonly ILogger _log = Log.ForContext<QuoteFlushService>();
    private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);

    public QuoteFlushService(IQuoteProcessor processor, IQuoteStorage storage, QuoteServiceConfig config)
    {
        _processor = processor;
        _storage = storage;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Information("Quote flush service starting...");
        await _processor.StartAsync(stoppingToken);
        var interval = TimeSpan.FromMilliseconds(_config.Processing.FlushIntervalMs);
        _log.Information("Quote flush service started with {Interval}ms flush interval", _config.Processing.FlushIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await _storage.FlushAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error during quote flush");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Information("Quote flush service stopping - flushing remaining buffers...");

        using var timeoutCts = new CancellationTokenSource(_shutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            // Stop the processor first (which drains its channel and flushes storage)
            await _processor.StopAsync().WaitAsync(linkedCts.Token);
            _log.Debug("Processor stopped successfully");

            // Final storage flush to ensure all data is persisted
            await _storage.FlushAsync().WaitAsync(linkedCts.Token);
            _log.Information("Quote flush service stopped - all buffers flushed successfully");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _log.Warning("Shutdown timeout ({Timeout}s) reached - some quote data may be lost",
                _shutdownTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during graceful shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
