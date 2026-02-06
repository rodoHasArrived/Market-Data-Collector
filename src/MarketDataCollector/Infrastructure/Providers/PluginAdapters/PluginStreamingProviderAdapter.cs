using MarketDataCollector.Application.Config;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.ProviderSdk.Providers;

namespace MarketDataCollector.Infrastructure.Providers.PluginAdapters;

/// <summary>
/// Adapts an SDK <see cref="IStreamingProvider"/> to the internal
/// <see cref="IMarketDataClient"/> interface, allowing plugin streaming providers
/// to be registered in the core ProviderRegistry.
/// </summary>
[ImplementsAdr("ADR-001", "Adapter bridging plugin streaming providers to core registry")]
public sealed class PluginStreamingProviderAdapter : IMarketDataClient
{
    private readonly IStreamingProvider _inner;

    public PluginStreamingProviderAdapter(IStreamingProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool IsEnabled => _inner.IsEnabled;

    // IProviderMetadata bridging
    public string ProviderId => _inner.ProviderId;
    public string ProviderDisplayName => _inner.DisplayName;
    public string ProviderDescription => _inner.Description;
    public int ProviderPriority => _inner.Priority;

    public Core.ProviderCapabilities ProviderCapabilities => new()
    {
        SupportsStreaming = _inner.Capabilities.SupportsStreaming,
        SupportsRealtimeTrades = _inner.Capabilities.SupportsRealtimeTrades,
        SupportsRealtimeQuotes = _inner.Capabilities.SupportsRealtimeQuotes,
        SupportsMarketDepth = _inner.Capabilities.SupportsMarketDepth,
        MaxDepthLevels = _inner.Capabilities.MaxDepthLevels,
        SupportedMarkets = _inner.Capabilities.SupportedMarkets
    };

    public Task ConnectAsync(CancellationToken ct = default)
        => _inner.ConnectAsync(ct);

    public Task DisconnectAsync(CancellationToken ct = default)
        => _inner.DisconnectAsync(ct);

    public int SubscribeMarketDepth(SymbolConfig cfg)
    {
        var subscription = new SymbolSubscription(
            Symbol: cfg.Symbol,
            SubscribeTrades: cfg.SubscribeTrades,
            SubscribeDepth: cfg.SubscribeDepth,
            DepthLevels: cfg.DepthLevels,
            SecurityType: cfg.SecurityType,
            Exchange: cfg.Exchange,
            Currency: cfg.Currency,
            PrimaryExchange: cfg.PrimaryExchange);

        return _inner.SubscribeMarketDepth(subscription);
    }

    public void UnsubscribeMarketDepth(int subscriptionId)
        => _inner.UnsubscribeMarketDepth(subscriptionId);

    public int SubscribeTrades(SymbolConfig cfg)
    {
        var subscription = new SymbolSubscription(
            Symbol: cfg.Symbol,
            SubscribeTrades: cfg.SubscribeTrades,
            SubscribeDepth: cfg.SubscribeDepth,
            DepthLevels: cfg.DepthLevels,
            SecurityType: cfg.SecurityType,
            Exchange: cfg.Exchange,
            Currency: cfg.Currency,
            PrimaryExchange: cfg.PrimaryExchange);

        return _inner.SubscribeTrades(subscription);
    }

    public void UnsubscribeTrades(int subscriptionId)
        => _inner.UnsubscribeTrades(subscriptionId);

    public ValueTask DisposeAsync()
        => _inner.DisposeAsync();
}
