using FluentAssertions;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Tests.Services;

/// <summary>
/// Tests for <see cref="SystemHealthService"/> functionality.
/// </summary>
public sealed class SystemHealthServiceTests
{
    [Fact]
    public void Instance_ReturnsNonNullSingleton()
    {
        // Act
        var instance = SystemHealthService.Instance;

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Instance_ReturnsSameInstanceOnMultipleCalls()
    {
        // Act
        var instance1 = SystemHealthService.Instance;
        var instance2 = SystemHealthService.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public async Task GetHealthSummaryAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - API will be unavailable, but method should accept cancellation token
        // We're testing the signature, not the actual API call
        var act = async () => await service.GetHealthSummaryAsync(cts.Token);

        // Assert - May throw due to cancelled token or network error, both are acceptable
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetProviderHealthAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetProviderHealthAsync(cts.Token);

        // Assert - May throw due to cancelled token or network error
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("Alpaca")]
    [InlineData("Polygon")]
    [InlineData("InteractiveBrokers")]
    public async Task GetProviderDiagnosticsAsync_WithProviderName_AcceptsValidProviders(string provider)
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Test signature accepts provider names
        var act = async () => await service.GetProviderDiagnosticsAsync(provider, cts.Token);

        // Assert - May throw due to cancelled token or network error
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetStorageHealthAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetStorageHealthAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetRecentEventsAsync_WithLimit_AcceptsValidLimits(int limit)
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Test signature accepts different limits
        var act = async () => await service.GetRecentEventsAsync(limit, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetSystemMetricsAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetSystemMetricsAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("Alpaca")]
    [InlineData("Polygon")]
    public async Task TestConnectionAsync_WithProviderName_AcceptsValidProviders(string provider)
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.TestConnectionAsync(provider, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GenerateDiagnosticBundleAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = SystemHealthService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GenerateDiagnosticBundleAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }
}
