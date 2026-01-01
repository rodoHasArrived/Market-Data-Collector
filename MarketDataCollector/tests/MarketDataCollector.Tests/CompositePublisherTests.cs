using FluentAssertions;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Messaging.Publishers;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests;

public class CompositePublisherTests
{
    [Fact]
    public void TryPublish_AllPublishersSucceed_ReturnsTrue()
    {
        // Arrange
        var publisher1 = new Mock<IMarketEventPublisher>();
        var publisher2 = new Mock<IMarketEventPublisher>();

        publisher1.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(true);
        publisher2.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(true);

        var composite = new CompositePublisher(publisher1.Object, publisher2.Object);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
        publisher1.Verify(p => p.TryPublish(evt), Times.Once);
        publisher2.Verify(p => p.TryPublish(evt), Times.Once);
    }

    [Fact]
    public void TryPublish_OnePublisherFails_StillReturnsTrue()
    {
        // Arrange
        var publisher1 = new Mock<IMarketEventPublisher>();
        var publisher2 = new Mock<IMarketEventPublisher>();

        publisher1.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(true);
        publisher2.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(false);

        var composite = new CompositePublisher(publisher1.Object, publisher2.Object);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue(); // At least one succeeded
        publisher1.Verify(p => p.TryPublish(evt), Times.Once);
        publisher2.Verify(p => p.TryPublish(evt), Times.Once);
    }

    [Fact]
    public void TryPublish_AllPublishersFail_ReturnsFalse()
    {
        // Arrange
        var publisher1 = new Mock<IMarketEventPublisher>();
        var publisher2 = new Mock<IMarketEventPublisher>();

        publisher1.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(false);
        publisher2.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(false);

        var composite = new CompositePublisher(publisher1.Object, publisher2.Object);
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
        var publisher1 = new Mock<IMarketEventPublisher>();
        var publisher2 = new Mock<IMarketEventPublisher>();

        publisher1.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Throws(new Exception("Test exception"));
        publisher2.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(true);

        var composite = new CompositePublisher(publisher1.Object, publisher2.Object);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue(); // Second publisher succeeded
        publisher1.Verify(p => p.TryPublish(evt), Times.Once);
        publisher2.Verify(p => p.TryPublish(evt), Times.Once);
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
        var publisher = new Mock<IMarketEventPublisher>();
        publisher.Setup(p => p.TryPublish(It.IsAny<MarketEvent>())).Returns(true);

        var composite = new CompositePublisher(publisher.Object);
        var evt = CreateTestEvent();

        // Act
        var result = composite.TryPublish(evt);

        // Assert
        result.Should().BeTrue();
        publisher.Verify(p => p.TryPublish(evt), Times.Once);
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
