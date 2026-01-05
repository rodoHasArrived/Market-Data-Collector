#if STOCKSHARP
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
#endif
using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Interfaces;
using Serilog;

namespace MarketDataCollector.Storage.StockSharp;

/// <summary>
/// Storage sink that writes market data to StockSharp's binary format.
/// Provides 10-25x compression compared to JSONL:
/// - Trade ticks: ~2 bytes per trade
/// - Order book snapshots: ~7 bytes per snapshot
///
/// Features (Hydra-inspired):
/// - Batch writing for improved throughput
/// - Automatic flush with configurable interval
/// - Thread-safe buffering for high-frequency data
///
/// The binary format is compatible with S#.Designer backtesting platform.
/// </summary>
public sealed class StockSharpStorageSink : IStorageSink
{
    private readonly ILogger _log = LoggingSetup.ForContext<StockSharpStorageSink>();
    private readonly string _storagePath;
    private readonly bool _useBinaryFormat;

#if STOCKSHARP
    private readonly StorageRegistry _storageRegistry;
    private readonly IExchangeInfoProvider _exchangeInfoProvider;
    private readonly StorageFormats _format;
    // Use ConcurrentDictionary for lock-free thread-safe access (improved Hydra pattern)
    // This eliminates lock contention during high-frequency security lookups
    private readonly ConcurrentDictionary<string, Security> _securities = new();

    // Batch writing support (Hydra-inspired pattern)
    private readonly ConcurrentQueue<(SecurityId SecId, ExecutionMessage Msg)> _tradeBatch = new();
    private readonly ConcurrentQueue<(SecurityId SecId, QuoteChangeMessage Msg)> _depthBatch = new();
    private readonly ConcurrentQueue<(SecurityId SecId, Level1ChangeMessage Msg)> _level1Batch = new();
    private readonly ConcurrentQueue<(SecurityId SecId, TimeFrameCandleMessage Msg)> _candleBatch = new();

    private readonly Timer _flushTimer;
    private readonly int _batchSize;
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);
    private const int DefaultBatchSize = 1000;
#endif

    private long _eventsWritten;
    private long _eventsBuffered;
    private bool _disposed;

    /// <summary>
    /// Creates a new StockSharp storage sink.
    /// </summary>
    /// <param name="storagePath">Path to the storage directory.</param>
    /// <param name="useBinaryFormat">If true, uses binary format (2 bytes/trade). If false, uses CSV.</param>
    /// <param name="batchSize">Number of events to buffer before writing (default: 1000).</param>
    /// <param name="flushInterval">Interval for automatic flush (default: 5 seconds).</param>
    public StockSharpStorageSink(
        string storagePath,
        bool useBinaryFormat = true,
        int batchSize = 1000,
        TimeSpan? flushInterval = null)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        _useBinaryFormat = useBinaryFormat;

#if STOCKSHARP
        _format = useBinaryFormat ? StorageFormats.Binary : StorageFormats.Csv;
        _batchSize = batchSize > 0 ? batchSize : DefaultBatchSize;

        // Initialize StockSharp storage registry
        _exchangeInfoProvider = new InMemoryExchangeInfoProvider();
        _storageRegistry = new StorageRegistry(_exchangeInfoProvider)
        {
            DefaultDrive = new LocalMarketDataDrive(storagePath)
        };

        // Initialize automatic flush timer (Hydra pattern)
        var interval = flushInterval ?? DefaultFlushInterval;
        _flushTimer = new Timer(
            _ => _ = FlushBatchesAsync(),
            null,
            interval,
            interval);

        Directory.CreateDirectory(storagePath);
        _log.Information("StockSharp storage initialized at {Path} with format {Format}, " +
            "batch size {BatchSize}, flush interval {FlushInterval}s",
            storagePath, _format, _batchSize, interval.TotalSeconds);
#else
        Directory.CreateDirectory(storagePath);
        _log.Warning("StockSharp packages not installed. Storage sink will not persist data. " +
            "Install StockSharp.Algo to enable binary storage.");
#endif
    }

    /// <summary>
    /// Number of events written to storage.
    /// </summary>
    public long EventsWritten => _eventsWritten;

    /// <summary>
    /// Number of events currently buffered.
    /// </summary>
    public long EventsBuffered => _eventsBuffered;

    /// <summary>
    /// Append a market event to storage.
    /// Uses batch buffering for improved throughput (Hydra pattern).
    /// </summary>
    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StockSharpStorageSink));

#if STOCKSHARP
        if (evt?.Payload == null) return;

        var security = GetOrCreateSecurity(evt.Symbol);
        var securityId = security.ToSecurityId();

        try
        {
            switch (evt.Type)
            {
                case MarketEventType.Trade when evt.Payload is Trade trade:
                    BufferTrade(securityId, trade);
                    break;

                case MarketEventType.L2Snapshot when evt.Payload is LOBSnapshot lob:
                    BufferDepth(securityId, lob);
                    break;

                case MarketEventType.BboQuote when evt.Payload is BboQuotePayload bbo:
                    BufferLevel1(securityId, bbo);
                    break;

                case MarketEventType.HistoricalBar when evt.Payload is HistoricalBar bar:
                    BufferCandle(securityId, bar);
                    break;

                default:
                    // Unsupported event type, skip silently
                    return;
            }

            Interlocked.Increment(ref _eventsBuffered);

            // Flush if batch size reached
            if (_eventsBuffered >= _batchSize)
            {
                await FlushBatchesAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error buffering event to StockSharp storage: {Type} {Symbol}",
                evt.Type, evt.Symbol);
        }
#else
        await Task.CompletedTask;
#endif
    }

#if STOCKSHARP

    #region Buffering Methods (Hydra Pattern)

    /// <summary>
    /// Buffer a trade message for batch writing.
    /// </summary>
    private void BufferTrade(SecurityId securityId, Trade trade)
    {
        var msg = new ExecutionMessage
        {
            SecurityId = securityId,
            ServerTime = trade.Timestamp,
            TradePrice = trade.Price,
            TradeVolume = trade.Size,
            OriginSide = trade.Aggressor switch
            {
                AggressorSide.Buy => Sides.Buy,
                AggressorSide.Sell => Sides.Sell,
                _ => null
            },
            DataTypeEx = DataType.Ticks
        };

        if (!string.IsNullOrEmpty(trade.StreamId) && long.TryParse(trade.StreamId, out var tradeId))
            msg.TradeId = tradeId;

        _tradeBatch.Enqueue((securityId, msg));
    }

    /// <summary>
    /// Buffer a depth message for batch writing.
    /// </summary>
    private void BufferDepth(SecurityId securityId, LOBSnapshot lob)
    {
        var msg = new QuoteChangeMessage
        {
            SecurityId = securityId,
            ServerTime = lob.Timestamp,
            Bids = lob.Bids.Select(b => new QuoteChange(b.Price, b.Size)).ToArray(),
            Asks = lob.Asks.Select(a => new QuoteChange(a.Price, a.Size)).ToArray()
        };

        _depthBatch.Enqueue((securityId, msg));
    }

    /// <summary>
    /// Buffer a Level1/BBO message for batch writing.
    /// </summary>
    private void BufferLevel1(SecurityId securityId, BboQuotePayload bbo)
    {
        var msg = new Level1ChangeMessage
        {
            SecurityId = securityId,
            ServerTime = bbo.Timestamp
        };

        msg.TryAdd(Level1Fields.BestBidPrice, bbo.BidPrice);
        msg.TryAdd(Level1Fields.BestBidVolume, (decimal)bbo.BidSize);
        msg.TryAdd(Level1Fields.BestAskPrice, bbo.AskPrice);
        msg.TryAdd(Level1Fields.BestAskVolume, (decimal)bbo.AskSize);

        if (bbo.MidPrice.HasValue)
            msg.TryAdd(Level1Fields.LastTradePrice, bbo.MidPrice.Value);

        _level1Batch.Enqueue((securityId, msg));
    }

    /// <summary>
    /// Buffer a candle message for batch writing.
    /// </summary>
    private void BufferCandle(SecurityId securityId, HistoricalBar bar)
    {
        var msg = new TimeFrameCandleMessage
        {
            SecurityId = securityId,
            OpenTime = bar.SessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            OpenPrice = bar.Open,
            HighPrice = bar.High,
            LowPrice = bar.Low,
            ClosePrice = bar.Close,
            TotalVolume = bar.Volume,
            State = CandleStates.Finished
        };

        _candleBatch.Enqueue((securityId, msg));
    }

    #endregion

    #region Batch Flush Methods

    /// <summary>
    /// Flush all batched messages to storage (Hydra pattern).
    /// Uses parallel flush for all data types to maximize throughput.
    /// </summary>
    private async Task FlushBatchesAsync(CancellationToken ct = default)
    {
        if (_disposed) return;

        var flushedCount = 0;

        try
        {
            // Flush all batches in parallel for better I/O throughput
            // Why parallel is better: Each storage type writes to separate files,
            // so we can utilize multiple I/O streams simultaneously
            var tasks = new[]
            {
                FlushBatchAsync(_tradeBatch, GetTickStorage, ct),
                FlushBatchAsync(_depthBatch, GetQuoteStorage, ct),
                FlushBatchAsync(_level1Batch, GetLevel1Storage, ct),
                FlushBatchAsync(_candleBatch, GetCandleStorage, ct)
            };

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            flushedCount = results.Sum();

            if (flushedCount > 0)
            {
                Interlocked.Add(ref _eventsWritten, flushedCount);
                Interlocked.Add(ref _eventsBuffered, -flushedCount);
                _log.Debug("Flushed {Count} events to StockSharp storage", flushedCount);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error flushing batches to StockSharp storage");
        }
    }

    /// <summary>
    /// Generic batch flush method that handles any message type.
    ///
    /// Why this is better than the previous implementation:
    /// 1. DRY: Single implementation for all message types eliminates 80+ lines of duplicated code
    /// 2. Maintainability: Bug fixes and improvements apply to all message types automatically
    /// 3. Consistency: Ensures all flush operations behave identically
    /// 4. Extensibility: Adding new message types requires only a new storage getter delegate
    /// 5. Testability: Generic method can be tested once with different message types
    /// </summary>
    /// <typeparam name="TMessage">The StockSharp message type (ExecutionMessage, QuoteChangeMessage, etc.)</typeparam>
    /// <param name="batch">The concurrent queue containing batched messages.</param>
    /// <param name="getStorage">Factory function to get the appropriate storage for a SecurityId.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of messages flushed.</returns>
    private async Task<int> FlushBatchAsync<TMessage>(
        ConcurrentQueue<(SecurityId SecId, TMessage Msg)> batch,
        Func<SecurityId, IMarketDataStorage<TMessage>> getStorage,
        CancellationToken ct) where TMessage : Message
    {
        // Drain queue and group by security for batch saving
        // Using Dictionary with TryGetValue pattern for efficient grouping
        var messagesBySecurityId = new Dictionary<SecurityId, List<TMessage>>();

        while (batch.TryDequeue(out var item))
        {
            if (!messagesBySecurityId.TryGetValue(item.SecId, out var list))
            {
                list = new List<TMessage>();
                messagesBySecurityId[item.SecId] = list;
            }
            list.Add(item.Msg);
        }

        if (messagesBySecurityId.Count == 0)
            return 0;

        // Perform I/O on background thread to avoid blocking
        int count = 0;
        await Task.Run(() =>
        {
            foreach (var (securityId, messages) in messagesBySecurityId)
            {
                ct.ThrowIfCancellationRequested();
                var storage = getStorage(securityId);
                storage.Save(messages);
                count += messages.Count;
            }
        }, ct).ConfigureAwait(false);

        return count;
    }

    // Storage getter delegates - cleaner separation of storage access logic
    private IMarketDataStorage<ExecutionMessage> GetTickStorage(SecurityId secId) =>
        _storageRegistry.GetTickMessageStorage(secId, _format);

    private IMarketDataStorage<QuoteChangeMessage> GetQuoteStorage(SecurityId secId) =>
        _storageRegistry.GetQuoteMessageStorage(secId, _format);

    private IMarketDataStorage<Level1ChangeMessage> GetLevel1Storage(SecurityId secId) =>
        _storageRegistry.GetLevel1MessageStorage(secId, _format);

    private IMarketDataStorage<TimeFrameCandleMessage> GetCandleStorage(SecurityId secId) =>
        _storageRegistry.GetCandleMessageStorage(secId, typeof(TimeFrameCandleMessage), TimeSpan.FromDays(1), _format);

    #endregion

    /// <summary>
    /// Get or create a StockSharp Security for a symbol.
    /// Uses ConcurrentDictionary.GetOrAdd for lock-free, thread-safe access.
    ///
    /// Why this is better than the previous lock-based approach:
    /// 1. Lock-free: No lock contention under high-frequency concurrent access
    /// 2. Atomic: GetOrAdd is an atomic operation ensuring single creation per key
    /// 3. Performance: ConcurrentDictionary uses fine-grained locking internally,
    ///    allowing multiple threads to read/write different buckets simultaneously
    /// 4. Simpler: Cleaner code without explicit lock management
    /// </summary>
    private Security GetOrCreateSecurity(string symbol)
    {
        return _securities.GetOrAdd(symbol, CreateSecurity);
    }

    /// <summary>
    /// Factory method for creating Security objects.
    /// Separated for clarity and potential future customization (e.g., per-exchange boards).
    /// </summary>
    private static Security CreateSecurity(string symbol)
    {
        return new Security
        {
            Id = symbol,
            Code = symbol,
            Board = ExchangeBoard.Nyse // Default, could be enhanced to detect exchange from symbol
        };
    }
#endif

    /// <summary>
    /// Flush any buffered data to disk.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
#if STOCKSHARP
        // Flush all pending batches (Hydra pattern)
        await FlushBatchesAsync(ct).ConfigureAwait(false);
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Dispose of the storage sink.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

#if STOCKSHARP
        // Stop the flush timer
        await _flushTimer.DisposeAsync().ConfigureAwait(false);
#endif

        // Flush any remaining buffered data
        await FlushAsync().ConfigureAwait(false);

#if STOCKSHARP
        // ConcurrentDictionary.Clear() is thread-safe, no lock needed
        _securities.Clear();
#endif

        _log.Information("StockSharp storage disposed. Total events written: {Count}, buffered: {Buffered}",
            _eventsWritten, _eventsBuffered);
    }
}
