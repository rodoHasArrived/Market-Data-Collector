using MassTransit;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Consumer for Best Bid/Offer quote updates. Logs quotes and updates metrics.
/// </summary>
public sealed class BboQuoteUpdatedConsumer : IConsumer<IBboQuoteUpdated>
{
    private readonly ILogger _log;

    public BboQuoteUpdatedConsumer()
    {
        _log = LoggingSetup.ForContext<BboQuoteUpdatedConsumer>();
    }

    public Task Consume(ConsumeContext<IBboQuoteUpdated> context)
    {
        var quote = context.Message;

        _log.Verbose(
            "BBO Quote: {Symbol} [{BidPrice} x {BidSize}] / [{AskPrice} x {AskSize}] Spread={Spread} @ {Timestamp}",
            quote.Symbol,
            quote.BidPrice,
            quote.BidSize,
            quote.AskPrice,
            quote.AskSize,
            quote.Spread,
            quote.Timestamp);

        Metrics.IncQuotes();

        return Task.CompletedTask;
    }
}
