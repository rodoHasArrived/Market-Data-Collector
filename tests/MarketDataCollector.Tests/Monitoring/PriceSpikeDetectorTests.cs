using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using Xunit;

namespace MarketDataCollector.Tests.Monitoring;

public sealed class PriceSpikeDetectorTests : IDisposable
{
    private readonly PriceSpikeDetector _detector;

    public PriceSpikeDetectorTests()
    {
        _detector = new PriceSpikeDetector(new PriceSpikeConfig
        {
            SpikeThresholdPercent = 5.0,
            WindowMs = 60000,
            AlertCooldownMs = 0 // Disable cooldown for tests
        });
    }

    [Fact]
    public void ProcessTrade_WithSmallPriceChange_ShouldNotDetectSpike()
    {
        // Arrange - establish baseline price
        _detector.ProcessTrade("AAPL", 150.00m, "Provider1");

        // Act - small 2% change
        var detected = _detector.ProcessTrade("AAPL", 153.00m, "Provider1");

        // Assert
        detected.Should().BeFalse();
        _detector.TotalSpikesDetected.Should().Be(0);
    }

    [Fact]
    public void ProcessTrade_WithLargePriceIncrease_ShouldDetectSpike()
    {
        // Arrange - establish baseline price
        _detector.ProcessTrade("AAPL", 100.00m, "Provider1");

        PriceSpikeAlert? capturedAlert = null;
        _detector.OnPriceSpike += alert => capturedAlert = alert;

        // Act - 10% increase (exceeds 5% threshold)
        var detected = _detector.ProcessTrade("AAPL", 110.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        _detector.TotalSpikesDetected.Should().Be(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.Symbol.Should().Be("AAPL");
        capturedAlert.Value.Direction.Should().Be(SpikeDirection.Up);
        capturedAlert.Value.ChangePercent.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public void ProcessTrade_WithLargePriceDecrease_ShouldDetectSpike()
    {
        // Arrange - establish baseline price
        _detector.ProcessTrade("AAPL", 100.00m, "Provider1");

        PriceSpikeAlert? capturedAlert = null;
        _detector.OnPriceSpike += alert => capturedAlert = alert;

        // Act - 10% decrease (exceeds 5% threshold)
        var detected = _detector.ProcessTrade("AAPL", 90.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        _detector.TotalSpikesDetected.Should().Be(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.Direction.Should().Be(SpikeDirection.Down);
    }

    [Fact]
    public void ProcessQuote_WithLargeMidPriceChange_ShouldDetectSpike()
    {
        // Arrange - establish baseline
        _detector.ProcessQuote("AAPL", 99.00m, 101.00m, "Provider1"); // Mid = 100

        PriceSpikeAlert? capturedAlert = null;
        _detector.OnPriceSpike += alert => capturedAlert = alert;

        // Act - 10% increase in mid-price
        var detected = _detector.ProcessQuote("AAPL", 109.00m, 111.00m, "Provider1"); // Mid = 110

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.PriceType.Should().Be(PriceSpikeType.Quote);
    }

    [Fact]
    public void ProcessBar_WithLargeCloseChange_ShouldDetectSpike()
    {
        // Arrange - establish baseline
        _detector.ProcessBar("AAPL", 100.00m, "Provider1");

        PriceSpikeAlert? capturedAlert = null;
        _detector.OnPriceSpike += alert => capturedAlert = alert;

        // Act - large change
        var detected = _detector.ProcessBar("AAPL", 115.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.PriceType.Should().Be(PriceSpikeType.Bar);
    }

    [Fact]
    public void FirstTrade_ShouldNotTriggerSpike()
    {
        // Arrange
        PriceSpikeAlert? capturedAlert = null;
        _detector.OnPriceSpike += alert => capturedAlert = alert;

        // Act - first trade has no reference
        var detected = _detector.ProcessTrade("AAPL", 100.00m, "Provider1");

        // Assert
        detected.Should().BeFalse();
        capturedAlert.Should().BeNull();
    }

    [Fact]
    public void MultipleSymbols_ShouldTrackIndependently()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", 100.00m, "Provider1");
        _detector.ProcessTrade("MSFT", 200.00m, "Provider1");

        var alerts = new List<PriceSpikeAlert>();
        _detector.OnPriceSpike += alert => alerts.Add(alert);

        // Act - spike on AAPL only
        _detector.ProcessTrade("AAPL", 110.00m, "Provider1"); // 10% spike
        _detector.ProcessTrade("MSFT", 205.00m, "Provider1"); // 2.5% - no spike

        // Assert
        alerts.Should().HaveCount(1);
        alerts[0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectStatistics()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", 100.00m, "Provider1");
        _detector.ProcessTrade("AAPL", 110.00m, "Provider1"); // Spike

        // Act
        var stats = _detector.GetStats();

        // Assert
        stats.TotalSpikesDetected.Should().Be(1);
        stats.TotalEventsProcessed.Should().BeGreaterThan(0);
        stats.SymbolStats.Should().Contain(s => s.Symbol == "AAPL");
    }

    [Fact]
    public void GetRecentSpikeSymbols_ShouldReturnAffectedSymbols()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", 100.00m, "Provider1");
        _detector.ProcessTrade("AAPL", 115.00m, "Provider1"); // Spike
        _detector.ProcessTrade("MSFT", 200.00m, "Provider1");
        _detector.ProcessTrade("MSFT", 202.00m, "Provider1"); // No spike

        // Act
        var symbols = _detector.GetRecentSpikeSymbols(60);

        // Assert
        symbols.Should().Contain("AAPL");
        symbols.Should().NotContain("MSFT");
    }

    [Fact]
    public void SensitiveConfig_ShouldDetectSmallerMoves()
    {
        // Arrange
        using var sensitiveDetector = new PriceSpikeDetector(PriceSpikeConfig.Sensitive);
        sensitiveDetector.ProcessTrade("AAPL", 100.00m, "Provider1");

        PriceSpikeAlert? capturedAlert = null;
        sensitiveDetector.OnPriceSpike += alert => capturedAlert = alert;

        // Act - 3% change should trigger with 2% threshold
        sensitiveDetector.ProcessTrade("AAPL", 103.00m, "Provider1");

        // Assert
        capturedAlert.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldStopProcessing()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", 100.00m, "Provider1");
        _detector.Dispose();

        // Act
        var detected = _detector.ProcessTrade("AAPL", 200.00m, "Provider1");

        // Assert
        detected.Should().BeFalse();
    }

    public void Dispose()
    {
        _detector.Dispose();
    }
}
