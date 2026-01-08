using MassTransit;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Publishers;

/// <summary>
/// MassTransit-based publisher that implements IMarketEventPublisher.
/// Publishes market events to the configured message broker (RabbitMQ, Azure Service Bus, etc.).
/// </summary>
public sealed class MassTransitPublisher : IMarketEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger _log;
    private readonly bool _enableMetrics;

    public MassTransitPublisher(IPublishEndpoint publishEndpoint, bool enableMetrics = true)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _log = LoggingSetup.ForContext<MassTransitPublisher>();
        _enableMetrics = enableMetrics;
    }

    /// <summary>
    /// Non-blocking publish that fires and forgets to maintain hot-path performance.
    /// Returns true if the message was queued for publishing.
    /// </summary>
    public bool TryPublish(in MarketEvent evt)
    {
        try
        {
            // Fire and forget - we don't await to keep hot path fast
            _ = PublishAsync(evt);

            if (_enableMetrics)
                Metrics.IncPublished();

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to queue market event for MassTransit publish: {Symbol} {Type}", evt.Symbol, evt.Type);

            if (_enableMetrics)
                Metrics.IncDropped();

            return false;
        }
    }

    private async Task PublishAsync(MarketEvent evt)
    {
        try
        {
            object? message = evt.Type switch
            {
                MarketEventType.Trade when evt.Payload is Trade trade => CreateTradeMessage(evt, trade),
                MarketEventType.L2Snapshot when evt.Payload is LOBSnapshot lob => CreateL2SnapshotMessage(evt, lob),
                MarketEventType.L2Snapshot when evt.Payload is L2SnapshotPayload l2Payload => CreateL2SnapshotMessage(evt, l2Payload.Snapshot),
                MarketEventType.BboQuote when evt.Payload is BboQuotePayload bbo => CreateBboQuoteMessage(evt, bbo),
                MarketEventType.OrderFlow when evt.Payload is OrderFlowStatistics orderFlow => CreateOrderFlowMessage(evt, orderFlow),
                MarketEventType.Integrity when evt.Payload is IntegrityEvent integrity => CreateIntegrityMessage(evt, integrity),
                MarketEventType.Integrity when evt.Payload is DepthIntegrityEvent depthIntegrity => CreateDepthIntegrityMessage(evt, depthIntegrity),
                MarketEventType.Heartbeat => CreateHeartbeatMessage(evt),
                MarketEventType.ConnectionStatus => CreateConnectionStatusMessage(evt),
                _ => null
            };

            if (message != null)
            {
                await _publishEndpoint.Publish(message);
                _log.Verbose("Published {EventType} for {Symbol}", evt.Type, evt.Symbol);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error publishing market event via MassTransit: {Symbol} {Type}", evt.Symbol, evt.Type);
            if (_enableMetrics)
                Metrics.IncDropped();
        }
    }

    private static TradeOccurredMessage CreateTradeMessage(MarketEvent evt, Trade trade)
    {
        return new TradeOccurredMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.Trade),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            Price = trade.Price,
            Size = trade.Size,
            AggressorSide = trade.Aggressor.ToString(),
            TradeId = trade.StreamId,
            Venue = trade.Venue
        };
    }

    private static L2SnapshotReceivedMessage CreateL2SnapshotMessage(MarketEvent evt, LOBSnapshot lob)
    {
        return new L2SnapshotReceivedMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.L2Snapshot),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            Bids = lob.Bids.Select(b => new OrderBookLevelData((decimal)b.Price, (long)b.Size, b.MarketMaker)).ToList(),
            Asks = lob.Asks.Select(a => new OrderBookLevelData((decimal)a.Price, (long)a.Size, a.MarketMaker)).ToList(),
            TotalBidVolume = (long)lob.Bids.Sum(b => b.Size),
            TotalAskVolume = (long)lob.Asks.Sum(a => a.Size)
        };
    }

    private static BboQuoteUpdatedMessage CreateBboQuoteMessage(MarketEvent evt, BboQuotePayload bbo)
    {
        return new BboQuoteUpdatedMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.BboQuote),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            BidPrice = bbo.BidPrice,
            BidSize = bbo.BidSize,
            AskPrice = bbo.AskPrice,
            AskSize = bbo.AskSize,
            Spread = bbo.Spread ?? 0,
            Exchange = bbo.Venue
        };
    }

    private static OrderFlowUpdatedMessage CreateOrderFlowMessage(MarketEvent evt, OrderFlowStatistics orderFlow)
    {
        return new OrderFlowUpdatedMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.OrderFlow),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            BuyVolume = orderFlow.BuyVolume,
            SellVolume = orderFlow.SellVolume,
            NetVolume = orderFlow.BuyVolume - orderFlow.SellVolume,
            Vwap = orderFlow.VWAP,
            TradeCount = orderFlow.TradeCount,
            WindowStart = orderFlow.Timestamp.AddMinutes(-1), // Approximation
            WindowEnd = orderFlow.Timestamp
        };
    }

    private static IntegrityEventOccurredMessage CreateIntegrityMessage(MarketEvent evt, IntegrityEvent integrity)
    {
        return new IntegrityEventOccurredMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.Integrity),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            Severity = integrity.Severity.ToString(),
            Description = integrity.Description,
            ErrorCode = integrity.ErrorCode,
            StreamId = integrity.StreamId,
            Venue = integrity.Venue
        };
    }

    private static IntegrityEventOccurredMessage CreateDepthIntegrityMessage(MarketEvent evt, DepthIntegrityEvent depthIntegrity)
    {
        // Map DepthIntegrityKind to severity string
        var severity = depthIntegrity.Kind switch
        {
            DepthIntegrityKind.Gap => "Error",
            DepthIntegrityKind.OutOfOrder => "Warning",
            DepthIntegrityKind.InvalidPosition => "Error",
            DepthIntegrityKind.Stale => "Warning",
            _ => "Info"
        };

        return new IntegrityEventOccurredMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.Integrity),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            Severity = severity,
            Description = depthIntegrity.Description,
            ErrorCode = (int)depthIntegrity.Kind,
            StreamId = depthIntegrity.StreamId,
            Venue = depthIntegrity.Venue
        };
    }

    private static HeartbeatReceivedMessage CreateHeartbeatMessage(MarketEvent evt)
    {
        return new HeartbeatReceivedMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.Heartbeat),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            TotalEventsPublished = Metrics.Published,
            TotalEventsDropped = Metrics.Dropped,
            EventsPerSecond = Metrics.EventsPerSecond,
            ActiveSubscriptions = 0 // Would need to inject subscription manager for accurate count
        };
    }

    private static ConnectionStatusChangedMessage CreateConnectionStatusMessage(MarketEvent evt)
    {
        return new ConnectionStatusChangedMessage
        {
            MessageId = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            Symbol = evt.Symbol,
            EventType = nameof(MarketEventType.ConnectionStatus),
            Sequence = evt.Sequence,
            Source = evt.Source,
            SchemaVersion = evt.SchemaVersion,
            Status = "Unknown",
            Provider = evt.Source,
            ErrorMessage = null,
            ReconnectAttempt = 0
        };
    }
}

#region Message Implementation Classes

internal sealed class TradeOccurredMessage : ITradeOccurred
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public decimal Price { get; init; }
    public long Size { get; init; }
    public string AggressorSide { get; init; } = string.Empty;
    public string? TradeId { get; init; }
    public string? Venue { get; init; }
}

internal sealed class L2SnapshotReceivedMessage : IL2SnapshotReceived
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public IReadOnlyList<OrderBookLevelData> Bids { get; init; } = Array.Empty<OrderBookLevelData>();
    public IReadOnlyList<OrderBookLevelData> Asks { get; init; } = Array.Empty<OrderBookLevelData>();
    public long TotalBidVolume { get; init; }
    public long TotalAskVolume { get; init; }
}

internal sealed class BboQuoteUpdatedMessage : IBboQuoteUpdated
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public decimal BidPrice { get; init; }
    public long BidSize { get; init; }
    public decimal AskPrice { get; init; }
    public long AskSize { get; init; }
    public decimal Spread { get; init; }
    public string? Exchange { get; init; }
}

internal sealed class OrderFlowUpdatedMessage : IOrderFlowUpdated
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public long BuyVolume { get; init; }
    public long SellVolume { get; init; }
    public long NetVolume { get; init; }
    public decimal Vwap { get; init; }
    public int TradeCount { get; init; }
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
}

internal sealed class IntegrityEventOccurredMessage : IIntegrityEventOccurred
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? ErrorCode { get; init; }
    public string? StreamId { get; init; }
    public string? Venue { get; init; }
}

internal sealed class HeartbeatReceivedMessage : IHeartbeatReceived
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public long TotalEventsPublished { get; init; }
    public long TotalEventsDropped { get; init; }
    public double EventsPerSecond { get; init; }
    public int ActiveSubscriptions { get; init; }
}

internal sealed class ConnectionStatusChangedMessage : IConnectionStatusChanged
{
    public Guid MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public int ReconnectAttempt { get; init; }
}

#endregion
