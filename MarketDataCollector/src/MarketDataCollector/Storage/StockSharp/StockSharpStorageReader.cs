#if STOCKSHARP
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
#endif
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Storage.StockSharp;

/// <summary>
/// Reads market data from StockSharp's binary storage format.
/// Provides async enumerable access to trades, depth snapshots, and candles.
///
/// The reader is designed for efficient historical data retrieval and backtesting.
/// </summary>
public sealed class StockSharpStorageReader
{
    private readonly ILogger _log = LoggingSetup.ForContext<StockSharpStorageReader>();
    private readonly string _storagePath;

#if STOCKSHARP
    private readonly StorageRegistry _storageRegistry;
    private readonly IExchangeInfoProvider _exchangeInfoProvider;
#endif

    /// <summary>
    /// Creates a new StockSharp storage reader.
    /// </summary>
    /// <param name="storagePath">Path to the StockSharp storage directory.</param>
    public StockSharpStorageReader(string storagePath)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));

#if STOCKSHARP
        _exchangeInfoProvider = new InMemoryExchangeInfoProvider();
        _storageRegistry = new StorageRegistry(_exchangeInfoProvider)
        {
            DefaultDrive = new LocalMarketDataDrive(storagePath)
        };

        _log.Debug("StockSharp storage reader initialized at {Path}", storagePath);
#else
        _log.Warning("StockSharp packages not installed. Storage reader will return empty results.");
#endif
    }

    /// <summary>
    /// Read trades for a symbol within a date range.
    /// </summary>
    /// <param name="symbol">Symbol to read trades for.</param>
    /// <param name="from">Start of date range (inclusive).</param>
    /// <param name="to">End of date range (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of Trade records.</returns>
    public async IAsyncEnumerable<Trade> ReadTradesAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
#if STOCKSHARP
        var securityId = new SecurityId { SecurityCode = symbol };
        var storage = _storageRegistry.GetTickMessageStorage(securityId);

        var dates = storage.Dates
            .Where(d => d >= from.Date && d <= to.Date)
            .OrderBy(d => d)
            .ToList();

        _log.Debug("Reading trades for {Symbol}: {DateCount} days in range", symbol, dates.Count);

        foreach (var date in dates)
        {
            ct.ThrowIfCancellationRequested();

            var messages = await Task.Run(() =>
                storage.Load(date).OfType<ExecutionMessage>().ToList(), ct).ConfigureAwait(false);

            foreach (var msg in messages)
            {
                if (msg.TradePrice == null) continue;

                yield return new Trade(
                    Timestamp: msg.ServerTime,
                    Symbol: symbol,
                    Price: msg.TradePrice.Value,
                    Size: (long)(msg.TradeVolume ?? 0),
                    Aggressor: msg.OriginSide switch
                    {
                        Sides.Buy => AggressorSide.Buy,
                        Sides.Sell => AggressorSide.Sell,
                        _ => AggressorSide.Unknown
                    },
                    SequenceNumber: msg.SeqNum ?? 0,
                    StreamId: msg.TradeId?.ToString(),
                    Venue: msg.SecurityId.BoardCode
                );
            }
        }
#else
        await Task.CompletedTask;
        yield break;
#endif
    }

    /// <summary>
    /// Read order book snapshots for a symbol within a date range.
    /// </summary>
    /// <param name="symbol">Symbol to read depth for.</param>
    /// <param name="from">Start of date range (inclusive).</param>
    /// <param name="to">End of date range (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of LOBSnapshot records.</returns>
    public async IAsyncEnumerable<LOBSnapshot> ReadDepthAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
#if STOCKSHARP
        var securityId = new SecurityId { SecurityCode = symbol };
        var storage = _storageRegistry.GetQuoteMessageStorage(securityId);

        var dates = storage.Dates
            .Where(d => d >= from.Date && d <= to.Date)
            .OrderBy(d => d)
            .ToList();

        _log.Debug("Reading depth for {Symbol}: {DateCount} days in range", symbol, dates.Count);

        foreach (var date in dates)
        {
            ct.ThrowIfCancellationRequested();

            var messages = await Task.Run(() =>
                storage.Load(date).OfType<QuoteChangeMessage>().ToList(), ct).ConfigureAwait(false);

            foreach (var msg in messages)
            {
                var bids = msg.Bids?
                    .Select((q, i) => new OrderBookLevel(
                        Side: OrderBookSide.Bid,
                        Level: i,
                        Price: q.Price,
                        Size: q.Volume))
                    .ToList() ?? new List<OrderBookLevel>();

                var asks = msg.Asks?
                    .Select((q, i) => new OrderBookLevel(
                        Side: OrderBookSide.Ask,
                        Level: i,
                        Price: q.Price,
                        Size: q.Volume))
                    .ToList() ?? new List<OrderBookLevel>();

                var bestBid = bids.FirstOrDefault()?.Price ?? 0;
                var bestAsk = asks.FirstOrDefault()?.Price ?? 0;
                var midPrice = bestBid > 0 && bestAsk > 0 ? (bestBid + bestAsk) / 2 : (decimal?)null;

                yield return new LOBSnapshot(
                    Timestamp: msg.ServerTime,
                    Symbol: symbol,
                    Bids: bids,
                    Asks: asks,
                    MidPrice: midPrice,
                    SequenceNumber: msg.SeqNum ?? 0,
                    Venue: msg.SecurityId.BoardCode
                );
            }
        }
#else
        await Task.CompletedTask;
        yield break;
#endif
    }

    /// <summary>
    /// Read daily candles for a symbol within a date range.
    /// </summary>
    /// <param name="symbol">Symbol to read candles for.</param>
    /// <param name="from">Start of date range (inclusive).</param>
    /// <param name="to">End of date range (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of HistoricalBar records.</returns>
    public async IAsyncEnumerable<HistoricalBar> ReadCandlesAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
#if STOCKSHARP
        var securityId = new SecurityId { SecurityCode = symbol };
        var storage = _storageRegistry.GetCandleMessageStorage(
            securityId,
            typeof(TimeFrameCandleMessage),
            TimeSpan.FromDays(1));

        var dates = storage.Dates
            .Where(d => d >= from.Date && d <= to.Date)
            .OrderBy(d => d)
            .ToList();

        _log.Debug("Reading candles for {Symbol}: {DateCount} days in range", symbol, dates.Count);

        foreach (var date in dates)
        {
            ct.ThrowIfCancellationRequested();

            var messages = await Task.Run(() =>
                storage.Load(date).OfType<TimeFrameCandleMessage>().ToList(), ct).ConfigureAwait(false);

            foreach (var msg in messages)
            {
                yield return new HistoricalBar(
                    Symbol: symbol,
                    SessionDate: DateOnly.FromDateTime(msg.OpenTime.Date),
                    Open: msg.OpenPrice,
                    High: msg.HighPrice,
                    Low: msg.LowPrice,
                    Close: msg.ClosePrice,
                    Volume: (long)msg.TotalVolume,
                    Source: "stocksharp",
                    SequenceNumber: 0
                );
            }
        }
#else
        await Task.CompletedTask;
        yield break;
#endif
    }

    /// <summary>
    /// Read Level1/BBO quotes for a symbol within a date range.
    /// </summary>
    /// <param name="symbol">Symbol to read quotes for.</param>
    /// <param name="from">Start of date range (inclusive).</param>
    /// <param name="to">End of date range (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of BboQuotePayload records.</returns>
    public async IAsyncEnumerable<BboQuotePayload> ReadQuotesAsync(
        string symbol,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
#if STOCKSHARP
        var securityId = new SecurityId { SecurityCode = symbol };
        var storage = _storageRegistry.GetLevel1MessageStorage(securityId);

        var dates = storage.Dates
            .Where(d => d >= from.Date && d <= to.Date)
            .OrderBy(d => d)
            .ToList();

        _log.Debug("Reading quotes for {Symbol}: {DateCount} days in range", symbol, dates.Count);

        foreach (var date in dates)
        {
            ct.ThrowIfCancellationRequested();

            var messages = await Task.Run(() =>
                storage.Load(date).OfType<Level1ChangeMessage>().ToList(), ct).ConfigureAwait(false);

            foreach (var msg in messages)
            {
                var bidPrice = GetDecimal(msg, Level1Fields.BestBidPrice);
                var askPrice = GetDecimal(msg, Level1Fields.BestAskPrice);
                var bidSize = (long)GetDecimal(msg, Level1Fields.BestBidVolume);
                var askSize = (long)GetDecimal(msg, Level1Fields.BestAskVolume);

                decimal? midPrice = null;
                decimal? spread = null;

                if (bidPrice > 0 && askPrice > 0 && askPrice >= bidPrice)
                {
                    spread = askPrice - bidPrice;
                    midPrice = bidPrice + (spread.Value / 2m);
                }

                yield return new BboQuotePayload(
                    Timestamp: msg.ServerTime,
                    Symbol: symbol,
                    BidPrice: bidPrice,
                    BidSize: bidSize,
                    AskPrice: askPrice,
                    AskSize: askSize,
                    MidPrice: midPrice,
                    Spread: spread,
                    SequenceNumber: msg.SeqNum ?? 0,
                    Venue: msg.SecurityId.BoardCode
                );
            }
        }
#else
        await Task.CompletedTask;
        yield break;
#endif
    }

    /// <summary>
    /// Get available date range for a symbol's trade data.
    /// </summary>
    public (DateOnly? First, DateOnly? Last) GetTradesDateRange(string symbol)
    {
#if STOCKSHARP
        var securityId = new SecurityId { SecurityCode = symbol };
        var storage = _storageRegistry.GetTickMessageStorage(securityId);
        var dates = storage.Dates.OrderBy(d => d).ToList();

        if (dates.Count == 0)
            return (null, null);

        return (DateOnly.FromDateTime(dates.First()), DateOnly.FromDateTime(dates.Last()));
#else
        return (null, null);
#endif
    }

    /// <summary>
    /// Get available date range for a symbol's depth data.
    /// </summary>
    public (DateOnly? First, DateOnly? Last) GetDepthDateRange(string symbol)
    {
#if STOCKSHARP
        var securityId = new SecurityId { SecurityCode = symbol };
        var storage = _storageRegistry.GetQuoteMessageStorage(securityId);
        var dates = storage.Dates.OrderBy(d => d).ToList();

        if (dates.Count == 0)
            return (null, null);

        return (DateOnly.FromDateTime(dates.First()), DateOnly.FromDateTime(dates.Last()));
#else
        return (null, null);
#endif
    }

#if STOCKSHARP
    /// <summary>
    /// Extract decimal value from Level1ChangeMessage changes dictionary.
    /// </summary>
    private static decimal GetDecimal(Level1ChangeMessage msg, Level1Fields field)
    {
        if (msg.Changes.TryGetValue(field, out var value) && value is decimal d)
            return d;
        return 0m;
    }
#endif
}
