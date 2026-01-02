using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.TradeService.Models;
using DataIngestion.TradeService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.TradeService.Consumers;

/// <summary>
/// Consumes raw trade messages from the message bus.
/// </summary>
public sealed class RawTradeConsumer : IConsumer<IRouteIngestionData>
{
    private readonly ITradeProcessor _processor;
    private readonly ILogger _log = Log.ForContext<RawTradeConsumer>();

    public RawTradeConsumer(ITradeProcessor processor)
    {
        _processor = processor;
    }

    public Task Consume(ConsumeContext<IRouteIngestionData> context)
    {
        var message = context.Message;

        // Only process trade data types
        if (message.DataType != IngestionDataType.Trade &&
            message.DataType != IngestionDataType.HistoricalTrade)
        {
            return Task.CompletedTask;
        }

        try
        {
            // Deserialize the trade data
            var tradeData = JsonSerializer.Deserialize<TradePayload>(message.RawPayload);
            if (tradeData == null)
            {
                _log.Warning("Failed to deserialize trade payload for {Symbol}", message.Symbol);
                return Task.CompletedTask;
            }

            var processedTrade = new ProcessedTrade
            {
                MessageId = message.MessageId,
                CorrelationId = context.CorrelationId ?? Guid.NewGuid(),
                Symbol = message.Symbol,
                Timestamp = tradeData.Timestamp ?? message.Timestamp,
                Price = tradeData.Price,
                Size = tradeData.Size,
                AggressorSide = tradeData.AggressorSide,
                TradeId = tradeData.TradeId,
                Exchange = tradeData.Exchange,
                Source = message.Source,
                Sequence = message.Sequence
            };

            if (!_processor.TrySubmit(processedTrade))
            {
                _log.Warning("Failed to submit trade for {Symbol} - queue full", message.Symbol);
            }
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "JSON error processing trade for {Symbol}", message.Symbol);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Trade payload from gateway.
/// </summary>
internal record TradePayload(
    decimal Price,
    long Size,
    string? AggressorSide,
    string? TradeId,
    string? Exchange,
    DateTimeOffset? Timestamp,
    string? Source
);
