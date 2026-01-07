using System.Reflection;
using System.Runtime.Loader;

namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation.
/// Each plugin gets its own load context to enable hot-reload
/// and prevent dependency conflicts.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly HashSet<string> _sharedAssemblies;
    private readonly string _pluginPath;

    /// <summary>
    /// Path to the plugin assembly.
    /// </summary>
    public string PluginPath => _pluginPath;

    /// <summary>
    /// Creates a new PluginLoadContext.
    /// </summary>
    /// <param name="pluginPath">Path to the plugin assembly.</param>
    /// <param name="sharedAssemblies">Names of assemblies to load from the host context.</param>
    /// <param name="isCollectible">Whether the context can be unloaded for hot-reload.</param>
    public PluginLoadContext(
        string pluginPath,
        IEnumerable<string>? sharedAssemblies = null,
        bool isCollectible = true)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: isCollectible)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);

        // Default shared assemblies that should be loaded from the host
        _sharedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core framework
            "System.Runtime",
            "System.Private.CoreLib",
            "netstandard",

            // MarketDataCollector core types
            "MarketDataCollector",
            "MarketDataCollector.Contracts",

            // Common dependencies that should be shared
            "Serilog",
            "Serilog.Sinks.Console",
            "Serilog.Sinks.File",
            "System.Reactive",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Configuration.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions"
        };

        if (sharedAssemblies != null)
        {
            foreach (var assembly in sharedAssemblies)
            {
                _sharedAssemblies.Add(assembly);
            }
        }
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Check if this assembly should be loaded from the host context
        if (assemblyName.Name != null && ShouldLoadFromHost(assemblyName.Name))
        {
            return null; // Defer to default context
        }

        // Try to resolve the assembly from the plugin's dependencies
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    /// <inheritdoc />
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private bool ShouldLoadFromHost(string assemblyName)
    {
        // Always load shared assemblies from host
        if (_sharedAssemblies.Contains(assemblyName))
            return true;

        // Load Microsoft and System assemblies from host
        if (assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return true;

        if (assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Adds an assembly name to the shared assembly list.
    /// </summary>
    public void AddSharedAssembly(string assemblyName)
    {
        _sharedAssemblies.Add(assemblyName);
    }

    /// <summary>
    /// Gets the list of shared assembly names.
    /// </summary>
    public IReadOnlySet<string> SharedAssemblies => _sharedAssemblies;

    /// <summary>
    /// Disposes the load context by unloading it.
    /// </summary>
    public void Dispose()
    {
        if (IsCollectible)
        {
            Unload();
        }
    }
}

/// <summary>
/// Factory for creating PluginLoadContext instances.
/// </summary>
public interface IPluginLoadContextFactory
{
    /// <summary>
    /// Creates a new PluginLoadContext for the specified plugin.
    /// </summary>
    /// <param name="pluginPath">Path to the plugin assembly.</param>
    /// <param name="isCollectible">Whether the context should be collectible (for hot-reload).</param>
    /// <returns>A new PluginLoadContext.</returns>
    PluginLoadContext Create(string pluginPath, bool isCollectible = true);
}

/// <summary>
/// Default implementation of IPluginLoadContextFactory.
/// </summary>
public sealed class PluginLoadContextFactory : IPluginLoadContextFactory
{
    private readonly IEnumerable<string>? _additionalSharedAssemblies;

    /// <summary>
    /// Creates a new PluginLoadContextFactory.
    /// </summary>
    /// <param name="additionalSharedAssemblies">Additional assemblies to share with plugins.</param>
    public PluginLoadContextFactory(IEnumerable<string>? additionalSharedAssemblies = null)
    {
        _additionalSharedAssemblies = additionalSharedAssemblies;
    }

    /// <inheritdoc />
    public PluginLoadContext Create(string pluginPath, bool isCollectible = true)
    {
        return new PluginLoadContext(pluginPath, _additionalSharedAssemblies, isCollectible);
    }
}

/// <summary>
/// Manages plugin load contexts and their lifecycle.
/// </summary>
public sealed class PluginLoadContextManager : IDisposable
{
    private readonly Dictionary<string, PluginLoadContextEntry> _contexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginLoadContextFactory _factory;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new PluginLoadContextManager.
    /// </summary>
    public PluginLoadContextManager(IPluginLoadContextFactory? factory = null)
    {
        _factory = factory ?? new PluginLoadContextFactory();
    }

    /// <summary>
    /// Gets or creates a load context for the specified plugin.
    /// </summary>
    public PluginLoadContext GetOrCreateContext(string pluginId, string pluginPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_contexts.TryGetValue(pluginId, out var existing))
            {
                return existing.Context;
            }

            var context = _factory.Create(pluginPath, isCollectible: true);
            _contexts[pluginId] = new PluginLoadContextEntry(context, pluginPath);
            return context;
        }
    }

    /// <summary>
    /// Unloads the context for a plugin.
    /// </summary>
    public bool UnloadContext(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (!_contexts.TryGetValue(pluginId, out var entry))
                return false;

            _contexts.Remove(pluginId);

            // Unload the context (will be collected when all references are released)
            entry.Context.Unload();

            // Suggest garbage collection to help unload
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }
    }

    /// <summary>
    /// Gets all active contexts.
    /// </summary>
    public IReadOnlyDictionary<string, PluginLoadContext> GetAllContexts()
    {
        lock (_lock)
        {
            return _contexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Context);
        }
    }

    /// <summary>
    /// Checks if a context exists for the specified plugin.
    /// </summary>
    public bool HasContext(string pluginId)
    {
        lock (_lock)
        {
            return _contexts.ContainsKey(pluginId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var entry in _contexts.Values)
            {
                try
                {
                    entry.Context.Unload();
                }
                catch
                {
                    // Best effort unload
                }
            }
            _contexts.Clear();
        }
    }

    private sealed record PluginLoadContextEntry(PluginLoadContext Context, string PluginPath);
}
