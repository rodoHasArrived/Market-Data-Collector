using MassTransit;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Sample consumer for Level 2 order book snapshots.
/// Extend this to implement custom depth processing logic.
/// </summary>
public sealed class L2SnapshotReceivedConsumer : IConsumer<IL2SnapshotReceived>
{
    private readonly ILogger _log;

    public L2SnapshotReceivedConsumer()
    {
        _log = Log.ForContext<L2SnapshotReceivedConsumer>();
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

        // TODO: Add custom order book processing logic here
        // Examples:
        // - Calculate market microstructure metrics
        // - Update visualization displays
        // - Detect large order imbalances
        // - Compute VWAP bands

        return Task.CompletedTask;
    }
}
