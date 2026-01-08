using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.OrderBook;

/// <summary>
/// High-performance price-time priority order book matching engine.
/// Implements order matching algorithms for simulation and analysis.
///
/// Based on: https://github.com/leboeuf/OrderBook (C# price-time order book)
/// Reference: docs/open-source-references.md #27
/// </summary>
public sealed class OrderBookMatchingEngine
{
    private readonly ILogger _log = LoggingSetup.ForContext<OrderBookMatchingEngine>();
    private readonly string _symbol;
    private readonly OrderBookSideManager _bids;
    private readonly OrderBookSideManager _asks;
    private readonly ConcurrentDictionary<long, Order> _orders = new();
    private readonly object _matchLock = new();
    private long _nextOrderId = 1;
    private long _nextTradeId = 1;

    /// <summary>
    /// Event raised when a trade occurs.
    /// </summary>
    public event EventHandler<TradeExecutedEventArgs>? TradeExecuted;

    /// <summary>
    /// Event raised when the order book changes.
    /// </summary>
    public event EventHandler<OrderBookChangedEventArgs>? OrderBookChanged;

    public OrderBookMatchingEngine(string symbol)
    {
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _bids = new OrderBookSideManager(isAscending: false); // Highest price first
        _asks = new OrderBookSideManager(isAscending: true);  // Lowest price first
    }

    public string Symbol => _symbol;
    public decimal BestBid => _bids.BestPrice;
    public decimal BestAsk => _asks.BestPrice;
    public decimal Spread => BestAsk > 0 && BestBid > 0 ? BestAsk - BestBid : 0;
    public decimal MidPrice => BestAsk > 0 && BestBid > 0 ? (BestAsk + BestBid) / 2 : 0;
    public int BidLevels => _bids.LevelCount;
    public int AskLevels => _asks.LevelCount;
    public int TotalOrders => _orders.Count;

    /// <summary>
    /// Submit a new order to the matching engine.
    /// </summary>
    public OrderResult SubmitOrder(OrderRequest request)
    {
        lock (_matchLock)
        {
            var order = new Order
            {
                OrderId = Interlocked.Increment(ref _nextOrderId),
                Symbol = _symbol,
                Side = request.Side,
                Price = request.Price,
                Quantity = request.Quantity,
                RemainingQuantity = request.Quantity,
                Type = request.Type,
                TimeInForce = request.TimeInForce,
                SubmitTime = DateTimeOffset.UtcNow,
                Status = OrderStatus.New
            };

            var trades = new List<TradeExecution>();

            if (request.Type == OrderType.Market || request.Type == OrderType.Limit)
            {
                // Try to match the order
                trades = MatchOrder(order);
            }

            // Handle remaining quantity based on order type and TIF
            if (order.RemainingQuantity > 0)
            {
                switch (request.Type)
                {
                    case OrderType.Market:
                        // Market orders that can't be filled are cancelled
                        order.Status = OrderStatus.Cancelled;
                        break;

                    case OrderType.Limit:
                        if (request.TimeInForce == TimeInForce.IOC || request.TimeInForce == TimeInForce.FOK)
                        {
                            // IOC/FOK orders are cancelled if not fully filled
                            if (request.TimeInForce == TimeInForce.FOK && trades.Count > 0)
                            {
                                // FOK requires full fill - cancel all trades (not implemented - would need rollback)
                            }
                            order.Status = OrderStatus.Cancelled;
                        }
                        else
                        {
                            // Add to book
                            AddToBook(order);
                            order.Status = OrderStatus.Open;
                        }
                        break;
                }
            }
            else
            {
                order.Status = OrderStatus.Filled;
            }

            // Store the order
            _orders[order.OrderId] = order;

            // Raise events
            foreach (var trade in trades)
            {
                TradeExecuted?.Invoke(this, new TradeExecutedEventArgs(trade));
            }

            if (trades.Count > 0 || order.Status == OrderStatus.Open)
            {
                OrderBookChanged?.Invoke(this, new OrderBookChangedEventArgs(_symbol, GetSnapshot()));
            }

            return new OrderResult(order, trades);
        }
    }

    /// <summary>
    /// Cancel an existing order.
    /// </summary>
    public bool CancelOrder(long orderId)
    {
        lock (_matchLock)
        {
            if (!_orders.TryGetValue(orderId, out var order))
                return false;

            if (order.Status != OrderStatus.Open)
                return false;

            // Remove from book
            var side = order.Side == OrderBookSide.Bid ? _bids : _asks;
            side.RemoveOrder(order.Price, orderId);

            order.Status = OrderStatus.Cancelled;

            OrderBookChanged?.Invoke(this, new OrderBookChangedEventArgs(_symbol, GetSnapshot()));
            return true;
        }
    }

    /// <summary>
    /// Modify an existing order (cancel and replace).
    /// Returns null if the order cannot be modified (not found or not in Open status).
    /// </summary>
    // TODO: Consider Result<T> pattern instead of null for better error handling
    public OrderResult? ModifyOrder(long orderId, decimal newPrice, int newQuantity)
    {
        lock (_matchLock)
        {
            // Check if order exists before attempting cancellation
            if (!_orders.TryGetValue(orderId, out var existingOrder))
            {
                _log.Warning("Order modification failed: order {OrderId} not found for {Symbol}", orderId, _symbol);
                return null;
            }

            // Check if order is in a modifiable state
            if (existingOrder.Status != OrderStatus.Open)
            {
                _log.Warning(
                    "Order modification failed: order {OrderId} has status {Status}, expected {ExpectedStatus} for {Symbol}",
                    orderId, existingOrder.Status, OrderStatus.Open, _symbol);
                return null;
            }

            if (!CancelOrder(orderId))
            {
                // This shouldn't happen given the checks above, but log it for safety
                _log.Warning(
                    "Order modification failed: unexpected cancellation failure for order {OrderId} on {Symbol}",
                    orderId, _symbol);
                return null;
            }

            var oldOrder = _orders[orderId];
            return SubmitOrder(new OrderRequest
            {
                Side = oldOrder.Side,
                Price = newPrice,
                Quantity = newQuantity,
                Type = oldOrder.Type,
                TimeInForce = oldOrder.TimeInForce
            });
        }
    }

    /// <summary>
    /// Get the current order book snapshot.
    /// </summary>
    public OrderBookSnapshot GetSnapshot()
    {
        lock (_matchLock)
        {
            return new OrderBookSnapshot(
                Symbol: _symbol,
                Timestamp: DateTimeOffset.UtcNow,
                Bids: _bids.GetLevels(),
                Asks: _asks.GetLevels(),
                BestBid: BestBid,
                BestAsk: BestAsk,
                Spread: Spread
            );
        }
    }

    /// <summary>
    /// Get order by ID.
    /// </summary>
    public Order? GetOrder(long orderId)
    {
        return _orders.TryGetValue(orderId, out var order) ? order : null;
    }

    /// <summary>
    /// Apply a market data update to the order book (for reconstruction).
    /// </summary>
    public void ApplyUpdate(MarketDepthUpdate update)
    {
        lock (_matchLock)
        {
            var side = update.Side == OrderBookSide.Bid ? _bids : _asks;

            switch (update.Operation)
            {
                case DepthOperation.Insert:
                case DepthOperation.Update:
                    side.SetLevel(update.Price, (int)update.Size);
                    break;

                case DepthOperation.Delete:
                    side.RemoveLevel(update.Price);
                    break;
            }

            OrderBookChanged?.Invoke(this, new OrderBookChangedEventArgs(_symbol, GetSnapshot()));
        }
    }

    /// <summary>
    /// Clear the entire order book.
    /// </summary>
    public void Clear()
    {
        lock (_matchLock)
        {
            _bids.Clear();
            _asks.Clear();

            foreach (var order in _orders.Values.Where(o => o.Status == OrderStatus.Open))
            {
                order.Status = OrderStatus.Cancelled;
            }
        }
    }

    private List<TradeExecution> MatchOrder(Order order)
    {
        var trades = new List<TradeExecution>();
        var opposingSide = order.Side == OrderBookSide.Bid ? _asks : _bids;

        while (order.RemainingQuantity > 0 && opposingSide.HasOrders)
        {
            var bestLevel = opposingSide.BestLevel;
            if (bestLevel == null) break;

            // Check price compatibility
            if (order.Type == OrderType.Limit)
            {
                if (order.Side == OrderBookSide.Bid && order.Price < bestLevel.Price)
                    break;
                if (order.Side == OrderBookSide.Ask && order.Price > bestLevel.Price)
                    break;
            }

            // Match against orders at this level
            var matchedOrders = new List<(long OrderId, int Quantity)>();

            foreach (var (restingOrderId, restingQty) in bestLevel.GetOrdersQueue())
            {
                if (order.RemainingQuantity <= 0) break;

                var fillQty = Math.Min(order.RemainingQuantity, restingQty);
                order.RemainingQuantity -= fillQty;

                // Update resting order
                if (_orders.TryGetValue(restingOrderId, out var restingOrder))
                {
                    restingOrder.RemainingQuantity -= fillQty;
                    if (restingOrder.RemainingQuantity <= 0)
                    {
                        restingOrder.Status = OrderStatus.Filled;
                        matchedOrders.Add((restingOrderId, 0));
                    }
                    else
                    {
                        matchedOrders.Add((restingOrderId, restingOrder.RemainingQuantity));
                    }
                }

                // Create trade execution
                var trade = new TradeExecution
                {
                    TradeId = Interlocked.Increment(ref _nextTradeId),
                    Symbol = _symbol,
                    Price = bestLevel.Price,
                    Quantity = fillQty,
                    AggressorOrderId = order.OrderId,
                    PassiveOrderId = restingOrderId,
                    AggressorSide = order.Side,
                    Timestamp = DateTimeOffset.UtcNow
                };

                trades.Add(trade);
            }

            // Update the level
            foreach (var (matchedId, remainingQty) in matchedOrders)
            {
                if (remainingQty <= 0)
                    opposingSide.RemoveOrder(bestLevel.Price, matchedId);
                else
                    opposingSide.UpdateOrderQuantity(bestLevel.Price, matchedId, remainingQty);
            }
        }

        return trades;
    }

    private void AddToBook(Order order)
    {
        var side = order.Side == OrderBookSide.Bid ? _bids : _asks;
        side.AddOrder(order.Price, order.OrderId, order.RemainingQuantity);
    }
}

/// <summary>
/// Manages one side of the order book.
/// </summary>
internal sealed class OrderBookSideManager
{
    private readonly SortedDictionary<decimal, PriceLevel> _levels;
    private readonly bool _isAscending;

    public OrderBookSideManager(bool isAscending)
    {
        _isAscending = isAscending;
        _levels = _isAscending
            ? new SortedDictionary<decimal, PriceLevel>()
            : new SortedDictionary<decimal, PriceLevel>(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
    }

    public decimal BestPrice => _levels.Count > 0 ? _levels.First().Key : 0;
    public PriceLevel? BestLevel => _levels.Count > 0 ? _levels.First().Value : null;
    public bool HasOrders => _levels.Count > 0 && _levels.First().Value.TotalQuantity > 0;
    public int LevelCount => _levels.Count;

    public void AddOrder(decimal price, long orderId, int quantity)
    {
        if (!_levels.TryGetValue(price, out var level))
        {
            level = new PriceLevel(price);
            _levels[price] = level;
        }
        level.AddOrder(orderId, quantity);
    }

    public void RemoveOrder(decimal price, long orderId)
    {
        if (_levels.TryGetValue(price, out var level))
        {
            level.RemoveOrder(orderId);
            if (level.TotalQuantity <= 0)
                _levels.Remove(price);
        }
    }

    public void UpdateOrderQuantity(decimal price, long orderId, int newQuantity)
    {
        if (_levels.TryGetValue(price, out var level))
        {
            level.UpdateOrder(orderId, newQuantity);
        }
    }

    public void SetLevel(decimal price, int quantity)
    {
        if (quantity <= 0)
        {
            _levels.Remove(price);
        }
        else
        {
            if (!_levels.TryGetValue(price, out var level))
            {
                level = new PriceLevel(price);
                _levels[price] = level;
            }
            level.SetTotalQuantity(quantity);
        }
    }

    public void RemoveLevel(decimal price)
    {
        _levels.Remove(price);
    }

    public void Clear()
    {
        _levels.Clear();
    }

    public List<PriceLevelSnapshot> GetLevels(int maxLevels = 10)
    {
        return _levels.Take(maxLevels)
            .Select(l => new PriceLevelSnapshot(l.Key, l.Value.TotalQuantity, l.Value.OrderCount))
            .ToList();
    }
}

/// <summary>
/// Represents a single price level in the order book.
/// </summary>
internal sealed class PriceLevel
{
    private readonly decimal _price;
    private readonly List<(long OrderId, int Quantity)> _orders = new();
    private int _aggregatedQuantity;

    public PriceLevel(decimal price)
    {
        _price = price;
    }

    public decimal Price => _price;
    public int TotalQuantity => _aggregatedQuantity > 0 ? _aggregatedQuantity : _orders.Sum(o => o.Quantity);
    public int OrderCount => _orders.Count > 0 ? _orders.Count : (_aggregatedQuantity > 0 ? 1 : 0);

    public void AddOrder(long orderId, int quantity)
    {
        _orders.Add((orderId, quantity));
    }

    public void RemoveOrder(long orderId)
    {
        _orders.RemoveAll(o => o.OrderId == orderId);
    }

    public void UpdateOrder(long orderId, int newQuantity)
    {
        for (int i = 0; i < _orders.Count; i++)
        {
            if (_orders[i].OrderId == orderId)
            {
                _orders[i] = (orderId, newQuantity);
                break;
            }
        }
    }

    public void SetTotalQuantity(int quantity)
    {
        _orders.Clear();
        _aggregatedQuantity = quantity;
    }

    public IEnumerable<(long OrderId, int Quantity)> GetOrdersQueue()
    {
        return _orders;
    }
}

// DTOs and supporting types

public sealed record OrderRequest
{
    public OrderBookSide Side { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public OrderType Type { get; init; } = OrderType.Limit;
    public TimeInForce TimeInForce { get; init; } = TimeInForce.GTC;
}

public sealed class Order
{
    public long OrderId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public OrderBookSide Side { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public int RemainingQuantity { get; set; }
    public OrderType Type { get; init; }
    public TimeInForce TimeInForce { get; init; }
    public DateTimeOffset SubmitTime { get; init; }
    public OrderStatus Status { get; set; }
}

public sealed record OrderResult(Order Order, List<TradeExecution> Trades);

public sealed class TradeExecution
{
    public long TradeId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public long AggressorOrderId { get; init; }
    public long PassiveOrderId { get; init; }
    public OrderBookSide AggressorSide { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public sealed record OrderBookSnapshot(
    string Symbol,
    DateTimeOffset Timestamp,
    List<PriceLevelSnapshot> Bids,
    List<PriceLevelSnapshot> Asks,
    decimal BestBid,
    decimal BestAsk,
    decimal Spread);

public sealed record PriceLevelSnapshot(decimal Price, int Quantity, int OrderCount);

public enum OrderType { Market, Limit }
public enum TimeInForce { GTC, IOC, FOK, DAY }
public enum OrderStatus { New, Open, PartiallyFilled, Filled, Cancelled, Rejected }

public sealed class TradeExecutedEventArgs : EventArgs
{
    public TradeExecution Trade { get; }
    public TradeExecutedEventArgs(TradeExecution trade) => Trade = trade;
}

public sealed class OrderBookChangedEventArgs : EventArgs
{
    public string Symbol { get; }
    public OrderBookSnapshot Snapshot { get; }
    public OrderBookChangedEventArgs(string symbol, OrderBookSnapshot snapshot)
    {
        Symbol = symbol;
        Snapshot = snapshot;
    }
}
