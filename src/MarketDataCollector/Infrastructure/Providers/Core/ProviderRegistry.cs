using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring.Core;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.SymbolSearch;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Core;

/// <summary>
/// Provider capability metadata for discovery and routing.
/// </summary>
/// <param name="Name">Unique provider identifier.</param>
/// <param name="DisplayName">Human-readable provider name.</param>
/// <param name="ProviderType">Type of provider (streaming, backfill, search).</param>
/// <param name="Priority">Priority for routing (lower = higher priority).</param>
/// <param name="IsEnabled">Whether the provider is currently enabled.</param>
/// <param name="Capabilities">Provider-specific capabilities.</param>
public sealed record ProviderInfo(
    string Name,
    string DisplayName,
    ProviderType ProviderType,
    int Priority,
    bool IsEnabled,
    IReadOnlyDictionary<string, object>? Capabilities = null);

/// <summary>
/// Types of market data providers.
/// </summary>
public enum ProviderType
{
    /// <summary>Real-time streaming data provider.</summary>
    Streaming,

    /// <summary>Historical backfill data provider.</summary>
    Backfill,

    /// <summary>Symbol search/lookup provider.</summary>
    SymbolSearch
}

/// <summary>
/// Centralized registry for all market data providers enabling plugin-style
/// provider management with discovery, routing, and health monitoring.
/// </summary>
/// <remarks>
/// The provider registry provides:
/// - Centralized provider registration and discovery
/// - Priority-based provider routing
/// - Provider health monitoring and automatic failover
/// - Capability-based provider selection
/// - Unified metadata access via <see cref="IProviderMetadata"/>
/// </remarks>
[ImplementsAdr("ADR-001", "Centralized provider registry for plugin-style management")]
public sealed class ProviderRegistry : IDisposable
{
    /// <summary>
    /// Single unified registry of all providers. Type-specific queries filter by ProviderCapabilities.
    /// </summary>
    private readonly ConcurrentDictionary<string, RegisteredProvider> _allProviders = new();
    private readonly ILogger _log;
    private readonly IAlertDispatcher? _alertDispatcher;
    private bool _disposed;

    public ProviderRegistry(IAlertDispatcher? alertDispatcher = null, ILogger? log = null)
    {
        _alertDispatcher = alertDispatcher;
        _log = log ?? LoggingSetup.ForContext<ProviderRegistry>();
    }

    #region Unified Provider Registration

    /// <summary>
    /// Registers any provider implementing <see cref="IProviderMetadata"/>.
    /// All providers are stored in a single unified registry.
    /// </summary>
    /// <typeparam name="T">The provider type.</typeparam>
    /// <param name="provider">The provider instance.</param>
    /// <param name="priorityOverride">Optional priority override.</param>
    public void Register<T>(T provider, int? priorityOverride = null) where T : IProviderMetadata
    {
        ArgumentNullException.ThrowIfNull(provider);

        var id = provider.ProviderId;
        var priority = priorityOverride ?? provider.ProviderPriority;
        ValidateName(id);

        var registered = new RegisteredProvider(id, provider, priority, true);
        if (_allProviders.TryAdd(id, registered))
        {
            _log.Information("Registered provider: {Name} (type: {Type}, priority: {Priority})",
                id, provider.ProviderCapabilities.PrimaryType, priority);
        }
        else
        {
            _log.Warning("Provider already registered: {Name}", id);
        }
    }

    /// <summary>
    /// Gets all registered providers as unified metadata.
    /// </summary>
    public IReadOnlyList<IProviderMetadata> GetAllProviderMetadata()
    {
        return _allProviders.Values
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .Select(r => r.Provider)
            .ToList();
    }

    /// <summary>
    /// Gets a provider by ID from the unified registry.
    /// </summary>
    public IProviderMetadata? GetProvider(string id)
    {
        return _allProviders.TryGetValue(id, out var registered) && registered.IsEnabled
            ? registered.Provider
            : null;
    }

    /// <summary>
    /// Gets providers filtered by capability.
    /// </summary>
    /// <param name="predicate">Capability filter predicate.</param>
    public IReadOnlyList<IProviderMetadata> GetProvidersByCapability(Func<ProviderCapabilities, bool> predicate)
    {
        return _allProviders.Values
            .Where(r => r.IsEnabled && predicate(r.Provider.ProviderCapabilities))
            .OrderBy(r => r.Priority)
            .Select(r => r.Provider)
            .ToList();
    }

    #endregion

    #region Streaming Providers

    /// <summary>
    /// Registers a streaming market data provider.
    /// </summary>
    public void RegisterStreaming(string name, IMarketDataClient provider, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Register(provider, priority);
    }

    /// <summary>
    /// Gets a streaming provider by name.
    /// </summary>
    public IMarketDataClient? GetStreamingProvider(string name)
    {
        return _allProviders.TryGetValue(name, out var registered) &&
               registered.IsEnabled &&
               registered.Provider is IMarketDataClient client
            ? client
            : null;
    }

    /// <summary>
    /// Gets all registered streaming providers ordered by priority.
    /// </summary>
    public IReadOnlyList<IMarketDataClient> GetStreamingProviders()
    {
        return _allProviders.Values
            .Where(r => r.IsEnabled && r.Provider is IMarketDataClient)
            .OrderBy(r => r.Priority)
            .Select(r => (IMarketDataClient)r.Provider)
            .ToList();
    }

    #endregion

    #region Backfill Providers

    /// <summary>
    /// Registers a historical data provider.
    /// </summary>
    public void RegisterBackfill(string name, IHistoricalDataProvider provider, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Register(provider, priority);
    }

    /// <summary>
    /// Gets a backfill provider by name.
    /// </summary>
    public IHistoricalDataProvider? GetBackfillProvider(string name)
    {
        return _allProviders.TryGetValue(name, out var registered) &&
               registered.IsEnabled &&
               registered.Provider is IHistoricalDataProvider provider
            ? provider
            : null;
    }

    /// <summary>
    /// Gets all registered backfill providers ordered by priority.
    /// </summary>
    public IReadOnlyList<IHistoricalDataProvider> GetBackfillProviders()
    {
        return _allProviders.Values
            .Where(r => r.IsEnabled && r.Provider is IHistoricalDataProvider)
            .OrderBy(r => r.Priority)
            .Select(r => (IHistoricalDataProvider)r.Provider)
            .ToList();
    }

    /// <summary>
    /// Gets the best available backfill provider based on priority and health.
    /// </summary>
    public async Task<IHistoricalDataProvider?> GetBestBackfillProviderAsync(CancellationToken ct = default)
    {
        var backfillProviders = _allProviders.Values
            .Where(r => r.IsEnabled && r.Provider is IHistoricalDataProvider)
            .OrderBy(r => r.Priority);

        foreach (var registered in backfillProviders)
        {
            var provider = (IHistoricalDataProvider)registered.Provider;
            try
            {
                if (await provider.IsAvailableAsync(ct).ConfigureAwait(false))
                {
                    return provider;
                }
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Backfill provider {Name} availability check failed", registered.Name);
            }
        }
        return null;
    }

    #endregion

    #region Symbol Search Providers

    /// <summary>
    /// Registers a symbol search provider.
    /// </summary>
    public void RegisterSymbolSearch(string name, ISymbolSearchProvider provider, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Register(provider, priority);
    }

    /// <summary>
    /// Gets a symbol search provider by name.
    /// </summary>
    public ISymbolSearchProvider? GetSymbolSearchProvider(string name)
    {
        return _allProviders.TryGetValue(name, out var registered) &&
               registered.IsEnabled &&
               registered.Provider is ISymbolSearchProvider provider
            ? provider
            : null;
    }

    /// <summary>
    /// Gets all registered symbol search providers ordered by priority.
    /// </summary>
    public IReadOnlyList<ISymbolSearchProvider> GetSymbolSearchProviders()
    {
        return _allProviders.Values
            .Where(r => r.IsEnabled && r.Provider is ISymbolSearchProvider)
            .OrderBy(r => r.Priority)
            .Select(r => (ISymbolSearchProvider)r.Provider)
            .ToList();
    }

    /// <summary>
    /// Gets the best available symbol search provider based on priority and health.
    /// </summary>
    public async Task<ISymbolSearchProvider?> GetBestSymbolSearchProviderAsync(CancellationToken ct = default)
    {
        var searchProviders = _allProviders.Values
            .Where(r => r.IsEnabled && r.Provider is ISymbolSearchProvider)
            .OrderBy(r => r.Priority);

        foreach (var registered in searchProviders)
        {
            var provider = (ISymbolSearchProvider)registered.Provider;
            try
            {
                if (await provider.IsAvailableAsync(ct).ConfigureAwait(false))
                {
                    return provider;
                }
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Symbol search provider {Name} availability check failed", registered.Name);
            }
        }
        return null;
    }

    #endregion

    #region Provider Management

    /// <summary>
    /// Enables a provider.
    /// </summary>
    public void Enable(string name)
    {
        if (_allProviders.TryGetValue(name, out var registered))
        {
            _allProviders[name] = registered with { IsEnabled = true };
            _log.Information("Enabled provider: {Name} (type: {Type})",
                name, registered.Provider.ProviderCapabilities.PrimaryType);
        }
        else
        {
            _log.Warning("Provider not found: {Name}", name);
        }
    }

    /// <summary>
    /// Disables a provider.
    /// </summary>
    public void Disable(string name)
    {
        if (_allProviders.TryGetValue(name, out var registered))
        {
            _allProviders[name] = registered with { IsEnabled = false };
            _log.Information("Disabled provider: {Name} (type: {Type})",
                name, registered.Provider.ProviderCapabilities.PrimaryType);

            if (registered.Provider is IMarketDataClient)
            {
                _alertDispatcher?.Publish(MonitoringAlert.Warning(
                    "ProviderRegistry",
                    AlertCategory.Provider,
                    $"Provider Disabled: {name}",
                    $"Streaming provider {name} has been disabled"));
            }
        }
        else
        {
            _log.Warning("Provider not found: {Name}", name);
        }
    }

    /// <summary>
    /// Gets information about all registered providers using standardized metadata.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ProviderTemplateFactory.FromMetadata"/> via the unified
    /// <see cref="_allProviders"/> dictionary to ensure consistent template output
    /// across all provider types without type-specific branching.
    /// </remarks>
    public IReadOnlyList<ProviderInfo> GetAllProviders()
    {
        return _allProviders.Values
            .Select(p => ProviderTemplateFactory.FromMetadata(p.Provider, p.IsEnabled, p.Priority).ToInfo())
            .ToList();
    }

    /// <summary>
    /// Generates catalog entries from all registered providers using <see cref="ProviderTemplateFactory.ToCatalogEntry"/>.
    /// This replaces static hardcoded catalog data with runtime-derived metadata.
    /// </summary>
    /// <returns>A list of <see cref="Contracts.Api.ProviderCatalogEntry"/> objects for UI consumption.</returns>
    public IReadOnlyList<Contracts.Api.ProviderCatalogEntry> GetProviderCatalog()
    {
        return _allProviders.Values
            .Select(p => ProviderTemplateFactory.ToCatalogEntry(p.Provider))
            .ToList();
    }

    /// <summary>
    /// Generates catalog entries for providers of a specific type.
    /// </summary>
    /// <param name="type">The provider type to filter by.</param>
    /// <returns>A list of <see cref="Contracts.Api.ProviderCatalogEntry"/> objects for UI consumption.</returns>
    public IReadOnlyList<Contracts.Api.ProviderCatalogEntry> GetProviderCatalogByType(ProviderType type)
    {
        return _allProviders.Values
            .Where(p => p.Provider.ProviderCapabilities.PrimaryType == type ||
                        (type == ProviderType.Streaming && p.Provider.ProviderCapabilities.SupportsStreaming) ||
                        (type == ProviderType.Backfill && p.Provider.ProviderCapabilities.SupportsBackfill))
            .Select(p => ProviderTemplateFactory.ToCatalogEntry(p.Provider))
            .ToList();
    }

    /// <summary>
    /// Gets a catalog entry for a specific provider by ID.
    /// </summary>
    /// <param name="providerId">The provider ID to look up.</param>
    /// <returns>The catalog entry, or null if not found.</returns>
    public Contracts.Api.ProviderCatalogEntry? GetProviderCatalogEntry(string providerId)
    {
        return _allProviders.TryGetValue(providerId, out var registered)
            ? ProviderTemplateFactory.ToCatalogEntry(registered.Provider)
            : null;
    }

    /// <summary>
    /// Gets a summary of registered provider counts.
    /// </summary>
    public ProviderRegistrySummary GetSummary()
    {
        var providers = _allProviders.Values.ToList();
        return new ProviderRegistrySummary(
            StreamingCount: providers.Count(p => p.Provider is IMarketDataClient),
            BackfillCount: providers.Count(p => p.Provider is IHistoricalDataProvider),
            SymbolSearchCount: providers.Count(p => p.Provider is ISymbolSearchProvider),
            TotalEnabled: providers.Count(p => p.IsEnabled));
    }

    #endregion

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Provider name is required", nameof(name));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose all providers based on their capabilities
        foreach (var registered in _allProviders.Values)
        {
            try
            {
                switch (registered.Provider)
                {
                    case IAsyncDisposable asyncDisposable:
                        _ = asyncDisposable.DisposeAsync().AsTask();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch { /* ignore disposal errors */ }
        }

        _allProviders.Clear();
    }

    /// <summary>
    /// Internal record for tracking registered providers in the unified registry.
    /// </summary>
    private sealed record RegisteredProvider(string Name, IProviderMetadata Provider, int Priority, bool IsEnabled);
}

/// <summary>
/// Summary of registered providers.
/// </summary>
/// <param name="StreamingCount">Number of streaming providers.</param>
/// <param name="BackfillCount">Number of backfill providers.</param>
/// <param name="SymbolSearchCount">Number of symbol search providers.</param>
/// <param name="TotalEnabled">Total number of enabled providers.</param>
public sealed record ProviderRegistrySummary(int StreamingCount, int BackfillCount, int SymbolSearchCount, int TotalEnabled);
