using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Models;
using Xunit;

namespace MarketDataCollector.Tests.Models;

/// <summary>
/// Unit tests for the HistoricalQuote model and its utility methods.
/// </summary>
public class HistoricalQuoteTests
{
    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var quote = new HistoricalQuote(
            Symbol: "SPY",
            Timestamp: DateTimeOffset.UtcNow,
            AskExchange: "NASDAQ",
            AskPrice: 450.25m,
            AskSize: 200,
            BidExchange: "NYSE",
            BidPrice: 450.20m,
            BidSize: 100,
            Source: "alpaca");

        // Assert
        quote.Symbol.Should().Be("SPY");
        quote.AskExchange.Should().Be("NASDAQ");
        quote.AskPrice.Should().Be(450.25m);
        quote.AskSize.Should().Be(200);
        quote.BidExchange.Should().Be("NYSE");
        quote.BidPrice.Should().Be(450.20m);
        quote.BidSize.Should().Be(100);
        quote.Source.Should().Be("alpaca");
    }

    [Fact]
    public void Constructor_WithEmptySymbol_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new HistoricalQuote(
            Symbol: "",
            Timestamp: DateTimeOffset.UtcNow,
            AskExchange: "NASDAQ",
            AskPrice: 100m,
            AskSize: 100,
            BidExchange: "NYSE",
            BidPrice: 99m,
            BidSize: 100);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("Symbol");
    }

    [Fact]
    public void Constructor_WithNegativeAskPrice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new HistoricalQuote(
            Symbol: "SPY",
            Timestamp: DateTimeOffset.UtcNow,
            AskExchange: "NASDAQ",
            AskPrice: -100m,
            AskSize: 100,
            BidExchange: "NYSE",
            BidPrice: 99m,
            BidSize: 100);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("AskPrice");
    }

    #endregion

    #region Spread Property Tests

    [Fact]
    public void Spread_ReturnsAskMinusBid()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 450.20m, askPrice: 450.25m);

        // Act
        var spread = quote.Spread;

        // Assert
        spread.Should().Be(0.05m);
    }

    [Fact]
    public void Spread_WhenZeroPrices_ReturnsZero()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 0m, askPrice: 0m);

        // Act
        var spread = quote.Spread;

        // Assert
        spread.Should().Be(0m);
    }

    #endregion

    #region MidPrice Property Tests

    [Fact]
    public void MidPrice_ReturnsAverageOfBidAndAsk()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 100m, askPrice: 102m);

        // Act
        var midPrice = quote.MidPrice;

        // Assert
        midPrice.Should().Be(101m);
    }

    #endregion

    #region SpreadBps Property Tests

    [Fact]
    public void SpreadBps_WhenValidPrices_ReturnsSpreadInBasisPoints()
    {
        // Arrange - 5 cent spread on $100 mid = 5 bps
        var quote = CreateQuote(bidPrice: 99.975m, askPrice: 100.025m);

        // Act
        var spreadBps = quote.SpreadBps;

        // Assert
        spreadBps.Should().BeApproximately(5.0m, 0.001m);
    }

    [Fact]
    public void SpreadBps_WhenMidPriceIsZero_ReturnsNull()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 0m, askPrice: 0m);

        // Act
        var spreadBps = quote.SpreadBps;

        // Assert
        spreadBps.Should().BeNull();
    }

    #endregion

    #region IsValid Property Tests

    [Fact]
    public void IsValid_WhenBidLessThanAsk_ReturnsTrue()
    {
        // Arrange - normal market state
        var quote = CreateQuote(bidPrice: 100m, askPrice: 101m);

        // Act & Assert
        quote.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenBidEqualsAsk_ReturnsFalse()
    {
        // Arrange - locked market
        var quote = CreateQuote(bidPrice: 100m, askPrice: 100m);

        // Act & Assert
        quote.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenBidGreaterThanAsk_ReturnsFalse()
    {
        // Arrange - crossed market
        var quote = CreateQuote(bidPrice: 101m, askPrice: 100m);

        // Act & Assert
        quote.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenBidIsZero_ReturnsFalse()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 0m, askPrice: 100m);

        // Act & Assert
        quote.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenAskIsZero_ReturnsFalse()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 100m, askPrice: 0m);

        // Act & Assert
        quote.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(99.50, 100.00, true)]   // Normal spread
    [InlineData(100.00, 100.01, true)]  // Penny spread
    [InlineData(100.00, 100.00, false)] // Locked
    [InlineData(100.01, 100.00, false)] // Crossed
    [InlineData(0, 100.00, false)]      // Zero bid
    [InlineData(100.00, 0, false)]      // Zero ask
    public void IsValid_VariousScenarios(decimal bid, decimal ask, bool expected)
    {
        // Arrange
        var quote = CreateQuote(bidPrice: bid, askPrice: ask);

        // Act & Assert
        quote.IsValid.Should().Be(expected);
    }

    #endregion

    #region IsLocked Property Tests

    [Fact]
    public void IsLocked_WhenBidEqualsAsk_ReturnsTrue()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 100m, askPrice: 100m);

        // Act & Assert
        quote.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void IsLocked_WhenBidLessThanAsk_ReturnsFalse()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 99m, askPrice: 100m);

        // Act & Assert
        quote.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void IsLocked_WhenBidGreaterThanAsk_ReturnsFalse()
    {
        // Arrange - crossed market is not considered locked
        var quote = CreateQuote(bidPrice: 101m, askPrice: 100m);

        // Act & Assert
        quote.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void IsLocked_WhenBothPricesAreZero_ReturnsFalse()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 0m, askPrice: 0m);

        // Act & Assert
        quote.IsLocked.Should().BeFalse();
    }

    #endregion

    #region IsCrossed Property Tests

    [Fact]
    public void IsCrossed_WhenBidGreaterThanAsk_ReturnsTrue()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 101m, askPrice: 100m);

        // Act & Assert
        quote.IsCrossed.Should().BeTrue();
    }

    [Fact]
    public void IsCrossed_WhenBidLessThanAsk_ReturnsFalse()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 99m, askPrice: 100m);

        // Act & Assert
        quote.IsCrossed.Should().BeFalse();
    }

    [Fact]
    public void IsCrossed_WhenBidEqualsAsk_ReturnsFalse()
    {
        // Arrange - locked market is not considered crossed
        var quote = CreateQuote(bidPrice: 100m, askPrice: 100m);

        // Act & Assert
        quote.IsCrossed.Should().BeFalse();
    }

    [Fact]
    public void IsCrossed_WhenBidIsZero_ReturnsFalse()
    {
        // Arrange
        var quote = CreateQuote(bidPrice: 0m, askPrice: 100m);

        // Act & Assert
        quote.IsCrossed.Should().BeFalse();
    }

    #endregion

    #region State Property Mutual Exclusivity Tests

    [Fact]
    public void MarketStates_AreMutuallyExclusive()
    {
        // Arrange
        var validQuote = CreateQuote(bidPrice: 99m, askPrice: 100m);
        var lockedQuote = CreateQuote(bidPrice: 100m, askPrice: 100m);
        var crossedQuote = CreateQuote(bidPrice: 101m, askPrice: 100m);

        // Assert - Valid quote
        validQuote.IsValid.Should().BeTrue();
        validQuote.IsLocked.Should().BeFalse();
        validQuote.IsCrossed.Should().BeFalse();

        // Assert - Locked quote
        lockedQuote.IsValid.Should().BeFalse();
        lockedQuote.IsLocked.Should().BeTrue();
        lockedQuote.IsCrossed.Should().BeFalse();

        // Assert - Crossed quote
        crossedQuote.IsValid.Should().BeFalse();
        crossedQuote.IsLocked.Should().BeFalse();
        crossedQuote.IsCrossed.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test HistoricalQuote with sensible defaults.
    /// Only specify the values you care about for the test.
    /// </summary>
    private static HistoricalQuote CreateQuote(
        string symbol = "SPY",
        DateTimeOffset? timestamp = null,
        string askExchange = "NASDAQ",
        decimal askPrice = 100.25m,
        long askSize = 200,
        string bidExchange = "NYSE",
        decimal bidPrice = 100.20m,
        long bidSize = 100,
        string source = "test")
    {
        return new HistoricalQuote(
            Symbol: symbol,
            Timestamp: timestamp ?? DateTimeOffset.UtcNow,
            AskExchange: askExchange,
            AskPrice: askPrice,
            AskSize: askSize,
            BidExchange: bidExchange,
            BidPrice: bidPrice,
            BidSize: bidSize,
            Source: source);
    }

    #endregion
}
