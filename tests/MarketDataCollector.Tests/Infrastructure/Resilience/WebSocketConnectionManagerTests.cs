using FluentAssertions;
using MarketDataCollector.Infrastructure.Resilience;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Resilience;

/// <summary>
/// Unit tests for WebSocketConnectionManager lifecycle and resource management.
/// </summary>
public class WebSocketConnectionManagerTests
{
    [Fact]
    public void Constructor_WithValidArgs_CreatesInstance()
    {
        var manager = new WebSocketConnectionManager(
            providerName: "test-provider",
            uri: new Uri("wss://example.com"));

        manager.Should().NotBeNull();
        manager.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void StartReceiveLoop_WithoutConnect_ThrowsInvalidOperationException()
    {
        var manager = new WebSocketConnectionManager(
            providerName: "test-provider",
            uri: new Uri("wss://example.com"));

        var act = () => manager.StartReceiveLoop(msg => Task.CompletedTask);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not connected*");
    }

    [Fact]
    public async Task DisposeAsync_WithoutConnect_DoesNotThrow()
    {
        var manager = new WebSocketConnectionManager(
            providerName: "test-provider",
            uri: new Uri("wss://example.com"));

        // Should not throw even if never connected
        await manager.DisposeAsync();
    }
}
