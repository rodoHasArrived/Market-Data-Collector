using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using Xunit;

namespace MarketDataCollector.Tests.Monitoring;

public sealed class FutureTimestampDetectorTests : IDisposable
{
    private readonly FutureTimestampDetector _detector;

    public FutureTimestampDetectorTests()
    {
        _detector = new FutureTimestampDetector(new FutureTimestampConfig
        {
            AlertCooldownMs = 0, // Disable cooldown for tests
            ToleranceMs = 1000 // 1 second tolerance
        });
    }

    [Fact]
    public void ProcessTimestamp_WithPastTimestamp_ShouldNotDetectAnomaly()
    {
        // Arrange
        var pastTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Act
        var detected = _detector.ProcessTimestamp("AAPL", pastTimestamp, EventTimestampType.Trade, "Provider1");

        // Assert
        detected.Should().BeFalse();
        _detector.TotalFutureTimestampEvents.Should().Be(0);
    }

    [Fact]
    public void ProcessTimestamp_WithCurrentTimestamp_ShouldNotDetectAnomaly()
    {
        // Arrange
        var currentTimestamp = DateTimeOffset.UtcNow;

        // Act
        var detected = _detector.ProcessTimestamp("AAPL", currentTimestamp, EventTimestampType.Trade, "Provider1");

        // Assert
        detected.Should().BeFalse();
        _detector.TotalFutureTimestampEvents.Should().Be(0);
    }

    [Fact]
    public void ProcessTimestamp_WithTimestampWithinTolerance_ShouldNotDetectAnomaly()
    {
        // Arrange
        var slightlyFutureTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(500); // Within 1 second tolerance

        // Act
        var detected = _detector.ProcessTimestamp("AAPL", slightlyFutureTimestamp, EventTimestampType.Trade, "Provider1");

        // Assert
        detected.Should().BeFalse();
        _detector.TotalFutureTimestampEvents.Should().Be(0);
    }

    [Fact]
    public void ProcessTimestamp_WithTimestampBeyondTolerance_ShouldDetectAnomaly()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var futureTimestamp = DateTimeOffset.UtcNow.AddSeconds(10); // 10 seconds in future (beyond 1 second tolerance)

        // Act
        var detected = _detector.ProcessTimestamp("AAPL", futureTimestamp, EventTimestampType.Trade, "Provider1");

        // Assert
        detected.Should().BeTrue();
        _detector.TotalFutureTimestampEvents.Should().Be(1);
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.Symbol.Should().Be("AAPL");
        capturedAlert.Value.EventType.Should().Be(EventTimestampType.Trade);
        capturedAlert.Value.Provider.Should().Be("Provider1");
        capturedAlert.Value.DriftMs.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void ProcessTrade_WithFutureTimestamp_ShouldDetect()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var detected = _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.EventType.Should().Be(EventTimestampType.Trade);
    }

    [Fact]
    public void ProcessQuote_WithFutureTimestamp_ShouldDetect()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var detected = _detector.ProcessQuote("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.EventType.Should().Be(EventTimestampType.Quote);
    }

    [Fact]
    public void ProcessBar_WithFutureTimestamp_ShouldDetect()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var detected = _detector.ProcessBar("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.EventType.Should().Be(EventTimestampType.Bar);
    }

    [Fact]
    public void ProcessOrderBook_WithFutureTimestamp_ShouldDetect()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var detected = _detector.ProcessOrderBook("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.EventType.Should().Be(EventTimestampType.OrderBook);
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectCounts()
    {
        // Arrange
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        _detector.ProcessTrade("MSFT", futureTimestamp, "Provider2");
        _detector.ProcessTrade("GOOGL", DateTimeOffset.UtcNow, "Provider1"); // Valid timestamp

        // Act
        var stats = _detector.GetStats();

        // Assert
        stats.TotalFutureTimestampEvents.Should().Be(2);
        stats.TotalEventsProcessed.Should().Be(3);
        stats.SymbolStats.Should().HaveCount(2);
    }

    [Fact]
    public void GetRecentFutureTimestampSymbols_ShouldReturnAffectedSymbols()
    {
        // Arrange
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        _detector.ProcessTrade("MSFT", DateTimeOffset.UtcNow, "Provider2"); // Valid timestamp
        _detector.ProcessTrade("GOOGL", futureTimestamp, "Provider1");

        // Act
        var symbols = _detector.GetRecentFutureTimestampSymbols(60);

        // Assert
        symbols.Should().HaveCount(2);
        symbols.Should().Contain("AAPL");
        symbols.Should().Contain("GOOGL");
        symbols.Should().NotContain("MSFT");
    }

    [Fact]
    public void ConsecutiveCount_ShouldIncrementForRepeatedFutureTimestamps()
    {
        // Arrange
        var alerts = new List<FutureTimestampAlert>();
        _detector.OnFutureTimestamp += alert => alerts.Add(alert);
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        _detector.ProcessTrade("AAPL", futureTimestamp.AddSeconds(1), "Provider1");
        _detector.ProcessTrade("AAPL", futureTimestamp.AddSeconds(2), "Provider1");

        // Assert
        alerts.Should().HaveCount(3);
        alerts[0].ConsecutiveCount.Should().Be(1);
        alerts[1].ConsecutiveCount.Should().Be(2);
        alerts[2].ConsecutiveCount.Should().Be(3);
    }

    [Fact]
    public void ConsecutiveCount_ShouldResetAfterValidTimestamp()
    {
        // Arrange
        var alerts = new List<FutureTimestampAlert>();
        _detector.OnFutureTimestamp += alert => alerts.Add(alert);
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        _detector.ProcessTrade("AAPL", futureTimestamp.AddSeconds(1), "Provider1");
        _detector.ProcessTrade("AAPL", DateTimeOffset.UtcNow, "Provider1"); // Valid timestamp - resets count
        _detector.ProcessTrade("AAPL", futureTimestamp.AddSeconds(3), "Provider1");

        // Assert
        alerts.Should().HaveCount(3);
        alerts[0].ConsecutiveCount.Should().Be(1);
        alerts[1].ConsecutiveCount.Should().Be(2);
        alerts[2].ConsecutiveCount.Should().Be(1); // Reset after valid timestamp
    }

    [Fact]
    public void AlertCooldown_ShouldPreventRapidAlerts()
    {
        // Arrange
        using var detectorWithCooldown = new FutureTimestampDetector(new FutureTimestampConfig
        {
            AlertCooldownMs = 10000, // 10 second cooldown
            ToleranceMs = 1000
        });

        var alerts = new List<FutureTimestampAlert>();
        detectorWithCooldown.OnFutureTimestamp += alert => alerts.Add(alert);
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        detectorWithCooldown.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        detectorWithCooldown.ProcessTrade("AAPL", futureTimestamp.AddSeconds(1), "Provider1"); // Should be suppressed

        // Assert
        alerts.Should().HaveCount(1); // Only first alert fired
        detectorWithCooldown.TotalFutureTimestampEvents.Should().Be(2); // Both events counted
    }

    [Fact]
    public void MultipleSymbols_ShouldTrackIndependently()
    {
        // Arrange
        var alerts = new List<FutureTimestampAlert>();
        _detector.OnFutureTimestamp += alert => alerts.Add(alert);
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        _detector.ProcessTrade("MSFT", futureTimestamp, "Provider1");
        _detector.ProcessTrade("GOOGL", futureTimestamp, "Provider1");

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
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        _detector.ProcessTrade("AAPL", futureTimestamp.AddSeconds(1), "Provider1");
        _detector.ProcessTrade("MSFT", futureTimestamp, "Provider2");

        // Act
        var stats = _detector.GetStats();
        var aaplStats = stats.SymbolStats.FirstOrDefault(s => s.Symbol == "AAPL");
        var msftStats = stats.SymbolStats.FirstOrDefault(s => s.Symbol == "MSFT");

        // Assert
        aaplStats.Should().NotBeNull();
        aaplStats.TotalFutureTimestampEvents.Should().Be(2);

        msftStats.Should().NotBeNull();
        msftStats.TotalFutureTimestampEvents.Should().Be(1);
    }

    [Fact]
    public void DriftMs_ShouldReflectActualDrift()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var driftSeconds = 30;
        var futureTimestamp = DateTimeOffset.UtcNow.AddSeconds(driftSeconds);

        // Act
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");

        // Assert
        capturedAlert.Should().NotBeNull();
        // Allow for some timing variance in test execution
        capturedAlert!.Value.DriftMs.Should().BeGreaterThanOrEqualTo((driftSeconds - 1) * 1000);
        capturedAlert.Value.DriftMs.Should().BeLessThanOrEqualTo((driftSeconds + 2) * 1000);
    }

    [Fact]
    public void Config_StrictProfile_ShouldUseLowerTolerance()
    {
        // Arrange
        using var strictDetector = new FutureTimestampDetector(FutureTimestampConfig.Strict);
        FutureTimestampAlert? capturedAlert = null;
        strictDetector.OnFutureTimestamp += alert => capturedAlert = alert;

        // 2 seconds ahead - should be detected with strict config (1 second tolerance)
        var futureTimestamp = DateTimeOffset.UtcNow.AddSeconds(2);

        // Act
        var detected = strictDetector.ProcessTrade("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
    }

    [Fact]
    public void Config_LenientProfile_ShouldUseHigherTolerance()
    {
        // Arrange
        using var lenientDetector = new FutureTimestampDetector(FutureTimestampConfig.Lenient);
        FutureTimestampAlert? capturedAlert = null;
        lenientDetector.OnFutureTimestamp += alert => capturedAlert = alert;

        // 10 seconds ahead - should NOT be detected with lenient config (30 second tolerance)
        var futureTimestamp = DateTimeOffset.UtcNow.AddSeconds(10);

        // Act
        var detected = lenientDetector.ProcessTrade("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeFalse();
        capturedAlert.Should().BeNull();
    }

    [Fact]
    public void Config_LenientProfile_ShouldDetectLargeDrift()
    {
        // Arrange
        using var lenientDetector = new FutureTimestampDetector(FutureTimestampConfig.Lenient);
        FutureTimestampAlert? capturedAlert = null;
        lenientDetector.OnFutureTimestamp += alert => capturedAlert = alert;

        // 60 seconds ahead - should be detected even with lenient config (30 second tolerance)
        var futureTimestamp = DateTimeOffset.UtcNow.AddSeconds(60);

        // Act
        var detected = lenientDetector.ProcessTrade("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeTrue();
        capturedAlert.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldNotProcessAfterDisposed()
    {
        // Arrange
        _detector.Dispose();
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        var detected = _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");

        // Assert
        detected.Should().BeFalse();
    }

    [Fact]
    public void Alert_ShouldIncludeServerTimestamp()
    {
        // Arrange
        FutureTimestampAlert? capturedAlert = null;
        _detector.OnFutureTimestamp += alert => capturedAlert = alert;
        var beforeTest = DateTimeOffset.UtcNow;
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(1);

        // Act
        _detector.ProcessTrade("AAPL", futureTimestamp, "Provider1");
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        capturedAlert.Should().NotBeNull();
        capturedAlert!.Value.ServerTimestamp.Should().BeOnOrAfter(beforeTest);
        capturedAlert.Value.ServerTimestamp.Should().BeOnOrBefore(afterTest);
        capturedAlert.Value.EventTimestamp.Should().Be(futureTimestamp);
    }

    public void Dispose()
    {
        _detector.Dispose();
    }
}
