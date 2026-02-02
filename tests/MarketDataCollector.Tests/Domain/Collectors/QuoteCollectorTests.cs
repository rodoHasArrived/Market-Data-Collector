using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests;

public class QuoteCollectorTests
{
    private readonly Mock<IMarketEventPublisher> _mockPublisher;
    private readonly QuoteCollector _collector;
    private readonly List<MarketEvent> _publishedEvents;

    public QuoteCollectorTests()
    {
        _mockPublisher = new Mock<IMarketEventPublisher>();
        _publishedEvents = new List<MarketEvent>();

        _mockPublisher
            .Setup(p => p.TryPublish(It.IsAny<MarketEvent>()))
            .Returns(true);

        _collector = new QuoteCollector(_mockPublisher.Object);
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
    public void OnQuote_WithValidUpdate_PublishesBboQuoteEvent()
    {
        // Arrange
        var update = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.00m,
            BidSize: 100,
            AskPrice: 450.05m,
            AskSize: 200,
            StreamId: "TEST",
            Venue: "NYSE"
        );

        // Act
        _collector.OnQuote(update);
        CapturePublishedEvents();

        // Assert
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.BboQuote);
    }

    [Fact]
    public void OnQuote_WithNullUpdate_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _collector.OnQuote(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OnQuote_WithEmptySymbol_DoesNotPublishAnyEvents()
    {
        // Arrange
        var update = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "",
            BidPrice: 100m,
            BidSize: 50,
            AskPrice: 101m,
            AskSize: 50
        );

        // Act
        _collector.OnQuote(update);

        // Assert
        _publishedEvents.Should().BeEmpty();
    }

    [Fact]
    public void TryGet_AfterQuoteUpdate_ReturnsLatestQuote()
    {
        // Arrange
        var update = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.00m,
            BidSize: 100,
            AskPrice: 450.05m,
            AskSize: 200
        );

        // Act
        _collector.OnQuote(update);
        var found = _collector.TryGet("SPY", out var quote);

        // Assert
        found.Should().BeTrue();
        quote.Symbol.Should().Be("SPY");
        quote.BidPrice.Should().Be(450.00m);
        quote.AskPrice.Should().Be(450.05m);
    }

    [Fact]
    public void TryGet_ForNonExistentSymbol_ReturnsFalse()
    {
        // Act
        var found = _collector.TryGet("UNKNOWN", out var quote);

        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public void Upsert_IncrementsSequenceNumberPerSymbol()
    {
        // Arrange
        var update1 = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.00m,
            BidSize: 100,
            AskPrice: 450.05m,
            AskSize: 200
        );
        var update2 = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.10m,
            BidSize: 150,
            AskPrice: 450.15m,
            AskSize: 250
        );

        // Act
        var payload1 = _collector.Upsert(update1);
        var payload2 = _collector.Upsert(update2);

        // Assert
        payload1.SequenceNumber.Should().Be(1);
        payload2.SequenceNumber.Should().Be(2);
    }

    [Fact]
    public void Upsert_TracksSeparateSequencePerSymbol()
    {
        // Arrange
        var spyUpdate = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.00m,
            BidSize: 100,
            AskPrice: 450.05m,
            AskSize: 200
        );
        var aaplUpdate = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "AAPL",
            BidPrice: 180.00m,
            BidSize: 50,
            AskPrice: 180.10m,
            AskSize: 75
        );

        // Act
        var spy1 = _collector.Upsert(spyUpdate);
        var aapl1 = _collector.Upsert(aaplUpdate);
        var spy2 = _collector.Upsert(spyUpdate);

        // Assert
        spy1.SequenceNumber.Should().Be(1);
        aapl1.SequenceNumber.Should().Be(1);
        spy2.SequenceNumber.Should().Be(2);
    }

    [Fact]
    public void TryRemove_ExistingSymbol_RemovesAndReturnsQuote()
    {
        // Arrange
        var update = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.00m,
            BidSize: 100,
            AskPrice: 450.05m,
            AskSize: 200
        );
        _collector.OnQuote(update);

        // Act
        var removed = _collector.TryRemove("SPY", out var quote);

        // Assert
        removed.Should().BeTrue();
        quote.Symbol.Should().Be("SPY");

        // Verify it's really gone
        _collector.TryGet("SPY", out _).Should().BeFalse();
    }

    [Fact]
    public void Snapshot_ReturnsAllCurrentQuotes()
    {
        // Arrange
        _collector.OnQuote(CreateQuote("SPY", 450m, 451m));
        _collector.OnQuote(CreateQuote("AAPL", 180m, 181m));
        _collector.OnQuote(CreateQuote("GOOGL", 140m, 141m));

        // Act
        var snapshot = _collector.Snapshot();

        // Assert
        snapshot.Should().HaveCount(3);
        snapshot.Should().ContainKey("SPY");
        snapshot.Should().ContainKey("AAPL");
        snapshot.Should().ContainKey("GOOGL");
    }

    private static MarketQuoteUpdate CreateQuote(string symbol, decimal bidPrice, decimal askPrice)
    {
        return new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: symbol,
            BidPrice: bidPrice,
            BidSize: 100,
            AskPrice: askPrice,
            AskSize: 100
        );
    }
}
