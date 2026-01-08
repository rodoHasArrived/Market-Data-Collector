using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Messaging.Contracts;
using MarketDataCollector.Messaging.Publishers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarketDataCollector.Tests;

public class MassTransitPublisherTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private MassTransitPublisher _publisher = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddMassTransitTestHarness(cfg =>
        {
            // No consumers needed for publish tests
        });

        _provider = services.BuildServiceProvider();
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();

        var publishEndpoint = _provider.GetRequiredService<IPublishEndpoint>();
        _publisher = new MassTransitPublisher(publishEndpoint, enableMetrics: false);
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public void TryPublish_TradeEvent_ReturnsTrue()
    {
        // Arrange
        var trade = new Trade(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Price: 450.25m,
            Size: 100,
            Aggressor: AggressorSide.Buy,
            SequenceNumber: 1,
            StreamId: "test",
            Venue: "ARCA"
        );

        var evt = MarketEvent.Trade(DateTimeOffset.UtcNow, "SPY", trade, seq: 1, source: "TEST");

        // Act
        var result = _publisher.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryPublish_TradeEvent_PublishesITradeOccurred()
    {
        // Arrange
        var trade = new Trade(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "AAPL",
            Price: 175.50m,
            Size: 50,
            Aggressor: AggressorSide.Sell,
            SequenceNumber: 42,
            StreamId: "stream1",
            Venue: "NASDAQ"
        );

        var evt = MarketEvent.Trade(DateTimeOffset.UtcNow, "AAPL", trade, seq: 42, source: "IB");

        // Act
        _publisher.TryPublish(evt);

        // Allow time for async publish
        await Task.Delay(100);

        // Assert
        var published = _harness.Published.Select<ITradeOccurred>().ToList();
        published.Should().NotBeEmpty();
    }

    [Fact]
    public void TryPublish_BboQuote_ReturnsTrue()
    {
        // Arrange
        var quote = new BboQuotePayload(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "MSFT",
            BidPrice: 380.10m,
            BidSize: 200,
            AskPrice: 380.15m,
            AskSize: 150,
            MidPrice: 380.125m,
            Spread: 0.05m,
            SequenceNumber: 100,
            StreamId: "test",
            Venue: "IEX"
        );

        var evt = MarketEvent.BboQuote(DateTimeOffset.UtcNow, "MSFT", quote, seq: 100, source: "ALPACA");

        // Act
        var result = _publisher.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryPublish_L2Snapshot_PublishesIL2SnapshotReceived()
    {
        // Arrange
        var bids = new List<OrderBookLevel>
        {
            new(OrderBookSide.Bid, 0, 100.50, 500, "MM1"),
            new(OrderBookSide.Bid, 1, 100.45, 300, "MM2")
        };

        var asks = new List<OrderBookLevel>
        {
            new(OrderBookSide.Ask, 0, 100.55, 400, "MM3"),
            new(OrderBookSide.Ask, 1, 100.60, 200, "MM4")
        };

        var snapshot = new LOBSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "GOOG",
            Bids: bids,
            Asks: asks,
            MidPrice: 100.525,
            MicroPrice: 100.52,
            Imbalance: 0.1,
            MarketState: MarketState.Normal,
            SequenceNumber: 200
        );

        var evt = MarketEvent.L2Snapshot(DateTimeOffset.UtcNow, "GOOG", snapshot, seq: 200, source: "IB");

        // Act
        _publisher.TryPublish(evt);

        // Allow time for async publish
        await Task.Delay(100);

        // Assert
        var published = _harness.Published.Select<IL2SnapshotReceived>().ToList();
        published.Should().NotBeEmpty();
    }

    [Fact]
    public void TryPublish_IntegrityEvent_ReturnsTrue()
    {
        // Arrange
        var integrity = IntegrityEvent.SequenceGap(
            DateTimeOffset.UtcNow,
            "TSLA",
            expectedNext: 100,
            received: 105,
            streamId: "test",
            venue: "ARCA"
        );

        var evt = MarketEvent.Integrity(DateTimeOffset.UtcNow, "TSLA", integrity, seq: 105, source: "IB");

        // Act
        var result = _publisher.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TryPublish_Heartbeat_ReturnsTrue()
    {
        // Arrange
        var evt = MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "TEST");

        // Act
        var result = _publisher.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
    }
}
