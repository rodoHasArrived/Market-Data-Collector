using Meridian.Backtesting.Portfolio;

namespace Meridian.Backtesting.Engine;

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

    public Guid PlaceOrder(OrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        if (request.Quantity == 0)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity cannot be zero.");

        if ((request.Type is OrderType.Limit or OrderType.StopLimit) && (!request.LimitPrice.HasValue || request.LimitPrice <= 0))
            throw new ArgumentOutOfRangeException(nameof(request.LimitPrice), "Limit price must be greater than zero.");

        if ((request.Type is OrderType.StopMarket or OrderType.StopLimit) && (!request.StopPrice.HasValue || request.StopPrice <= 0))
            throw new ArgumentOutOfRangeException(nameof(request.StopPrice), "Stop price must be greater than zero.");

        var order = new Order(
            Guid.NewGuid(),
            request.Symbol,
            request.Type,
            request.Quantity,
            request.LimitPrice,
            request.StopPrice,
            CurrentTime,
            request.TimeInForce,
            request.ExecutionModel,
            request.AllowPartialFills,
            request.ProviderParameters);

        _pendingOrders.Add(order);
        return order.OrderId;
    }

    public Guid PlaceMarketOrder(string symbol, long quantity)
    {
        return PlaceOrder(new OrderRequest(symbol, quantity, OrderType.Market));
    }

    public Guid PlaceLimitOrder(string symbol, long quantity, decimal limitPrice)
    {
        return PlaceOrder(new OrderRequest(symbol, quantity, OrderType.Limit, LimitPrice: limitPrice));
    }

    public Guid PlaceStopMarketOrder(string symbol, long quantity, decimal stopPrice)
    {
        return PlaceOrder(new OrderRequest(symbol, quantity, OrderType.StopMarket, StopPrice: stopPrice));
    }

    public Guid PlaceStopLimitOrder(string symbol, long quantity, decimal stopPrice, decimal limitPrice)
    {
        return PlaceOrder(new OrderRequest(
            symbol,
            quantity,
            OrderType.StopLimit,
            LimitPrice: limitPrice,
            StopPrice: stopPrice));
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
