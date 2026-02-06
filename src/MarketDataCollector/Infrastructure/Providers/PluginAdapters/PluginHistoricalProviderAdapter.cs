using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.ProviderSdk.Providers;

namespace MarketDataCollector.Infrastructure.Providers.PluginAdapters;

/// <summary>
/// Adapts an SDK <see cref="IHistoricalProvider"/> to the internal
/// <see cref="IHistoricalDataProvider"/> interface, allowing plugin providers
/// to be registered in the core ProviderRegistry.
/// </summary>
[ImplementsAdr("ADR-001", "Adapter bridging plugin historical providers to core registry")]
public sealed class PluginHistoricalProviderAdapter : IHistoricalDataProvider
{
    private readonly IHistoricalProvider _inner;

    public PluginHistoricalProviderAdapter(IHistoricalProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Name => _inner.ProviderId;
    public string DisplayName => _inner.DisplayName;
    public string Description => _inner.Description;
    public int Priority => _inner.Priority;

    public TimeSpan RateLimitDelay => _inner.RateLimitDelay;
    public int MaxRequestsPerWindow => _inner.MaxRequestsPerWindow;
    public TimeSpan RateLimitWindow => _inner.RateLimitWindow;

    public HistoricalDataCapabilities Capabilities => new()
    {
        AdjustedPrices = _inner.Capabilities.SupportsAdjustedPrices,
        Intraday = _inner.Capabilities.SupportsIntraday,
        Dividends = _inner.Capabilities.SupportsDividends,
        Splits = _inner.Capabilities.SupportsSplits,
        Quotes = _inner.Capabilities.SupportsHistoricalQuotes,
        Trades = _inner.Capabilities.SupportsHistoricalTrades,
        Auctions = _inner.Capabilities.SupportsHistoricalAuctions,
        SupportedMarkets = _inner.Capabilities.SupportedMarkets
    };

    // IProviderMetadata bridging
    public string ProviderId => _inner.ProviderId;
    public string ProviderDisplayName => _inner.DisplayName;
    public string ProviderDescription => _inner.Description;
    public int ProviderPriority => _inner.Priority;

    public Core.ProviderCapabilities ProviderCapabilities =>
        Core.ProviderCapabilities.FromHistoricalCapabilities(
            Capabilities,
            MaxRequestsPerWindow == int.MaxValue ? null : MaxRequestsPerWindow,
            RateLimitWindow,
            RateLimitDelay == TimeSpan.Zero ? null : RateLimitDelay);

    public Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
        => _inner.GetDailyBarsAsync(symbol, from, to, ct);

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => _inner.IsAvailableAsync(ct);

    public void Dispose()
    {
        _inner.Dispose();
    }
}
