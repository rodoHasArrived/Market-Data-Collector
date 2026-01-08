using FluentAssertions;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.OrderBook;
using Xunit;

namespace MarketDataCollector.Tests.OrderBook;

/// <summary>
/// Tests for OrderBookMatchingEngine.
///
/// Reference: docs/open-source-references.md #27 (leboeuf/OrderBook)
/// </summary>
public class OrderBookMatchingEngineTests
{
    [Fact]
    public void SubmitOrder_LimitBid_ShouldAddToBook()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Act
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Open);
        result.Trades.Should().BeEmpty();
        engine.BestBid.Should().Be(450.00m);
        engine.BidLevels.Should().Be(1);
    }

    [Fact]
    public void SubmitOrder_LimitAsk_ShouldAddToBook()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Act
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.10m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Open);
        result.Trades.Should().BeEmpty();
        engine.BestAsk.Should().Be(450.10m);
        engine.AskLevels.Should().Be(1);
    }

    [Fact]
    public void SubmitOrder_MarketBuy_ShouldMatchAgainstAsks()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.10m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 0,
            Quantity = 50,
            Type = OrderType.Market
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Filled);
        result.Trades.Should().HaveCount(1);
        result.Trades[0].Price.Should().Be(450.10m);
        result.Trades[0].Quantity.Should().Be(50);
        result.Trades[0].AggressorSide.Should().Be(OrderBookSide.Bid);
    }

    [Fact]
    public void SubmitOrder_MarketSell_ShouldMatchAgainstBids()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 0,
            Quantity = 50,
            Type = OrderType.Market
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Filled);
        result.Trades.Should().HaveCount(1);
        result.Trades[0].Price.Should().Be(450.00m);
        result.Trades[0].Quantity.Should().Be(50);
        result.Trades[0].AggressorSide.Should().Be(OrderBookSide.Ask);
    }

    [Fact]
    public void SubmitOrder_CrossedLimitBuy_ShouldMatch()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act - submit bid above best ask
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.10m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Filled);
        result.Trades.Should().HaveCount(1);
        result.Trades[0].Price.Should().Be(450.00m); // Price improvement
    }

    [Fact]
    public void SubmitOrder_PartialFill_ShouldUpdateRemainingQuantity()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        // Act
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.10m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Open); // Partial fill, remainder added to book
        result.Order.RemainingQuantity.Should().Be(50);
        result.Trades.Should().HaveCount(1);
        result.Trades[0].Quantity.Should().Be(50);
    }

    [Fact]
    public void SubmitOrder_IOC_ShouldCancelUnfilledPortion()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        // Act
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.10m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.IOC
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        result.Order.RemainingQuantity.Should().Be(50);
        result.Trades.Should().HaveCount(1);
        engine.BidLevels.Should().Be(0); // Nothing added to book
    }

    [Fact]
    public void CancelOrder_ExistingOrder_ShouldRemoveFromBook()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act
        var cancelled = engine.CancelOrder(result.Order.OrderId);

        // Assert
        cancelled.Should().BeTrue();
        var order = engine.GetOrder(result.Order.OrderId);
        order!.Status.Should().Be(OrderStatus.Cancelled);
        engine.BidLevels.Should().Be(0);
    }

    [Fact]
    public void CancelOrder_NonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Act
        var cancelled = engine.CancelOrder(99999);

        // Assert
        cancelled.Should().BeFalse();
    }

    [Fact]
    public void GetSnapshot_ShouldReturnCurrentState()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.SubmitOrder(new OrderRequest { Side = OrderBookSide.Bid, Price = 450.00m, Quantity = 100, Type = OrderType.Limit });
        engine.SubmitOrder(new OrderRequest { Side = OrderBookSide.Bid, Price = 449.99m, Quantity = 200, Type = OrderType.Limit });
        engine.SubmitOrder(new OrderRequest { Side = OrderBookSide.Ask, Price = 450.10m, Quantity = 150, Type = OrderType.Limit });
        engine.SubmitOrder(new OrderRequest { Side = OrderBookSide.Ask, Price = 450.11m, Quantity = 250, Type = OrderType.Limit });

        // Act
        var snapshot = engine.GetSnapshot();

        // Assert
        snapshot.Symbol.Should().Be("SPY");
        snapshot.BestBid.Should().Be(450.00m);
        snapshot.BestAsk.Should().Be(450.10m);
        snapshot.Spread.Should().Be(0.10m);
        snapshot.Bids.Should().HaveCount(2);
        snapshot.Asks.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyUpdate_InsertOperation_ShouldAddLevel()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Act
        engine.ApplyUpdate(new MarketDepthUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Position: 0,
            Operation: DepthOperation.Insert,
            Side: OrderBookSide.Bid,
            Price: 450.00m,
            Size: 100,
            MarketMaker: "MM1"
        ));

        // Assert
        engine.BestBid.Should().Be(450.00m);
    }

    [Fact]
    public void ApplyUpdate_DeleteOperation_ShouldRemoveLevel()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        engine.ApplyUpdate(new MarketDepthUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Position: 0,
            Operation: DepthOperation.Insert,
            Side: OrderBookSide.Bid,
            Price: 450.00m,
            Size: 100,
            MarketMaker: "MM1"
        ));

        // Act
        engine.ApplyUpdate(new MarketDepthUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Position: 0,
            Operation: DepthOperation.Delete,
            Side: OrderBookSide.Bid,
            Price: 450.00m,
            Size: 0,
            MarketMaker: "MM1"
        ));

        // Assert
        engine.BidLevels.Should().Be(0);
    }

    [Fact]
    public void PriceTimePriority_EarlierOrdersShouldMatchFirst()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Submit two asks at the same price
        var ask1 = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        var ask2 = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        // Act - submit market buy for 50
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 0,
            Quantity = 50,
            Type = OrderType.Market
        });

        // Assert - should match against first order (time priority)
        result.Trades.Should().HaveCount(1);
        result.Trades[0].PassiveOrderId.Should().Be(ask1.Order.OrderId);
    }

    [Fact]
    public void Spread_WithNoOrders_ShouldBeZero()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Assert
        engine.Spread.Should().Be(0);
        engine.MidPrice.Should().Be(0);
    }

    [Fact]
    public void TradeExecuted_Event_ShouldBeRaised()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        TradeExecution? executedTrade = null;
        engine.TradeExecuted += (sender, args) => executedTrade = args.Trade;

        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 0,
            Quantity = 50,
            Type = OrderType.Market
        });

        // Assert
        executedTrade.Should().NotBeNull();
        executedTrade!.Quantity.Should().Be(50);
    }
}
