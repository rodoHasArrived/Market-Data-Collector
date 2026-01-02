using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.TradeService.Models;
using DataIngestion.TradeService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.TradeService.Consumers;

/// <summary>
/// Consumes batch trade messages for efficient bulk processing.
/// </summary>
public sealed class TradesBatchConsumer : IConsumer<IIngestTradesBatch>
{
    private readonly ITradeProcessor _processor;
    private readonly ILogger _log = Log.ForContext<TradesBatchConsumer>();

    public TradesBatchConsumer(ITradeProcessor processor)
    {
        _processor = processor;
    }

    public Task Consume(ConsumeContext<IIngestTradesBatch> context)
    {
        var message = context.Message;

        if (message.Trades == null || message.Trades.Count == 0)
        {
            return Task.CompletedTask;
        }

        _log.Debug("Processing batch of {Count} trades for {Symbol}",
            message.Trades.Count, message.Symbol);

        var trades = message.Trades.Select((t, i) => new ProcessedTrade
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = context.CorrelationId ?? message.CorrelationId,
            Symbol = message.Symbol,
            Timestamp = t.Timestamp,
            Price = t.Price,
            Size = t.Size,
            AggressorSide = t.AggressorSide,
            TradeId = t.TradeId,
            Exchange = t.Exchange,
            Conditions = t.Conditions,
            Source = message.Source,
            Sequence = i
        });

        var submitted = _processor.SubmitBatch(trades);

        if (submitted < message.Trades.Count)
        {
            _log.Warning("Only submitted {Submitted}/{Total} trades for {Symbol}",
                submitted, message.Trades.Count, message.Symbol);
        }

        return Task.CompletedTask;
    }
}
