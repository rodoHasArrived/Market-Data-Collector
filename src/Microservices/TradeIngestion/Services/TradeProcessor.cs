using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading;
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
    private readonly IDeadLetterQueue _deadLetterQueue;
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
    private long _storageRetries;
    private long _totalLatencyTicks;

    public TradeProcessor(
        ITradeStorage storage,
        ITradeValidator validator,
        IDeadLetterQueue deadLetterQueue,
        TradeMetrics metrics,
        TradeServiceConfig config)
    {
        _storage = storage;
        _validator = validator;
        _deadLetterQueue = deadLetterQueue;
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
            StorageRetries: Interlocked.Read(ref _storageRetries),
            DeadLetterCount: _deadLetterQueue.Count,
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
                    var written = await WriteBatchWithRetryAsync(batch, ct);
                    if (written)
                    {
                        Interlocked.Add(ref _processed, batch.Count);
                        _metrics.RecordProcessed(batch.Count);
                    }
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
                var written = await WriteBatchWithRetryAsync(batch, ct);
                if (written)
                {
                    Interlocked.Add(ref _processed, batch.Count);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.RecordProcessorError();
            _log.Error(ex, "Processor {Id} encountered unrecoverable error", processorId);

            // Move remaining batch to dead letter queue
            if (batch.Count > 0)
            {
                await _deadLetterQueue.EnqueueBatchAsync(batch, "processor_error", ex, ct);
                _log.Warning(
                    "Moved {Count} trades to dead letter queue due to processor error",
                    batch.Count);
            }
        }

        _log.Debug("Processor {Id} stopped", processorId);
    }

    private ProcessedTrade? ProcessSingleTrade(ProcessedTrade trade)
    {
        var sw = Stopwatch.GetTimestamp();

        try
        {
            // Deduplication check with metrics
            if (_config.Processing.EnableDeduplication && !string.IsNullOrEmpty(trade.TradeId))
            {
                var key = $"{trade.Symbol}:{trade.TradeId}";
                if (!_recentTradeIds.TryAdd(key, 0))
                {
                    Interlocked.Increment(ref _duplicates);
                    _metrics.RecordDuplicate();
                    _metrics.RecordDeduplicationFiltered("duplicate_trade_id");
                    return null;
                }
            }
            else if (_config.Processing.EnableDeduplication && string.IsNullOrEmpty(trade.TradeId))
            {
                // Track trades without trade IDs that skip deduplication
                _metrics.RecordDeduplicationFiltered("missing_trade_id");
            }

            // Validation
            var validationResult = _validator.Validate(trade);
            if (!validationResult.IsValid)
            {
                Interlocked.Increment(ref _validationErrors);
                _metrics.RecordValidationError();

                if (_config.Validation.RejectInvalid)
                {
                    _log.Warning(
                        "Trade rejected for {Symbol} (TradeId={TradeId}): {ValidationErrors}",
                        trade.Symbol,
                        trade.TradeId ?? "N/A",
                        string.Join("; ", validationResult.Errors));

                    // Send rejected trades to dead letter queue for data loss prevention
                    _ = _deadLetterQueue.EnqueueAsync(trade, "validation_rejected");
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
            // Known error scenarios:
            // - Symbol state lock contention (rare, recoverable)
            // - Validation errors (handled above)
            // - Unexpected serialization issues
            _log.Warning(ex, "Error processing trade for {Symbol}", trade.Symbol);

            // Send to dead letter queue for later analysis
            _ = _deadLetterQueue.EnqueueAsync(trade, "processing_error", ex);
            return null;
        }
    }

    /// <summary>
    /// Writes a batch with exponential backoff retry for transient failures.
    /// On permanent failure, moves trades to dead letter queue.
    /// </summary>
    private async Task<bool> WriteBatchWithRetryAsync(
        List<ProcessedTrade> batch,
        CancellationToken ct)
    {
        var maxRetries = _config.DeadLetter.MaxRetryAttempts;
        var baseDelay = _config.DeadLetter.RetryBaseDelayMs;
        var maxDelay = _config.DeadLetter.RetryMaxDelayMs;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var sw = Stopwatch.GetTimestamp();
                await _storage.WriteBatchAsync(batch);

                // Record retry metrics if this wasn't the first attempt
                if (attempt > 0)
                {
                    var latencyMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
                    _metrics.RecordRetryLatency(latencyMs);
                    _log.Information(
                        "Storage write succeeded on attempt {Attempt} for {Count} trades",
                        attempt + 1,
                        batch.Count);
                }

                return true;
            }
            catch (Exception ex) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref _storageRetries);
                _metrics.RecordStorageRetry(attempt + 1);

                // Calculate exponential backoff delay with jitter
                var delay = Math.Min(baseDelay * (1 << attempt), maxDelay);
                var jitter = Random.Shared.Next(0, delay / 4);
                var totalDelay = delay + jitter;

                _log.Warning(
                    ex,
                    "Storage write failed on attempt {Attempt}/{MaxRetries} for {Count} trades. Retrying in {Delay}ms",
                    attempt + 1,
                    maxRetries + 1,
                    batch.Count,
                    totalDelay);

                await Task.Delay(totalDelay, ct);
            }
            catch (Exception ex)
            {
                // Final attempt failed or cancellation requested
                _log.Error(
                    ex,
                    "Storage write permanently failed after {Attempts} attempts for {Count} trades",
                    attempt + 1,
                    batch.Count);

                // Move to dead letter queue
                if (_config.DeadLetter.Enabled)
                {
                    await _deadLetterQueue.EnqueueBatchAsync(batch, "storage_failure", ex, ct);
                    _log.Warning(
                        "Moved {Count} trades to dead letter queue after storage failure",
                        batch.Count);
                }

                return false;
            }
        }

        return false;
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
    long StorageRetries,
    int DeadLetterCount,
    int QueueDepth,
    double AverageLatencyMs
);
