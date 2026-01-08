using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.OrderBook;

namespace MarketDataCollector.Benchmarks;

/// <summary>
/// Benchmarks for order book operations.
///
/// Reference: docs/open-source-references.md #27 (leboeuf/OrderBook)
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class OrderBookBenchmarks
{
    private OrderBookMatchingEngine _engine = null!;
    private OrderRequest[] _bidOrders = null!;
    private OrderRequest[] _askOrders = null!;
    private MarketDepthUpdate[] _updates = null!;

    [Params(100, 1000, 10000)]
    public int OrderCount;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new OrderBookMatchingEngine("SPY");

        // Generate bid orders at various price levels
        _bidOrders = Enumerable.Range(0, OrderCount)
            .Select(i => new OrderRequest
            {
                Side = OrderBookSide.Bid,
                Price = 450m - (i % 100) * 0.01m,
                Quantity = 100 + (i % 1000),
                Type = OrderType.Limit,
                TimeInForce = TimeInForce.GTC
            })
            .ToArray();

        // Generate ask orders at various price levels
        _askOrders = Enumerable.Range(0, OrderCount)
            .Select(i => new OrderRequest
            {
                Side = OrderBookSide.Ask,
                Price = 450.01m + (i % 100) * 0.01m,
                Quantity = 100 + (i % 1000),
                Type = OrderType.Limit,
                TimeInForce = TimeInForce.GTC
            })
            .ToArray();

        // Generate market depth updates
        _updates = Enumerable.Range(0, OrderCount)
            .Select(i => new MarketDepthUpdate(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: "SPY",
                Position: i % 10,
                Operation: (DepthOperation)(i % 3),
                Side: i % 2 == 0 ? OrderBookSide.Bid : OrderBookSide.Ask,
                Price: 450m + (i % 200 - 100) * 0.01m,
                Size: 100 + (i % 1000),
                MarketMaker: $"MM{i % 10}"
            ))
            .ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _engine = new OrderBookMatchingEngine("SPY");
    }

    [Benchmark(Baseline = true)]
    public void AddBidOrders()
    {
        foreach (var order in _bidOrders)
            _engine.SubmitOrder(order);
    }

    [Benchmark]
    public void AddAskOrders()
    {
        foreach (var order in _askOrders)
            _engine.SubmitOrder(order);
    }

    [Benchmark]
    public void AddAndMatch_CrossedOrders()
    {
        // Add ask orders first
        foreach (var order in _askOrders.Take(OrderCount / 2))
            _engine.SubmitOrder(order);

        // Add bid orders that will match
        foreach (var order in _bidOrders.Take(OrderCount / 2).Select(o => o with { Price = 451m }))
            _engine.SubmitOrder(order);
    }

    [Benchmark]
    public void ApplyDepthUpdates()
    {
        foreach (var update in _updates)
            _engine.ApplyUpdate(update);
    }

    [Benchmark]
    public void GetSnapshot_AfterPopulating()
    {
        // Populate first
        foreach (var order in _bidOrders.Take(100))
            _engine.SubmitOrder(order);
        foreach (var order in _askOrders.Take(100))
            _engine.SubmitOrder(order);

        // Benchmark snapshot
        for (int i = 0; i < 1000; i++)
            _ = _engine.GetSnapshot();
    }
}

/// <summary>
/// Benchmarks for order matching latency.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class OrderMatchingLatencyBenchmarks
{
    private OrderBookMatchingEngine _engine = null!;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new OrderBookMatchingEngine("SPY");

        // Pre-populate with liquidity
        for (int i = 0; i < 10; i++)
        {
            _engine.SubmitOrder(new OrderRequest
            {
                Side = OrderBookSide.Bid,
                Price = 449.90m - i * 0.01m,
                Quantity = 1000,
                Type = OrderType.Limit
            });

            _engine.SubmitOrder(new OrderRequest
            {
                Side = OrderBookSide.Ask,
                Price = 450.10m + i * 0.01m,
                Quantity = 1000,
                Type = OrderType.Limit
            });
        }
    }

    [Benchmark]
    public OrderResult SubmitMarketBuy()
    {
        return _engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 0,
            Quantity = 100,
            Type = OrderType.Market
        });
    }

    [Benchmark]
    public OrderResult SubmitLimitBuy_AtBest()
    {
        return _engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = _engine.BestAsk,
            Quantity = 100,
            Type = OrderType.Limit
        });
    }

    [Benchmark]
    public OrderResult SubmitLimitBuy_Passive()
    {
        return _engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = _engine.BestBid - 0.01m,
            Quantity = 100,
            Type = OrderType.Limit
        });
    }

    [Benchmark]
    public OrderBookSnapshot GetSpread()
    {
        return _engine.GetSnapshot();
    }
}
