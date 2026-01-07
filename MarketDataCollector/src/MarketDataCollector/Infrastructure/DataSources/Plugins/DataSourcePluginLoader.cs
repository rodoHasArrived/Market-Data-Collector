using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Default implementation of IDataSourcePluginLoader for loading plugins from assemblies.
/// </summary>
public sealed class DataSourcePluginLoader : IDataSourcePluginLoader, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly PluginLoadContextManager _contextManager;
    private readonly PluginLoaderOptions _options;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _pluginPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <inheritdoc />
    public Version HostVersion { get; }

    /// <inheritdoc />
    public IReadOnlyList<LoadedPlugin> LoadedPlugins => _loadedPlugins.Values.ToList();

    /// <inheritdoc />
    public event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <inheritdoc />
    public event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <inheritdoc />
    public event EventHandler<PluginLoadFailedEventArgs>? PluginLoadFailed;

    /// <summary>
    /// Creates a new DataSourcePluginLoader.
    /// </summary>
    public DataSourcePluginLoader(
        IServiceProvider services,
        PluginLoaderOptions? options = null,
        ILogger? logger = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? new PluginLoaderOptions();
        _log = logger ?? LoggingSetup.ForContext<DataSourcePluginLoader>();

        // Get host version from the main assembly
        HostVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

        var contextFactory = new PluginLoadContextFactory(_options.SharedAssemblies);
        _contextManager = new PluginLoadContextManager(contextFactory);

        _log.Information("DataSourcePluginLoader initialized with host version {Version}", HostVersion);
    }

    /// <inheritdoc />
    public async Task<PluginLoadResult> LoadPluginAsync(
        string assemblyPath,
        PluginConfiguration? configuration = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();
        _log.Debug("Loading plugin from {Path}", assemblyPath);

        try
        {
            // Validate the assembly first
            var validation = ValidatePlugin(assemblyPath);
            if (!validation.IsValid)
            {
                var result = PluginLoadResult.FailedValidation(validation, assemblyPath);
                RaisePluginLoadFailed(assemblyPath, result.ErrorMessage!, null);
                return result;
            }

            // Load each discovered plugin type
            var loadedPlugins = new List<LoadedPlugin>();

            foreach (var pluginType in validation.DiscoveredPluginTypes)
            {
                var pluginResult = await LoadPluginTypeAsync(
                    assemblyPath,
                    pluginType,
                    configuration ?? new PluginConfiguration(),
                    ct).ConfigureAwait(false);

                if (pluginResult.Success && pluginResult.Plugin != null)
                {
                    loadedPlugins.Add(pluginResult.Plugin);
                }
                else
                {
                    _log.Warning("Failed to load plugin type {Type}: {Error}",
                        pluginType.FullName, pluginResult.ErrorMessage);
                }
            }

            if (loadedPlugins.Count == 0)
            {
                var result = PluginLoadResult.Failed("No plugins could be loaded from assembly", null, assemblyPath);
                RaisePluginLoadFailed(assemblyPath, result.ErrorMessage!, null);
                return result;
            }

            sw.Stop();

            // Return the first loaded plugin (typical case)
            var primaryPlugin = loadedPlugins[0];
            _log.Information("Loaded plugin {PluginId} v{Version} from {Path} in {Elapsed}ms",
                primaryPlugin.Metadata.PluginId,
                primaryPlugin.Metadata.Version,
                assemblyPath,
                sw.ElapsedMilliseconds);

            RaisePluginLoaded(primaryPlugin, sw.Elapsed);
            return PluginLoadResult.Succeeded(primaryPlugin, assemblyPath);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load plugin from {Path}", assemblyPath);
            RaisePluginLoadFailed(assemblyPath, ex.Message, ex);
            return PluginLoadResult.Failed(ex.Message, ex, assemblyPath);
        }
    }

    private async Task<PluginLoadResult> LoadPluginTypeAsync(
        string assemblyPath,
        Type pluginType,
        PluginConfiguration configuration,
        CancellationToken ct)
    {
        var attr = pluginType.GetPluginAttribute();
        if (attr == null)
        {
            return PluginLoadResult.Failed($"Type {pluginType.FullName} is not a valid plugin type", null, assemblyPath);
        }

        var metadata = attr.ToPluginMetadata(pluginType);

        // Check if already loaded
        if (_loadedPlugins.ContainsKey(metadata.PluginId))
        {
            return PluginLoadResult.Failed($"Plugin {metadata.PluginId} is already loaded", null, assemblyPath);
        }

        // Check version compatibility
        if (!metadata.IsCompatibleWith(HostVersion))
        {
            return PluginLoadResult.Failed(
                $"Plugin {metadata.PluginId} is not compatible with host version {HostVersion}. " +
                $"Requires: {metadata.MinHostVersion} - {metadata.MaxHostVersion}",
                null,
                assemblyPath);
        }

        // Check permissions
        if (_options.RequireExplicitPermissions &&
            metadata.RequiredPermissions != PluginPermissions.None &&
            !_options.GrantedPermissions.HasFlag(metadata.RequiredPermissions))
        {
            return PluginLoadResult.Failed(
                $"Plugin {metadata.PluginId} requires permissions that are not granted: {metadata.RequiredPermissions}",
                null,
                assemblyPath);
        }

        try
        {
            // Create the plugin instance
            var instance = CreatePluginInstance(pluginType, configuration);
            if (instance == null)
            {
                return PluginLoadResult.Failed($"Failed to create instance of {pluginType.FullName}", null, assemblyPath);
            }

            // Create the plugin context
            var dataDir = Path.Combine(_options.PluginDataDirectory, metadata.PluginId);
            Directory.CreateDirectory(dataDir);

            var context = new PluginContext
            {
                Services = _services,
                Configuration = configuration,
                PluginPath = assemblyPath,
                DataDirectory = dataDir,
                Loader = this,
                HostVersion = HostVersion,
                GrantedPermissions = _options.GrantedPermissions,
                Logger = _log.ForContext("PluginId", metadata.PluginId)
            };

            // Initialize the plugin
            await instance.OnLoadAsync(context, ct).ConfigureAwait(false);

            // Create the loaded plugin record
            var loadedPlugin = new LoadedPlugin
            {
                Instance = instance,
                Metadata = metadata,
                Context = context,
                Assembly = pluginType.Assembly,
                AssemblyPath = assemblyPath,
                State = PluginState.Active
            };

            // Register the plugin
            _loadedPlugins[metadata.PluginId] = loadedPlugin;
            _pluginPaths[metadata.PluginId] = assemblyPath;

            return PluginLoadResult.Succeeded(loadedPlugin, assemblyPath);
        }
        catch (Exception ex)
        {
            return PluginLoadResult.Failed($"Failed to initialize plugin: {ex.Message}", ex, assemblyPath);
        }
    }

    private IDataSourcePlugin? CreatePluginInstance(Type pluginType, PluginConfiguration configuration)
    {
        // Try to find a constructor that matches our available parameters
        var constructors = pluginType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        foreach (var ctor in constructors)
        {
            try
            {
                var parameters = ctor.GetParameters();
                var args = new object?[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];

                    // Handle known types
                    if (param.ParameterType == typeof(PluginConfiguration))
                        args[i] = configuration;
                    else if (param.ParameterType == typeof(DataSourceOptions))
                        args[i] = configuration.SourceOptions ?? DataSourceOptions.Default;
                    else if (param.ParameterType == typeof(ILogger))
                        args[i] = _log.ForContext(pluginType);
                    else
                        args[i] = _services.GetService(param.ParameterType);
                }

                var instance = ctor.Invoke(args);
                return instance as IDataSourcePlugin;
            }
            catch
            {
                // Try next constructor
            }
        }

        // Fallback: try parameterless constructor
        try
        {
            return Activator.CreateInstance(pluginType) as IDataSourcePlugin;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PluginLoadResult>> LoadPluginsFromDirectoryAsync(
        string pluginDirectory,
        string searchPattern = "*.dll",
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(pluginDirectory))
        {
            _log.Warning("Plugin directory does not exist: {Directory}", pluginDirectory);
            return [];
        }

        var results = new List<PluginLoadResult>();
        var assemblies = Directory.GetFiles(pluginDirectory, searchPattern, SearchOption.AllDirectories);

        _log.Information("Scanning {Count} assemblies in {Directory} for plugins",
            assemblies.Length, pluginDirectory);

        foreach (var assemblyPath in assemblies)
        {
            ct.ThrowIfCancellationRequested();

            // Skip assemblies that don't look like plugins
            var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (ShouldSkipAssembly(fileName))
                continue;

            // Validate first to avoid loading non-plugin assemblies
            var validation = ValidatePlugin(assemblyPath);
            if (!validation.IsValid || validation.DiscoveredPluginTypes.Count == 0)
                continue;

            var result = await LoadPluginAsync(assemblyPath, null, ct).ConfigureAwait(false);
            results.Add(result);
        }

        _log.Information("Loaded {Success} of {Total} plugins from {Directory}",
            results.Count(r => r.Success), results.Count, pluginDirectory);

        return results;
    }

    private static bool ShouldSkipAssembly(string fileName)
    {
        // Skip system and common framework assemblies
        return fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_loadedPlugins.TryRemove(pluginId, out var plugin))
        {
            _log.Warning("Cannot unload plugin {PluginId}: not loaded", pluginId);
            return false;
        }

        _log.Information("Unloading plugin {PluginId}", pluginId);

        try
        {
            plugin.State = PluginState.Unloading;

            // Call the plugin's unload hook
            await plugin.Instance.OnUnloadAsync(ct).ConfigureAwait(false);

            // Dispose the plugin
            await plugin.Instance.DisposeAsync().ConfigureAwait(false);

            plugin.State = PluginState.Unloaded;

            // Unload the assembly context if we created one
            _contextManager.UnloadContext(pluginId);
            _pluginPaths.TryRemove(pluginId, out _);

            RaisePluginUnloaded(pluginId, "Explicit unload requested");

            _log.Information("Plugin {PluginId} unloaded successfully", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error unloading plugin {PluginId}", pluginId);
            plugin.State = PluginState.Error;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<PluginLoadResult> ReloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_pluginPaths.TryGetValue(pluginId, out var assemblyPath))
        {
            return PluginLoadResult.Failed($"Plugin {pluginId} not found or path unknown");
        }

        // Get the existing configuration
        PluginConfiguration? existingConfig = null;
        if (_loadedPlugins.TryGetValue(pluginId, out var existing))
        {
            existingConfig = existing.Context.Configuration;
        }

        _log.Information("Reloading plugin {PluginId} from {Path}", pluginId, assemblyPath);

        // Unload the plugin
        await UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);

        // Wait a moment for the assembly to be released
        await Task.Delay(100, ct).ConfigureAwait(false);

        // Reload the plugin
        return await LoadPluginAsync(assemblyPath, existingConfig, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public LoadedPlugin? GetPlugin(string pluginId)
    {
        _loadedPlugins.TryGetValue(pluginId, out var plugin);
        return plugin;
    }

    /// <inheritdoc />
    public PluginValidationResult ValidatePlugin(string assemblyPath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!File.Exists(assemblyPath))
        {
            return PluginValidationResult.Invalid([$"Assembly file not found: {assemblyPath}"]);
        }

        try
        {
            // Create a temporary context for inspection
            var inspectionContext = new PluginLoadContext(assemblyPath, isCollectible: true);

            // Load the assembly for inspection
            var assembly = inspectionContext.LoadFromAssemblyPath(assemblyPath);
            var assemblyName = assembly.GetName();

            // Find all plugin types
            var pluginTypes = new List<Type>();
            var pluginMetadata = new List<PluginMetadata>();

            try
            {
                var types = assembly.GetExportedTypes()
                    .Where(t => t.IsDataSourcePlugin())
                    .ToList();

                foreach (var type in types)
                {
                    pluginTypes.Add(type);

                    var attr = type.GetPluginAttribute();
                    if (attr != null)
                    {
                        var metadata = attr.ToPluginMetadata(type);
                        pluginMetadata.Add(metadata);

                        // Check host version compatibility
                        if (!metadata.IsCompatibleWith(HostVersion))
                        {
                            warnings.Add($"Plugin {metadata.PluginId} may not be compatible with host version {HostVersion}");
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var loaderEx in ex.LoaderExceptions)
                {
                    if (loaderEx != null)
                    {
                        errors.Add($"Type load error: {loaderEx.Message}");
                    }
                }
            }

            if (pluginTypes.Count == 0)
            {
                return PluginValidationResult.Invalid(
                    ["No plugin types found. Ensure types implement IDataSourcePlugin and have [DataSourcePlugin] attribute."],
                    assemblyName.Name);
            }

            // Unload the inspection context
            inspectionContext.Unload();

            return PluginValidationResult.Valid(
                pluginTypes,
                pluginMetadata,
                assemblyName.Name ?? "Unknown",
                assemblyName.Version,
                warnings);
        }
        catch (Exception ex)
        {
            return PluginValidationResult.Invalid([$"Failed to load assembly: {ex.Message}"]);
        }
    }

    #region Event Helpers

    private void RaisePluginLoaded(LoadedPlugin plugin, TimeSpan loadTime)
    {
        PluginLoaded?.Invoke(this, new PluginLoadedEventArgs
        {
            Plugin = plugin,
            LoadTime = loadTime
        });
    }

    private void RaisePluginUnloaded(string pluginId, string? reason)
    {
        PluginUnloaded?.Invoke(this, new PluginUnloadedEventArgs
        {
            PluginId = pluginId,
            Reason = reason
        });
    }

    private void RaisePluginLoadFailed(string assemblyPath, string errorMessage, Exception? ex)
    {
        PluginLoadFailed?.Invoke(this, new PluginLoadFailedEventArgs
        {
            AssemblyPath = assemblyPath,
            ErrorMessage = errorMessage,
            Exception = ex
        });
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unload all plugins
        foreach (var plugin in _loadedPlugins.Values)
        {
            try
            {
                plugin.Instance.OnUnloadAsync(CancellationToken.None).GetAwaiter().GetResult();
                plugin.Instance.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort cleanup
            }
        }

        _loadedPlugins.Clear();
        _pluginPaths.Clear();
        _contextManager.Dispose();
    }
}

/// <summary>
/// Configuration options for the plugin loader.
/// </summary>
public sealed record PluginLoaderOptions
{
    /// <summary>
    /// Directory for plugin data storage.
    /// </summary>
    public string PluginDataDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarketDataCollector",
        "Plugins");

    /// <summary>
    /// Permissions granted to plugins by default.
    /// </summary>
    public PluginPermissions GrantedPermissions { get; init; } = PluginPermissions.Network | PluginPermissions.Environment;

    /// <summary>
    /// Whether plugins must explicitly request permissions.
    /// </summary>
    public bool RequireExplicitPermissions { get; init; } = false;

    /// <summary>
    /// Additional assemblies to share between host and plugins.
    /// </summary>
    public IReadOnlyList<string>? SharedAssemblies { get; init; }

    /// <summary>
    /// Whether to enable hot reload support.
    /// </summary>
    public bool EnableHotReload { get; init; } = true;

    /// <summary>
    /// Timeout for plugin operations.
    /// </summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
