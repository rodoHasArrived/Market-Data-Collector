#if STOCKSHARP
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
#endif
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
    private readonly Dictionary<string, Security> _securities = new();
    private readonly object _gate = new();
#endif

    private long _eventsWritten;
    private bool _disposed;

    /// <summary>
    /// Creates a new StockSharp storage sink.
    /// </summary>
    /// <param name="storagePath">Path to the storage directory.</param>
    /// <param name="useBinaryFormat">If true, uses binary format (2 bytes/trade). If false, uses CSV.</param>
    public StockSharpStorageSink(string storagePath, bool useBinaryFormat = true)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        _useBinaryFormat = useBinaryFormat;

#if STOCKSHARP
        _format = useBinaryFormat ? StorageFormats.Binary : StorageFormats.Csv;

        // Initialize StockSharp storage registry
        _exchangeInfoProvider = new InMemoryExchangeInfoProvider();
        _storageRegistry = new StorageRegistry(_exchangeInfoProvider)
        {
            DefaultDrive = new LocalMarketDataDrive(storagePath)
        };

        Directory.CreateDirectory(storagePath);
        _log.Information("StockSharp storage initialized at {Path} with format {Format}",
            storagePath, _format);
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
    /// Append a market event to storage.
    /// </summary>
    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(StockSharpStorageSink));

#if STOCKSHARP
        if (evt?.Payload == null) return;

        var security = GetOrCreateSecurity(evt.Symbol);

        try
        {
            switch (evt.Type)
            {
                case MarketEventType.Trade when evt.Payload is Trade trade:
                    await AppendTradeAsync(security, trade, ct).ConfigureAwait(false);
                    break;

                case MarketEventType.L2Snapshot when evt.Payload is LOBSnapshot lob:
                    await AppendDepthAsync(security, lob, ct).ConfigureAwait(false);
                    break;

                case MarketEventType.BboQuote when evt.Payload is BboQuotePayload bbo:
                    await AppendLevel1Async(security, bbo, ct).ConfigureAwait(false);
                    break;

                case MarketEventType.HistoricalBar when evt.Payload is HistoricalBar bar:
                    await AppendCandleAsync(security, bar, ct).ConfigureAwait(false);
                    break;

                default:
                    // Unsupported event type, skip silently
                    return;
            }

            Interlocked.Increment(ref _eventsWritten);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error writing event to StockSharp storage: {Type} {Symbol}",
                evt.Type, evt.Symbol);
        }
#else
        await Task.CompletedTask;
#endif
    }

#if STOCKSHARP
    /// <summary>
    /// Write a trade tick to storage.
    /// </summary>
    private async ValueTask AppendTradeAsync(Security security, Trade trade, CancellationToken ct)
    {
        var storage = _storageRegistry.GetTickMessageStorage(security.ToSecurityId(), _format);

        var msg = new ExecutionMessage
        {
            SecurityId = security.ToSecurityId(),
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

        await Task.Run(() => storage.Save(msg), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Write an order book snapshot to storage.
    /// </summary>
    private async ValueTask AppendDepthAsync(Security security, LOBSnapshot lob, CancellationToken ct)
    {
        var storage = _storageRegistry.GetQuoteMessageStorage(security.ToSecurityId(), _format);

        var msg = new QuoteChangeMessage
        {
            SecurityId = security.ToSecurityId(),
            ServerTime = lob.Timestamp,
            Bids = lob.Bids.Select(b => new QuoteChange(b.Price, b.Size)).ToArray(),
            Asks = lob.Asks.Select(a => new QuoteChange(a.Price, a.Size)).ToArray()
        };

        await Task.Run(() => storage.Save(msg), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Write a Level1/BBO quote to storage.
    /// </summary>
    private async ValueTask AppendLevel1Async(Security security, BboQuotePayload bbo, CancellationToken ct)
    {
        var storage = _storageRegistry.GetLevel1MessageStorage(security.ToSecurityId(), _format);

        var msg = new Level1ChangeMessage
        {
            SecurityId = security.ToSecurityId(),
            ServerTime = bbo.Timestamp
        };

        msg.TryAdd(Level1Fields.BestBidPrice, bbo.BidPrice);
        msg.TryAdd(Level1Fields.BestBidVolume, (decimal)bbo.BidSize);
        msg.TryAdd(Level1Fields.BestAskPrice, bbo.AskPrice);
        msg.TryAdd(Level1Fields.BestAskVolume, (decimal)bbo.AskSize);

        if (bbo.MidPrice.HasValue)
            msg.TryAdd(Level1Fields.LastTradePrice, bbo.MidPrice.Value); // Approximation

        await Task.Run(() => storage.Save(msg), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Write a candle/bar to storage.
    /// </summary>
    private async ValueTask AppendCandleAsync(Security security, HistoricalBar bar, CancellationToken ct)
    {
        var storage = _storageRegistry.GetCandleMessageStorage(
            security.ToSecurityId(),
            typeof(TimeFrameCandleMessage),
            TimeSpan.FromDays(1),
            _format);

        var msg = new TimeFrameCandleMessage
        {
            SecurityId = security.ToSecurityId(),
            OpenTime = bar.SessionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            OpenPrice = bar.Open,
            HighPrice = bar.High,
            LowPrice = bar.Low,
            ClosePrice = bar.Close,
            TotalVolume = bar.Volume,
            State = CandleStates.Finished
        };

        await Task.Run(() => storage.Save(msg), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Get or create a StockSharp Security for a symbol.
    /// </summary>
    private Security GetOrCreateSecurity(string symbol)
    {
        lock (_gate)
        {
            if (!_securities.TryGetValue(symbol, out var security))
            {
                security = new Security
                {
                    Id = symbol,
                    Code = symbol,
                    Board = ExchangeBoard.Nyse // Default, could be configurable
                };
                _securities[symbol] = security;
            }
            return security;
        }
    }
#endif

    /// <summary>
    /// Flush any buffered data to disk.
    /// </summary>
    public Task FlushAsync(CancellationToken ct = default)
    {
        // StockSharp storage writes are synchronous/immediate
        // Nothing to flush explicitly
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose of the storage sink.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await FlushAsync().ConfigureAwait(false);

#if STOCKSHARP
        lock (_gate)
        {
            _securities.Clear();
        }
#endif

        _log.Information("StockSharp storage disposed. Total events written: {Count}", _eventsWritten);
    }
}
