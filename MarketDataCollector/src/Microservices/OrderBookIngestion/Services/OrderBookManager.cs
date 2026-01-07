using System.Collections.Concurrent;
using DataIngestion.Contracts.Messages;
using DataIngestion.OrderBookService.Configuration;
using DataIngestion.OrderBookService.Models;
using OrderBookLevel = DataIngestion.OrderBookService.Models.OrderBookLevel;
using Serilog;

namespace DataIngestion.OrderBookService.Services;

/// <summary>
/// Manages order book state for multiple symbols.
/// </summary>
public interface IOrderBookManager
{
    /// <summary>Apply a full snapshot.</summary>
    void ApplySnapshot(string symbol, OrderBookSnapshot snapshot);

    /// <summary>Apply an incremental update.</summary>
    bool ApplyUpdate(string symbol, OrderBookUpdate update);

    /// <summary>Get current order book state.</summary>
    ManagedOrderBook? GetOrderBook(string symbol);

    /// <summary>Get all active order books.</summary>
    IEnumerable<ManagedOrderBook> GetAllOrderBooks();

    /// <summary>Get count of active books.</summary>
    int GetActiveBookCount();

    /// <summary>Get books that have changed since last snapshot.</summary>
    IEnumerable<ManagedOrderBook> GetChangedBooks();

    /// <summary>Mark books as snapshotted.</summary>
    void MarkSnapshotted(IEnumerable<string> symbols);
}

/// <summary>
/// Thread-safe order book manager with integrity checking.
/// </summary>
public sealed class OrderBookManager : IOrderBookManager
{
    private readonly OrderBookServiceConfig _config;
    private readonly OrderBookMetrics _metrics;
    private readonly Serilog.ILogger _log = Log.ForContext<OrderBookManager>();
    private readonly ConcurrentDictionary<string, ManagedOrderBook> _books = new();

    public OrderBookManager(OrderBookServiceConfig config, OrderBookMetrics metrics)
    {
        _config = config;
        _metrics = metrics;
    }

    public void ApplySnapshot(string symbol, OrderBookSnapshot snapshot)
    {
        var book = _books.GetOrAdd(symbol, _ => new ManagedOrderBook(symbol, _config.OrderBook.MaxDepthLevels));

        lock (book.SyncLock)
        {
            book.Bids.Clear();
            book.Asks.Clear();

            foreach (var bid in snapshot.Bids.Take(_config.OrderBook.MaxDepthLevels))
            {
                book.Bids[bid.Price] = new OrderBookLevel(bid.Price, bid.Size, bid.MarketMaker);
            }

            foreach (var ask in snapshot.Asks.Take(_config.OrderBook.MaxDepthLevels))
            {
                book.Asks[ask.Price] = new OrderBookLevel(ask.Price, ask.Size, ask.MarketMaker);
            }

            book.LastSequence = snapshot.Sequence;
            book.LastUpdateTime = snapshot.Timestamp;
            book.IsFrozen = false;
            book.HasChanged = true;
            book.SnapshotCount++;
        }

        _metrics.RecordSnapshot(symbol);
        _log.Debug("Applied snapshot for {Symbol}: {BidLevels} bids, {AskLevels} asks",
            symbol, book.Bids.Count, book.Asks.Count);
    }

    public bool ApplyUpdate(string symbol, OrderBookUpdate update)
    {
        if (!_books.TryGetValue(symbol, out var book))
        {
            _log.Warning("Received update for unknown symbol: {Symbol}", symbol);
            return false;
        }

        lock (book.SyncLock)
        {
            if (book.IsFrozen)
            {
                _log.Debug("Ignoring update for frozen book: {Symbol}", symbol);
                return false;
            }

            // Sequence validation
            if (_config.OrderBook.EnableIntegrityCheck)
            {
                if (update.Sequence <= book.LastSequence && book.LastSequence > 0)
                {
                    _metrics.RecordIntegrityError(symbol, "OutOfOrder");
                    _log.Warning("Out of order update for {Symbol}: {Seq} <= {LastSeq}",
                        symbol, update.Sequence, book.LastSequence);

                    if (_config.OrderBook.FreezeOnIntegrityError)
                    {
                        book.IsFrozen = true;
                        return false;
                    }
                }

                if (update.Sequence > book.LastSequence + 1 && book.LastSequence > 0)
                {
                    _metrics.RecordIntegrityError(symbol, "Gap");
                    _log.Warning("Sequence gap for {Symbol}: expected {Expected}, got {Got}",
                        symbol, book.LastSequence + 1, update.Sequence);

                    if (_config.OrderBook.FreezeOnIntegrityError)
                    {
                        book.IsFrozen = true;
                        return false;
                    }
                }
            }

            var levels = update.Side == OrderBookSide.Bid ? book.Bids : book.Asks;

            switch (update.UpdateType)
            {
                case OrderBookUpdateType.Insert:
                case OrderBookUpdateType.Update:
                    if (update.Price.HasValue && update.Size.HasValue)
                    {
                        levels[update.Price.Value] = new OrderBookLevel(
                            update.Price.Value,
                            update.Size.Value,
                            update.MarketMaker);
                    }
                    break;

                case OrderBookUpdateType.Delete:
                    if (update.Price.HasValue)
                    {
                        levels.Remove(update.Price.Value, out _);
                    }
                    break;
            }

            // Trim to max depth
            TrimToMaxDepth(book);

            book.LastSequence = update.Sequence;
            book.LastUpdateTime = update.Timestamp;
            book.HasChanged = true;
            book.UpdateCount++;
        }

        _metrics.RecordUpdate(symbol);
        return true;
    }

    public ManagedOrderBook? GetOrderBook(string symbol)
    {
        return _books.TryGetValue(symbol, out var book) ? book : null;
    }

    public IEnumerable<ManagedOrderBook> GetAllOrderBooks()
    {
        return _books.Values;
    }

    public int GetActiveBookCount()
    {
        return _books.Count;
    }

    public IEnumerable<ManagedOrderBook> GetChangedBooks()
    {
        return _books.Values.Where(b => b.HasChanged && !b.IsFrozen);
    }

    public void MarkSnapshotted(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            if (_books.TryGetValue(symbol, out var book))
            {
                lock (book.SyncLock)
                {
                    book.HasChanged = false;
                }
            }
        }
    }

    private void TrimToMaxDepth(ManagedOrderBook book)
    {
        var maxDepth = _config.OrderBook.MaxDepthLevels;

        // Bids: keep highest prices
        while (book.Bids.Count > maxDepth)
        {
            var lowest = book.Bids.Keys.Min();
            book.Bids.Remove(lowest, out _);
        }

        // Asks: keep lowest prices
        while (book.Asks.Count > maxDepth)
        {
            var highest = book.Asks.Keys.Max();
            book.Asks.Remove(highest, out _);
        }
    }
}
