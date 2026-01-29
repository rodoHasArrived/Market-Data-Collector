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
/// <remarks>
/// The <see cref="FromMetadata"/> method is the preferred unified approach for creating
/// templates from any provider implementing <see cref="IProviderMetadata"/>.
/// The type-specific methods are kept for backwards compatibility.
/// </remarks>
public static class ProviderTemplateFactory
{
    /// <summary>
    /// Creates a provider template from any provider implementing <see cref="IProviderMetadata"/>.
    /// This is the preferred unified approach that eliminates special-case logic.
    /// </summary>
    /// <param name="provider">The provider implementing IProviderMetadata.</param>
    /// <param name="isEnabled">Whether the provider is currently enabled.</param>
    /// <param name="priorityOverride">Optional priority override (uses provider's priority if null).</param>
    /// <returns>A normalized ProviderTemplate for UI/monitoring consumption.</returns>
    public static ProviderTemplate FromMetadata(IProviderMetadata provider, bool isEnabled, int? priorityOverride = null)
    {
        var caps = provider.ProviderCapabilities;
        var priority = priorityOverride ?? provider.ProviderPriority;

        ProviderRateLimitProfile? rateLimit = null;
        if (caps.MaxRequestsPerWindow.HasValue && caps.RateLimitWindow.HasValue)
        {
            rateLimit = new ProviderRateLimitProfile(
                caps.MaxRequestsPerWindow.Value,
                caps.RateLimitWindow.Value,
                caps.MinRequestDelay);
        }

        return new ProviderTemplate(
            Name: provider.ProviderId,
            DisplayName: provider.ProviderDisplayName,
            ProviderType: caps.PrimaryType,
            Priority: priority,
            IsEnabled: isEnabled,
            Capabilities: caps.ToDictionary(),
            RateLimit: rateLimit);
    }

    /// <summary>
    /// Creates a provider template for a streaming provider.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="FromMetadata"/> for unified handling.
    /// Kept for backwards compatibility.
    /// </remarks>
    public static ProviderTemplate ForStreaming(string name, IMarketDataClient provider, int priority, bool isEnabled)
    {
        // Use unified metadata path
        return FromMetadata(provider, isEnabled, priority);
    }

    /// <summary>
    /// Creates a provider template for a backfill provider.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="FromMetadata"/> for unified handling.
    /// Kept for backwards compatibility.
    /// </remarks>
    public static ProviderTemplate ForBackfill(string name, IHistoricalDataProvider provider, int priority, bool isEnabled)
    {
        // Use unified metadata path
        return FromMetadata(provider, isEnabled, priority);
    }

    /// <summary>
    /// Creates a provider template for a symbol search provider.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="FromMetadata"/> for unified handling.
    /// Kept for backwards compatibility.
    /// </remarks>
    public static ProviderTemplate ForSymbolSearch(ISymbolSearchProvider provider, int priority, bool isEnabled)
    {
        // Use unified metadata path
        return FromMetadata(provider, isEnabled, priority);
    }
}
