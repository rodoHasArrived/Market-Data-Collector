using IBDataCollector.Application.Config;

namespace IBDataCollector.Infrastructure.IB;

/// <summary>
/// Runtime no-op implementation used when IBApi isn't available or you don't want to connect.
/// Keeps Program logic identical in all builds.
/// </summary>
public sealed class NoOpIBClient : IIBMarketDataClient
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
