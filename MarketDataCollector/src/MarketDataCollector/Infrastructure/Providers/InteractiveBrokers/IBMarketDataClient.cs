using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;

/// <summary>
/// Concrete Interactive Brokers market data client. Buildable out-of-the-box:
/// - Without IBAPI defined, this type exists but delegates to NoOp.
/// - With IBAPI defined, it uses EnhancedIBConnectionManager + IBCallbackRouter.
/// </summary>
public sealed class IBMarketDataClient : IMarketDataClient
{
    private readonly IMarketDataClient _inner;

    public IBMarketDataClient(IMarketEventPublisher publisher, TradeDataCollector tradeCollector, MarketDepthCollector depthCollector)
    {
#if IBAPI
        _inner = new IBMarketDataClientIBApi(publisher, tradeCollector, depthCollector);
#else
        _inner = new NoOpMarketDataClient();
#endif
    }

    public bool IsEnabled => _inner.IsEnabled;

    public Task ConnectAsync(CancellationToken ct = default) => _inner.ConnectAsync(ct);
    public Task DisconnectAsync(CancellationToken ct = default) => _inner.DisconnectAsync(ct);

    public int SubscribeMarketDepth(SymbolConfig cfg) => _inner.SubscribeMarketDepth(cfg);
    public void UnsubscribeMarketDepth(int subscriptionId) => _inner.UnsubscribeMarketDepth(subscriptionId);

    public int SubscribeTrades(SymbolConfig cfg) => _inner.SubscribeTrades(cfg);
    public void UnsubscribeTrades(int subscriptionId) => _inner.UnsubscribeTrades(subscriptionId);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

#if IBAPI
internal sealed class IBMarketDataClientIBApi : IMarketDataClient
{
    private readonly EnhancedIBConnectionManager _conn;
    private readonly IBCallbackRouter _router;

    // Track subscription ids if you want per-symbol teardown later
    public bool IsEnabled => true;

    public IBMarketDataClientIBApi(IMarketEventPublisher publisher, TradeDataCollector tradeCollector, MarketDepthCollector depthCollector)
    {
        // Router wires IB callbacks -> collectors (collectors already publish into publisher).
        _router = new IBCallbackRouter(depthCollector, tradeCollector);
        _conn = new EnhancedIBConnectionManager(_router, host: "127.0.0.1", port: 7497, clientId: 1);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _conn.ConnectAsync().ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _conn.DisconnectAsync().ConfigureAwait(false);
    }

    public int SubscribeMarketDepth(SymbolConfig cfg)
        => _conn.SubscribeMarketDepth(cfg);

    public void UnsubscribeMarketDepth(int subscriptionId)
        => _conn.UnsubscribeMarketDepth(subscriptionId);

    public int SubscribeTrades(SymbolConfig cfg)
        => _conn.SubscribeTrades(cfg);

    public void UnsubscribeTrades(int subscriptionId)
        => _conn.UnsubscribeTrades(subscriptionId);

    public ValueTask DisposeAsync()
    {
        try { _conn.Dispose(); } catch { }
        return ValueTask.CompletedTask;
    }
}
#endif
