using FluentAssertions;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Interfaces;
using Xunit;

namespace MarketDataCollector.Tests.Pipeline;

/// <summary>
/// Tests for the <see cref="IBackpressureSignal"/> implementation on <see cref="EventPipeline"/>,
/// verifying that producers can observe pipeline pressure and make throttling decisions.
/// </summary>
public sealed class BackpressureSignalTests : IAsyncLifetime
{
    private MockBpSink _sink = null!;
    private EventPipeline _pipeline = null!;

    public Task InitializeAsync()
    {
        _sink = new MockBpSink();
        _pipeline = new EventPipeline(
            _sink,
            capacity: 100,
            enablePeriodicFlush: false);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _pipeline.DisposeAsync();
    }

    // ------------------------------------------------------------------ //
    //  IBackpressureSignal contract                                        //
    // ------------------------------------------------------------------ //

    [Fact]
    public void EventPipeline_ImplementsIBackpressureSignal()
    {
        _pipeline.Should().BeAssignableTo<IBackpressureSignal>(
            "EventPipeline must implement IBackpressureSignal so producers can observe pressure");
    }

    [Fact]
    public void BackpressureSignal_InitialUtilization_IsZeroOrLow()
    {
        IBackpressureSignal signal = _pipeline;
        signal.QueueUtilization.Should().BeInRange(0.0, 0.1,
            "an idle pipeline should have near-zero utilization");
    }

    [Fact]
    public void BackpressureSignal_InitialPressure_IsFalse()
    {
        IBackpressureSignal signal = _pipeline;
        signal.IsUnderPressure.Should().BeFalse(
            "a freshly created pipeline must not report pressure before any events are published");
    }

    [Fact]
    public void IsUnderPressure_Initially_IsFalse()
    {
        _pipeline.IsUnderPressure.Should().BeFalse();
    }

    [Fact]
    public void QueueUtilization_Fraction_IsBetweenZeroAndOne()
    {
        IBackpressureSignal signal = _pipeline;
        // Push some events to get a non-trivial utilization reading
        var ts = DateTimeOffset.UtcNow;
        var trade = new Trade(ts, "SPY", 520m, 100L, AggressorSide.Buy, 1L);
        for (var i = 0; i < 10; i++)
        {
            _pipeline.TryPublish(MarketEvent.Trade(ts, "SPY", trade, seq: i));
        }

        signal.QueueUtilization.Should().BeInRange(0.0, 1.0,
            "IBackpressureSignal.QueueUtilization must return a 0–1 fraction");
    }

    [Fact]
    public async Task PublicQueueUtilization_And_SignalQueueUtilization_AreConsistent()
    {
        // Use a dedicated blocking pipeline (batchSize: 1) so the consumer holds one event in
        // AppendAsync while the remaining events stay in the channel, giving a stable non-zero
        // utilization to read from both properties without a race condition.
        var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sink = new BlockingStorageSink(releaseTcs.Task);
        await using var pipeline = new EventPipeline(sink, capacity: 100, batchSize: 1, enablePeriodicFlush: false);
        IBackpressureSignal signal = pipeline;

        // Publish 10 events so the consumer processes event 1 (and blocks) while events 2-10 stay in the channel.
        var ts = DateTimeOffset.UtcNow;
        var trade = new Trade(ts, "MSFT", 420m, 50L, AggressorSide.Sell, 1L);
        for (var i = 1; i <= 10; i++)
            pipeline.TryPublish(MarketEvent.Trade(ts, "MSFT", trade, seq: i));

        // Wait until the consumer is blocked inside AppendAsync.
        await sink.WaitForFirstBlockAsync(TimeSpan.FromSeconds(5));

        // Both reads happen while the consumer is frozen — no race possible.
        var publicUtil = pipeline.QueueUtilization; // 0–100
        var signalUtil = signal.QueueUtilization;   // 0–1

        // Release the consumer for clean disposal.
        releaseTcs.SetResult(true);

        (publicUtil / 100.0).Should().BeApproximately(signalUtil, precision: 1e-9,
            "IBackpressureSignal.QueueUtilization must be the public property divided by 100");
    }

    // ------------------------------------------------------------------ //
    //  Helpers                                                             //
    // ------------------------------------------------------------------ //

    private sealed class MockBpSink : IStorageSink
    {
        public ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
