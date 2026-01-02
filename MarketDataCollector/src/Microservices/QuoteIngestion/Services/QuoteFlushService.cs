using DataIngestion.QuoteService.Configuration;
using Serilog;

namespace DataIngestion.QuoteService.Services;

public sealed class QuoteFlushService : BackgroundService
{
    private readonly IQuoteProcessor _processor;
    private readonly IQuoteStorage _storage;
    private readonly QuoteServiceConfig _config;
    private readonly ILogger _log = Log.ForContext<QuoteFlushService>();

    public QuoteFlushService(IQuoteProcessor processor, IQuoteStorage storage, QuoteServiceConfig config)
    {
        _processor = processor;
        _storage = storage;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _processor.StartAsync(stoppingToken);
        var interval = TimeSpan.FromMilliseconds(_config.Processing.FlushIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await _storage.FlushAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.Error(ex, "Error during quote flush"); }
        }

        await _processor.StopAsync();
    }
}
