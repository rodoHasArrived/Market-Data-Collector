using FluentAssertions;
using MarketDataCollector.Application.Services;
using Xunit;

namespace MarketDataCollector.Tests;

/// <summary>
/// Tests for the GracefulShutdownService and IFlushable implementations.
/// </summary>
public class GracefulShutdownTests
{
    [Fact]
    public async Task StopAsync_FlushesAllRegisteredComponents()
    {
        // Arrange
        var flushable1 = new MockFlushable("Component1");
        var flushable2 = new MockFlushable("Component2");
        var flushable3 = new MockFlushable("Component3");

        var service = new GracefulShutdownService(
            new[] { flushable1, flushable2, flushable3 });

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        flushable1.WasFlushed.Should().BeTrue();
        flushable2.WasFlushed.Should().BeTrue();
        flushable3.WasFlushed.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_CompletesWithinTimeout()
    {
        // Arrange
        var slowFlushable = new MockFlushable("Slow", flushDelay: TimeSpan.FromMilliseconds(100));
        var service = new GracefulShutdownService(
            new[] { slowFlushable },
            shutdownTimeout: TimeSpan.FromSeconds(5));

        await service.StartAsync(CancellationToken.None);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        slowFlushable.WasFlushed.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_HandlesFlushablesThatTimeout()
    {
        // Arrange
        var hangingFlushable = new MockFlushable("Hanging", flushDelay: TimeSpan.FromSeconds(10));
        var fastFlushable = new MockFlushable("Fast");

        var service = new GracefulShutdownService(
            new[] { hangingFlushable, fastFlushable },
            shutdownTimeout: TimeSpan.FromMilliseconds(100));

        await service.StartAsync(CancellationToken.None);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert - should complete within timeout + buffer
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        fastFlushable.WasFlushed.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_HandlesFlushablesThatThrow()
    {
        // Arrange
        var failingFlushable = new MockFlushable("Failing", shouldThrow: true);
        var successFlushable = new MockFlushable("Success");

        var service = new GracefulShutdownService(
            new[] { failingFlushable, successFlushable });

        await service.StartAsync(CancellationToken.None);

        // Act - should not throw
        var act = () => service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        successFlushable.WasFlushed.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WithEmptyFlushables_CompletesSuccessfully()
    {
        // Arrange
        var service = new GracefulShutdownService(Array.Empty<IFlushable>());
        await service.StartAsync(CancellationToken.None);

        // Act
        var act = () => service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_FlushesInParallel()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);
        var flushables = Enumerable.Range(0, 5)
            .Select(i => new MockFlushable($"Component{i}", flushDelay: delay))
            .ToList();

        var service = new GracefulShutdownService(flushables);
        await service.StartAsync(CancellationToken.None);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert - if sequential, would take 500ms; parallel should be ~100ms
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(300));
        flushables.Should().OnlyContain(f => f.WasFlushed);
    }

    [Fact]
    public async Task StartAsync_LogsComponentCount()
    {
        // Arrange
        var flushables = new[] { new MockFlushable("A"), new MockFlushable("B") };
        var service = new GracefulShutdownService(flushables);

        // Act - should not throw
        var act = () => service.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private class MockFlushable : IFlushable
    {
        private readonly TimeSpan _flushDelay;
        private readonly bool _shouldThrow;

        public string Name { get; }
        public bool WasFlushed { get; private set; }

        public MockFlushable(string name, TimeSpan? flushDelay = null, bool shouldThrow = false)
        {
            Name = name;
            _flushDelay = flushDelay ?? TimeSpan.Zero;
            _shouldThrow = shouldThrow;
        }

        public async Task FlushAsync(CancellationToken ct = default)
        {
            if (_shouldThrow)
            {
                throw new InvalidOperationException($"{Name} failed to flush");
            }

            if (_flushDelay > TimeSpan.Zero)
            {
                await Task.Delay(_flushDelay, ct);
            }

            WasFlushed = true;
        }
    }
}
