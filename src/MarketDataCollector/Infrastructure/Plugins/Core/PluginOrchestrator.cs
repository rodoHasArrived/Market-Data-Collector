using System.Runtime.CompilerServices;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using MarketDataCollector.Infrastructure.Plugins.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataCollector.Infrastructure.Plugins.Core;

/// <summary>
/// Orchestrates multiple plugins for data collection.
/// Handles plugin selection, failover, and storage coordination.
/// </summary>
/// <remarks>
/// This is the "simple orchestrator" that replaces the complex multi-provider
/// architecture. It provides:
/// - Automatic plugin selection based on capabilities
/// - Fallback to alternative plugins on failure
/// - Unified streaming interface
/// - Storage coordination
///
/// For more complex scenarios (priority-based routing, rate limit balancing,
/// cross-provider validation), extend this class or use the legacy
/// CompositeHistoricalDataProvider.
/// </remarks>
public sealed class PluginOrchestrator : IAsyncDisposable
{
    private readonly IPluginRegistry _registry;
    private readonly IMarketDataStore? _store;
    private readonly ILogger _logger;
    private readonly Dictionary<string, IMarketDataPlugin> _activePlugins = new();
    private readonly SemaphoreSlim _pluginLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Creates a new orchestrator.
    /// </summary>
    public PluginOrchestrator(
        IPluginRegistry registry,
        IMarketDataStore? store = null,
        ILogger<PluginOrchestrator>? logger = null)
    {
        _registry = registry;
        _store = store;
        _logger = logger ?? NullLogger<PluginOrchestrator>.Instance;
    }

    /// <summary>
    /// Gets the list of available (configured) plugins.
    /// </summary>
    public IReadOnlyList<PluginDescriptor> AvailablePlugins =>
        _registry.GetAll().Where(p => p.IsConfigured).ToList();

    /// <summary>
    /// Streams data using the best available plugin for the request.
    /// Automatically falls back to alternative plugins on failure.
    /// </summary>
    public async IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var capablePlugins = _registry.GetCapable(request);

        if (capablePlugins.Count == 0)
        {
            throw new InvalidOperationException(
                $"No plugins available to fulfill request. " +
                $"Request type: {(request.IsHistorical ? "Historical" : "Realtime")}, " +
                $"Data types: {string.Join(", ", request.DataTypes)}");
        }

        _logger.LogInformation(
            "Found {Count} capable plugins for request: {Plugins}",
            capablePlugins.Count,
            string.Join(", ", capablePlugins.Select(p => p.Id)));

        // Try plugins in priority order
        Exception? lastException = null;
        foreach (var descriptor in capablePlugins)
        {
            ct.ThrowIfCancellationRequested();

            var plugin = await GetOrCreatePluginAsync(descriptor, ct).ConfigureAwait(false);

            if (plugin == null || plugin.Health.ShouldAvoid)
            {
                _logger.LogDebug("Skipping plugin {PluginId} (health: {Status})",
                    descriptor.Id, plugin?.Health.Status ?? HealthStatus.Unknown);
                continue;
            }

            _logger.LogInformation("Using plugin {PluginId} for streaming", descriptor.Id);

            var hasYielded = false;
            try
            {
                await foreach (var evt in plugin.StreamAsync(request, ct).ConfigureAwait(false))
                {
                    hasYielded = true;

                    // Store event if storage is configured
                    if (_store != null)
                    {
                        await _store.AppendAsync(evt, ct).ConfigureAwait(false);
                    }

                    yield return evt;
                }

                // Stream completed successfully
                _logger.LogInformation("Plugin {PluginId} completed streaming successfully", descriptor.Id);
                yield break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "Plugin {PluginId} failed{YieldedData}, trying next plugin",
                    descriptor.Id,
                    hasYielded ? " after yielding some data" : "");

                // If we yielded data but then failed, we can't cleanly switch plugins
                if (hasYielded)
                {
                    throw;
                }
            }
        }

        // All plugins failed
        throw new InvalidOperationException(
            "All plugins failed to fulfill request",
            lastException);
    }

    /// <summary>
    /// Runs a backfill operation for the given symbols and date range.
    /// </summary>
    public async Task BackfillAsync(
        IEnumerable<string> symbols,
        DateOnly from,
        DateOnly? to = null,
        string interval = "1day",
        IProgress<BackfillProgress>? progress = null,
        CancellationToken ct = default)
    {
        var symbolList = symbols.ToList();
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation(
            "Starting backfill for {Count} symbols from {From} to {To}",
            symbolList.Count, from, toDate);

        var request = DataStreamRequest.Historical(symbolList, from, toDate, interval);

        var completed = 0;
        var total = symbolList.Count;

        await foreach (var evt in StreamAsync(request, ct).ConfigureAwait(false))
        {
            // Progress tracking for bars
            if (evt is BarEvent bar)
            {
                var symbolIndex = symbolList.IndexOf(bar.Symbol);
                if (symbolIndex >= 0 && symbolIndex > completed)
                {
                    completed = symbolIndex;
                    progress?.Report(new BackfillProgress(completed, total, bar.Symbol));
                }
            }
        }

        // Flush storage
        if (_store != null)
        {
            await _store.FlushAsync(ct).ConfigureAwait(false);
        }

        progress?.Report(new BackfillProgress(total, total, "Complete"));
        _logger.LogInformation("Backfill completed for {Count} symbols", total);
    }

    /// <summary>
    /// Gets health status for all plugins.
    /// </summary>
    public Dictionary<string, PluginHealth> GetHealthStatus()
    {
        var health = new Dictionary<string, PluginHealth>();

        foreach (var descriptor in _registry.GetAll())
        {
            if (_activePlugins.TryGetValue(descriptor.Id, out var plugin))
            {
                health[descriptor.Id] = plugin.Health;
            }
            else
            {
                health[descriptor.Id] = descriptor.IsConfigured
                    ? PluginHealth.Unknown
                    : PluginHealth.Unhealthy("Not configured");
            }
        }

        return health;
    }

    #region Private Methods

    private async Task<IMarketDataPlugin?> GetOrCreatePluginAsync(
        PluginDescriptor descriptor,
        CancellationToken ct)
    {
        await _pluginLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_activePlugins.TryGetValue(descriptor.Id, out var existing))
            {
                if (existing.State != PluginState.Disposed && existing.State != PluginState.Error)
                {
                    return existing;
                }

                // Dispose failed plugin and recreate
                await existing.DisposeAsync().ConfigureAwait(false);
                _activePlugins.Remove(descriptor.Id);
            }

            // Create and initialize new plugin
            var plugin = _registry.CreateInstance(descriptor.Id);

            var config = new PluginConfigBuilder(descriptor.ConfigPrefix)
                .AddEnvironment()
                .Build();

            await plugin.InitializeAsync(config, ct).ConfigureAwait(false);

            _activePlugins[descriptor.Id] = plugin;

            _logger.LogDebug("Created and initialized plugin {PluginId}", descriptor.Id);

            return plugin;
        }
        finally
        {
            _pluginLock.Release();
        }
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _pluginLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var plugin in _activePlugins.Values)
            {
                try
                {
                    await plugin.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing plugin");
                }
            }
            _activePlugins.Clear();
        }
        finally
        {
            _pluginLock.Release();
        }

        _pluginLock.Dispose();

        if (_store != null)
        {
            await _store.DisposeAsync().ConfigureAwait(false);
        }
    }

    #endregion
}

/// <summary>
/// Progress information for backfill operations.
/// </summary>
public readonly record struct BackfillProgress(
    int CompletedSymbols,
    int TotalSymbols,
    string CurrentSymbol)
{
    public double PercentComplete => TotalSymbols > 0
        ? 100.0 * CompletedSymbols / TotalSymbols
        : 0;
}
