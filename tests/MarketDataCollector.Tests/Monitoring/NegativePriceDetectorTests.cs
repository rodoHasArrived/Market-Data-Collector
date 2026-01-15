using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using Xunit;

namespace MarketDataCollector.Tests.Monitoring;

public sealed class NegativePriceDetectorTests : IDisposable
{
    private readonly NegativePriceDetector _detector;

    public NegativePriceDetectorTests()
    {
        _detector = new NegativePriceDetector(new NegativePriceConfig
        {
            AlertCooldownMs = 0, // Disable cooldown for tests
            DetectZeroPrices = true,
            ZeroPriceCooldownMs = 0
        });
    }

    [Fact]
    public void ProcessTrade_WithPositivePrice_ShouldNotDetectAnomaly()
    {
        // Arrange & Act
        var detected = _detector.ProcessTrade("AAPL", 150.00m, 100, "Provider1");

        // Assert
        detected.Should().BeFalse();
        _detector.TotalNegativePriceEvents.Should().Be(0);
    }

    [Fact]
    public void ProcessTrade_WithNegativePrice_ShouldDetectAnomaly()
    {
        // Arrange
        NegativePriceAlert? capturedAlert = null;
        _detector.OnNegativePrice += alert => capturedAlert = alert;

        // Act
        var detected = _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");

        // Assert
        detected.Should().BeTrue();
        _detector.TotalNegativePriceEvents.Should().Be(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.Symbol.Should().Be("AAPL");
        capturedAlert.Value.Price.Should().Be(-150.00m);
        capturedAlert.Value.PriceType.Should().Be(PriceType.Trade);
        capturedAlert.Value.Provider.Should().Be("Provider1");
    }

    [Fact]
    public void ProcessTrade_WithZeroPrice_ShouldDetectAnomaly()
    {
        // Arrange
        ZeroPriceAlert? capturedAlert = null;
        _detector.OnZeroPrice += alert => capturedAlert = alert;

        // Act
        var detected = _detector.ProcessTrade("AAPL", 0m, 100, "Provider1");

        // Assert
        detected.Should().BeTrue();
        _detector.TotalZeroPriceEvents.Should().Be(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.Symbol.Should().Be("AAPL");
        capturedAlert.Value.PriceType.Should().Be(PriceType.Trade);
    }

    [Fact]
    public void ProcessTrade_WithZeroPriceDisabled_ShouldNotDetect()
    {
        // Arrange
        using var detector = new NegativePriceDetector(new NegativePriceConfig
        {
            DetectZeroPrices = false
        });

        // Act
        var detected = detector.ProcessTrade("AAPL", 0m, 100, "Provider1");

        // Assert
        detected.Should().BeFalse();
        detector.TotalZeroPriceEvents.Should().Be(0);
    }

    [Fact]
    public void ProcessQuote_WithNegativeBidPrice_ShouldDetect()
    {
        // Arrange
        NegativePriceAlert? capturedAlert = null;
        _detector.OnNegativePrice += alert => capturedAlert = alert;

        // Act
        var detected = _detector.ProcessQuote("AAPL", -150.00m, 151.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.PriceType.Should().Be(PriceType.Bid);
    }

    [Fact]
    public void ProcessQuote_WithNegativeAskPrice_ShouldDetect()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        var detected = _detector.ProcessQuote("AAPL", 150.00m, -151.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        alerts.Should().ContainSingle();
        alerts[0].PriceType.Should().Be(PriceType.Ask);
    }

    [Fact]
    public void ProcessQuote_WithBothNegativePrices_ShouldDetectBoth()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        var detected = _detector.ProcessQuote("AAPL", -150.00m, -151.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        alerts.Should().HaveCount(2);
        alerts.Select(a => a.PriceType).Should().Contain(PriceType.Bid);
        alerts.Select(a => a.PriceType).Should().Contain(PriceType.Ask);
    }

    [Fact]
    public void ProcessBar_WithNegativeOpenPrice_ShouldDetect()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        var detected = _detector.ProcessBar("AAPL", -100.00m, 150.00m, 140.00m, 145.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        alerts.Should().ContainSingle();
        alerts[0].PriceType.Should().Be(PriceType.Open);
    }

    [Fact]
    public void ProcessBar_WithNegativeHighPrice_ShouldDetect()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        var detected = _detector.ProcessBar("AAPL", 100.00m, -150.00m, 140.00m, 145.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        alerts.Should().ContainSingle();
        alerts[0].PriceType.Should().Be(PriceType.High);
    }

    [Fact]
    public void ProcessBar_WithNegativeLowPrice_ShouldDetect()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        var detected = _detector.ProcessBar("AAPL", 100.00m, 150.00m, -140.00m, 145.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        alerts.Should().ContainSingle();
        alerts[0].PriceType.Should().Be(PriceType.Low);
    }

    [Fact]
    public void ProcessBar_WithNegativeClosePrice_ShouldDetect()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        var detected = _detector.ProcessBar("AAPL", 100.00m, 150.00m, 140.00m, -145.00m, "Provider1");

        // Assert
        detected.Should().BeTrue();
        alerts.Should().ContainSingle();
        alerts[0].PriceType.Should().Be(PriceType.Close);
    }

    [Fact]
    public void ProcessBar_WithAllValidPrices_ShouldNotDetect()
    {
        // Arrange & Act
        var detected = _detector.ProcessBar("AAPL", 100.00m, 150.00m, 95.00m, 145.00m, "Provider1");

        // Assert
        detected.Should().BeFalse();
        _detector.TotalNegativePriceEvents.Should().Be(0);
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectCounts()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        _detector.ProcessTrade("AAPL", 0m, 100, "Provider1");
        _detector.ProcessTrade("MSFT", -300.00m, 50, "Provider2");

        // Act
        var stats = _detector.GetStats();

        // Assert
        stats.TotalNegativePriceEvents.Should().Be(2);
        stats.TotalZeroPriceEvents.Should().Be(1);
        stats.TotalEventsProcessed.Should().Be(3);
        stats.SymbolStats.Should().HaveCount(2);
    }

    [Fact]
    public void GetRecentNegativePriceSymbols_ShouldReturnAffectedSymbols()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        _detector.ProcessTrade("MSFT", 300.00m, 50, "Provider2"); // Valid price
        _detector.ProcessTrade("GOOGL", -200.00m, 75, "Provider1");

        // Act
        var symbols = _detector.GetRecentNegativePriceSymbols(60);

        // Assert
        symbols.Should().HaveCount(2);
        symbols.Should().Contain("AAPL");
        symbols.Should().Contain("GOOGL");
        symbols.Should().NotContain("MSFT");
    }

    [Fact]
    public void ConsecutiveCount_ShouldIncrementForRepeatedNegativePrices()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        _detector.ProcessTrade("AAPL", -155.00m, 100, "Provider1");
        _detector.ProcessTrade("AAPL", -160.00m, 100, "Provider1");

        // Assert
        alerts.Should().HaveCount(3);
        alerts[0].ConsecutiveCount.Should().Be(1);
        alerts[1].ConsecutiveCount.Should().Be(2);
        alerts[2].ConsecutiveCount.Should().Be(3);
    }

    [Fact]
    public void ConsecutiveCount_ShouldResetAfterValidPrice()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        _detector.ProcessTrade("AAPL", -155.00m, 100, "Provider1");
        _detector.ProcessTrade("AAPL", 160.00m, 100, "Provider1"); // Valid price - resets count
        _detector.ProcessTrade("AAPL", -165.00m, 100, "Provider1");

        // Assert
        alerts.Should().HaveCount(3);
        alerts[0].ConsecutiveCount.Should().Be(1);
        alerts[1].ConsecutiveCount.Should().Be(2);
        alerts[2].ConsecutiveCount.Should().Be(1); // Reset after valid price
    }

    [Fact]
    public void AlertCooldown_ShouldPreventRapidAlerts()
    {
        // Arrange
        using var detectorWithCooldown = new NegativePriceDetector(new NegativePriceConfig
        {
            AlertCooldownMs = 10000, // 10 second cooldown
            DetectZeroPrices = true
        });

        var alerts = new List<NegativePriceAlert>();
        detectorWithCooldown.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        detectorWithCooldown.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        detectorWithCooldown.ProcessTrade("AAPL", -155.00m, 100, "Provider1"); // Should be suppressed

        // Assert
        alerts.Should().HaveCount(1); // Only first alert fired
        detectorWithCooldown.TotalNegativePriceEvents.Should().Be(2); // Both events counted
    }

    [Fact]
    public void MultipleSymbols_ShouldTrackIndependently()
    {
        // Arrange
        var alerts = new List<NegativePriceAlert>();
        _detector.OnNegativePrice += alert => alerts.Add(alert);

        // Act
        _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        _detector.ProcessTrade("MSFT", -300.00m, 50, "Provider1");
        _detector.ProcessTrade("GOOGL", -200.00m, 75, "Provider1");

        // Assert
        alerts.Should().HaveCount(3);
        alerts.Select(a => a.Symbol).Should().Contain("AAPL");
        alerts.Select(a => a.Symbol).Should().Contain("MSFT");
        alerts.Select(a => a.Symbol).Should().Contain("GOOGL");
    }

    [Fact]
    public void SymbolStats_ShouldTrackPerSymbol()
    {
        // Arrange
        _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");
        _detector.ProcessTrade("AAPL", -155.00m, 100, "Provider1");
        _detector.ProcessTrade("MSFT", -300.00m, 50, "Provider2");

        // Act
        var stats = _detector.GetStats();
        var aaplStats = stats.SymbolStats.FirstOrDefault(s => s.Symbol == "AAPL");
        var msftStats = stats.SymbolStats.FirstOrDefault(s => s.Symbol == "MSFT");

        // Assert
        aaplStats.Should().NotBeNull();
        aaplStats.TotalNegativePriceEvents.Should().Be(2);

        msftStats.Should().NotBeNull();
        msftStats.TotalNegativePriceEvents.Should().Be(1);
    }

    [Fact]
    public void Dispose_ShouldNotProcessAfterDisposed()
    {
        // Arrange
        _detector.Dispose();

        // Act
        var detected = _detector.ProcessTrade("AAPL", -150.00m, 100, "Provider1");

        // Assert
        detected.Should().BeFalse();
    }

    public void Dispose()
    {
        _detector.Dispose();
    }
}
