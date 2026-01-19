using System.Reflection;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using MarketDataCollector.Infrastructure.Plugins.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins;

/// <summary>
/// Extension methods for registering the plugin system with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the plugin system to the service collection.
    /// Scans the calling assembly and entry assembly for plugins.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration callback.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Example usage in Program.cs:
    /// <code>
    /// builder.Services.AddMarketDataPlugins(options =>
    /// {
    ///     options.DataPath = "./data";
    ///     options.EnableStorage = true;
    ///     options.EnableCompression = true;
    /// });
    /// </code>
    ///
    /// After this, you can inject IPluginRegistry or PluginOrchestrator:
    /// <code>
    /// app.Services.GetRequiredService&lt;PluginOrchestrator&gt;();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddMarketDataPlugins(
        this IServiceCollection services,
        Action<PluginSystemOptions>? configureOptions = null)
    {
        var options = new PluginSystemOptions();
        configureOptions?.Invoke(options);

        // Register options
        services.AddSingleton(options);

        // Register plugin registry with assembly scanning
        services.AddSingleton<IPluginRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PluginRegistry>>();
            var registry = new PluginRegistry(sp, logger);

            // Scan entry assembly
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                registry.ScanAssembly(entryAssembly);
            }

            // Scan this assembly (where plugins are defined)
            registry.ScanAssembly(typeof(ServiceCollectionExtensions).Assembly);

            // Scan additional assemblies
            foreach (var assembly in options.AdditionalAssemblies)
            {
                registry.ScanAssembly(assembly);
            }

            return registry;
        });

        // Register storage if enabled
        if (options.EnableStorage)
        {
            services.AddSingleton<IMarketDataStore>(sp =>
            {
                var storeOptions = new StoreOptions
                {
                    DataPath = options.DataPath,
                    Compress = options.EnableCompression,
                    BufferSize = options.BufferSize,
                    FlushInterval = options.FlushInterval
                };

                var logger = sp.GetRequiredService<ILogger<FileSystemStore>>();
                return new FileSystemStore(storeOptions, logger);
            });
        }

        // Register orchestrator
        services.AddSingleton<PluginOrchestrator>(sp =>
        {
            var registry = sp.GetRequiredService<IPluginRegistry>();
            var store = sp.GetService<IMarketDataStore>();
            var logger = sp.GetRequiredService<ILogger<PluginOrchestrator>>();

            return new PluginOrchestrator(registry, store, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a specific plugin type to the service collection.
    /// </summary>
    public static IServiceCollection AddPlugin<TPlugin>(this IServiceCollection services)
        where TPlugin : class, IMarketDataPlugin
    {
        services.AddTransient<TPlugin>();
        return services;
    }
}

/// <summary>
/// Configuration options for the plugin system.
/// </summary>
public sealed class PluginSystemOptions
{
    /// <summary>
    /// Root directory for data storage.
    /// Default: ./data
    /// </summary>
    public string DataPath { get; set; } = "./data";

    /// <summary>
    /// Whether to enable storage (persisting received data).
    /// Default: true
    /// </summary>
    public bool EnableStorage { get; set; } = true;

    /// <summary>
    /// Whether to compress stored data.
    /// Default: true for historical, false for real-time
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Buffer size for batch writes.
    /// Default: 1000
    /// </summary>
    public int BufferSize { get; set; } = 1000;

    /// <summary>
    /// Flush interval for buffered writes.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Additional assemblies to scan for plugins.
    /// </summary>
    public List<Assembly> AdditionalAssemblies { get; set; } = [];
}
