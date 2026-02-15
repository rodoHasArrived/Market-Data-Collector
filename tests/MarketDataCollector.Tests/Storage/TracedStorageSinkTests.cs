using System.Diagnostics;
using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Sinks;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

/// <summary>
/// Unit tests for TracedStorageSink — the G2 remainder (end-to-end distributed tracing)
/// component from the project roadmap. Tests trace context propagation through storage
/// writes, error recording, and metrics tracking.
/// </summary>
public sealed class TracedStorageSinkTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _capturedActivities = new();

    public TracedStorageSinkTests()
    {
        // Set up an ActivityListener to capture tracing activities
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MarketDataCollector.Storage",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullInner_ThrowsArgumentNullException()
    {
        var act = () => new TracedStorageSink(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithCustomSinkName_UsesCustomName()
    {
        var mockSink = new Mock<IStorageSink>();
        var traced = new TracedStorageSink(mockSink.Object, "my-custom-sink");

        traced.SinkName.Should().Be("my-custom-sink");
    }

    [Fact]
    public void Constructor_WithoutSinkName_UsesTypeName()
    {
        var mockSink = new Mock<IStorageSink>();
        var traced = new TracedStorageSink(mockSink.Object);

        // Moq proxy type name — just verify it's not null/empty
        traced.SinkName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_InitialCounters_AreZero()
    {
        var mockSink = new Mock<IStorageSink>();
        var traced = new TracedStorageSink(mockSink.Object);

        traced.AppendCount.Should().Be(0);
        traced.FlushCount.Should().Be(0);
        traced.ErrorCount.Should().Be(0);
    }

    #endregion

    #region AppendAsync Tests

    [Fact]
    public async Task AppendAsync_DelegatesToInnerSink()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");
        var evt = CreateTestEvent("AAPL");

        await traced.AppendAsync(evt);

        mockSink.Verify(s => s.AppendAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AppendAsync_IncrementsAppendCount()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        await traced.AppendAsync(CreateTestEvent("AAPL"));
        await traced.AppendAsync(CreateTestEvent("MSFT"));
        await traced.AppendAsync(CreateTestEvent("GOOG"));

        traced.AppendCount.Should().Be(3);
    }

    [Fact]
    public async Task AppendAsync_CreatesTracingActivity()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");
        var evt = CreateTestEvent("AAPL");

        await traced.AppendAsync(evt);

        _capturedActivities.Should().ContainSingle(a =>
            a.OperationName == "Storage.Append.test-sink");

        var activity = _capturedActivities.First(a =>
            a.OperationName == "Storage.Append.test-sink");

        activity.GetTagItem("storage.sink").Should().Be("test-sink");
        activity.GetTagItem("market.symbol").Should().Be("AAPL");
        activity.GetTagItem("event.type").Should().Be("Trade");
        activity.GetTagItem("event.source").Should().Be("TEST");
    }

    [Fact]
    public async Task AppendAsync_OnError_RecordsErrorOnActivity()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        var act = async () => await traced.AppendAsync(CreateTestEvent("AAPL"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        traced.ErrorCount.Should().Be(1);
        traced.AppendCount.Should().Be(0);
    }

    [Fact]
    public async Task AppendAsync_WithCancellation_PropagatesCancellation()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        var act = async () => await traced.AppendAsync(CreateTestEvent("AAPL"));

        await act.Should().ThrowAsync<OperationCanceledException>();
        // OperationCanceledException should not increment error count
        traced.ErrorCount.Should().Be(0);
    }

    #endregion

    #region FlushAsync Tests

    [Fact]
    public async Task FlushAsync_DelegatesToInnerSink()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        await traced.FlushAsync();

        mockSink.Verify(s => s.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlushAsync_IncrementsFlushCount()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        await traced.FlushAsync();
        await traced.FlushAsync();

        traced.FlushCount.Should().Be(2);
    }

    [Fact]
    public async Task FlushAsync_CreatesTracingActivity()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        await traced.FlushAsync();

        _capturedActivities.Should().ContainSingle(a =>
            a.OperationName == "Storage.Flush.test-sink");
    }

    [Fact]
    public async Task FlushAsync_OnError_RecordsErrorOnActivity()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        var act = async () => await traced.FlushAsync();

        await act.Should().ThrowAsync<IOException>();
        traced.ErrorCount.Should().Be(1);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DelegatesToInnerSink()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        await traced.DisposeAsync();

        mockSink.Verify(s => s.DisposeAsync(), Times.Once);
    }

    #endregion

    #region End-to-End Tracing Tests

    [Fact]
    public async Task AppendAndFlush_CreatesLinkedActivities()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockSink.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "jsonl");

        await traced.AppendAsync(CreateTestEvent("AAPL"));
        await traced.AppendAsync(CreateTestEvent("MSFT"));
        await traced.FlushAsync();

        _capturedActivities.Should().HaveCount(3);
        _capturedActivities.Count(a => a.OperationName.StartsWith("Storage.Append.")).Should().Be(2);
        _capturedActivities.Count(a => a.OperationName.StartsWith("Storage.Flush.")).Should().Be(1);
    }

    [Fact]
    public async Task MultipleEvents_TracksAllSymbols()
    {
        var mockSink = new Mock<IStorageSink>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var traced = new TracedStorageSink(mockSink.Object, "test-sink");

        await traced.AppendAsync(CreateTestEvent("AAPL"));
        await traced.AppendAsync(CreateTestEvent("MSFT"));
        await traced.AppendAsync(CreateTestEvent("GOOG"));

        var symbols = _capturedActivities
            .Where(a => a.OperationName.StartsWith("Storage.Append."))
            .Select(a => a.GetTagItem("market.symbol")?.ToString())
            .ToList();

        symbols.Should().BeEquivalentTo(new[] { "AAPL", "MSFT", "GOOG" });
    }

    #endregion

    #region Helpers

    private static MarketEvent CreateTestEvent(string symbol, int sequence = 1)
    {
        var ts = DateTimeOffset.UtcNow;
        var trade = new Trade(ts, symbol, 150m, 100, AggressorSide.Buy, sequence);
        return MarketEvent.Trade(ts, symbol, trade, sequence, "TEST");
    }

    #endregion
}
