using FluentAssertions;
using MarketDataCollector.Domain.Models;
using Xunit;

namespace MarketDataCollector.Tests.Models;

/// <summary>
/// Unit tests for the AggregateBar model.
/// </summary>
public class AggregateBarTests
{
    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 450.00m,
            High: 451.00m,
            Low: 449.50m,
            Close: 450.75m,
            Volume: 1000000,
            Vwap: 450.50m,
            TradeCount: 5000,
            Timeframe: AggregateTimeframe.Minute,
            Source: "Polygon",
            SequenceNumber: 123456789);

        // Assert
        bar.Symbol.Should().Be("SPY");
        bar.StartTime.Should().Be(startTime);
        bar.EndTime.Should().Be(endTime);
        bar.Open.Should().Be(450.00m);
        bar.High.Should().Be(451.00m);
        bar.Low.Should().Be(449.50m);
        bar.Close.Should().Be(450.75m);
        bar.Volume.Should().Be(1000000);
        bar.Vwap.Should().Be(450.50m);
        bar.TradeCount.Should().Be(5000);
        bar.Timeframe.Should().Be(AggregateTimeframe.Minute);
        bar.Source.Should().Be("Polygon");
        bar.SequenceNumber.Should().Be(123456789);
    }

    [Fact]
    public void Constructor_WithNullSymbol_ThrowsArgumentException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: null!,
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Symbol*");
    }

    [Fact]
    public void Constructor_WithEmptySymbol_ThrowsArgumentException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Symbol*");
    }

    [Fact]
    public void Constructor_WithWhitespaceSymbol_ThrowsArgumentException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "   ",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Symbol*");
    }

    [Theory]
    [InlineData(0, 101, 99, 100.5)]      // Zero Open
    [InlineData(-1, 101, 99, 100.5)]     // Negative Open
    [InlineData(100, 0, 99, 100.5)]      // Zero High
    [InlineData(100, -101, 99, 100.5)]   // Negative High
    [InlineData(100, 101, 0, 100.5)]     // Zero Low
    [InlineData(100, 101, -99, 100.5)]   // Negative Low
    [InlineData(100, 101, 99, 0)]        // Zero Close
    [InlineData(100, 101, 99, -100.5)]   // Negative Close
    public void Constructor_WithInvalidOhlcValues_ThrowsArgumentOutOfRangeException(
        decimal open, decimal high, decimal low, decimal close)
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: open,
            High: high,
            Low: low,
            Close: close,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithLowGreaterThanHigh_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 99m,    // High is less than Low
            Low: 100m,
            Close: 99.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Low*");
    }

    [Fact]
    public void Constructor_WithOpenGreaterThanHigh_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 102m,   // Open exceeds High
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*High*");
    }

    [Fact]
    public void Constructor_WithCloseGreaterThanHigh_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 102m,  // Close exceeds High
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*High*");
    }

    [Fact]
    public void Constructor_WithOpenLessThanLow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 98m,    // Open below Low
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Low*");
    }

    [Fact]
    public void Constructor_WithCloseLessThanLow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 98m,   // Close below Low
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Low*");
    }

    [Fact]
    public void Constructor_WithNegativeVolume_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: -1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Volume*");
    }

    [Fact]
    public void Constructor_WithEndTimeBeforeStartTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(-1); // End before start

        // Act
        var act = () => new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*End time*");
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Constructor_WithMinimalParameters_UsesDefaultValues()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(1);

        // Act
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Assert - verify defaults
        bar.Vwap.Should().Be(0m);
        bar.TradeCount.Should().Be(0);
        bar.Timeframe.Should().Be(AggregateTimeframe.Minute);
        bar.Source.Should().Be("Polygon");
        bar.SequenceNumber.Should().Be(0);
    }

    #endregion

    #region Timeframe Tests

    [Theory]
    [InlineData(AggregateTimeframe.Second)]
    [InlineData(AggregateTimeframe.Minute)]
    [InlineData(AggregateTimeframe.Hour)]
    [InlineData(AggregateTimeframe.Day)]
    public void Constructor_WithDifferentTimeframes_SetsCorrectTimeframe(AggregateTimeframe timeframe)
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000,
            Timeframe: timeframe);

        // Assert
        bar.Timeframe.Should().Be(timeframe);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Constructor_WithZeroVolume_Succeeds()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 0);

        // Assert
        bar.Volume.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithSameStartAndEndTime_Succeeds()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;

        // Act
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: time,
            EndTime: time,
            Open: 100m,
            High: 100m,
            Low: 100m,
            Close: 100m,
            Volume: 1000);

        // Assert
        bar.StartTime.Should().Be(bar.EndTime);
    }

    [Fact]
    public void Constructor_WithAllOhlcSameValue_Succeeds()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(1);

        // Act
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 100m,
            Low: 100m,
            Close: 100m,
            Volume: 1000);

        // Assert
        bar.Open.Should().Be(100m);
        bar.High.Should().Be(100m);
        bar.Low.Should().Be(100m);
        bar.Close.Should().Be(100m);
    }

    #endregion

    #region Range Property Tests

    [Fact]
    public void Range_ReturnsHighMinusLow()
    {
        // Arrange
        var bar = CreateBar(high: 455.00m, low: 448.00m);

        // Act
        var range = bar.Range;

        // Assert
        range.Should().Be(7.00m);
    }

    [Fact]
    public void Range_WhenHighEqualsLow_ReturnsZero()
    {
        // Arrange - flat bar with no range
        var bar = CreateBar(open: 100m, high: 100m, low: 100m, close: 100m);

        // Act
        var range = bar.Range;

        // Assert
        range.Should().Be(0m);
    }

    #endregion

    #region BodySize Property Tests

    [Fact]
    public void BodySize_WhenCloseGreaterThanOpen_ReturnsPositiveDifference()
    {
        // Arrange - bullish bar
        var bar = CreateBar(open: 450.00m, close: 455.00m);

        // Act
        var bodySize = bar.BodySize;

        // Assert
        bodySize.Should().Be(5.00m);
    }

    [Fact]
    public void BodySize_WhenCloseLessThanOpen_ReturnsAbsoluteDifference()
    {
        // Arrange - bearish bar
        var bar = CreateBar(open: 455.00m, high: 456.00m, low: 449.00m, close: 450.00m);

        // Act
        var bodySize = bar.BodySize;

        // Assert
        bodySize.Should().Be(5.00m);
    }

    [Fact]
    public void BodySize_WhenCloseEqualsOpen_ReturnsZero()
    {
        // Arrange - doji pattern
        var bar = CreateBar(open: 450.00m, close: 450.00m);

        // Act
        var bodySize = bar.BodySize;

        // Assert
        bodySize.Should().Be(0m);
    }

    #endregion

    #region IsBullish Property Tests

    [Fact]
    public void IsBullish_WhenCloseGreaterThanOpen_ReturnsTrue()
    {
        // Arrange
        var bar = CreateBar(open: 450.00m, close: 455.00m);

        // Act & Assert
        bar.IsBullish.Should().BeTrue();
    }

    [Fact]
    public void IsBullish_WhenCloseLessThanOpen_ReturnsFalse()
    {
        // Arrange
        var bar = CreateBar(open: 455.00m, high: 456.00m, low: 449.00m, close: 450.00m);

        // Act & Assert
        bar.IsBullish.Should().BeFalse();
    }

    [Fact]
    public void IsBullish_WhenCloseEqualsOpen_ReturnsFalse()
    {
        // Arrange - doji pattern is not considered bullish
        var bar = CreateBar(open: 450.00m, close: 450.00m);

        // Act & Assert
        bar.IsBullish.Should().BeFalse();
    }

    #endregion

    #region IsBearish Property Tests

    [Fact]
    public void IsBearish_WhenCloseLessThanOpen_ReturnsTrue()
    {
        // Arrange
        var bar = CreateBar(open: 455.00m, high: 456.00m, low: 449.00m, close: 450.00m);

        // Act & Assert
        bar.IsBearish.Should().BeTrue();
    }

    [Fact]
    public void IsBearish_WhenCloseGreaterThanOpen_ReturnsFalse()
    {
        // Arrange
        var bar = CreateBar(open: 450.00m, close: 455.00m);

        // Act & Assert
        bar.IsBearish.Should().BeFalse();
    }

    [Fact]
    public void IsBearish_WhenCloseEqualsOpen_ReturnsFalse()
    {
        // Arrange - doji pattern is not considered bearish
        var bar = CreateBar(open: 450.00m, close: 450.00m);

        // Act & Assert
        bar.IsBearish.Should().BeFalse();
    }

    [Fact]
    public void IsBullishAndIsBearish_AreMutuallyExclusive()
    {
        // Arrange
        var bullishBar = CreateBar(open: 450.00m, close: 455.00m);
        var bearishBar = CreateBar(open: 455.00m, high: 456.00m, low: 449.00m, close: 450.00m);
        var dojiBar = CreateBar(open: 450.00m, close: 450.00m);

        // Assert
        bullishBar.IsBullish.Should().BeTrue();
        bullishBar.IsBearish.Should().BeFalse();

        bearishBar.IsBullish.Should().BeFalse();
        bearishBar.IsBearish.Should().BeTrue();

        // Doji is neither bullish nor bearish
        dojiBar.IsBullish.Should().BeFalse();
        dojiBar.IsBearish.Should().BeFalse();
    }

    #endregion

    #region ChangePercent Property Tests

    [Fact]
    public void ChangePercent_WhenPriceIncreased_ReturnsPositivePercentage()
    {
        // Arrange - 2% increase (from 100 to 102)
        var bar = CreateBar(open: 100.00m, high: 103.00m, low: 99.00m, close: 102.00m);

        // Act
        var changePercent = bar.ChangePercent;

        // Assert
        changePercent.Should().Be(2.00m);
    }

    [Fact]
    public void ChangePercent_WhenPriceDecreased_ReturnsNegativePercentage()
    {
        // Arrange - 5% decrease (from 100 to 95)
        var bar = CreateBar(open: 100.00m, high: 101.00m, low: 94.00m, close: 95.00m);

        // Act
        var changePercent = bar.ChangePercent;

        // Assert
        changePercent.Should().Be(-5.00m);
    }

    [Fact]
    public void ChangePercent_WhenPriceUnchanged_ReturnsZero()
    {
        // Arrange
        var bar = CreateBar(open: 100.00m, close: 100.00m);

        // Act
        var changePercent = bar.ChangePercent;

        // Assert
        changePercent.Should().Be(0m);
    }

    [Theory]
    [InlineData(200.00, 210.00, 5.00)]   // 5% up
    [InlineData(200.00, 190.00, -5.00)]  // 5% down
    [InlineData(50.00, 55.00, 10.00)]    // 10% up
    [InlineData(100.00, 75.00, -25.00)]  // 25% down
    public void ChangePercent_CalculatesCorrectlyForVariousScenarios(
        decimal open, decimal close, decimal expectedChange)
    {
        // Arrange
        var high = Math.Max(open, close) + 1m;
        var low = Math.Min(open, close) - 1m;
        var bar = CreateBar(open: open, high: high, low: low, close: close);

        // Act
        var changePercent = bar.ChangePercent;

        // Assert
        changePercent.Should().Be(expectedChange);
    }

    #endregion

    #region TypicalPrice Property Tests

    [Fact]
    public void TypicalPrice_CalculatesAverageOfHighLowClose()
    {
        // Arrange - (455 + 448 + 452) / 3 = 451.666...
        var bar = CreateBar(high: 455.00m, low: 448.00m, close: 452.00m);

        // Act
        var typicalPrice = bar.TypicalPrice;

        // Assert
        typicalPrice.Should().BeApproximately(451.6666666666666666666666667m, 0.0000001m);
    }

    [Fact]
    public void TypicalPrice_WhenAllPricesSame_ReturnsThatPrice()
    {
        // Arrange
        var bar = CreateBar(open: 100m, high: 100m, low: 100m, close: 100m);

        // Act
        var typicalPrice = bar.TypicalPrice;

        // Assert
        typicalPrice.Should().Be(100m);
    }

    #endregion

    #region Notional Property Tests

    [Fact]
    public void Notional_ReturnsCloseMultipliedByVolume()
    {
        // Arrange
        var bar = CreateBar(close: 450.00m, volume: 1000000);

        // Act
        var notional = bar.Notional;

        // Assert
        notional.Should().Be(450000000m); // 450 * 1,000,000
    }

    [Fact]
    public void Notional_WhenVolumeIsZero_ReturnsZero()
    {
        // Arrange
        var bar = CreateBar(close: 450.00m, volume: 0);

        // Act
        var notional = bar.Notional;

        // Assert
        notional.Should().Be(0m);
    }

    [Fact]
    public void Notional_WithLargeValues_CalculatesCorrectly()
    {
        // Arrange - simulating a high-priced stock with significant volume
        var bar = CreateBar(close: 3500.00m, volume: 5000000);

        // Act
        var notional = bar.Notional;

        // Assert
        notional.Should().Be(17500000000m); // 3500 * 5,000,000 = 17.5 billion
    }

    #endregion

    #region Duration Property Tests

    [Fact]
    public void Duration_ReturnsEndTimeMinusStartTime()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var endTime = startTime.AddMinutes(5);
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Act
        var duration = bar.Duration;

        // Assert
        duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Duration_WhenStartAndEndSame_ReturnsZero()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: time,
            EndTime: time,
            Open: 100m,
            High: 100m,
            Low: 100m,
            Close: 100m,
            Volume: 1000);

        // Act
        var duration = bar.Duration;

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1)]    // 1 second
    [InlineData(60)]   // 1 minute
    [InlineData(3600)] // 1 hour
    public void Duration_CalculatesCorrectlyForVariousTimeframes(int seconds)
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(seconds);
        var bar = new AggregateBar(
            Symbol: "SPY",
            StartTime: startTime,
            EndTime: endTime,
            Open: 100m,
            High: 101m,
            Low: 99m,
            Close: 100.5m,
            Volume: 1000);

        // Act
        var duration = bar.Duration;

        // Assert
        duration.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test AggregateBar with sensible defaults.
    /// Only specify the values you care about for the test.
    /// </summary>
    private static AggregateBar CreateBar(
        string symbol = "SPY",
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        decimal open = 450.00m,
        decimal? high = null,
        decimal? low = null,
        decimal close = 452.50m,
        long volume = 50000000,
        string source = "test")
    {
        // Ensure OHLC constraints are valid
        var actualHigh = high ?? Math.Max(open, close) + 2m;
        var actualLow = low ?? Math.Min(open, close) - 2m;
        var actualStartTime = startTime ?? DateTimeOffset.UtcNow;
        var actualEndTime = endTime ?? actualStartTime.AddMinutes(1);

        return new AggregateBar(
            Symbol: symbol,
            StartTime: actualStartTime,
            EndTime: actualEndTime,
            Open: open,
            High: actualHigh,
            Low: actualLow,
            Close: close,
            Volume: volume,
            Source: source);
    }

    #endregion
}
