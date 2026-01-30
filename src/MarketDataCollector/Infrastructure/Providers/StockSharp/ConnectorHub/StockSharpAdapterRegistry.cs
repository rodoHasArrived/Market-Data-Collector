using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Interface for the StockSharp adapter registry.
/// Provides lookup capabilities for resolving adapters based on providers, exchanges, or asset classes.
/// </summary>
[ImplementsAdr("ADR-001", "Adapter registry contract for StockSharp connector hub")]
public interface IStockSharpAdapterRegistry
{
    /// <summary>
    /// Get all registered adapter configurations.
    /// </summary>
    IReadOnlyList<IStockSharpAdapterConfig> GetAllAdapters();

    /// <summary>
    /// Get an adapter configuration by ID.
    /// </summary>
    IStockSharpAdapterConfig? GetAdapterConfig(string adapterId);

    /// <summary>
    /// Get the adapter ID that handles a specific provider.
    /// </summary>
    string? GetAdapterForProvider(string providerId);

    /// <summary>
    /// Get the adapter ID that handles a specific exchange.
    /// </summary>
    string? GetAdapterForExchange(string exchange);

    /// <summary>
    /// Get the adapter ID that handles a specific asset class.
    /// </summary>
    string? GetAdapterForAssetClass(string assetClass);

    /// <summary>
    /// Register a new adapter configuration.
    /// </summary>
    void RegisterAdapter(IStockSharpAdapterConfig config);

    /// <summary>
    /// Unregister an adapter by ID.
    /// </summary>
    bool UnregisterAdapter(string adapterId);

    /// <summary>
    /// Get adapters supporting a specific market region.
    /// </summary>
    IReadOnlyList<IStockSharpAdapterConfig> GetAdaptersForMarket(string market);

    /// <summary>
    /// Get adapters supporting market depth.
    /// </summary>
    IReadOnlyList<IStockSharpAdapterConfig> GetDepthCapableAdapters();

    /// <summary>
    /// Get adapters supporting historical backfill.
    /// </summary>
    IReadOnlyList<IStockSharpAdapterConfig> GetBackfillCapableAdapters();
}

/// <summary>
/// Default implementation of the StockSharp adapter registry.
/// Manages adapter configurations and provides routing logic based on providers, exchanges, and asset classes.
/// </summary>
[ImplementsAdr("ADR-001", "Default adapter registry implementation")]
public sealed class StockSharpAdapterRegistry : IStockSharpAdapterRegistry
{
    private readonly ILogger _log = LoggingSetup.ForContext<StockSharpAdapterRegistry>();
    private readonly Dictionary<string, IStockSharpAdapterConfig> _adapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _providerToAdapter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _exchangeToAdapter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _assetClassToAdapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new adapter registry with optional initial adapters.
    /// </summary>
    public StockSharpAdapterRegistry(IEnumerable<IStockSharpAdapterConfig>? initialAdapters = null)
    {
        if (initialAdapters != null)
        {
            foreach (var adapter in initialAdapters)
            {
                RegisterAdapterInternal(adapter);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStockSharpAdapterConfig> GetAllAdapters()
    {
        lock (_lock)
        {
            return _adapters.Values
                .Where(a => a.Enabled)
                .OrderBy(a => a.Priority)
                .ToList();
        }
    }

    /// <inheritdoc/>
    public IStockSharpAdapterConfig? GetAdapterConfig(string adapterId)
    {
        lock (_lock)
        {
            return _adapters.GetValueOrDefault(adapterId);
        }
    }

    /// <inheritdoc/>
    public string? GetAdapterForProvider(string providerId)
    {
        lock (_lock)
        {
            if (_providerToAdapter.TryGetValue(providerId, out var adapterId))
            {
                var config = _adapters.GetValueOrDefault(adapterId);
                if (config?.Enabled == true)
                    return adapterId;
            }

            // Also check if providerId is itself an adapter ID
            if (_adapters.TryGetValue(providerId, out var directAdapter) && directAdapter.Enabled)
                return providerId;

            return null;
        }
    }

    /// <inheritdoc/>
    public string? GetAdapterForExchange(string exchange)
    {
        lock (_lock)
        {
            if (_exchangeToAdapter.TryGetValue(exchange, out var adapterId))
            {
                var config = _adapters.GetValueOrDefault(adapterId);
                if (config?.Enabled == true)
                    return adapterId;
            }

            // Fall back to searching all adapters
            return _adapters.Values
                .Where(a => a.Enabled && a.SupportedExchanges.Contains(exchange, StringComparer.OrdinalIgnoreCase))
                .OrderBy(a => a.Priority)
                .Select(a => a.AdapterId)
                .FirstOrDefault();
        }
    }

    /// <inheritdoc/>
    public string? GetAdapterForAssetClass(string assetClass)
    {
        lock (_lock)
        {
            if (_assetClassToAdapters.TryGetValue(assetClass, out var adapterIds))
            {
                foreach (var adapterId in adapterIds)
                {
                    var config = _adapters.GetValueOrDefault(adapterId);
                    if (config?.Enabled == true)
                        return adapterId;
                }
            }

            // Fall back to searching all adapters
            return _adapters.Values
                .Where(a => a.Enabled && a.SupportedAssetClasses.Contains(assetClass, StringComparer.OrdinalIgnoreCase))
                .OrderBy(a => a.Priority)
                .Select(a => a.AdapterId)
                .FirstOrDefault();
        }
    }

    /// <inheritdoc/>
    public void RegisterAdapter(IStockSharpAdapterConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        lock (_lock)
        {
            RegisterAdapterInternal(config);
        }

        _log.Information("Registered adapter {AdapterId}: {DisplayName} (priority={Priority})",
            config.AdapterId, config.DisplayName, config.Priority);
    }

    private void RegisterAdapterInternal(IStockSharpAdapterConfig config)
    {
        // Store the adapter config
        _adapters[config.AdapterId] = config;

        // Build provider mappings
        foreach (var providerId in config.MappedProviderIds)
        {
            _providerToAdapter[providerId] = config.AdapterId;
        }

        // Build exchange mappings (first adapter wins for each exchange based on priority)
        foreach (var exchange in config.SupportedExchanges)
        {
            if (!_exchangeToAdapter.ContainsKey(exchange))
            {
                _exchangeToAdapter[exchange] = config.AdapterId;
            }
            else
            {
                // Check if new adapter has higher priority
                var existingAdapterId = _exchangeToAdapter[exchange];
                var existingAdapter = _adapters.GetValueOrDefault(existingAdapterId);
                if (existingAdapter == null || config.Priority < existingAdapter.Priority)
                {
                    _exchangeToAdapter[exchange] = config.AdapterId;
                }
            }
        }

        // Build asset class mappings
        foreach (var assetClass in config.SupportedAssetClasses)
        {
            if (!_assetClassToAdapters.TryGetValue(assetClass, out var list))
            {
                list = new List<string>();
                _assetClassToAdapters[assetClass] = list;
            }

            if (!list.Contains(config.AdapterId))
            {
                list.Add(config.AdapterId);
                // Sort by priority
                list.Sort((a, b) =>
                {
                    var adapterA = _adapters.GetValueOrDefault(a);
                    var adapterB = _adapters.GetValueOrDefault(b);
                    return (adapterA?.Priority ?? int.MaxValue).CompareTo(adapterB?.Priority ?? int.MaxValue);
                });
            }
        }
    }

    /// <inheritdoc/>
    public bool UnregisterAdapter(string adapterId)
    {
        lock (_lock)
        {
            if (!_adapters.TryGetValue(adapterId, out var config))
                return false;

            _adapters.Remove(adapterId);

            // Clean up provider mappings
            var providersToRemove = _providerToAdapter
                .Where(kvp => kvp.Value == adapterId)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var provider in providersToRemove)
            {
                _providerToAdapter.Remove(provider);
            }

            // Clean up exchange mappings
            var exchangesToRemove = _exchangeToAdapter
                .Where(kvp => kvp.Value == adapterId)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var exchange in exchangesToRemove)
            {
                _exchangeToAdapter.Remove(exchange);
            }

            // Clean up asset class mappings
            foreach (var list in _assetClassToAdapters.Values)
            {
                list.Remove(adapterId);
            }

            _log.Information("Unregistered adapter {AdapterId}", adapterId);
            return true;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStockSharpAdapterConfig> GetAdaptersForMarket(string market)
    {
        lock (_lock)
        {
            return _adapters.Values
                .Where(a => a.Enabled && a.SupportedMarkets.Contains(market, StringComparer.OrdinalIgnoreCase))
                .OrderBy(a => a.Priority)
                .ToList();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStockSharpAdapterConfig> GetDepthCapableAdapters()
    {
        lock (_lock)
        {
            return _adapters.Values
                .Where(a => a.Enabled && a.SupportsMarketDepth)
                .OrderBy(a => a.Priority)
                .ToList();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStockSharpAdapterConfig> GetBackfillCapableAdapters()
    {
        lock (_lock)
        {
            return _adapters.Values
                .Where(a => a.Enabled && a.SupportsBackfill)
                .OrderBy(a => a.Priority)
                .ToList();
        }
    }

    /// <summary>
    /// Create a registry with all built-in adapter configurations.
    /// </summary>
    public static StockSharpAdapterRegistry CreateWithBuiltInAdapters()
    {
        var registry = new StockSharpAdapterRegistry();

        // Register built-in adapters
        registry.RegisterAdapter(new InteractiveBrokersAdapterConfig());
        registry.RegisterAdapter(new AlpacaAdapterConfig());
        registry.RegisterAdapter(new PolygonAdapterConfig());
        registry.RegisterAdapter(new RithmicAdapterConfig());
        registry.RegisterAdapter(new IQFeedAdapterConfig());
        registry.RegisterAdapter(new BinanceAdapterConfig());

        return registry;
    }

    /// <summary>
    /// Create a registry from configuration options.
    /// </summary>
    public static StockSharpAdapterRegistry CreateFromOptions(ConnectorHubOptions options)
    {
        var registry = new StockSharpAdapterRegistry();

        foreach (var adapterOptions in options.Adapters)
        {
            var config = CreateAdapterFromOptions(adapterOptions);
            if (config != null)
            {
                registry.RegisterAdapter(config);
            }
        }

        return registry;
    }

    private static IStockSharpAdapterConfig? CreateAdapterFromOptions(AdapterOptions options)
    {
        return options.Type?.ToLowerInvariant() switch
        {
            "ib" or "interactivebrokers" => new InteractiveBrokersAdapterConfig
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                Host = options.GetSetting("Host", "127.0.0.1"),
                Port = options.GetSetting("Port", 7496),
                ClientId = options.GetSetting("ClientId", 1)
            },
            "alpaca" => new AlpacaAdapterConfig
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                KeyId = options.GetSetting("KeyId", ""),
                SecretKey = options.GetSetting("SecretKey", ""),
                UsePaper = options.GetSetting("UsePaper", true),
                Feed = options.GetSetting("Feed", "iex")
            },
            "polygon" => new PolygonAdapterConfig
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                ApiKey = options.GetSetting("ApiKey", ""),
                UseDelayed = options.GetSetting("UseDelayed", false)
            },
            "rithmic" => new RithmicAdapterConfig
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                Server = options.GetSetting("Server", "Rithmic Test"),
                UserName = options.GetSetting("UserName", ""),
                Password = options.GetSetting("Password", ""),
                CertFile = options.GetSetting("CertFile", "")
            },
            "iqfeed" => new IQFeedAdapterConfig
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                Host = options.GetSetting("Host", "127.0.0.1"),
                Level1Port = options.GetSetting("Level1Port", 9100),
                Level2Port = options.GetSetting("Level2Port", 9200),
                LookupPort = options.GetSetting("LookupPort", 9300)
            },
            "binance" => new BinanceAdapterConfig
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                ApiKey = options.GetSetting("ApiKey", ""),
                ApiSecret = options.GetSetting("ApiSecret", ""),
                UseTestnet = options.GetSetting("UseTestnet", false)
            },
            "custom" => new CustomAdapterConfig(
                options.Id ?? "custom",
                options.GetSetting("AdapterType", ""),
                options.DisplayName,
                options.Description)
            {
                Enabled = options.Enabled,
                Priority = options.Priority,
                AdapterAssembly = options.GetSetting<string?>("AdapterAssembly", null),
                Properties = options.Settings
            },
            _ => null
        };
    }
}

/// <summary>
/// Extension methods for adapter configuration lookups.
/// </summary>
public static class AdapterRegistryExtensions
{
    /// <summary>
    /// Get the best adapter for a symbol based on its configuration.
    /// </summary>
    public static string? GetBestAdapterForSymbol(
        this IStockSharpAdapterRegistry registry,
        Application.Config.SymbolConfig symbolConfig)
    {
        // Try provider first
        if (!string.IsNullOrEmpty(symbolConfig.Provider))
        {
            var adapter = registry.GetAdapterForProvider(symbolConfig.Provider);
            if (adapter != null) return adapter;
        }

        // Try exchange
        if (!string.IsNullOrEmpty(symbolConfig.Exchange))
        {
            var adapter = registry.GetAdapterForExchange(symbolConfig.Exchange);
            if (adapter != null) return adapter;
        }

        // Try asset class
        if (!string.IsNullOrEmpty(symbolConfig.AssetClass))
        {
            var adapter = registry.GetAdapterForAssetClass(symbolConfig.AssetClass);
            if (adapter != null) return adapter;
        }

        // Return first available adapter
        return registry.GetAllAdapters().FirstOrDefault()?.AdapterId;
    }
}
