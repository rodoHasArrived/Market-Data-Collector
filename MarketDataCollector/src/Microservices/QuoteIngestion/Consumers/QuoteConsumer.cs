using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.QuoteService.Models;
using DataIngestion.QuoteService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.QuoteService.Consumers;

public sealed class QuoteConsumer : IConsumer<IRouteIngestionData>
{
    private readonly IQuoteProcessor _processor;
    private readonly Serilog.ILogger _log = Log.ForContext<QuoteConsumer>();

    public QuoteConsumer(IQuoteProcessor processor) => _processor = processor;

    public Task Consume(ConsumeContext<IRouteIngestionData> context)
    {
        var msg = context.Message;
        if (msg.DataType != IngestionDataType.Quote) return Task.CompletedTask;

        try
        {
            var payload = JsonSerializer.Deserialize<QuotePayload>(msg.RawPayload);
            if (payload == null) return Task.CompletedTask;

            var quote = new ProcessedQuote
            {
                MessageId = msg.MessageId,
                CorrelationId = context.CorrelationId ?? Guid.NewGuid(),
                Symbol = msg.Symbol,
                Timestamp = payload.Timestamp ?? msg.Timestamp,
                BidPrice = payload.BidPrice,
                BidSize = payload.BidSize,
                AskPrice = payload.AskPrice,
                AskSize = payload.AskSize,
                BidExchange = payload.BidExchange,
                AskExchange = payload.AskExchange,
                Source = msg.Source,
                Sequence = msg.Sequence
            };

            if (!_processor.TrySubmit(quote))
                _log.Warning("Failed to submit quote for {Symbol}", msg.Symbol);
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to deserialize quote for {Symbol}", msg.Symbol);
        }

        return Task.CompletedTask;
    }

    private record QuotePayload(decimal BidPrice, long BidSize, decimal AskPrice, long AskSize,
        string? BidExchange, string? AskExchange, DateTimeOffset? Timestamp);
}
