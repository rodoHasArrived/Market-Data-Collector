using MarketDataCollector.Application.Config;
using System.Threading;

namespace MarketDataCollector.Infrastructure;

/// <summary>
/// Market data client abstraction for provider-agnostic market data ingestion.
/// Implementations must be non-blocking on publish paths.
/// </summary>
public interface IMarketDataClient : IAsyncDisposable
{
    bool IsEnabled { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Subscribe to market depth for the symbol described by cfg.</summary>
    int SubscribeMarketDepth(SymbolConfig cfg);

    /// <summary>Unsubscribe a previously returned depth subscription id.</summary>
    void UnsubscribeMarketDepth(int subscriptionId);

    /// <summary>Subscribe to tick-by-tick trade prints for the symbol described by cfg.</summary>
    int SubscribeTrades(SymbolConfig cfg);

    /// <summary>Unsubscribe a previously returned trade subscription id.</summary>
    void UnsubscribeTrades(int subscriptionId);
}
