using FluentAssertions;
using MarketDataCollector.Domain.Models;
using Xunit;

namespace MarketDataCollector.Tests;

public class OrderBookLevelTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesLevel()
    {
        // Act
        var level = new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: 0,
            Price: 450.50,
            Size: 100,
            MarketMaker: "MM1"
        );

        // Assert
        level.Side.Should().Be(OrderBookSide.Bid);
        level.Level.Should().Be(0);
        level.Price.Should().Be(450.50);
        level.Size.Should().Be(100);
        level.MarketMaker.Should().Be("MM1");
    }

    [Fact]
    public void Constructor_WithZeroPrice_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: 0,
            Price: 0,
            Size: 100
        );

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Price must be greater than 0*");
    }

    [Fact]
    public void Constructor_WithNegativePrice_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: 0,
            Price: -10.50,
            Size: 100
        );

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Price must be greater than 0*");
    }

    [Fact]
    public void Constructor_WithNegativeSize_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: 0,
            Price: 100,
            Size: -1
        );

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Size must be greater than or equal to 0*");
    }

    [Fact]
    public void Constructor_WithZeroSize_Succeeds()
    {
        // Act (zero size is valid - represents an empty level)
        var level = new OrderBookLevel(
            Side: OrderBookSide.Ask,
            Level: 0,
            Price: 100,
            Size: 0
        );

        // Assert
        level.Size.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNegativeLevel_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: -1,
            Price: 100,
            Size: 50
        );

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Level must be greater than or equal to 0*");
    }

    [Fact]
    public void Constructor_WithZeroLevel_Succeeds()
    {
        // Act (level 0 is the best bid/ask)
        var level = new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: 0,
            Price: 100,
            Size: 50
        );

        // Assert
        level.Level.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullMarketMaker_Succeeds()
    {
        // Act
        var level = new OrderBookLevel(
            Side: OrderBookSide.Bid,
            Level: 0,
            Price: 100,
            Size: 50,
            MarketMaker: null
        );

        // Assert
        level.MarketMaker.Should().BeNull();
    }

    [Theory]
    [InlineData(OrderBookSide.Bid)]
    [InlineData(OrderBookSide.Ask)]
    public void Constructor_WithDifferentSides_StoresSideCorrectly(OrderBookSide side)
    {
        // Act
        var level = new OrderBookLevel(
            Side: side,
            Level: 0,
            Price: 100,
            Size: 50
        );

        // Assert
        level.Side.Should().Be(side);
    }

    [Fact]
    public void Equality_TwoLevelsWithSameValues_AreEqual()
    {
        // Arrange
        var level1 = new OrderBookLevel(OrderBookSide.Bid, 0, 100.50, 200, "MM1");
        var level2 = new OrderBookLevel(OrderBookSide.Bid, 0, 100.50, 200, "MM1");

        // Assert (records have value equality)
        level1.Should().Be(level2);
    }

    [Fact]
    public void Equality_TwoLevelsWithDifferentPrices_AreNotEqual()
    {
        // Arrange
        var level1 = new OrderBookLevel(OrderBookSide.Bid, 0, 100.50, 200);
        var level2 = new OrderBookLevel(OrderBookSide.Bid, 0, 100.51, 200);

        // Assert
        level1.Should().NotBe(level2);
    }

    [Fact]
    public void With_CreatesNewLevelWithModifiedValue()
    {
        // Arrange
        var original = new OrderBookLevel(OrderBookSide.Bid, 0, 100, 200);

        // Act (records support 'with' expressions)
        var modified = original with { Size = 300 };

        // Assert
        original.Size.Should().Be(200);
        modified.Size.Should().Be(300);
        modified.Price.Should().Be(100);
    }
}
