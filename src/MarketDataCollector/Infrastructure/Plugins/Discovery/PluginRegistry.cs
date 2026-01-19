using System.Reflection;
using MarketDataCollector.Infrastructure.Plugins.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins.Discovery;

/// <summary>
/// Registry for discovering, registering, and managing market data plugins.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// Gets all registered plugin descriptors.
    /// </summary>
    IReadOnlyList<PluginDescriptor> GetAll();

    /// <summary>
    /// Gets a plugin descriptor by ID.
    /// </summary>
    PluginDescriptor? GetById(string id);

    /// <summary>
    /// Gets plugins that can fulfill the given request.
    /// </summary>
    IReadOnlyList<PluginDescriptor> GetCapable(DataStreamRequest request);

    /// <summary>
    /// Creates an instance of a plugin by ID.
    /// </summary>
    IMarketDataPlugin CreateInstance(string id);

    /// <summary>
    /// Registers a plugin type.
    /// </summary>
    void Register<TPlugin>() where TPlugin : IMarketDataPlugin;

    /// <summary>
    /// Registers a plugin type.
    /// </summary>
    void Register(Type pluginType);

    /// <summary>
    /// Scans an assembly for plugins and registers them.
    /// </summary>
    void ScanAssembly(Assembly assembly);
}

/// <summary>
/// Describes a registered plugin.
/// </summary>
public sealed record PluginDescriptor
{
    /// <summary>
    /// Plugin identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Plugin type (Realtime, Historical, Hybrid).
    /// </summary>
    public required PluginType Type { get; init; }

    /// <summary>
    /// Plugin category.
    /// </summary>
    public required PluginCategory Category { get; init; }

    /// <summary>
    /// Priority for fallback ordering.
    /// </summary>
    public required int Priority { get; init; }

    /// <summary>
    /// Plugin version.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Configuration prefix.
    /// </summary>
    public required string ConfigPrefix { get; init; }

    /// <summary>
    /// The implementation type.
    /// </summary>
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// Whether the plugin has required configuration available.
    /// </summary>
    public bool IsConfigured { get; init; }
}

/// <summary>
/// Default implementation of the plugin registry.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    private readonly Dictionary<string, PluginDescriptor> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginRegistry> _logger;

    public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IReadOnlyList<PluginDescriptor> GetAll() =>
        _plugins.Values.OrderBy(p => p.Priority).ToList();

    public PluginDescriptor? GetById(string id) =>
        _plugins.TryGetValue(id, out var descriptor) ? descriptor : null;

    public IReadOnlyList<PluginDescriptor> GetCapable(DataStreamRequest request)
    {
        return _plugins.Values
            .Where(p => CanFulfill(p, request))
            .OrderBy(p => p.Priority)
            .ToList();
    }

    private static bool CanFulfill(PluginDescriptor descriptor, DataStreamRequest request)
    {
        // Check type compatibility
        if (request.IsRealtime && descriptor.Type == PluginType.Historical)
            return false;
        if (request.IsHistorical && descriptor.Type == PluginType.Realtime)
            return false;

        // Must be configured
        if (!descriptor.IsConfigured)
            return false;

        return true;
    }

    public IMarketDataPlugin CreateInstance(string id)
    {
        var descriptor = GetById(id) ??
            throw new ArgumentException($"Plugin '{id}' not found", nameof(id));

        _logger.LogDebug("Creating instance of plugin {PluginId}", id);

        // Try to resolve from DI first
        var plugin = _serviceProvider.GetService(descriptor.ImplementationType) as IMarketDataPlugin;

        if (plugin == null)
        {
            // Fall back to Activator
            plugin = Activator.CreateInstance(descriptor.ImplementationType) as IMarketDataPlugin ??
                throw new InvalidOperationException(
                    $"Failed to create instance of plugin {id}");
        }

        return plugin;
    }

    public void Register<TPlugin>() where TPlugin : IMarketDataPlugin =>
        Register(typeof(TPlugin));

    public void Register(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);

        var attr = pluginType.GetCustomAttribute<MarketDataPluginAttribute>();
        if (attr == null)
        {
            throw new ArgumentException(
                $"Type {pluginType.Name} is not marked with [MarketDataPlugin] attribute",
                nameof(pluginType));
        }

        if (!typeof(IMarketDataPlugin).IsAssignableFrom(pluginType))
        {
            throw new ArgumentException(
                $"Type {pluginType.Name} does not implement IMarketDataPlugin",
                nameof(pluginType));
        }

        var configPrefix = attr.ConfigPrefix ?? attr.Id.ToUpperInvariant();
        var isConfigured = CheckConfiguration(configPrefix);

        var descriptor = new PluginDescriptor
        {
            Id = attr.Id,
            DisplayName = attr.DisplayName,
            Description = attr.Description,
            Type = attr.Type,
            Category = attr.Category,
            Priority = attr.Priority,
            Version = System.Version.TryParse(attr.Version, out var v) ? v : new Version(1, 0, 0),
            ConfigPrefix = configPrefix,
            ImplementationType = pluginType,
            IsConfigured = isConfigured
        };

        _plugins[attr.Id] = descriptor;

        _logger.LogInformation(
            "Registered plugin {PluginId} ({DisplayName}) [Type={Type}, Configured={IsConfigured}]",
            descriptor.Id, descriptor.DisplayName, descriptor.Type, descriptor.IsConfigured);
    }

    public void ScanAssembly(Assembly assembly)
    {
        _logger.LogDebug("Scanning assembly {Assembly} for plugins", assembly.GetName().Name);

        var pluginTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(IMarketDataPlugin).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<MarketDataPluginAttribute>() != null);

        foreach (var pluginType in pluginTypes)
        {
            try
            {
                Register(pluginType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register plugin type {Type}", pluginType.Name);
            }
        }
    }

    private static bool CheckConfiguration(string prefix)
    {
        // Check if any environment variables with this prefix exist
        var envPrefix = prefix.ToUpperInvariant() + "__";
        return Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Any(e => e.Key.ToString()?.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase) == true);
    }
}

/// <summary>
/// Extension methods for plugin registration.
/// </summary>
public static class PluginRegistryExtensions
{
    /// <summary>
    /// Adds the plugin registry to the service collection.
    /// </summary>
    public static IServiceCollection AddPluginRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IPluginRegistry, PluginRegistry>();
        return services;
    }

    /// <summary>
    /// Adds the plugin registry and scans the calling assembly for plugins.
    /// </summary>
    public static IServiceCollection AddPluginRegistryWithDiscovery(
        this IServiceCollection services,
        params Assembly[] additionalAssemblies)
    {
        services.AddSingleton<IPluginRegistry>(sp =>
        {
            var registry = new PluginRegistry(sp, sp.GetRequiredService<ILogger<PluginRegistry>>());

            // Scan calling assembly
            registry.ScanAssembly(Assembly.GetCallingAssembly());

            // Scan entry assembly
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                registry.ScanAssembly(entryAssembly);
            }

            // Scan additional assemblies
            foreach (var assembly in additionalAssemblies)
            {
                registry.ScanAssembly(assembly);
            }

            return registry;
        });

        return services;
    }
}
