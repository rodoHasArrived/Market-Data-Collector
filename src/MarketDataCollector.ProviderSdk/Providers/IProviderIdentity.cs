namespace MarketDataCollector.ProviderSdk.Providers;

/// <summary>
/// Base identity contract for all provider types in the plugin system.
/// Every provider must declare who it is, what it does, and what it supports.
/// </summary>
public interface IProviderIdentity
{
    /// <summary>
    /// Unique identifier for the provider (e.g., "alpaca", "stooq", "polygon").
    /// Must be stable across versions and unique across all loaded plugins.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable display name for UI presentation (e.g., "Alpaca Markets").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description of the provider's capabilities and data coverage.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Priority for routing and failover (lower = higher priority, tried first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Consolidated capability flags and metadata for this provider.
    /// </summary>
    ProviderCapabilities Capabilities { get; }
}
