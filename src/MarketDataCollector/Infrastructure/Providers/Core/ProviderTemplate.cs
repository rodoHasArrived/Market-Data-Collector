using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.SymbolSearch;

namespace MarketDataCollector.Infrastructure.Providers.Core;

/// <summary>
/// Standardized provider metadata template for registry and UI surfaces.
/// </summary>
public sealed record ProviderTemplate(
    string Name,
    string DisplayName,
    ProviderType ProviderType,
    int Priority,
    bool IsEnabled,
    IReadOnlyDictionary<string, object> Capabilities,
    ProviderRateLimitProfile? RateLimit = null)
{
    public ProviderInfo ToInfo()
    {
        var capabilities = new Dictionary<string, object>(Capabilities);

        if (RateLimit is not null)
        {
            capabilities["MaxRequestsPerWindow"] = RateLimit.MaxRequestsPerWindow;
            capabilities["RateLimitWindowSeconds"] = RateLimit.Window.TotalSeconds;
            if (RateLimit.MinDelay.HasValue)
            {
                capabilities["RateLimitMinDelayMs"] = RateLimit.MinDelay.Value.TotalMilliseconds;
            }
        }

        return new ProviderInfo(Name, DisplayName, ProviderType, Priority, IsEnabled, capabilities);
    }
}

/// <summary>
/// Standardized rate limit profile for provider metadata.
/// </summary>
public sealed record ProviderRateLimitProfile(
    int MaxRequestsPerWindow,
    TimeSpan Window,
    TimeSpan? MinDelay = null);

/// <summary>
/// Factory for consistent provider templates across streaming, backfill, and search providers.
/// </summary>
public static class ProviderTemplateFactory
{
    public static ProviderTemplate ForStreaming(string name, IMarketDataClient provider, int priority, bool isEnabled)
    {
        var capabilities = new Dictionary<string, object>
        {
            ["SupportsStreaming"] = true,
            ["SupportsBackfill"] = false,
            ["SupportsSymbolSearch"] = false
        };

        return new ProviderTemplate(
            Name: name,
            DisplayName: name,
            ProviderType: ProviderType.Streaming,
            Priority: priority,
            IsEnabled: isEnabled,
            Capabilities: capabilities);
    }

    public static ProviderTemplate ForBackfill(string name, IHistoricalDataProvider provider, int priority, bool isEnabled)
    {
        var caps = provider.Capabilities;
        var capabilities = new Dictionary<string, object>
        {
            ["SupportsAdjustedPrices"] = caps.AdjustedPrices,
            ["SupportsIntraday"] = caps.Intraday,
            ["SupportsDividends"] = caps.Dividends,
            ["SupportsSplits"] = caps.Splits,
            ["SupportsQuotes"] = caps.Quotes,
            ["SupportsTrades"] = caps.Trades,
            ["SupportsAuctions"] = caps.Auctions,
            ["SupportedMarkets"] = caps.SupportedMarkets
        };

        var rateLimit = new ProviderRateLimitProfile(
            provider.MaxRequestsPerWindow,
            provider.RateLimitWindow,
            provider.RateLimitDelay == TimeSpan.Zero ? null : provider.RateLimitDelay);

        return new ProviderTemplate(
            Name: name,
            DisplayName: provider.DisplayName,
            ProviderType: ProviderType.Backfill,
            Priority: priority,
            IsEnabled: isEnabled,
            Capabilities: capabilities,
            RateLimit: rateLimit);
    }

    public static ProviderTemplate ForSymbolSearch(ISymbolSearchProvider provider, int priority, bool isEnabled)
    {
        var capabilities = new Dictionary<string, object>
        {
            ["SupportsSymbolSearch"] = true,
            ["SupportsStreaming"] = false,
            ["SupportsBackfill"] = false
        };

        if (provider is IFilterableSymbolSearchProvider filterable)
        {
            capabilities["SupportedAssetTypes"] = filterable.SupportedAssetTypes;
            capabilities["SupportedExchanges"] = filterable.SupportedExchanges;
        }

        return new ProviderTemplate(
            Name: provider.Name,
            DisplayName: provider.DisplayName,
            ProviderType: ProviderType.SymbolSearch,
            Priority: priority,
            IsEnabled: isEnabled,
            Capabilities: capabilities);
    }
}
