using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Xunit;

namespace MarketDataCollector.Tests.Application.Backfill;

/// <summary>
/// Unit tests for ProviderRateLimitTracker — the per-provider rate limit tracking
/// and proactive enforcement layer used by CompositeHistoricalDataProvider.
/// Covers H1 (per-provider backfill rate limiting) from the project roadmap.
/// </summary>
public sealed class ProviderRateLimitTrackerTests : IDisposable
{
    private ProviderRateLimitTracker? _tracker;

    public void Dispose()
    {
        _tracker?.Dispose();
    }

    #region Registration Tests

    [Fact]
    public void RegisterProvider_TracksProviderState()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();

        // Act
        _tracker.RegisterProvider("alpaca", maxRequestsPerWindow: 200, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Assert
        var status = _tracker.GetStatus("alpaca");
        status.Should().NotBeNull();
        status!.ProviderName.Should().Be("alpaca");
        status.MaxRequestsPerWindow.Should().Be(200);
        status.RequestsInWindow.Should().Be(0);
    }

    [Fact]
    public void RegisterProvider_FromInterface_TracksProviderState()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        var mockProvider = new Moq.Mock<IHistoricalDataProvider>();
        mockProvider.Setup(p => p.Name).Returns("tiingo");
        mockProvider.Setup(p => p.MaxRequestsPerWindow).Returns(500);
        mockProvider.Setup(p => p.RateLimitWindow).Returns(TimeSpan.FromHours(1));
        mockProvider.Setup(p => p.RateLimitDelay).Returns(TimeSpan.FromMilliseconds(100));

        // Act
        _tracker.RegisterProvider(mockProvider.Object);

        // Assert
        var status = _tracker.GetStatus("tiingo");
        status.Should().NotBeNull();
        status!.ProviderName.Should().Be("tiingo");
        status.MaxRequestsPerWindow.Should().Be(500);
    }

    [Fact]
    public void GetStatus_UnregisteredProvider_ReturnsNull()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();

        // Act & Assert
        _tracker.GetStatus("unknown").Should().BeNull();
    }

    #endregion

    #region RecordRequest and Status Tests

    [Fact]
    public void RecordRequest_IncrementsRequestCount()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 200, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Act
        _tracker.RecordRequest("alpaca");
        _tracker.RecordRequest("alpaca");
        _tracker.RecordRequest("alpaca");

        // Assert
        var status = _tracker.GetStatus("alpaca");
        status!.RequestsInWindow.Should().Be(3);
        status.RemainingRequests.Should().Be(197);
    }

    [Fact]
    public void RecordRequest_UnknownProvider_DoesNotThrow()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();

        // Act & Assert — should silently ignore
        var act = () => _tracker.RecordRequest("unknown");
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAllStatus_ReturnsAllRegisteredProviders()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 200, TimeSpan.FromMinutes(1), TimeSpan.Zero);
        _tracker.RegisterProvider("tiingo", 500, TimeSpan.FromHours(1), TimeSpan.Zero);

        // Act
        var allStatus = _tracker.GetAllStatus();

        // Assert
        allStatus.Should().HaveCount(2);
        allStatus.Should().ContainKey("alpaca");
        allStatus.Should().ContainKey("tiingo");
    }

    #endregion

    #region Rate Limit Hit (429) Tests

    [Fact]
    public void RecordRateLimitHit_MarksProviderAsRateLimited()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 200, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Act
        _tracker.RecordRateLimitHit("alpaca", TimeSpan.FromMinutes(1));

        // Assert
        _tracker.IsRateLimited("alpaca").Should().BeTrue();
    }

    [Fact]
    public void ClearRateLimitState_ResetsRateLimitedStatus()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 200, TimeSpan.FromMinutes(1), TimeSpan.Zero);
        _tracker.RecordRateLimitHit("alpaca", TimeSpan.FromMinutes(1));

        // Act
        _tracker.ClearRateLimitState("alpaca");

        // Assert
        _tracker.IsRateLimited("alpaca").Should().BeFalse();
    }

    [Fact]
    public void IsRateLimited_UnregisteredProvider_ReturnsFalse()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();

        // Act & Assert
        _tracker.IsRateLimited("unknown").Should().BeFalse();
    }

    [Fact]
    public void GetTimeUntilReset_WhenRateLimited_ReturnsPositiveValue()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 200, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Act
        _tracker.RecordRateLimitHit("alpaca", TimeSpan.FromSeconds(30));
        var timeUntilReset = _tracker.GetTimeUntilReset("alpaca");

        // Assert
        timeUntilReset.Should().NotBeNull();
        timeUntilReset!.Value.Should().BeGreaterThan(TimeSpan.Zero);
        timeUntilReset.Value.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetTimeUntilReset_UnregisteredProvider_ReturnsNull()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();

        // Act & Assert
        _tracker.GetTimeUntilReset("unknown").Should().BeNull();
    }

    #endregion

    #region IsApproachingLimit Tests

    [Fact]
    public void IsApproachingLimit_BelowThreshold_ReturnsFalse()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Record 5 requests (50% usage, below 80% threshold)
        for (int i = 0; i < 5; i++)
            _tracker.RecordRequest("alpaca");

        // Act & Assert
        _tracker.IsApproachingLimit("alpaca").Should().BeFalse();
    }

    [Fact]
    public void IsApproachingLimit_AboveThreshold_ReturnsTrue()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Record 9 requests (90% usage, above 80% threshold)
        for (int i = 0; i < 9; i++)
            _tracker.RecordRequest("alpaca");

        // Act & Assert
        _tracker.IsApproachingLimit("alpaca").Should().BeTrue();
    }

    [Fact]
    public void IsApproachingLimit_CustomThreshold_UsesCustomValue()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Record 6 requests (60% usage)
        for (int i = 0; i < 6; i++)
            _tracker.RecordRequest("alpaca");

        // Act & Assert
        _tracker.IsApproachingLimit("alpaca", threshold: 0.5).Should().BeTrue();
        _tracker.IsApproachingLimit("alpaca", threshold: 0.7).Should().BeFalse();
    }

    #endregion

    #region GetBestAvailableProvider Tests

    [Fact]
    public void GetBestAvailableProvider_ReturnsProviderWithLowestUsage()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("heavy", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);
        _tracker.RegisterProvider("light", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Heavy provider: 8 requests (80% usage)
        for (int i = 0; i < 8; i++)
            _tracker.RecordRequest("heavy");
        // Light provider: 2 requests (20% usage)
        for (int i = 0; i < 2; i++)
            _tracker.RecordRequest("light");

        // Act
        var best = _tracker.GetBestAvailableProvider(new[] { "heavy", "light" });

        // Assert
        best.Should().Be("light");
    }

    [Fact]
    public void GetBestAvailableProvider_SkipsRateLimitedProviders()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("limited", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);
        _tracker.RegisterProvider("available", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        _tracker.RecordRateLimitHit("limited", TimeSpan.FromMinutes(1));

        // Act
        var best = _tracker.GetBestAvailableProvider(new[] { "limited", "available" });

        // Assert
        best.Should().Be("available");
    }

    [Fact]
    public void GetBestAvailableProvider_UnknownProvider_ReturnsItImmediately()
    {
        // Arrange — unknown providers are assumed to be available
        _tracker = new ProviderRateLimitTracker();

        // Act
        var best = _tracker.GetBestAvailableProvider(new[] { "unknown" });

        // Assert
        best.Should().Be("unknown");
    }

    [Fact]
    public void GetBestAvailableProvider_AllRateLimited_ReturnsNull()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("p1", 1, TimeSpan.FromMinutes(1), TimeSpan.Zero);
        _tracker.RegisterProvider("p2", 1, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Fill both to capacity so IsRateLimited returns true
        _tracker.RecordRequest("p1");
        _tracker.RecordRequest("p2");

        // Act
        var best = _tracker.GetBestAvailableProvider(new[] { "p1", "p2" });

        // Assert
        best.Should().BeNull();
    }

    #endregion

    #region WaitForSlotAsync Enforcement Tests (H1)

    [Fact]
    public async Task WaitForSlotAsync_WithinLimit_ReturnsQuickly()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 100, TimeSpan.FromSeconds(60), TimeSpan.Zero);

        // Act
        var waited = await _tracker.WaitForSlotAsync("alpaca");

        // Assert
        waited.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task WaitForSlotAsync_UnregisteredProvider_ReturnsZero()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();

        // Act
        var waited = await _tracker.WaitForSlotAsync("unknown");

        // Assert
        waited.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task WaitForSlotAsync_ExceedsLimit_WaitsForWindow()
    {
        // Arrange — tight limit for fast test
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("test", 2, TimeSpan.FromMilliseconds(200), TimeSpan.Zero);

        // Fill the window
        await _tracker.WaitForSlotAsync("test");
        await _tracker.WaitForSlotAsync("test");

        // Act — third request should wait for window to pass
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _tracker.WaitForSlotAsync("test");
        sw.Stop();

        // Assert — wide tolerance for CI stability
        sw.ElapsedMilliseconds.Should().BeGreaterThan(30);
    }

    [Fact]
    public async Task WaitForSlotAsync_WithExplicitRateLimit_WaitsForReset()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("test", 100, TimeSpan.FromSeconds(60), TimeSpan.Zero);

        // Simulate a 429 hit with short retry-after
        _tracker.RecordRateLimitHit("test", TimeSpan.FromMilliseconds(200));

        // Act — should wait for the explicit rate limit to reset
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _tracker.WaitForSlotAsync("test");
        sw.Stop();

        // Assert — should have waited for the 200ms retry-after
        sw.ElapsedMilliseconds.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task WaitForSlotAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("test", 1, TimeSpan.FromSeconds(60), TimeSpan.Zero);

        // Fill the window
        await _tracker.WaitForSlotAsync("test");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _tracker.WaitForSlotAsync("test", cts.Token));
    }

    [Fact]
    public async Task WaitForSlotAsync_MultipleProviders_EnforcesIndependently()
    {
        // Arrange — one tight provider, one generous
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("tight", 1, TimeSpan.FromMilliseconds(300), TimeSpan.Zero);
        _tracker.RegisterProvider("generous", 100, TimeSpan.FromSeconds(60), TimeSpan.Zero);

        // Fill tight provider
        await _tracker.WaitForSlotAsync("tight");

        // Act — generous provider should still return quickly
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _tracker.WaitForSlotAsync("generous");
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    #endregion

    #region RateLimitStatus Record Tests

    [Fact]
    public void RateLimitStatus_RemainingRequests_CalculatedCorrectly()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("alpaca", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        for (int i = 0; i < 7; i++)
            _tracker.RecordRequest("alpaca");

        // Act
        var status = _tracker.GetStatus("alpaca")!;

        // Assert
        status.RemainingRequests.Should().Be(3);
        status.UsagePercent.Should().BeApproximately(70.0, 1.0);
    }

    [Fact]
    public void RateLimitStatus_AtCapacity_RemainingRequestsIsZero()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("p1", 5, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        for (int i = 0; i < 5; i++)
            _tracker.RecordRequest("p1");

        // Act
        var status = _tracker.GetStatus("p1")!;

        // Assert
        status.RemainingRequests.Should().Be(0);
        status.IsRateLimited.Should().BeTrue();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        _tracker = new ProviderRateLimitTracker();
        _tracker.RegisterProvider("p1", 10, TimeSpan.FromMinutes(1), TimeSpan.Zero);

        // Act & Assert
        _tracker.Dispose();
        var act = () => _tracker.Dispose();
        act.Should().NotThrow();
    }

    #endregion
}
