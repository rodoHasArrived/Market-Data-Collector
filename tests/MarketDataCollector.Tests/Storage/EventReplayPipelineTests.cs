using System.Text;
using System.Text.Json;
using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Replay;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

/// <summary>
/// Unit tests for EventReplayPipeline — the H3 (Event Replay Infrastructure) component
/// from the project roadmap. Tests replay pipeline with filtering, speed control,
/// sink publishing, pause/resume, and statistics tracking.
/// </summary>
public sealed class EventReplayPipelineTests : IDisposable
{
    private readonly string _testRoot;

    public EventReplayPipelineTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"mdc_replay_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDataRoot_ThrowsArgumentException()
    {
        var act = () => new EventReplayPipeline(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyDataRoot_ThrowsArgumentException()
    {
        var act = () => new EventReplayPipeline("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidRoot_SetsDefaultState()
    {
        using var pipeline = new EventReplayPipeline(_testRoot);

        pipeline.IsPaused.Should().BeFalse();
        pipeline.IsRunning.Should().BeFalse();
        pipeline.IsCompleted.Should().BeFalse();
        pipeline.Statistics.EventsReplayed.Should().Be(0);
    }

    #endregion

    #region Basic Replay Tests

    [Fact]
    public async Task ReplayAsync_EmptyDirectory_ReturnsNoEvents()
    {
        await using var pipeline = new EventReplayPipeline(_testRoot);

        var events = await CollectEventsAsync(pipeline);

        events.Should().BeEmpty();
        pipeline.Statistics.EventsReplayed.Should().Be(0);
        pipeline.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ReplayAsync_WithEvents_ReplaysAll()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 20);

        await using var pipeline = new EventReplayPipeline(_testRoot);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(20);
        pipeline.Statistics.EventsReplayed.Should().Be(20);
        pipeline.Statistics.EventsSkipped.Should().Be(0);
        pipeline.Statistics.EventsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReplayAsync_WithMultipleFiles_ReadsAll()
    {
        await CreateTestJsonlFileAsync("a_first.jsonl", "AAPL", 10);
        await CreateTestJsonlFileAsync("b_second.jsonl", "MSFT", 15);

        await using var pipeline = new EventReplayPipeline(_testRoot);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(25);
        pipeline.Statistics.EventsReplayed.Should().Be(25);
    }

    [Fact]
    public async Task ReplayAsync_NonExistentDirectory_ReturnsNoEvents()
    {
        var nonExistent = Path.Combine(_testRoot, "nonexistent");
        await using var pipeline = new EventReplayPipeline(nonExistent);

        var events = await CollectEventsAsync(pipeline);

        events.Should().BeEmpty();
    }

    #endregion

    #region Symbol Filter Tests

    [Fact]
    public async Task ReplayAsync_WithSymbolFilter_FiltersCorrectly()
    {
        await CreateMultiSymbolJsonlFileAsync("multi.jsonl",
            new[] { "AAPL", "MSFT", "GOOG" }, eventsPerSymbol: 5);

        var options = new ReplayPipelineOptions
        {
            Symbols = new HashSet<string> { "AAPL", "GOOG" }
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(10);
        events.All(e => e.Symbol is "AAPL" or "GOOG").Should().BeTrue();
        pipeline.Statistics.EventsSkipped.Should().Be(5); // 5 MSFT events skipped
    }

    [Fact]
    public async Task ReplayAsync_WithEmptySymbolFilter_ReplaysAll()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 10);

        var options = new ReplayPipelineOptions
        {
            Symbols = new HashSet<string>()
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(10);
    }

    #endregion

    #region Time Range Filter Tests

    [Fact]
    public async Task ReplayAsync_WithTimeRange_FiltersCorrectly()
    {
        var baseTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 100, baseTime);

        var options = new ReplayPipelineOptions
        {
            From = baseTime.AddSeconds(10),
            To = baseTime.AddSeconds(30)
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(21); // inclusive range [10..30]
        events.All(e => e.Timestamp >= options.From && e.Timestamp <= options.To)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ReplayAsync_WithFromOnly_FiltersFromStart()
    {
        var baseTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 50, baseTime);

        var options = new ReplayPipelineOptions
        {
            From = baseTime.AddSeconds(40)
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(10); // events 40..49
        events.All(e => e.Timestamp >= options.From).Should().BeTrue();
    }

    #endregion

    #region Event Type Filter Tests

    [Fact]
    public async Task ReplayAsync_WithEventTypeFilter_FiltersCorrectly()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 20);

        var options = new ReplayPipelineOptions
        {
            EventTypes = new HashSet<string> { "Trade" }
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        // All test events are trades, so all should pass
        events.Should().HaveCount(20);
    }

    [Fact]
    public async Task ReplayAsync_WithNonMatchingEventType_ReturnsEmpty()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 20);

        var options = new ReplayPipelineOptions
        {
            EventTypes = new HashSet<string> { "L2Snapshot" }
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().BeEmpty();
        pipeline.Statistics.EventsSkipped.Should().Be(20);
    }

    #endregion

    #region MaxEvents Limit Tests

    [Fact]
    public async Task ReplayAsync_WithMaxEvents_LimitsOutput()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 100);

        var options = new ReplayPipelineOptions
        {
            MaxEvents = 25
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(25);
        pipeline.Statistics.EventsReplayed.Should().Be(25);
    }

    #endregion

    #region Sink Publishing Tests

    [Fact]
    public async Task ReplayAsync_WithSinkPublishing_WritesToSink()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 10);

        var mockSink = new Mock<IStorageSink>();
        var appendedEvents = new List<MarketEvent>();
        mockSink.Setup(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MarketEvent, CancellationToken>((evt, _) => appendedEvents.Add(evt))
            .Returns(ValueTask.CompletedTask);
        mockSink.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = new ReplayPipelineOptions { PublishToSink = true };

        await using var pipeline = new EventReplayPipeline(_testRoot, options, mockSink.Object);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(10);
        appendedEvents.Should().HaveCount(10);
        mockSink.Verify(s => s.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplayAsync_WithoutSinkPublishing_DoesNotWriteToSink()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 10);

        var mockSink = new Mock<IStorageSink>();

        var options = new ReplayPipelineOptions { PublishToSink = false };

        await using var pipeline = new EventReplayPipeline(_testRoot, options, mockSink.Object);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(10);
        mockSink.Verify(s => s.AppendAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Pause/Resume Tests

    [Fact]
    public async Task PauseAndResume_ControlsReplayFlow()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 50);

        await using var pipeline = new EventReplayPipeline(_testRoot);

        var events = new List<MarketEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var evt in pipeline.ReplayAsync(cts.Token))
        {
            events.Add(evt);

            if (events.Count == 10)
            {
                pipeline.Pause();
                pipeline.IsPaused.Should().BeTrue();

                // Resume after brief pause
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    pipeline.Resume();
                });
            }
        }

        events.Should().HaveCount(50);
        pipeline.IsPaused.Should().BeFalse();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task Statistics_TracksReplayMetrics()
    {
        var baseTime = new DateTimeOffset(2026, 2, 1, 9, 30, 0, TimeSpan.Zero);
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 30, baseTime);

        await using var pipeline = new EventReplayPipeline(_testRoot);

        await CollectEventsAsync(pipeline);

        var stats = pipeline.Statistics;
        stats.EventsReplayed.Should().Be(30);
        stats.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
        stats.EventsPerSecond.Should().BeGreaterThan(0);
        stats.FirstEventTimestamp.Should().Be(baseTime);
        stats.LastEventTimestamp.Should().Be(baseTime.AddSeconds(29));
        stats.DataTimeSpan.Should().Be(TimeSpan.FromSeconds(29));
        stats.BytesRead.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Statistics_TracksSkippedEvents()
    {
        await CreateMultiSymbolJsonlFileAsync("multi.jsonl",
            new[] { "AAPL", "MSFT" }, eventsPerSymbol: 10);

        var options = new ReplayPipelineOptions
        {
            Symbols = new HashSet<string> { "AAPL" }
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        await CollectEventsAsync(pipeline);

        pipeline.Statistics.EventsReplayed.Should().Be(10);
        pipeline.Statistics.EventsSkipped.Should().Be(10);
    }

    #endregion

    #region State Machine Tests

    [Fact]
    public async Task ReplayAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 5);

        await using var pipeline = new EventReplayPipeline(_testRoot);

        // First replay
        await CollectEventsAsync(pipeline);
        pipeline.IsCompleted.Should().BeTrue();

        // Second replay should throw
        var act = async () => await CollectEventsAsync(pipeline);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReplayAsync_WithCancellation_StopsReplay()
    {
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 1000);

        await using var pipeline = new EventReplayPipeline(_testRoot);
        var cts = new CancellationTokenSource();
        var events = new List<MarketEvent>();

        try
        {
            await foreach (var evt in pipeline.ReplayAsync(cts.Token))
            {
                events.Add(evt);
                if (events.Count == 50)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        events.Count.Should().BeGreaterOrEqualTo(50);
        events.Count.Should().BeLessThan(1000);
    }

    #endregion

    #region GetSourceStatistics Tests

    [Fact]
    public void GetSourceStatistics_ReturnsFileInfo()
    {
        File.WriteAllText(Path.Combine(_testRoot, "a.jsonl"), "{}");
        File.WriteAllText(Path.Combine(_testRoot, "b.jsonl"), "{}");

        using var pipeline = new EventReplayPipeline(_testRoot);

        var stats = pipeline.GetSourceStatistics();

        stats.TotalFiles.Should().Be(2);
        stats.UncompressedFiles.Should().Be(2);
    }

    [Fact]
    public void GetSourceStatistics_EmptyDirectory_ReturnsZero()
    {
        using var pipeline = new EventReplayPipeline(_testRoot);

        var stats = pipeline.GetSourceStatistics();

        stats.TotalFiles.Should().Be(0);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var pipeline = new EventReplayPipeline(_testRoot);

        await pipeline.DisposeAsync();
        var act = async () => await pipeline.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Pause_AfterDispose_ThrowsObjectDisposedException()
    {
        var pipeline = new EventReplayPipeline(_testRoot);
        await pipeline.DisposeAsync();

        var act = () => pipeline.Pause();

        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Combined Filter Tests

    [Fact]
    public async Task ReplayAsync_WithCombinedFilters_AppliesAll()
    {
        var baseTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        await CreateMultiSymbolJsonlFileAsync("multi.jsonl",
            new[] { "AAPL", "MSFT", "GOOG" }, eventsPerSymbol: 20, baseTime);

        var options = new ReplayPipelineOptions
        {
            Symbols = new HashSet<string> { "AAPL" },
            From = baseTime.AddSeconds(5),
            To = baseTime.AddSeconds(15),
            MaxEvents = 5
        };

        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var events = await CollectEventsAsync(pipeline);

        events.Should().HaveCount(5);
        events.All(e => e.Symbol == "AAPL").Should().BeTrue();
        events.All(e => e.Timestamp >= options.From && e.Timestamp <= options.To)
            .Should().BeTrue();
    }

    #endregion

    #region Speed Control Tests

    [Fact]
    public async Task ReplayAsync_WithZeroSpeed_ReplaysAtMaxSpeed()
    {
        var baseTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        // Events spread over 100 seconds
        await CreateTestJsonlFileAsync("test.jsonl", "AAPL", 100, baseTime);

        var options = new ReplayPipelineOptions { SpeedMultiplier = 0 };
        await using var pipeline = new EventReplayPipeline(_testRoot, options);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await CollectEventsAsync(pipeline);
        sw.Stop();

        // At max speed, should complete much faster than 100 seconds
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Helpers

    private static async Task<List<MarketEvent>> CollectEventsAsync(
        EventReplayPipeline pipeline,
        CancellationToken ct = default)
    {
        var events = new List<MarketEvent>();
        await foreach (var evt in pipeline.ReplayAsync(ct))
        {
            events.Add(evt);
        }
        return events;
    }

    private async Task CreateTestJsonlFileAsync(
        string fileName, string symbol, int count, DateTimeOffset? baseTime = null)
    {
        var filePath = Path.Combine(_testRoot, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var sb = new StringBuilder();
        var time = baseTime ?? DateTimeOffset.UtcNow;

        for (int i = 0; i < count; i++)
        {
            sb.AppendLine(CreateEventJson(symbol, i, time.AddSeconds(i)));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private async Task CreateMultiSymbolJsonlFileAsync(
        string fileName, string[] symbols, int eventsPerSymbol, DateTimeOffset? baseTime = null)
    {
        var filePath = Path.Combine(_testRoot, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var sb = new StringBuilder();
        var time = baseTime ?? DateTimeOffset.UtcNow;

        foreach (var symbol in symbols)
        {
            for (int i = 0; i < eventsPerSymbol; i++)
            {
                sb.AppendLine(CreateEventJson(symbol, i, time.AddSeconds(i)));
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private static string CreateEventJson(string symbol, int sequence, DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var trade = new Trade(ts, symbol, 100m + sequence, 100, AggressorSide.Buy, sequence);
        var evt = MarketEvent.Trade(ts, symbol, trade, sequence, "TEST");
        return JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    #endregion
}
