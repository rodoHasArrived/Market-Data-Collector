using MassTransit;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Sample consumer for trade events.
/// Extend this to implement custom trade processing logic.
/// </summary>
public sealed class TradeOccurredConsumer : IConsumer<ITradeOccurred>
{
    private readonly ILogger _log;

    public TradeOccurredConsumer()
    {
        _log = Log.ForContext<TradeOccurredConsumer>();
    }

    public Task Consume(ConsumeContext<ITradeOccurred> context)
    {
        var trade = context.Message;

        _log.Verbose(
            "Trade received: {Symbol} {Price} x {Size} ({Side}) @ {Timestamp}",
            trade.Symbol,
            trade.Price,
            trade.Size,
            trade.AggressorSide,
            trade.Timestamp);

        Metrics.IncTrades();

        // TODO: Add custom trade processing logic here
        // Examples:
        // - Update real-time price displays
        // - Calculate running VWAP
        // - Trigger trading signals
        // - Forward to external systems

        return Task.CompletedTask;
    }
}
