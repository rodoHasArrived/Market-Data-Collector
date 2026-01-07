using System.Collections.Concurrent;
using System.Threading.Channels;
using DataIngestion.QuoteService.Configuration;
using DataIngestion.QuoteService.Models;
using Serilog;

namespace DataIngestion.QuoteService.Services;

public interface IQuoteProcessor
{
    bool TrySubmit(ProcessedQuote quote);
    ProcessedQuote? GetLatestQuote(string symbol);
    ProcessingStats GetStats();
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}

public sealed class QuoteProcessor : IQuoteProcessor, IAsyncDisposable
{
    private readonly Channel<ProcessedQuote> _channel;
    private readonly IQuoteStorage _storage;
    private readonly QuoteMetrics _metrics;
    private readonly QuoteServiceConfig _config;
    private readonly Serilog.ILogger _log = Log.ForContext<QuoteProcessor>();
    private readonly ConcurrentDictionary<string, ProcessedQuote> _latestQuotes = new();
    private readonly List<Task> _processorTasks = [];
    private CancellationTokenSource? _cts;
    private long _processed;

    public QuoteProcessor(IQuoteStorage storage, QuoteMetrics metrics, QuoteServiceConfig config)
    {
        _storage = storage;
        _metrics = metrics;
        _config = config;
        _channel = Channel.CreateBounded<ProcessedQuote>(new BoundedChannelOptions(
            config.Processing.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public bool TrySubmit(ProcessedQuote quote)
    {
        // Validate and enrich
        var enrichedQuote = EnrichQuote(quote);

        if (_channel.Writer.TryWrite(enrichedQuote))
        {
            _latestQuotes[quote.Symbol] = enrichedQuote;
            _metrics.RecordSubmission();
            return true;
        }

        _metrics.RecordDropped();
        return false;
    }

    public ProcessedQuote? GetLatestQuote(string symbol)
    {
        return _latestQuotes.TryGetValue(symbol, out var quote) ? quote : null;
    }

    public ProcessingStats GetStats() => new(
        Interlocked.Read(ref _processed),
        _channel.Reader.Count,
        _latestQuotes.Count
    );

    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _processorTasks.Add(Task.Run(() => ProcessLoopAsync(_cts.Token), _cts.Token));
        _log.Information("Quote processor started");
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _channel.Writer.Complete();
        _cts?.Cancel();
        try { await Task.WhenAll(_processorTasks).WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (OperationCanceledException) { }
        await _storage.FlushAsync();
        _log.Information("Quote processor stopped");
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        var batch = new List<ProcessedQuote>(_config.Processing.BatchSize);

        while (!ct.IsCancellationRequested)
        {
            while (batch.Count < _config.Processing.BatchSize && _channel.Reader.TryRead(out var quote))
            {
                batch.Add(quote);
            }

            if (batch.Count > 0)
            {
                await _storage.WriteBatchAsync(batch);
                Interlocked.Add(ref _processed, batch.Count);
                _metrics.RecordProcessed(batch.Count);
                batch.Clear();
            }

            if (batch.Count == 0)
            {
                try
                {
                    await _channel.Reader.WaitToReadAsync(ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        if (batch.Count > 0)
        {
            await _storage.WriteBatchAsync(batch);
            Interlocked.Add(ref _processed, batch.Count);
        }
    }

    private ProcessedQuote EnrichQuote(ProcessedQuote quote)
    {
        var spread = quote.AskPrice - quote.BidPrice;
        var midPrice = (quote.BidPrice + quote.AskPrice) / 2;
        var spreadBps = midPrice > 0 ? spread / midPrice * 10000 : 0;
        var isCrossed = quote.BidPrice > quote.AskPrice;
        var isLocked = quote.BidPrice == quote.AskPrice;

        if (isCrossed) _metrics.RecordCrossedQuote();
        if (isLocked) _metrics.RecordLockedQuote();

        return quote with
        {
            Spread = spread,
            SpreadBps = spreadBps,
            MidPrice = midPrice,
            IsCrossed = isCrossed,
            IsLocked = isLocked
        };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}

public record ProcessingStats(long Processed, int QueueDepth, int ActiveSymbols);
