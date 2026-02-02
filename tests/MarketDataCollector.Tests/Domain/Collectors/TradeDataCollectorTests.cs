using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests;

public class TradeDataCollectorTests
{
    private readonly Mock<IMarketEventPublisher> _mockPublisher;
    private readonly TradeDataCollector _collector;
    private readonly List<MarketEvent> _publishedEvents;

    public TradeDataCollectorTests()
    {
        _mockPublisher = new Mock<IMarketEventPublisher>();
        _publishedEvents = new List<MarketEvent>();

        _mockPublisher
            .Setup(p => p.TryPublish(It.IsAny<MarketEvent>()))
            .Returns(true);

        _collector = new TradeDataCollector(_mockPublisher.Object);
    }

    // Helper method to capture events from mock invocations
    private void CapturePublishedEvents()
    {
        _publishedEvents.Clear();
        foreach (var invocation in _mockPublisher.Invocations)
        {
            if (invocation.Method.Name == nameof(IMarketEventPublisher.TryPublish))
            {
                var evt = (MarketEvent)invocation.Arguments[0];
                _publishedEvents.Add(evt);
            }
        }
    }

    [Fact]
    public void OnTrade_WithValidUpdate_PublishesTradeAndOrderFlowEvents()
    {
        // Arrange
        var update = new MarketTradeUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Price: 450.50m,
            Size: 100,
            Aggressor: AggressorSide.Buy,
            SequenceNumber: 1,
            StreamId: "TEST",
            Venue: "NYSE"
        );

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert
        _publishedEvents.Should().HaveCount(2);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Trade);
        _publishedEvents[1].Type.Should().Be(MarketEventType.OrderFlow);
    }

    [Fact]
    public void OnTrade_WithNullUpdate_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _collector.OnTrade(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OnTrade_WithEmptySymbol_DoesNotPublishAnyEvents()
    {
        // Arrange
        var update = new MarketTradeUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "",
            Price: 100m,
            Size: 50,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: 1
        );

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert
        _publishedEvents.Should().BeEmpty();
    }

    [Fact]
    public void OnTrade_WithOutOfOrderSequence_PublishesIntegrityEvent()
    {
        // Arrange
        var first = CreateTrade("SPY", seqNum: 5);
        var second = CreateTrade("SPY", seqNum: 3); // Out of order

        // Act
        _collector.OnTrade(first);
        _publishedEvents.Clear();
        _collector.OnTrade(second);
        CapturePublishedEvents();

        // Assert
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Integrity);
    }

    [Fact]
    public void OnTrade_WithSequenceGap_PublishesIntegrityEventAndAcceptsTrade()
    {
        // Arrange
        var first = CreateTrade("SPY", seqNum: 1);
        var second = CreateTrade("SPY", seqNum: 5); // Gap of 3

        // Act
        _collector.OnTrade(first);
        _publishedEvents.Clear();
        _collector.OnTrade(second);
        CapturePublishedEvents();

        // Assert
        // Should publish: Integrity (gap), Trade, OrderFlow
        _publishedEvents.Should().HaveCount(3);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Integrity);
        _publishedEvents[1].Type.Should().Be(MarketEventType.Trade);
        _publishedEvents[2].Type.Should().Be(MarketEventType.OrderFlow);
    }

    [Fact]
    public void OnTrade_TracksVolumeByAggressorSide()
    {
        // Arrange & Act
        _collector.OnTrade(CreateTrade("SPY", size: 100, aggressor: AggressorSide.Buy, seqNum: 1));
        _collector.OnTrade(CreateTrade("SPY", size: 50, aggressor: AggressorSide.Sell, seqNum: 2));
        _collector.OnTrade(CreateTrade("SPY", size: 25, aggressor: AggressorSide.Unknown, seqNum: 3));
        CapturePublishedEvents();

        // Assert - Check the last OrderFlow event
        var orderFlowEvents = _publishedEvents.Where(e => e.Type == MarketEventType.OrderFlow).ToList();
        orderFlowEvents.Should().HaveCount(3);

        var lastOrderFlow = orderFlowEvents.Last().Payload as OrderFlowStatistics;
        lastOrderFlow.Should().NotBeNull();
        lastOrderFlow!.BuyVolume.Should().Be(100);
        lastOrderFlow.SellVolume.Should().Be(50);
        lastOrderFlow.UnknownVolume.Should().Be(25);
    }

    [Fact]
    public void OnTrade_WithDuplicateSequence_RejectsTrade()
    {
        // Arrange
        var first = CreateTrade("SPY", seqNum: 1);
        var duplicate = CreateTrade("SPY", seqNum: 1);

        // Act
        _collector.OnTrade(first);
        _publishedEvents.Clear();
        _collector.OnTrade(duplicate);
        CapturePublishedEvents();

        // Assert - Should only publish integrity event (rejected)
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Integrity);
    }

    [Fact]
    public void OnTrade_TracksSeparateStatePerSymbol()
    {
        // Arrange & Act
        _collector.OnTrade(CreateTrade("SPY", seqNum: 1));
        _collector.OnTrade(CreateTrade("AAPL", seqNum: 1));
        _collector.OnTrade(CreateTrade("SPY", seqNum: 2));
        _collector.OnTrade(CreateTrade("AAPL", seqNum: 2));
        CapturePublishedEvents();

        // Assert - No integrity events, all trades accepted
        var integrityEvents = _publishedEvents.Where(e => e.Type == MarketEventType.Integrity);
        integrityEvents.Should().BeEmpty();
    }

    [Fact]
    public void OnTrade_WithSymbolTooLong_PublishesIntegrityEventAndRejectsTrade()
    {
        // Arrange - Symbol exceeding 50 character limit
        var longSymbol = new string('A', 51);
        var update = CreateTrade(longSymbol, seqNum: 1);

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert - Should only publish integrity event (rejected)
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Integrity);
        var integrity = _publishedEvents[0].Payload as IntegrityEvent;
        integrity.Should().NotBeNull();
        integrity!.Description.Should().Contain("exceeds maximum length");
    }

    [Theory]
    [InlineData("SPY@NYSE")]
    [InlineData("AAPL$")]
    [InlineData("TEST#1")]
    [InlineData("SYMBOL<>")]
    public void OnTrade_WithInvalidSymbolCharacters_PublishesIntegrityEventAndRejectsTrade(string invalidSymbol)
    {
        // Arrange
        var update = CreateTrade(invalidSymbol, seqNum: 1);

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert - Should only publish integrity event (rejected)
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Integrity);
        var integrity = _publishedEvents[0].Payload as IntegrityEvent;
        integrity.Should().NotBeNull();
        integrity!.Description.Should().Contain("invalid character");
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("AAPL.O")]
    [InlineData("ES-2024")]
    [InlineData("BTC_USD")]
    [InlineData("EUR:USD")]
    [InlineData("SPY/CALL")]
    [InlineData("AAPL123")]
    public void OnTrade_WithValidSymbolFormats_AcceptsTrade(string validSymbol)
    {
        // Arrange
        var update = CreateTrade(validSymbol, seqNum: 1);

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert - Should publish Trade and OrderFlow events (no integrity event)
        _publishedEvents.Should().HaveCount(2);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Trade);
        _publishedEvents[1].Type.Should().Be(MarketEventType.OrderFlow);
    }

    [Fact]
    public void OnTrade_WithNegativeSequenceNumber_PublishesIntegrityEventAndRejectsTrade()
    {
        // Arrange
        var update = CreateTrade("SPY", seqNum: -1);

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert - Should only publish integrity event (rejected)
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Integrity);
        var integrity = _publishedEvents[0].Payload as IntegrityEvent;
        integrity.Should().NotBeNull();
        integrity!.Description.Should().Contain("non-negative");
    }

    [Fact]
    public void OnTrade_WithZeroSequenceNumber_AcceptsTrade()
    {
        // Arrange - Zero is a valid sequence number
        var update = CreateTrade("SPY", seqNum: 0);

        // Act
        _collector.OnTrade(update);
        CapturePublishedEvents();

        // Assert - Should publish Trade and OrderFlow events
        _publishedEvents.Should().HaveCount(2);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Trade);
        _publishedEvents[1].Type.Should().Be(MarketEventType.OrderFlow);
    }

    private static MarketTradeUpdate CreateTrade(
        string symbol,
        decimal price = 100m,
        long size = 100,
        AggressorSide aggressor = AggressorSide.Buy,
        long seqNum = 1)
    {
        return new MarketTradeUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: symbol,
            Price: price,
            Size: size,
            Aggressor: aggressor,
            SequenceNumber: seqNum,
            StreamId: "TEST",
            Venue: "TEST"
        );
    }
}
