using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.QuoteService.Models;
using DataIngestion.QuoteService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.QuoteService.Consumers;

public sealed class NbboConsumer : IConsumer<INbboUpdate>
{
    private readonly IQuoteProcessor _processor;
    private readonly Serilog.ILogger _log = Log.ForContext<NbboConsumer>();

    public NbboConsumer(IQuoteProcessor processor) => _processor = processor;

    public Task Consume(ConsumeContext<INbboUpdate> context)
    {
        var msg = context.Message;

        var quote = new ProcessedQuote
        {
            MessageId = msg.MessageId,
            CorrelationId = context.CorrelationId ?? msg.CorrelationId,
            Symbol = msg.Symbol,
            Timestamp = msg.Timestamp,
            BidPrice = msg.BidPrice,
            BidSize = msg.BidSize,
            AskPrice = msg.AskPrice,
            AskSize = msg.AskSize,
            BidExchange = msg.BidExchange,
            AskExchange = msg.AskExchange,
            Source = msg.Source,
            Sequence = msg.Sequence,
            Spread = msg.Spread,
            MidPrice = msg.MidPrice
        };

        if (!_processor.TrySubmit(quote))
            _log.Warning("Failed to submit NBBO for {Symbol}", msg.Symbol);

        return Task.CompletedTask;
    }
}
