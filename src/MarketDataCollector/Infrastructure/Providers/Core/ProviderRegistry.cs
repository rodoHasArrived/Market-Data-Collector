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
/// </remarks>
[ImplementsAdr("ADR-001", "Centralized provider registry for plugin-style management")]
public sealed class ProviderRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, RegisteredStreamingProvider> _streamingProviders = new();
    private readonly ConcurrentDictionary<string, RegisteredBackfillProvider> _backfillProviders = new();
    private readonly ConcurrentDictionary<string, RegisteredSymbolSearchProvider> _symbolSearchProviders = new();
    private readonly ILogger _log;
    private readonly IAlertDispatcher? _alertDispatcher;
    private bool _disposed;

    public ProviderRegistry(IAlertDispatcher? alertDispatcher = null, ILogger? log = null)
    {
        _alertDispatcher = alertDispatcher;
        _log = log ?? LoggingSetup.ForContext<ProviderRegistry>();
    }

    #region Streaming Providers

    /// <summary>
    /// Registers a streaming market data provider.
    /// </summary>
    public void RegisterStreaming(string name, IMarketDataClient provider, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateName(name);

        var registered = new RegisteredStreamingProvider(name, provider, priority, true);
        if (_streamingProviders.TryAdd(name, registered))
        {
            _log.Information("Registered streaming provider: {Name} (priority: {Priority})", name, priority);
        }
        else
        {
            _log.Warning("Streaming provider already registered: {Name}", name);
        }
    }

    /// <summary>
    /// Gets a streaming provider by name.
    /// </summary>
    public IMarketDataClient? GetStreamingProvider(string name)
    {
        return _streamingProviders.TryGetValue(name, out var registered) && registered.IsEnabled
            ? registered.Provider
            : null;
    }

    /// <summary>
    /// Gets all registered streaming providers ordered by priority.
    /// </summary>
    public IReadOnlyList<IMarketDataClient> GetStreamingProviders()
    {
        return _streamingProviders.Values
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .Select(r => r.Provider)
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
        ValidateName(name);

        var registered = new RegisteredBackfillProvider(name, provider, priority, true);
        if (_backfillProviders.TryAdd(name, registered))
        {
            _log.Information("Registered backfill provider: {Name} (priority: {Priority})", name, priority);
        }
        else
        {
            _log.Warning("Backfill provider already registered: {Name}", name);
        }
    }

    /// <summary>
    /// Gets a backfill provider by name.
    /// </summary>
    public IHistoricalDataProvider? GetBackfillProvider(string name)
    {
        return _backfillProviders.TryGetValue(name, out var registered) && registered.IsEnabled
            ? registered.Provider
            : null;
    }

    /// <summary>
    /// Gets all registered backfill providers ordered by priority.
    /// </summary>
    public IReadOnlyList<IHistoricalDataProvider> GetBackfillProviders()
    {
        return _backfillProviders.Values
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .Select(r => r.Provider)
            .ToList();
    }

    /// <summary>
    /// Gets the best available backfill provider based on priority and health.
    /// </summary>
    public async Task<IHistoricalDataProvider?> GetBestBackfillProviderAsync(CancellationToken ct = default)
    {
        foreach (var registered in _backfillProviders.Values.Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            try
            {
                if (await registered.Provider.IsAvailableAsync(ct).ConfigureAwait(false))
                {
                    return registered.Provider;
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
        ValidateName(name);

        var registered = new RegisteredSymbolSearchProvider(name, provider, priority, true);
        if (_symbolSearchProviders.TryAdd(name, registered))
        {
            _log.Information("Registered symbol search provider: {Name} (priority: {Priority})", name, priority);
        }
        else
        {
            _log.Warning("Symbol search provider already registered: {Name}", name);
        }
    }

    /// <summary>
    /// Gets a symbol search provider by name.
    /// </summary>
    public ISymbolSearchProvider? GetSymbolSearchProvider(string name)
    {
        return _symbolSearchProviders.TryGetValue(name, out var registered) && registered.IsEnabled
            ? registered.Provider
            : null;
    }

    /// <summary>
    /// Gets all registered symbol search providers ordered by priority.
    /// </summary>
    public IReadOnlyList<ISymbolSearchProvider> GetSymbolSearchProviders()
    {
        return _symbolSearchProviders.Values
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .Select(r => r.Provider)
            .ToList();
    }

    /// <summary>
    /// Gets the best available symbol search provider based on priority and health.
    /// </summary>
    public async Task<ISymbolSearchProvider?> GetBestSymbolSearchProviderAsync(CancellationToken ct = default)
    {
        foreach (var registered in _symbolSearchProviders.Values.Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            try
            {
                if (await registered.Provider.IsAvailableAsync(ct).ConfigureAwait(false))
                {
                    return registered.Provider;
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
        if (_streamingProviders.TryGetValue(name, out var streaming))
        {
            _streamingProviders[name] = streaming with { IsEnabled = true };
            _log.Information("Enabled streaming provider: {Name}", name);
            return;
        }

        if (_backfillProviders.TryGetValue(name, out var backfill))
        {
            _backfillProviders[name] = backfill with { IsEnabled = true };
            _log.Information("Enabled backfill provider: {Name}", name);
            return;
        }

        if (_symbolSearchProviders.TryGetValue(name, out var search))
        {
            _symbolSearchProviders[name] = search with { IsEnabled = true };
            _log.Information("Enabled symbol search provider: {Name}", name);
            return;
        }

        _log.Warning("Provider not found: {Name}", name);
    }

    /// <summary>
    /// Disables a provider.
    /// </summary>
    public void Disable(string name)
    {
        if (_streamingProviders.TryGetValue(name, out var streaming))
        {
            _streamingProviders[name] = streaming with { IsEnabled = false };
            _log.Information("Disabled streaming provider: {Name}", name);

            _alertDispatcher?.Publish(MonitoringAlert.Warning(
                "ProviderRegistry",
                AlertCategory.Provider,
                $"Provider Disabled: {name}",
                $"Streaming provider {name} has been disabled"));
            return;
        }

        if (_backfillProviders.TryGetValue(name, out var backfill))
        {
            _backfillProviders[name] = backfill with { IsEnabled = false };
            _log.Information("Disabled backfill provider: {Name}", name);
            return;
        }

        if (_symbolSearchProviders.TryGetValue(name, out var search))
        {
            _symbolSearchProviders[name] = search with { IsEnabled = false };
            _log.Information("Disabled symbol search provider: {Name}", name);
            return;
        }

        _log.Warning("Provider not found: {Name}", name);
    }

    /// <summary>
    /// Gets information about all registered providers.
    /// </summary>
    public IReadOnlyList<ProviderInfo> GetAllProviders()
    {
        var result = new List<ProviderInfo>();

        foreach (var p in _streamingProviders.Values)
        {
            result.Add(new ProviderInfo(p.Name, p.Name, ProviderType.Streaming, p.Priority, p.IsEnabled));
        }

        foreach (var p in _backfillProviders.Values)
        {
            result.Add(new ProviderInfo(p.Name, p.Provider.DisplayName, ProviderType.Backfill, p.Priority, p.IsEnabled,
                new Dictionary<string, object>
                {
                    ["SupportsAdjustedPrices"] = p.Provider.Capabilities.SupportsAdjustedPrices,
                    ["SupportsIntraday"] = p.Provider.Capabilities.SupportsIntraday
                }));
        }

        foreach (var p in _symbolSearchProviders.Values)
        {
            result.Add(new ProviderInfo(p.Name, p.Provider.DisplayName, ProviderType.SymbolSearch, p.Priority, p.IsEnabled));
        }

        return result;
    }

    /// <summary>
    /// Gets a summary of registered provider counts.
    /// </summary>
    public ProviderRegistrySummary GetSummary()
    {
        return new ProviderRegistrySummary(
            StreamingCount: _streamingProviders.Count,
            BackfillCount: _backfillProviders.Count,
            SymbolSearchCount: _symbolSearchProviders.Count,
            TotalEnabled: _streamingProviders.Values.Count(p => p.IsEnabled) +
                         _backfillProviders.Values.Count(p => p.IsEnabled) +
                         _symbolSearchProviders.Values.Count(p => p.IsEnabled));
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

        // Dispose streaming providers
        foreach (var p in _streamingProviders.Values)
        {
            try { _ = p.Provider.DisposeAsync().AsTask(); }
            catch { /* ignore */ }
        }
        _streamingProviders.Clear();

        // Dispose backfill providers
        foreach (var p in _backfillProviders.Values)
        {
            try { p.Provider.Dispose(); }
            catch { /* ignore */ }
        }
        _backfillProviders.Clear();

        // Dispose symbol search providers
        foreach (var p in _symbolSearchProviders.Values)
        {
            try { (p.Provider as IDisposable)?.Dispose(); }
            catch { /* ignore */ }
        }
        _symbolSearchProviders.Clear();
    }

    private sealed record RegisteredStreamingProvider(string Name, IMarketDataClient Provider, int Priority, bool IsEnabled);
    private sealed record RegisteredBackfillProvider(string Name, IHistoricalDataProvider Provider, int Priority, bool IsEnabled);
    private sealed record RegisteredSymbolSearchProvider(string Name, ISymbolSearchProvider Provider, int Priority, bool IsEnabled);
}

/// <summary>
/// Summary of registered providers.
/// </summary>
/// <param name="StreamingCount">Number of streaming providers.</param>
/// <param name="BackfillCount">Number of backfill providers.</param>
/// <param name="SymbolSearchCount">Number of symbol search providers.</param>
/// <param name="TotalEnabled">Total number of enabled providers.</param>
public sealed record ProviderRegistrySummary(int StreamingCount, int BackfillCount, int SymbolSearchCount, int TotalEnabled);
