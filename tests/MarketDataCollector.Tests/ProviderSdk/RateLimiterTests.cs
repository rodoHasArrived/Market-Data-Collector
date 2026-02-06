using FluentAssertions;
using MarketDataCollector.ProviderSdk.Http;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for the SDK <see cref="RateLimiter"/> sliding window rate limiter.
/// </summary>
public sealed class RateLimiterTests : IDisposable
{
    private RateLimiter? _limiter;

    [Fact]
    public async Task WaitForSlotAsync_FirstRequest_ReturnsImmediately()
    {
        // Arrange
        _limiter = new RateLimiter(10, TimeSpan.FromSeconds(60));

        // Act
        var waited = await _limiter.WaitForSlotAsync();

        // Assert
        waited.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RecordRequest_IncreasesRequestCount()
    {
        // Arrange
        _limiter = new RateLimiter(10, TimeSpan.FromSeconds(60));

        // Act
        _limiter.RecordRequest();
        _limiter.RecordRequest();
        _limiter.RecordRequest();

        // Assert
        var (count, max, _) = _limiter.GetStatus();
        count.Should().Be(3);
        max.Should().Be(10);
    }

    [Fact]
    public void GetStatus_ReturnsCorrectMaxRequests()
    {
        // Arrange
        _limiter = new RateLimiter(42, TimeSpan.FromMinutes(5));

        // Act
        var (_, max, _) = _limiter.GetStatus();

        // Assert
        max.Should().Be(42);
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidArguments()
    {
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RateLimiter(0, TimeSpan.FromSeconds(1)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RateLimiter(-1, TimeSpan.FromSeconds(1)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RateLimiter(10, TimeSpan.Zero));
    }

    [Fact]
    public void Dispose_PreventsSubsequentCalls()
    {
        // Arrange
        _limiter = new RateLimiter(10, TimeSpan.FromSeconds(60));
        _limiter.Dispose();

        // Act & Assert
        Assert.ThrowsAsync<ObjectDisposedException>(
            () => _limiter.WaitForSlotAsync());
    }

    [Fact]
    public async Task WaitForSlotAsync_RespectsMinDelay()
    {
        // Arrange - minimum 50ms between requests
        _limiter = new RateLimiter(100, TimeSpan.FromSeconds(60),
            minDelayBetweenRequests: TimeSpan.FromMilliseconds(50));

        // Act - make two requests back-to-back
        await _limiter.WaitForSlotAsync();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _limiter.WaitForSlotAsync();
        sw.Stop();

        // Assert - second request should have waited at least ~50ms
        sw.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(40)); // small margin
    }

    [Fact]
    public async Task WaitForSlotAsync_SupportsCancellation()
    {
        // Arrange - very tight rate limit
        _limiter = new RateLimiter(1, TimeSpan.FromSeconds(60));
        await _limiter.WaitForSlotAsync(); // use the one slot

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _limiter.WaitForSlotAsync(cts.Token));
    }

    public void Dispose()
    {
        _limiter?.Dispose();
    }
}
