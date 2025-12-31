using MarketDataCollector.Application.Config;

namespace MarketDataCollector.Infrastructure;

/// <summary>
/// Runtime no-op implementation used when no data provider is configured or available.
/// Keeps Program logic identical in all builds.
/// </summary>
public sealed class NoOpMarketDataClient : IMarketDataClient
{
    public bool IsEnabled => false;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public int SubscribeMarketDepth(SymbolConfig cfg) => -1;
    public void UnsubscribeMarketDepth(int subscriptionId) { }

    public int SubscribeTrades(SymbolConfig cfg) => -1;
    public void UnsubscribeTrades(int subscriptionId) { }
}
