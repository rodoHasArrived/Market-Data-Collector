using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.ProviderSdk;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.PluginAdapters;

/// <summary>
/// Hosts provider plugins and bridges them into the core provider system.
/// Discovers IProviderPlugin implementations, invokes their registration,
/// and adapts SDK provider interfaces to the internal ProviderRegistry.
/// </summary>
[ImplementsAdr("ADR-001", "Plugin host for external provider assemblies")]
[ImplementsAdr("ADR-005", "Plugin-based provider discovery")]
public sealed class ProviderPluginHost
{
    private readonly IServiceCollection _services;
    private readonly ILogger _log;
    private readonly PluginRegistrationContext _registrationContext;
    private readonly List<ProviderPluginInfo> _loadedPlugins = new();

    public ProviderPluginHost(IServiceCollection services, ILogger? log = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _log = log ?? Log.ForContext<ProviderPluginHost>();
        _registrationContext = new PluginRegistrationContext(services, _log);
    }

    /// <summary>
    /// Loaded plugins metadata.
    /// </summary>
    public IReadOnlyList<ProviderPluginInfo> LoadedPlugins => _loadedPlugins;

    /// <summary>
    /// Discover and load plugins from all currently loaded assemblies.
    /// </summary>
    public void DiscoverAndLoadPlugins()
    {
        _log.Information("Discovering provider plugins from loaded assemblies...");

        var plugins = ProviderPluginLoader.DiscoverFromLoadedAssemblies(
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        foreach (var plugin in plugins)
        {
            LoadPlugin(plugin);
        }

        _log.Information("Plugin discovery complete. Loaded {Count} plugins with {Streaming} streaming, " +
                         "{Historical} historical, {SymbolSearch} symbol search providers",
            _loadedPlugins.Count,
            _registrationContext.StreamingProviderTypes.Count,
            _registrationContext.HistoricalProviderTypes.Count,
            _registrationContext.SymbolSearchProviderTypes.Count);
    }

    /// <summary>
    /// Load plugins from a specific directory (runtime plugin loading).
    /// </summary>
    public void LoadPluginsFromDirectory(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _log.Debug("Plugin directory does not exist: {Directory}", pluginDirectory);
            return;
        }

        _log.Information("Loading provider plugins from {Directory}...", pluginDirectory);

        var plugins = ProviderPluginLoader.LoadFromDirectory(
            pluginDirectory,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        foreach (var plugin in plugins)
        {
            LoadPlugin(plugin);
        }
    }

    /// <summary>
    /// Load a specific plugin instance.
    /// </summary>
    public void LoadPlugin(IProviderPlugin plugin)
    {
        try
        {
            _log.Information("Loading plugin: {PluginId} v{Version} ({DisplayName})",
                plugin.Info.PluginId, plugin.Info.Version, plugin.Info.DisplayName);

            plugin.Register(_registrationContext);
            _loadedPlugins.Add(plugin.Info);

            _log.Information("Plugin {PluginId} loaded successfully", plugin.Info.PluginId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load plugin {PluginId}", plugin.Info.PluginId);
        }
    }

    /// <summary>
    /// Register all discovered plugin providers into the DI container and ProviderRegistry.
    /// Call this after all plugins have been loaded.
    /// </summary>
    public void RegisterProviderServices()
    {
        // Register plugin provider types in DI
        foreach (var type in _registrationContext.HistoricalProviderTypes)
        {
            _services.AddSingleton(typeof(IHistoricalProvider), type);
            _log.Debug("Registered historical plugin provider: {Type}", type.Name);
        }

        foreach (var type in _registrationContext.StreamingProviderTypes)
        {
            _services.AddSingleton(typeof(IStreamingProvider), type);
            _log.Debug("Registered streaming plugin provider: {Type}", type.Name);
        }

        foreach (var type in _registrationContext.SymbolSearchProviderTypes)
        {
            _services.AddSingleton(typeof(ProviderSdk.Providers.ISymbolSearchProvider), type);
            _log.Debug("Registered symbol search plugin provider: {Type}", type.Name);
        }

        // Register the host itself for later access
        _services.AddSingleton(this);
    }

    /// <summary>
    /// Register discovered plugin providers into the ProviderRegistry at runtime.
    /// Call this after the service provider has been built.
    /// </summary>
    public static void RegisterPluginProvidersInRegistry(
        IServiceProvider serviceProvider,
        ProviderRegistry registry)
    {
        var log = Log.ForContext<ProviderPluginHost>();

        // Register historical providers from plugins
        var historicalProviders = serviceProvider.GetServices<IHistoricalProvider>();
        foreach (var provider in historicalProviders)
        {
            var adapter = new PluginHistoricalProviderAdapter(provider);
            registry.Register(adapter);
            log.Information("Registered plugin historical provider in registry: {ProviderId} (priority {Priority})",
                provider.ProviderId, provider.Priority);
        }

        // Register streaming providers from plugins
        var streamingProviders = serviceProvider.GetServices<IStreamingProvider>();
        foreach (var provider in streamingProviders)
        {
            var adapter = new PluginStreamingProviderAdapter(provider);
            registry.Register(adapter);
            log.Information("Registered plugin streaming provider in registry: {ProviderId}",
                provider.ProviderId);
        }

        // Register symbol search providers from plugins
        var searchProviders = serviceProvider.GetServices<ProviderSdk.Providers.ISymbolSearchProvider>();
        foreach (var provider in searchProviders)
        {
            var adapter = new PluginSymbolSearchProviderAdapter(provider);
            registry.Register(adapter);
            log.Information("Registered plugin symbol search provider in registry: {ProviderId}",
                provider.ProviderId);
        }
    }
}

/// <summary>
/// Collects provider registrations from plugins during the Register() phase.
/// </summary>
internal sealed class PluginRegistrationContext : IProviderRegistration
{
    private readonly IServiceCollection _services;
    private readonly ILogger _log;

    public List<Type> StreamingProviderTypes { get; } = new();
    public List<Type> HistoricalProviderTypes { get; } = new();
    public List<Type> SymbolSearchProviderTypes { get; } = new();
    public List<ProviderCredentialField> DeclaredCredentials { get; } = new();

    public PluginRegistrationContext(IServiceCollection services, ILogger log)
    {
        _services = services;
        _log = log;
    }

    public void AddStreamingProvider<T>() where T : class, IStreamingProvider
    {
        StreamingProviderTypes.Add(typeof(T));
        _services.AddSingleton<T>();
        _log.Debug("Plugin registered streaming provider: {Type}", typeof(T).Name);
    }

    public void AddHistoricalProvider<T>() where T : class, IHistoricalProvider
    {
        HistoricalProviderTypes.Add(typeof(T));
        _services.AddSingleton<T>();
        _log.Debug("Plugin registered historical provider: {Type}", typeof(T).Name);
    }

    public void AddSymbolSearchProvider<T>() where T : class, ProviderSdk.Providers.ISymbolSearchProvider
    {
        SymbolSearchProviderTypes.Add(typeof(T));
        _services.AddSingleton<T>();
        _log.Debug("Plugin registered symbol search provider: {Type}", typeof(T).Name);
    }

    public void AddServices(Action<IServiceCollection> configure)
    {
        configure(_services);
        _log.Debug("Plugin registered additional services");
    }

    public void DeclareCredentials(params ProviderCredentialField[] fields)
    {
        DeclaredCredentials.AddRange(fields);
        foreach (var field in fields)
        {
            _log.Debug("Plugin declared credential: {Name} (env: {Env})", field.Name, field.EnvironmentVariable);
        }
    }
}
