using System.Threading;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Sinks;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

/// <summary>
/// Unit tests for CompositeSink.
/// Part of B4 improvement (multi-sink storage).
/// </summary>
public sealed class CompositeSinkTests
{
    [Fact]
    public async Task AppendAsync_FanOutToAllSinks()
    {
        var sink1 = new Mock<IStorageSink>();
        var sink2 = new Mock<IStorageSink>();
        var evt = CreateTestEvent();

        var composite = new CompositeSink(new[] { sink1.Object, sink2.Object });
        await composite.AppendAsync(evt);

        sink1.Verify(s => s.AppendAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
        sink2.Verify(s => s.AppendAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AppendAsync_OneSinkFails_OtherStillReceives()
    {
        var sink1 = new Mock<IStorageSink>();
        sink1.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Sink 1 failed"));
        var sink2 = new Mock<IStorageSink>();
        var evt = CreateTestEvent();

        var composite = new CompositeSink(new[] { sink1.Object, sink2.Object });
        await composite.AppendAsync(evt); // Should not throw

        sink2.Verify(s => s.AppendAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlushAsync_FlushesAllSinks()
    {
        var sink1 = new Mock<IStorageSink>();
        var sink2 = new Mock<IStorageSink>();

        var composite = new CompositeSink(new[] { sink1.Object, sink2.Object });
        await composite.FlushAsync();

        sink1.Verify(s => s.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
        sink2.Verify(s => s.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlushAsync_OneSinkFails_ThrowsAggregateException()
    {
        var sink1 = new Mock<IStorageSink>();
        sink1.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Flush failed"));
        var sink2 = new Mock<IStorageSink>();

        var composite = new CompositeSink(new[] { sink1.Object, sink2.Object });
        await Assert.ThrowsAsync<AggregateException>(() => composite.FlushAsync());
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllSinks()
    {
        var sink1 = new Mock<IStorageSink>();
        var sink2 = new Mock<IStorageSink>();

        var composite = new CompositeSink(new[] { sink1.Object, sink2.Object });
        await composite.DisposeAsync();

        sink1.Verify(s => s.DisposeAsync(), Times.Once);
        sink2.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void Constructor_EmptySinks_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CompositeSink(Enumerable.Empty<IStorageSink>()));
    }

    [Fact]
    public void SinkCount_ReturnsCorrectCount()
    {
        var sink1 = new Mock<IStorageSink>();
        var sink2 = new Mock<IStorageSink>();
        var composite = new CompositeSink(new[] { sink1.Object, sink2.Object });
        Assert.Equal(2, composite.SinkCount);
    }

    private static MarketEvent CreateTestEvent()
    {
        return new MarketEvent(
            DateTimeOffset.UtcNow,
            "AAPL",
            MarketEventType.Trade,
            null
        );
    }
}
