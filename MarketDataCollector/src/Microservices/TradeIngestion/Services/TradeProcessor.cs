using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using DataIngestion.TradeService.Configuration;
using DataIngestion.TradeService.Models;
using Serilog;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// High-performance trade processor using channels.
/// </summary>
public interface ITradeProcessor
{
    /// <summary>Submit a trade for processing.</summary>
    bool TrySubmit(ProcessedTrade trade);

    /// <summary>Submit a batch of trades.</summary>
    int SubmitBatch(IEnumerable<ProcessedTrade> trades);

    /// <summary>Get processing statistics.</summary>
    ProcessingStatistics GetStatistics();

    /// <summary>Start processing.</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stop processing and flush pending trades.</summary>
    Task StopAsync();
}

/// <summary>
/// Channel-based high-throughput trade processor.
/// </summary>
public sealed class TradeProcessor : ITradeProcessor, IAsyncDisposable
{
    private readonly Channel<ProcessedTrade> _channel;
    private readonly ITradeStorage _storage;
    private readonly ITradeValidator _validator;
    private readonly TradeMetrics _metrics;
    private readonly TradeServiceConfig _config;
    private readonly Serilog.ILogger _log = Log.ForContext<TradeProcessor>();

    private readonly ConcurrentDictionary<string, SymbolState> _symbolStates = new();
    private readonly ConcurrentDictionary<string, byte> _recentTradeIds = new();
    private readonly List<Task> _processorTasks = [];
    private CancellationTokenSource? _cts;

    private long _submitted;
    private long _processed;
    private long _duplicates;
    private long _validationErrors;
    private long _totalLatencyTicks;

    public TradeProcessor(
        ITradeStorage storage,
        ITradeValidator validator,
        TradeMetrics metrics,
        TradeServiceConfig config)
    {
        _storage = storage;
        _validator = validator;
        _metrics = metrics;
        _config = config;

        _channel = Channel.CreateBounded<ProcessedTrade>(new BoundedChannelOptions(
            _config.Processing.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = false
        });
    }

    public bool TrySubmit(ProcessedTrade trade)
    {
        if (_channel.Writer.TryWrite(trade))
        {
            Interlocked.Increment(ref _submitted);
            _metrics.RecordSubmission();
            return true;
        }

        _metrics.RecordDropped();
        return false;
    }

    public int SubmitBatch(IEnumerable<ProcessedTrade> trades)
    {
        var count = 0;
        foreach (var trade in trades)
        {
            if (TrySubmit(trade)) count++;
        }
        return count;
    }

    public ProcessingStatistics GetStatistics()
    {
        return new ProcessingStatistics(
            Submitted: Interlocked.Read(ref _submitted),
            Processed: Interlocked.Read(ref _processed),
            Duplicates: Interlocked.Read(ref _duplicates),
            ValidationErrors: Interlocked.Read(ref _validationErrors),
            QueueDepth: _channel.Reader.Count,
            AverageLatencyMs: _processed > 0
                ? (double)Interlocked.Read(ref _totalLatencyTicks) / _processed / TimeSpan.TicksPerMillisecond
                : 0
        );
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Start multiple processor tasks
        for (var i = 0; i < _config.Processing.ProcessorCount; i++)
        {
            var processorId = i;
            _processorTasks.Add(Task.Run(() => ProcessLoopAsync(processorId, _cts.Token), _cts.Token));
        }

        // Start deduplication cleanup
        if (_config.Processing.EnableDeduplication)
        {
            _processorTasks.Add(Task.Run(() => DeduplicationCleanupLoopAsync(_cts.Token), _cts.Token));
        }

        _log.Information("Trade processor started with {Count} workers", _config.Processing.ProcessorCount);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _log.Information("Stopping trade processor...");

        _channel.Writer.Complete();
        _cts?.Cancel();

        try
        {
            await Task.WhenAll(_processorTasks).WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await _storage.FlushAsync();
        _log.Information("Trade processor stopped. Processed {Count} trades", _processed);
    }

    private async Task ProcessLoopAsync(int processorId, CancellationToken ct)
    {
        var batch = new List<ProcessedTrade>(_config.Processing.BatchSize);
        var lastFlush = Stopwatch.GetTimestamp();

        _log.Debug("Processor {Id} started", processorId);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var shouldFlush = false;

                // Try to read trades
                while (batch.Count < _config.Processing.BatchSize &&
                       _channel.Reader.TryRead(out var trade))
                {
                    var processedTrade = ProcessSingleTrade(trade);
                    if (processedTrade != null)
                    {
                        batch.Add(processedTrade);
                    }
                }

                // Check if we need to flush based on time
                var elapsed = Stopwatch.GetElapsedTime(lastFlush);
                if (elapsed.TotalMilliseconds >= _config.Processing.FlushIntervalMs && batch.Count > 0)
                {
                    shouldFlush = true;
                }

                // Flush full batch
                if (batch.Count >= _config.Processing.BatchSize)
                {
                    shouldFlush = true;
                }

                if (shouldFlush && batch.Count > 0)
                {
                    await _storage.WriteBatchAsync(batch);
                    Interlocked.Add(ref _processed, batch.Count);
                    _metrics.RecordProcessed(batch.Count);
                    batch.Clear();
                    lastFlush = Stopwatch.GetTimestamp();
                }

                // Wait for more data if batch is empty
                if (batch.Count == 0)
                {
                    try
                    {
                        var available = await _channel.Reader.WaitToReadAsync(ct);
                        if (!available) break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            // Final flush
            if (batch.Count > 0)
            {
                await _storage.WriteBatchAsync(batch);
                Interlocked.Add(ref _processed, batch.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Error(ex, "Processor {Id} error", processorId);
        }

        _log.Debug("Processor {Id} stopped", processorId);
    }

    private ProcessedTrade? ProcessSingleTrade(ProcessedTrade trade)
    {
        var sw = Stopwatch.GetTimestamp();

        try
        {
            // Deduplication check
            if (_config.Processing.EnableDeduplication && !string.IsNullOrEmpty(trade.TradeId))
            {
                var key = $"{trade.Symbol}:{trade.TradeId}";
                if (!_recentTradeIds.TryAdd(key, 0))
                {
                    Interlocked.Increment(ref _duplicates);
                    _metrics.RecordDuplicate();
                    return null;
                }
            }

            // Validation
            var validationResult = _validator.Validate(trade);
            if (!validationResult.IsValid)
            {
                Interlocked.Increment(ref _validationErrors);
                _metrics.RecordValidationError();

                if (_config.Validation.RejectInvalid)
                {
                    return null;
                }

                // Mark as invalid but continue
                trade = trade with { IsValid = false, ValidationErrors = validationResult.Errors };
            }

            // Update symbol state for sequence validation
            UpdateSymbolState(trade);

            // Record latency
            var latencyTicks = Stopwatch.GetTimestamp() - sw;
            Interlocked.Add(ref _totalLatencyTicks, latencyTicks);

            return trade;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error processing trade for {Symbol}", trade.Symbol);
            return null;
        }
    }

    private void UpdateSymbolState(ProcessedTrade trade)
    {
        var state = _symbolStates.GetOrAdd(trade.Symbol, _ => new SymbolState());

        lock (state)
        {
            // Sequence validation
            if (_config.Processing.ValidateSequence && trade.Sequence > 0)
            {
                if (trade.Sequence <= state.LastSequence && state.LastSequence > 0)
                {
                    // Out of order or duplicate sequence
                    _log.Warning("Out of order sequence for {Symbol}: {Current} <= {Last}",
                        trade.Symbol, trade.Sequence, state.LastSequence);
                }
                state.LastSequence = trade.Sequence;
            }

            // Update last price for change detection
            state.LastPrice = trade.Price;
            state.LastTimestamp = trade.Timestamp;
            state.TradeCount++;
        }
    }

    private async Task DeduplicationCleanupLoopAsync(CancellationToken ct)
    {
        var cleanupInterval = TimeSpan.FromSeconds(_config.Processing.DeduplicationWindowSeconds / 2.0);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, ct);

                // Clear old entries (simple approach - full clear)
                // In production, would use time-based expiration
                if (_recentTradeIds.Count > 100000)
                {
                    _recentTradeIds.Clear();
                    _log.Debug("Cleared deduplication cache");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }

    private class SymbolState
    {
        public long LastSequence { get; set; }
        public decimal LastPrice { get; set; }
        public DateTimeOffset LastTimestamp { get; set; }
        public long TradeCount { get; set; }
    }
}

/// <summary>
/// Processing statistics.
/// </summary>
public record ProcessingStatistics(
    long Submitted,
    long Processed,
    long Duplicates,
    long ValidationErrors,
    int QueueDepth,
    double AverageLatencyMs
);
