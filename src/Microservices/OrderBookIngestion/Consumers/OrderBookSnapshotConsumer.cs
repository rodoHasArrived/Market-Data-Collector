using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.OrderBookService.Models;
using DataIngestion.OrderBookService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.OrderBookService.Consumers;

public sealed class OrderBookSnapshotConsumer : IConsumer<IRouteIngestionData>
{
    private readonly IOrderBookManager _manager;
    private readonly Serilog.ILogger _log = Log.ForContext<OrderBookSnapshotConsumer>();

    public OrderBookSnapshotConsumer(IOrderBookManager manager)
    {
        _manager = manager;
    }

    public Task Consume(ConsumeContext<IRouteIngestionData> context)
    {
        var message = context.Message;

        if (message.DataType != IngestionDataType.OrderBookSnapshot)
            return Task.CompletedTask;

        try
        {
            var payload = JsonSerializer.Deserialize<SnapshotPayload>(message.RawPayload);
            if (payload == null) return Task.CompletedTask;

            var snapshot = new OrderBookSnapshot(
                message.Symbol,
                message.Timestamp,
                message.Sequence,
                payload.Bids?.Select(b => new OrderBookLevel(b.Price, b.Size, b.MarketMaker)).ToList()
                    ?? [],
                payload.Asks?.Select(a => new OrderBookLevel(a.Price, a.Size, a.MarketMaker)).ToList()
                    ?? []
            );

            _manager.ApplySnapshot(message.Symbol, snapshot);
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to deserialize order book snapshot for {Symbol}", message.Symbol);
        }

        return Task.CompletedTask;
    }

    private record SnapshotPayload(List<LevelData>? Bids, List<LevelData>? Asks);
    private record LevelData(decimal Price, long Size, string? MarketMaker);
}
