using MarketDataCollector.Application.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Extension methods for registering plugin services with dependency injection.
/// </summary>
public static class PluginServiceExtensions
{
    /// <summary>
    /// Adds the data source plugin system to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataSourcePlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var log = LoggingSetup.ForContext("PluginRegistration");

        // Get plugin configuration
        var pluginConfig = new PluginSystemConfig();
        configuration.GetSection("DataSources:Plugins").Bind(pluginConfig);

        if (!pluginConfig.Enabled)
        {
            log.Information("Plugin system is disabled by configuration");
            return services;
        }

        // Register configuration
        services.AddSingleton(pluginConfig);

        // Build loader options
        var loaderOptions = new PluginLoaderOptions
        {
            PluginDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MarketDataCollector",
                "PluginData"),
            GrantedPermissions = ParsePermissions(pluginConfig.DefaultPermissions),
            RequireExplicitPermissions = pluginConfig.RequireExplicitPermissions,
            SharedAssemblies = pluginConfig.SharedAssemblies.ToList(),
            EnableHotReload = pluginConfig.EnableHotReload,
            OperationTimeout = TimeSpan.FromSeconds(30)
        };

        services.AddSingleton(loaderOptions);

        // Build manager options
        var managerOptions = new PluginManagerOptions
        {
            PluginDirectory = ResolvePluginDirectory(pluginConfig.PluginDirectory),
            PluginDirectories = ResolvePluginDirectories(pluginConfig),
            EnableDirectoryWatching = pluginConfig.EnableDirectoryWatching,
            EnableHotReload = pluginConfig.EnableHotReload,
            AutoLoadNewPlugins = pluginConfig.AutoLoadNewPlugins,
            HotReloadDebounce = TimeSpan.FromMilliseconds(pluginConfig.HotReloadDebounceMs)
        };

        services.AddSingleton(managerOptions);

        // Register plugin loader
        services.TryAddSingleton<IDataSourcePluginLoader>(sp =>
        {
            var opts = sp.GetRequiredService<PluginLoaderOptions>();
            var logger = sp.GetService<ILogger>() ?? LoggingSetup.ForContext<DataSourcePluginLoader>();
            return new DataSourcePluginLoader(sp, opts, logger);
        });

        // Register plugin manager
        services.TryAddSingleton<IDataSourcePluginManager>(sp =>
        {
            var loader = sp.GetRequiredService<IDataSourcePluginLoader>();
            var opts = sp.GetRequiredService<PluginManagerOptions>();
            var logger = sp.GetService<ILogger>() ?? LoggingSetup.ForContext<DataSourcePluginManager>();
            return new DataSourcePluginManager(loader, opts, logger);
        });

        log.Information("Plugin system registered with plugin directory: {Directory}", managerOptions.PluginDirectory);

        return services;
    }

    /// <summary>
    /// Adds the data source plugin system and integrates it with the DataSourceManager.
    /// </summary>
    public static IServiceCollection AddDataSourcePluginsWithIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add plugin system
        services.AddDataSourcePlugins(configuration);

        // Register a data source provider that includes plugins
        services.AddSingleton<PluginIntegratedDataSourceProvider>();

        return services;
    }

    private static string ResolvePluginDirectory(string configuredPath)
    {
        // If absolute path, use as-is
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        // Otherwise, relative to app base directory
        return Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    private static IReadOnlyList<string> ResolvePluginDirectories(PluginSystemConfig config)
    {
        var directories = new List<string>
        {
            ResolvePluginDirectory(config.PluginDirectory)
        };

        foreach (var additionalDir in config.AdditionalDirectories)
        {
            directories.Add(ResolvePluginDirectory(additionalDir));
        }

        return directories;
    }

    private static PluginPermissions ParsePermissions(string[] permissions)
    {
        var result = PluginPermissions.None;

        foreach (var perm in permissions)
        {
            if (Enum.TryParse<PluginPermissions>(perm, ignoreCase: true, out var parsed))
            {
                result |= parsed;
            }
        }

        return result;
    }
}

/// <summary>
/// Provides data sources including plugins to the DataSourceManager.
/// </summary>
public sealed class PluginIntegratedDataSourceProvider : IAsyncDisposable
{
    private readonly IDataSourcePluginManager _pluginManager;
    private readonly ILogger _log;
    private bool _initialized;

    /// <summary>
    /// Creates a new PluginIntegratedDataSourceProvider.
    /// </summary>
    public PluginIntegratedDataSourceProvider(
        IDataSourcePluginManager pluginManager,
        ILogger? logger = null)
    {
        _pluginManager = pluginManager;
        _log = logger ?? LoggingSetup.ForContext<PluginIntegratedDataSourceProvider>();
    }

    /// <summary>
    /// Initializes the plugin system and returns all data sources including plugins.
    /// </summary>
    public async Task<IEnumerable<IDataSource>> GetDataSourcesAsync(
        IEnumerable<IDataSource> builtInSources,
        CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await _pluginManager.InitializeAsync(ct).ConfigureAwait(false);
            _initialized = true;
        }

        var allSources = new List<IDataSource>(builtInSources);

        // Add plugin data sources
        var pluginSources = _pluginManager.PluginDataSources;
        allSources.AddRange(pluginSources);

        _log.Information("Providing {BuiltIn} built-in sources and {Plugin} plugin sources",
            builtInSources.Count(), pluginSources.Count);

        return allSources;
    }

    /// <summary>
    /// Gets the current plugin data sources.
    /// </summary>
    public IReadOnlyList<IDataSource> PluginDataSources => _pluginManager.PluginDataSources;

    /// <summary>
    /// Gets the plugin manager for advanced operations.
    /// </summary>
    public IDataSourcePluginManager PluginManager => _pluginManager;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _pluginManager.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Hosted service for initializing the plugin system at startup.
/// </summary>
public sealed class PluginInitializationService : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly IDataSourcePluginManager _pluginManager;
    private readonly ILogger _log;

    /// <summary>
    /// Creates a new PluginInitializationService.
    /// </summary>
    public PluginInitializationService(
        IDataSourcePluginManager pluginManager,
        ILogger? logger = null)
    {
        _pluginManager = pluginManager;
        _log = logger ?? LoggingSetup.ForContext<PluginInitializationService>();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Information("Initializing plugin system...");

        try
        {
            await _pluginManager.InitializeAsync(cancellationToken).ConfigureAwait(false);

            var status = _pluginManager.GetStatus();
            _log.Information("Plugin system initialized: {Active}/{Total} plugins active",
                status.ActivePlugins, status.TotalPlugins);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to initialize plugin system");
            // Don't throw - allow app to continue without plugins
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Information("Shutting down plugin system...");

        try
        {
            await _pluginManager.DisposeAsync().ConfigureAwait(false);
            _log.Information("Plugin system shut down successfully");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during plugin system shutdown");
        }
    }
}

/// <summary>
/// Extension methods for adding the plugin initialization hosted service.
/// </summary>
public static class PluginHostedServiceExtensions
{
    /// <summary>
    /// Adds the plugin initialization hosted service for automatic startup/shutdown.
    /// </summary>
    public static IServiceCollection AddPluginInitializationService(this IServiceCollection services)
    {
        services.AddHostedService<PluginInitializationService>();
        return services;
    }
}
