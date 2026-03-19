using MarketDataCollector.Backtesting.Portfolio;

namespace MarketDataCollector.Backtesting.Engine;

/// <summary>
/// Mutable context object passed into every strategy callback during replay.
/// Wraps <see cref="SimulatedPortfolio"/> and collects submitted orders.
/// </summary>
internal sealed class BacktestContext(
    SimulatedPortfolio portfolio,
    IReadOnlySet<string> universe,
    BacktestLedger ledger) : IBacktestContext
{
    private readonly List<Order> _pendingOrders = [];

    public IReadOnlySet<string> Universe => universe;
    public DateTimeOffset CurrentTime { get; internal set; }
    public DateOnly CurrentDate { get; internal set; }
    public decimal Cash => portfolio.Cash;
    public decimal PortfolioValue => portfolio.ComputeCurrentEquity();
    public IReadOnlyDictionary<string, Position> Positions => portfolio.GetCurrentPositions();
    public IReadOnlyLedger Ledger => ledger;

    public decimal? GetLastPrice(string symbol) =>
        portfolio.LastPrices.TryGetValue(symbol, out var p) ? p : null;

    public Guid PlaceMarketOrder(string symbol, long quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var order = new Order(Guid.NewGuid(), symbol, OrderType.Market, quantity, null, null, CurrentTime);
        _pendingOrders.Add(order);
        return order.OrderId;
    }

    public Guid PlaceLimitOrder(string symbol, long quantity, decimal limitPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (limitPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(limitPrice));
        var order = new Order(Guid.NewGuid(), symbol, OrderType.Limit, quantity, limitPrice, null, CurrentTime);
        _pendingOrders.Add(order);
        return order.OrderId;
    }

    public void CancelOrder(Guid orderId) =>
        _pendingOrders.RemoveAll(o => o.OrderId == orderId);

    internal IReadOnlyList<Order> DrainPendingOrders()
    {
        var orders = _pendingOrders.ToList();
        _pendingOrders.Clear();
        return orders;
    }
}
