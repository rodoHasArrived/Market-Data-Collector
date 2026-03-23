using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Meridian.Application.Pipeline;
using Meridian.Contracts.Domain.Enums;
using Meridian.Contracts.Domain.Models;
using Meridian.Domain.Events;
using Meridian.Storage.Interfaces;
using Xunit;

namespace Meridian.Tests.Pipeline;

public sealed class EventPipelineTracingTests
{
    [Fact]
    public async Task PublishAsync_CapturesOriginatingActivityContext_And_PropagatesToSinkOperations()
    {
        using var listener = CreateActivityListener();
        await using var sink = new ActivityCapturingSink();
        await using var pipeline = new EventPipeline(
            sink,
            capacity: 16,
            batchSize: 1,
            enablePeriodicFlush: false);

        using var sourceActivity = new Activity("Provider.Receive").Start();
        var evt = CreateTradeEvent("SPY");

        await pipeline.PublishAsync(evt);
        await pipeline.FlushAsync();

        sink.ReceivedEvents.Should().ContainSingle();
        var storedEvent = sink.ReceivedEvents.Single();
        storedEvent.ActivityTraceId.Should().Be(sourceActivity.TraceId.ToHexString());
        storedEvent.ActivitySpanId.Should().Be(sourceActivity.SpanId.ToHexString());
        storedEvent.GetOriginatingActivityContext().Should().NotBeNull();

        sink.AppendActivityTraceIds.Should().ContainSingle(sourceActivity.TraceId.ToHexString());

        listener.StoppedActivities.Should().Contain(a => a.OperationName == "ProcessMarketEvent.Trade");
        listener.StoppedActivities.Should().Contain(a => a.OperationName == $"StoreMarketEvent.{nameof(ActivityCapturingSink)}");

        var processActivity = listener.StoppedActivities.Single(a => a.OperationName == "ProcessMarketEvent.Trade");
        var storageActivity = listener.StoppedActivities.Single(a => a.OperationName == $"StoreMarketEvent.{nameof(ActivityCapturingSink)}");

        processActivity.TraceId.Should().Be(sourceActivity.TraceId);
        processActivity.ParentSpanId.Should().Be(sourceActivity.SpanId);
        storageActivity.TraceId.Should().Be(sourceActivity.TraceId);
        storageActivity.ParentSpanId.Should().Be(processActivity.SpanId);
    }

    private static ActivityCaptureListener CreateActivityListener()
    {
        var listener = new ActivityCaptureListener();
        ActivitySource.AddActivityListener(listener.Listener);
        return listener;
    }

    private static MarketEvent CreateTradeEvent(string symbol, long sequence = 1)
        => MarketEvent.Trade(
            DateTimeOffset.UtcNow,
            symbol,
            new Trade(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: symbol,
                Price: 100.25m,
                Size: 10,
                Aggressor: AggressorSide.Unknown,
                SequenceNumber: sequence,
                Venue: "XNYS"),
            source: "test");

    private sealed class ActivityCapturingSink : IStorageSink
    {
        public List<MarketEvent> ReceivedEvents { get; } = new();
        public List<string?> AppendActivityTraceIds { get; } = new();

        public ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
        {
            ReceivedEvents.Add(evt);
            AppendActivityTraceIds.Add(Activity.Current?.TraceId.ToHexString());
            return ValueTask.CompletedTask;
        }

        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ActivityCaptureListener : IDisposable
    {
        private readonly ConcurrentQueue<Activity> _stoppedActivities = new();

        public ActivityListener Listener { get; } = new()
        {
            ShouldListenTo = _ => true,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { }
        };

        public ActivityCaptureListener()
        {
            Listener.ActivityStopped = activity => _stoppedActivities.Enqueue(activity);
        }

        public IReadOnlyCollection<Activity> StoppedActivities => _stoppedActivities.ToArray();

        public void Dispose() => Listener.Dispose();
    }
}
