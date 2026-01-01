using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Providers;

namespace MarketDataCollector.Infrastructure.Providers.Polygon;

/// <summary>
/// Minimal stub for a Polygon market data adapter. This validates the provider abstraction and can be
/// evolved to a full WebSocket client later. For now it exercises the pipelines with a synthetic heartbeat.
/// </summary>
public sealed class PolygonMarketDataClient : IMarketDataClient
{
    private readonly IMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly QuoteCollector _quoteCollector;

    public PolygonMarketDataClient(IMarketEventPublisher publisher, TradeDataCollector tradeCollector, QuoteCollector quoteCollector)
    {
        _publisher = publisher;
        _tradeCollector = tradeCollector;
        _quoteCollector = quoteCollector;
    }

    public bool IsEnabled => true;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        // Emit a synthetic heartbeat so downstream consumers can verify connectivity without real credentials.
        _publisher.TryPublish(MarketEvent.Heartbeat(DateTimeOffset.UtcNow, source: "PolygonStub"));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public int SubscribeMarketDepth(SymbolConfig cfg) => -1; // Depth not wired yet

    public void UnsubscribeMarketDepth(int subscriptionId) { }

    public int SubscribeTrades(SymbolConfig cfg)
    {
        // Emit a lightweight synthetic trade for testing cross-provider reconciliation.
        _tradeCollector.OnTrade(new Domain.Models.MarketTradeUpdate(DateTimeOffset.UtcNow, cfg.Symbol, 0m, 0, Domain.Models.AggressorSide.Unknown, 0, "POLY", "STUB"));
        return -1;
    }

    public void UnsubscribeTrades(int subscriptionId) { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
