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

    [Fact]
    public void ModifyOrder_NonExistentOrder_ShouldReturnFailure()
    public void ModifyOrder_NonExistentOrder_ShouldReturnOrderNotFoundError()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Act
        var result = engine.ModifyOrder(99999, 450.00m, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ModifyOrderError.OrderNotFound);
        result.ErrorMessage.Should().Contain("99999");
    }

    [Fact]
    public void ModifyOrder_CancelledOrder_ShouldReturnFailure()
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ModifyOrderErrorKind.OrderNotFound);
        result.Error.OrderId.Should().Be(99999);
    }

    [Fact]
    public void ModifyOrder_CancelledOrder_ShouldReturnInvalidStatusError()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        var originalOrder = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });
        engine.CancelOrder(originalOrder.Order.OrderId);

        // Act
        var result = engine.ModifyOrder(originalOrder.Order.OrderId, 451.00m, 150);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ModifyOrderError.InvalidOrderStatus);
        result.ErrorMessage.Should().Contain("Cancelled");
    }

    [Fact]
    public void ModifyOrder_FilledOrder_ShouldReturnFailure()
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ModifyOrderErrorKind.InvalidStatus);
        result.Error.CurrentStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void ModifyOrder_FilledOrder_ShouldReturnInvalidStatusError()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Submit a limit ask
        var askResult = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Fill it completely with a market order
        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 0,
            Quantity = 100,
            Type = OrderType.Market
        });

        // Verify the ask is filled
        var order = engine.GetOrder(askResult.Order.OrderId);
        order!.Status.Should().Be(OrderStatus.Filled);

        // Act
        var result = engine.ModifyOrder(askResult.Order.OrderId, 451.00m, 150);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ModifyOrderError.InvalidOrderStatus);
        result.ErrorMessage.Should().Contain("Filled");
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ModifyOrderErrorKind.InvalidStatus);
        result.Error.CurrentStatus.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void ModifyOrder_OpenOrder_ShouldSucceed()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        var originalOrder = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act
        var result = engine.ModifyOrder(originalOrder.Order.OrderId, 451.00m, 150);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Order.Price.Should().Be(451.00m);
        result.Value.Order.Quantity.Should().Be(150);
        result.Value.Order.Status.Should().Be(OrderStatus.Open);
        result.OrderResult.Should().NotBeNull();
        result.OrderResult!.Order.Price.Should().Be(451.00m);
        result.OrderResult.Order.Quantity.Should().Be(150);
        result.OrderResult.Order.Status.Should().Be(OrderStatus.Open);
        engine.BestBid.Should().Be(451.00m);
    }

    [Fact]
    public void ModifyOrder_Result_MatchPattern_ShouldWork()
    public void ModifyOrderResult_Match_ShouldHandleSuccessCorrectly()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        var originalOrder = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit
        });

        // Act
        var result = engine.ModifyOrder(originalOrder.Order.OrderId, 451.00m, 150);
        var message = result.Match(
            onSuccess: orderResult => $"Modified to price {orderResult.Order.Price}",
            onFailure: (error, msg) => $"Failed: {error} - {msg}");

        // Assert
        message.Should().Be("Modified to price 451.00");
        var matchResult = result.Match(
            onSuccess: orderResult => $"Modified to price {orderResult.Order.Price}",
            onFailure: error => $"Failed: {error.Message}"
        );

        // Assert
        matchResult.Should().Be("Modified to price 451.00");
    }

    [Fact]
    public void ModifyOrderResult_Match_ShouldHandleFailureCorrectly()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Act
        var result = engine.ModifyOrder(99999, 450.00m, 100);
        var matchResult = result.Match(
            onSuccess: orderResult => $"Modified to price {orderResult.Order.Price}",
            onFailure: error => $"Error: {error.Kind}"
        );

        // Assert
        matchResult.Should().Be("Error: OrderNotFound");
    }

    [Fact]
    public void SubmitOrder_FOK_FullyFilled_ShouldExecuteNormally()
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

        // Act - FOK order can be fully filled
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.FOK
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Filled);
        result.Trades.Should().HaveCount(1);
        result.Trades[0].Quantity.Should().Be(100);
        result.Order.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public void SubmitOrder_FOK_PartiallyFilled_ShouldRollbackAndCancel()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        var askResult = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        // Act - FOK order cannot be fully filled (wants 100, only 50 available)
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.FOK
        });

        // Assert - FOK should be cancelled with no trades
        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        result.Trades.Should().BeEmpty();
        result.Order.RemainingQuantity.Should().Be(100); // Full quantity restored

        // The resting ask order should be restored to original state
        var restoredAsk = engine.GetOrder(askResult.Order.OrderId);
        restoredAsk.Should().NotBeNull();
        restoredAsk!.Status.Should().Be(OrderStatus.Open);
        restoredAsk.RemainingQuantity.Should().Be(50);
        engine.BestAsk.Should().Be(450.00m);
    }

    [Fact]
    public void SubmitOrder_FOK_MultipleRestingOrders_ShouldRollbackAll()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Create multiple resting orders at the same price
        var ask1 = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 30,
            Type = OrderType.Limit
        });

        var ask2 = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 40,
            Type = OrderType.Limit
        });

        // Act - FOK order cannot be fully filled (wants 100, only 70 available)
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.FOK
        });

        // Assert - FOK should be cancelled with no trades
        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        result.Trades.Should().BeEmpty();

        // Both resting orders should be restored
        var restoredAsk1 = engine.GetOrder(ask1.Order.OrderId);
        restoredAsk1.Should().NotBeNull();
        restoredAsk1!.Status.Should().Be(OrderStatus.Open);
        restoredAsk1.RemainingQuantity.Should().Be(30);

        var restoredAsk2 = engine.GetOrder(ask2.Order.OrderId);
        restoredAsk2.Should().NotBeNull();
        restoredAsk2!.Status.Should().Be(OrderStatus.Open);
        restoredAsk2.RemainingQuantity.Should().Be(40);
    }

    [Fact]
    public void SubmitOrder_FOK_RestoresFilledOrders_ShouldReopenInBook()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // Create a resting order that will be fully consumed during partial match
        var askResult = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        // Verify initial state
        engine.AskLevels.Should().Be(1);

        // Act - FOK order cannot be fully filled (wants 100, only 50 available)
        // The resting order would have been "filled" during matching before rollback
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.FOK
        });

        // Assert - Order book should be restored with the ask level back
        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        engine.AskLevels.Should().Be(1);
        engine.BestAsk.Should().Be(450.00m);

        // The previously "filled" order should be reopened
        var restoredAsk = engine.GetOrder(askResult.Order.OrderId);
        restoredAsk.Should().NotBeNull();
        restoredAsk!.Status.Should().Be(OrderStatus.Open);
    }

    [Fact]
    public void SubmitOrder_FOK_NoLiquidity_ShouldCancelWithoutTrades()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");

        // No resting orders - empty book

        // Act - FOK order with no matching liquidity
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.FOK
        });

        // Assert
        result.Order.Status.Should().Be(OrderStatus.Cancelled);
        result.Trades.Should().BeEmpty();
        result.Order.RemainingQuantity.Should().Be(100);
    }

    [Fact]
    public void SubmitOrder_FOK_NoTradesExecuted_ShouldNotRaiseTradeEvent()
    {
        // Arrange
        var engine = new OrderBookMatchingEngine("SPY");
        var tradesExecuted = new List<TradeExecution>();
        engine.TradeExecuted += (sender, args) => tradesExecuted.Add(args.Trade);

        engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Ask,
            Price = 450.00m,
            Quantity = 50,
            Type = OrderType.Limit
        });

        // Act - FOK order that will be rolled back
        var result = engine.SubmitOrder(new OrderRequest
        {
            Side = OrderBookSide.Bid,
            Price = 450.00m,
            Quantity = 100,
            Type = OrderType.Limit,
            TimeInForce = TimeInForce.FOK
        });

        // Assert - No trade events should have been raised (trades cleared before events)
        tradesExecuted.Should().BeEmpty();
        result.Trades.Should().BeEmpty();
    }
}
