using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Events.Publishers;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Tests.TestHelpers;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests;

public class CompositePublisherTests
{
    [Fact]
    public void TryPublish_AllPublishersSucceed_ReturnsTrue()
    {
        // Arrange
        var events1 = new List<MarketEvent>();
        var events2 = new List<MarketEvent>();
        var publisher1 = new FakeMarketEventPublisher(events1);
        var publisher2 = new FakeMarketEventPublisher(events2);

        var composite = new CompositePublisher(publisher1, publisher2);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
        events1.Should().ContainSingle().Which.Should().BeEquivalentTo(evt);
        events2.Should().ContainSingle().Which.Should().BeEquivalentTo(evt);
    }

    [Fact]
    public void TryPublish_OnePublisherFails_StillReturnsTrue()
    {
        // Arrange
        var events1 = new List<MarketEvent>();
        var publisher1 = new FakeMarketEventPublisher(events1);
        var publisher2 = new FakeMarketEventPublisher(new List<MarketEvent>(), _ => false); // Always fails

        var composite = new CompositePublisher(publisher1, publisher2);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue(); // At least one succeeded
        events1.Should().ContainSingle().Which.Should().BeEquivalentTo(evt);
    }

    [Fact]
    public void TryPublish_AllPublishersFail_ReturnsFalse()
    {
        // Arrange
        var publisher1 = new FakeMarketEventPublisher(new List<MarketEvent>(), _ => false);
        var publisher2 = new FakeMarketEventPublisher(new List<MarketEvent>(), _ => false);

        var composite = new CompositePublisher(publisher1, publisher2);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryPublish_PublisherThrowsException_ContinuesToOthers()
    {
        // Arrange
        var events2 = new List<MarketEvent>();
        var publisher1 = new FakeMarketEventPublisher(new List<MarketEvent>(), _ => throw new Exception("Test exception"));
        var publisher2 = new FakeMarketEventPublisher(events2);

        var composite = new CompositePublisher(publisher1, publisher2);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue(); // Second publisher succeeded
        events2.Should().ContainSingle().Which.Should().BeEquivalentTo(evt);
    }

    [Fact]
    public void Constructor_NullPublishers_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CompositePublisher((IMarketEventPublisher[])null!));
    }

    [Fact]
    public void TryPublish_NoPublishers_ReturnsFalse()
    {
        // Arrange
        var composite = new CompositePublisher(Array.Empty<IMarketEventPublisher>());
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryPublish_SinglePublisher_DelegatesToPublisher()
    {
        // Arrange
        var events = new List<MarketEvent>();
        var publisher = new FakeMarketEventPublisher(events);

        var composite = new CompositePublisher(publisher);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
        events.Should().ContainSingle().Which.Should().BeEquivalentTo(evt);
    }

    private static MarketEvent CreateTestEvent()
    {
        var trade = new Trade(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "TEST",
            Price: 100.00m,
            Size: 100,
            Aggressor: AggressorSide.Buy,
            SequenceNumber: 1
        );

        return MarketEvent.Trade(DateTimeOffset.UtcNow, "TEST", trade, seq: 1, source: "TEST");
    }
}
