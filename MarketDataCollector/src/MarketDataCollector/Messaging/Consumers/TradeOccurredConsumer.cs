using MassTransit;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Consumer for trade events. Logs trades and updates metrics.
/// </summary>
public sealed class TradeOccurredConsumer : IConsumer<ITradeOccurred>
{
    private readonly ILogger _log;

    public TradeOccurredConsumer()
    {
        _log = LoggingSetup.ForContext<TradeOccurredConsumer>();
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

        return Task.CompletedTask;
    }
}
