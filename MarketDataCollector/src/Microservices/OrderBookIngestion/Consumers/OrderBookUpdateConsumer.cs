using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.OrderBookService.Models;
using DataIngestion.OrderBookService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.OrderBookService.Consumers;

public sealed class OrderBookUpdateConsumer : IConsumer<IRouteIngestionData>
{
    private readonly IOrderBookManager _manager;
    private readonly ILogger _log = Log.ForContext<OrderBookUpdateConsumer>();

    public OrderBookUpdateConsumer(IOrderBookManager manager)
    {
        _manager = manager;
    }

    public Task Consume(ConsumeContext<IRouteIngestionData> context)
    {
        var message = context.Message;

        if (message.DataType != IngestionDataType.OrderBookUpdate)
            return Task.CompletedTask;

        try
        {
            var payload = JsonSerializer.Deserialize<UpdatePayload>(message.RawPayload);
            if (payload == null) return Task.CompletedTask;

            var updateType = Enum.TryParse<OrderBookUpdateType>(payload.UpdateType, true, out var ut)
                ? ut : OrderBookUpdateType.Update;

            var side = Enum.TryParse<OrderBookSide>(payload.Side, true, out var s)
                ? s : OrderBookSide.Bid;

            var update = new OrderBookUpdate(
                message.Symbol,
                message.Timestamp,
                message.Sequence,
                updateType,
                side,
                payload.Position,
                payload.Price,
                payload.Size,
                payload.MarketMaker
            );

            _manager.ApplyUpdate(message.Symbol, update);
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to deserialize order book update for {Symbol}", message.Symbol);
        }

        return Task.CompletedTask;
    }

    private record UpdatePayload(
        string UpdateType,
        string Side,
        int Position,
        decimal? Price,
        long? Size,
        string? MarketMaker
    );
}
