using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.ProviderSdk;

/// <summary>
/// Discovers and loads <see cref="IProviderPlugin"/> implementations from assemblies.
/// Supports both compile-time referenced assemblies and runtime-loaded plugin DLLs.
/// </summary>
public static class ProviderPluginLoader
{
    /// <summary>
    /// Discover all IProviderPlugin implementations in the currently loaded assemblies.
    /// Used for compile-time referenced provider projects.
    /// </summary>
    public static IReadOnlyList<IProviderPlugin> DiscoverFromLoadedAssemblies(ILogger? logger = null)
    {
        var plugins = new List<IProviderPlugin>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                    continue;

                plugins.AddRange(DiscoverFromAssembly(assembly, logger));
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Skipping assembly {Assembly} during plugin discovery", assembly.FullName);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Discover all IProviderPlugin implementations in a specific assembly.
    /// </summary>
    public static IReadOnlyList<IProviderPlugin> DiscoverFromAssembly(Assembly assembly, ILogger? logger = null)
    {
        var plugins = new List<IProviderPlugin>();

        try
        {
            var pluginTypes = assembly.GetExportedTypes()
                .Where(t => typeof(IProviderPlugin).IsAssignableFrom(t)
                            && t is { IsAbstract: false, IsInterface: false });

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    if (Activator.CreateInstance(pluginType) is IProviderPlugin plugin)
                    {
                        plugins.Add(plugin);
                        logger?.LogInformation(
                            "Discovered provider plugin: {PluginId} v{Version} ({DisplayName}) from {Assembly}",
                            plugin.Info.PluginId, plugin.Info.Version, plugin.Info.DisplayName,
                            assembly.GetName().Name);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex,
                        "Failed to instantiate plugin type {Type} from {Assembly}",
                        pluginType.FullName, assembly.GetName().Name);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger?.LogDebug(ex, "Could not load types from assembly {Assembly}", assembly.FullName);
        }

        return plugins;
    }

    /// <summary>
    /// Load provider plugins from DLL files in a directory.
    /// Used for runtime plugin loading (drop-in provider DLLs).
    /// </summary>
    /// <param name="pluginDirectory">Directory containing provider plugin DLLs.</param>
    /// <param name="searchPattern">File pattern to search for (default: *.Providers.*.dll).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>List of discovered plugins.</returns>
    public static IReadOnlyList<IProviderPlugin> LoadFromDirectory(
        string pluginDirectory,
        string searchPattern = "MarketDataCollector.Providers.*.dll",
        ILogger? logger = null)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            logger?.LogDebug("Plugin directory does not exist: {Directory}", pluginDirectory);
            return Array.Empty<IProviderPlugin>();
        }

        var plugins = new List<IProviderPlugin>();
        var dllFiles = Directory.GetFiles(pluginDirectory, searchPattern);

        logger?.LogInformation("Scanning {Count} assemblies in {Directory} for provider plugins",
            dllFiles.Length, pluginDirectory);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var loadContext = new PluginLoadContext(dllPath);
                var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(dllPath));
                var assembly = loadContext.LoadFromAssemblyName(assemblyName);

                var discovered = DiscoverFromAssembly(assembly, logger);
                plugins.AddRange(discovered);

                if (discovered.Count == 0)
                {
                    logger?.LogDebug("No plugins found in {File}", Path.GetFileName(dllPath));
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load plugin from {File}", Path.GetFileName(dllPath));
            }
        }

        return plugins;
    }

    /// <summary>
    /// Custom assembly load context for plugin isolation.
    /// Ensures plugin dependencies don't conflict with the host application.
    /// </summary>
    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
        }
    }
}
