using MassTransit;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Sample consumer for Best Bid/Offer quote updates.
/// Extend this to implement custom quote processing logic.
/// </summary>
public sealed class BboQuoteUpdatedConsumer : IConsumer<IBboQuoteUpdated>
{
    private readonly ILogger _log;

    public BboQuoteUpdatedConsumer()
    {
        _log = Log.ForContext<BboQuoteUpdatedConsumer>();
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

        // TODO: Add custom quote processing logic here
        // Examples:
        // - Track spread history
        // - Monitor quote staleness
        // - Update pricing engines
        // - Generate mid-price series

        return Task.CompletedTask;
    }
}
