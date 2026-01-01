using MassTransit;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Consumer for Level 2 order book snapshots. Logs snapshots and updates metrics.
/// </summary>
public sealed class L2SnapshotReceivedConsumer : IConsumer<IL2SnapshotReceived>
{
    private readonly ILogger _log;

    public L2SnapshotReceivedConsumer()
    {
        _log = LoggingSetup.ForContext<L2SnapshotReceivedConsumer>();
    }

    public Task Consume(ConsumeContext<IL2SnapshotReceived> context)
    {
        var snapshot = context.Message;

        var bestBid = snapshot.Bids.FirstOrDefault();
        var bestAsk = snapshot.Asks.FirstOrDefault();

        _log.Verbose(
            "L2 Snapshot: {Symbol} BBO [{BidPrice} x {BidSize}] / [{AskPrice} x {AskSize}] @ {Timestamp}",
            snapshot.Symbol,
            bestBid?.Price ?? 0,
            bestBid?.Size ?? 0,
            bestAsk?.Price ?? 0,
            bestAsk?.Size ?? 0,
            snapshot.Timestamp);

        Metrics.IncDepthUpdates();

        return Task.CompletedTask;
    }
}
