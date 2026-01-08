using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Interface for managing the lifecycle of data source plugins.
/// </summary>
public interface IDataSourcePluginManager : IAsyncDisposable
{
    /// <summary>
    /// Gets all managed plugins.
    /// </summary>
    IReadOnlyList<ManagedPlugin> AllPlugins { get; }

    /// <summary>
    /// Gets all enabled plugins.
    /// </summary>
    IReadOnlyList<ManagedPlugin> EnabledPlugins { get; }

    /// <summary>
    /// Gets all plugin data sources for integration with DataSourceManager.
    /// </summary>
    IReadOnlyList<IDataSource> PluginDataSources { get; }

    /// <summary>
    /// Initializes the plugin manager and loads plugins from configured directories.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads a plugin from an assembly path.
    /// </summary>
    Task<PluginManagementResult> LoadPluginAsync(string assemblyPath, CancellationToken ct = default);

    /// <summary>
    /// Unloads a plugin by ID.
    /// </summary>
    Task<PluginManagementResult> UnloadPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Enables a plugin.
    /// </summary>
    Task<PluginManagementResult> EnablePluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Disables a plugin (keeps it loaded but inactive).
    /// </summary>
    Task<PluginManagementResult> DisablePluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Reloads a plugin (hot-reload).
    /// </summary>
    Task<PluginManagementResult> ReloadPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Updates plugin configuration.
    /// </summary>
    Task<PluginManagementResult> UpdateConfigurationAsync(string pluginId, PluginConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Gets a plugin by ID.
    /// </summary>
    ManagedPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// Scans a directory for new plugins.
    /// </summary>
    Task<IReadOnlyList<PluginManagementResult>> ScanDirectoryAsync(string directory, CancellationToken ct = default);

    /// <summary>
    /// Observable stream of plugin state changes.
    /// </summary>
    IObservable<PluginStateChange> StateChanges { get; }

    /// <summary>
    /// Gets the overall plugin system status.
    /// </summary>
    PluginSystemStatus GetStatus();
}

/// <summary>
/// Default implementation of IDataSourcePluginManager.
/// </summary>
public sealed class DataSourcePluginManager : IDataSourcePluginManager
{
    private readonly IDataSourcePluginLoader _loader;
    private readonly PluginManagerOptions _options;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, ManagedPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Subject<PluginStateChange> _stateChanges = new();
    private readonly FileSystemWatcher? _directoryWatcher;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    /// <inheritdoc />
    public IReadOnlyList<ManagedPlugin> AllPlugins => _plugins.Values.ToList();

    /// <inheritdoc />
    public IReadOnlyList<ManagedPlugin> EnabledPlugins => _plugins.Values
        .Where(p => p.IsEnabled && p.LoadedPlugin?.State == PluginState.Active)
        .ToList();

    /// <inheritdoc />
    public IReadOnlyList<IDataSource> PluginDataSources => _plugins.Values
        .Where(p => p.IsEnabled && p.LoadedPlugin?.State == PluginState.Active)
        .Select(p => p.LoadedPlugin!.Instance as IDataSource)
        .Where(s => s != null)
        .Cast<IDataSource>()
        .ToList();

    /// <inheritdoc />
    public IObservable<PluginStateChange> StateChanges => _stateChanges.AsObservable();

    /// <summary>
    /// Creates a new DataSourcePluginManager.
    /// </summary>
    public DataSourcePluginManager(
        IDataSourcePluginLoader loader,
        PluginManagerOptions? options = null,
        ILogger? logger = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _options = options ?? new PluginManagerOptions();
        _log = logger ?? LoggingSetup.ForContext<DataSourcePluginManager>();

        // Subscribe to loader events
        _loader.PluginLoaded += OnPluginLoaded;
        _loader.PluginUnloaded += OnPluginUnloaded;
        _loader.PluginLoadFailed += OnPluginLoadFailed;

        // Set up directory watcher for hot-reload
        if (_options.EnableDirectoryWatching && Directory.Exists(_options.PluginDirectory))
        {
            _directoryWatcher = new FileSystemWatcher(_options.PluginDirectory)
            {
                Filter = "*.dll",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _directoryWatcher.Changed += OnPluginFileChanged;
            _directoryWatcher.Created += OnPluginFileCreated;

            _log.Information("Plugin directory watcher enabled for {Directory}", _options.PluginDirectory);
        }
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized) return;

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            _log.Information("Initializing plugin manager...");

            // Ensure plugin directory exists
            if (!string.IsNullOrEmpty(_options.PluginDirectory))
            {
                Directory.CreateDirectory(_options.PluginDirectory);
            }

            // Load plugins from configured directories
            foreach (var directory in _options.PluginDirectories)
            {
                if (Directory.Exists(directory))
                {
                    await ScanDirectoryAsync(directory, ct).ConfigureAwait(false);
                }
                else
                {
                    _log.Warning("Plugin directory does not exist: {Directory}", directory);
                }
            }

            // Initialize enabled plugins
            foreach (var plugin in _plugins.Values.Where(p => p.IsEnabled))
            {
                try
                {
                    if (plugin.LoadedPlugin != null)
                    {
                        await plugin.LoadedPlugin.Instance.InitializeAsync(ct).ConfigureAwait(false);
                        plugin.LoadedPlugin.State = PluginState.Active;
                        EmitStateChange(plugin.PluginId, PluginState.Active, "Initialized");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Failed to initialize plugin {PluginId}", plugin.PluginId);
                    if (plugin.LoadedPlugin != null)
                        plugin.LoadedPlugin.State = PluginState.Error;
                    EmitStateChange(plugin.PluginId, PluginState.Error, ex.Message);
                }
            }

            _initialized = true;
            _log.Information("Plugin manager initialized with {Count} plugins ({Enabled} enabled)",
                _plugins.Count, EnabledPlugins.Count);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PluginManagementResult> LoadPluginAsync(string assemblyPath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await _loader.LoadPluginAsync(assemblyPath, null, ct).ConfigureAwait(false);

            if (!result.Success || result.Plugin == null)
            {
                return PluginManagementResult.Failed(result.ErrorMessage ?? "Unknown error");
            }

            var managedPlugin = new ManagedPlugin
            {
                PluginId = result.Plugin.PluginId,
                AssemblyPath = assemblyPath,
                LoadedPlugin = result.Plugin,
                IsEnabled = true
            };

            _plugins[managedPlugin.PluginId] = managedPlugin;
            EmitStateChange(managedPlugin.PluginId, PluginState.Loaded, "Plugin loaded");

            return PluginManagementResult.Succeeded(managedPlugin);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PluginManagementResult> UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryRemove(pluginId, out var plugin))
            {
                return PluginManagementResult.Failed($"Plugin {pluginId} not found");
            }

            var success = await _loader.UnloadPluginAsync(pluginId, ct).ConfigureAwait(false);

            if (!success)
            {
                // Re-add the plugin if unload failed
                _plugins[pluginId] = plugin;
                return PluginManagementResult.Failed($"Failed to unload plugin {pluginId}");
            }

            EmitStateChange(pluginId, PluginState.Unloaded, "Plugin unloaded");
            return PluginManagementResult.Succeeded(plugin);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PluginManagementResult> EnablePluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                return PluginManagementResult.Failed($"Plugin {pluginId} not found");
            }

            if (plugin.IsEnabled)
            {
                return PluginManagementResult.Succeeded(plugin);
            }

            plugin.IsEnabled = true;

            // Initialize the plugin if not already active
            if (plugin.LoadedPlugin?.State == PluginState.Paused)
            {
                plugin.LoadedPlugin.State = PluginState.Active;
                EmitStateChange(pluginId, PluginState.Active, "Plugin enabled");
            }

            return PluginManagementResult.Succeeded(plugin);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PluginManagementResult> DisablePluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                return PluginManagementResult.Failed($"Plugin {pluginId} not found");
            }

            if (!plugin.IsEnabled)
            {
                return PluginManagementResult.Succeeded(plugin);
            }

            plugin.IsEnabled = false;

            if (plugin.LoadedPlugin?.State == PluginState.Active)
            {
                plugin.LoadedPlugin.State = PluginState.Paused;
                EmitStateChange(pluginId, PluginState.Paused, "Plugin disabled");
            }

            return PluginManagementResult.Succeeded(plugin);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PluginManagementResult> ReloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var existingPlugin))
            {
                return PluginManagementResult.Failed($"Plugin {pluginId} not found");
            }

            var wasEnabled = existingPlugin.IsEnabled;
            var assemblyPath = existingPlugin.AssemblyPath;

            EmitStateChange(pluginId, PluginState.Unloading, "Reloading plugin");

            var result = await _loader.ReloadPluginAsync(pluginId, ct).ConfigureAwait(false);

            if (!result.Success || result.Plugin == null)
            {
                return PluginManagementResult.Failed(result.ErrorMessage ?? "Failed to reload plugin");
            }

            var reloadedPlugin = new ManagedPlugin
            {
                PluginId = result.Plugin.PluginId,
                AssemblyPath = assemblyPath,
                LoadedPlugin = result.Plugin,
                IsEnabled = wasEnabled
            };

            _plugins[pluginId] = reloadedPlugin;

            if (wasEnabled)
            {
                await result.Plugin.Instance.InitializeAsync(ct).ConfigureAwait(false);
                result.Plugin.State = PluginState.Active;
            }

            EmitStateChange(pluginId, wasEnabled ? PluginState.Active : PluginState.Paused, "Plugin reloaded");

            return PluginManagementResult.Succeeded(reloadedPlugin);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PluginManagementResult> UpdateConfigurationAsync(
        string pluginId,
        PluginConfiguration config,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var plugin))
            {
                return PluginManagementResult.Failed($"Plugin {pluginId} not found");
            }

            if (plugin.LoadedPlugin == null)
            {
                return PluginManagementResult.Failed($"Plugin {pluginId} is not loaded");
            }

            await plugin.LoadedPlugin.Instance.OnConfigurationChangedAsync(config, ct).ConfigureAwait(false);

            // Update enabled state based on config
            plugin.IsEnabled = config.Enabled;

            return PluginManagementResult.Succeeded(plugin);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc />
    public ManagedPlugin? GetPlugin(string pluginId)
    {
        _plugins.TryGetValue(pluginId, out var plugin);
        return plugin;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PluginManagementResult>> ScanDirectoryAsync(
        string directory,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(directory))
        {
            _log.Warning("Plugin directory does not exist: {Directory}", directory);
            return [];
        }

        _log.Information("Scanning plugin directory: {Directory}", directory);

        var results = await _loader.LoadPluginsFromDirectoryAsync(directory, "*.dll", ct)
            .ConfigureAwait(false);

        var managementResults = new List<PluginManagementResult>();

        foreach (var result in results)
        {
            if (result.Success && result.Plugin != null)
            {
                var managedPlugin = new ManagedPlugin
                {
                    PluginId = result.Plugin.PluginId,
                    AssemblyPath = result.AssemblyPath!,
                    LoadedPlugin = result.Plugin,
                    IsEnabled = true
                };

                _plugins[managedPlugin.PluginId] = managedPlugin;
                managementResults.Add(PluginManagementResult.Succeeded(managedPlugin));
            }
            else
            {
                managementResults.Add(PluginManagementResult.Failed(result.ErrorMessage ?? "Unknown error"));
            }
        }

        return managementResults;
    }

    /// <inheritdoc />
    public PluginSystemStatus GetStatus()
    {
        var plugins = _plugins.Values.ToList();

        return new PluginSystemStatus
        {
            TotalPlugins = plugins.Count,
            EnabledPlugins = plugins.Count(p => p.IsEnabled),
            ActivePlugins = plugins.Count(p => p.LoadedPlugin?.State == PluginState.Active),
            ErrorPlugins = plugins.Count(p => p.LoadedPlugin?.State == PluginState.Error),
            PluginDirectory = _options.PluginDirectory,
            HostVersion = _loader.HostVersion,
            IsInitialized = _initialized,
            DirectoryWatchingEnabled = _options.EnableDirectoryWatching && _directoryWatcher != null
        };
    }

    #region Event Handlers

    private void OnPluginLoaded(object? sender, PluginLoadedEventArgs e)
    {
        _log.Debug("Plugin loaded event: {PluginId}", e.Plugin.PluginId);
    }

    private void OnPluginUnloaded(object? sender, PluginUnloadedEventArgs e)
    {
        _log.Debug("Plugin unloaded event: {PluginId}", e.PluginId);
    }

    private void OnPluginLoadFailed(object? sender, PluginLoadFailedEventArgs e)
    {
        _log.Warning("Plugin load failed: {Path} - {Error}", e.AssemblyPath, e.ErrorMessage);
    }

    private async void OnPluginFileChanged(object? sender, FileSystemEventArgs e)
    {
        if (!_options.EnableHotReload) return;

        // Debounce file changes
        await Task.Delay(_options.HotReloadDebounce).ConfigureAwait(false);

        // Find the plugin that uses this assembly
        var plugin = _plugins.Values.FirstOrDefault(p =>
            string.Equals(p.AssemblyPath, e.FullPath, StringComparison.OrdinalIgnoreCase));

        if (plugin != null)
        {
            _log.Information("Detected change in plugin assembly, reloading: {PluginId}", plugin.PluginId);
            await ReloadPluginAsync(plugin.PluginId).ConfigureAwait(false);
        }
    }

    private async void OnPluginFileCreated(object? sender, FileSystemEventArgs e)
    {
        if (!_options.AutoLoadNewPlugins) return;

        // Debounce file creation
        await Task.Delay(_options.HotReloadDebounce).ConfigureAwait(false);

        _log.Information("Detected new plugin assembly: {Path}", e.FullPath);
        await LoadPluginAsync(e.FullPath).ConfigureAwait(false);
    }

    #endregion

    private void EmitStateChange(string pluginId, PluginState newState, string? reason = null)
    {
        _stateChanges.OnNext(new PluginStateChange
        {
            PluginId = pluginId,
            NewState = newState,
            Reason = reason,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _directoryWatcher?.Dispose();

        // Unload all plugins
        foreach (var plugin in _plugins.Values)
        {
            try
            {
                if (plugin.LoadedPlugin != null)
                {
                    await _loader.UnloadPluginAsync(plugin.PluginId).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        _plugins.Clear();
        _stateChanges.OnCompleted();
        _stateChanges.Dispose();
        _operationLock.Dispose();

        // Dispose the loader if it implements IDisposable
        if (_loader is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Represents a managed plugin with state and configuration.
/// </summary>
public sealed class ManagedPlugin
{
    /// <summary>
    /// Plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Path to the plugin assembly.
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// The loaded plugin, if currently loaded.
    /// </summary>
    public LoadedPlugin? LoadedPlugin { get; set; }

    /// <summary>
    /// Whether the plugin is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// When the plugin was first loaded.
    /// </summary>
    public DateTimeOffset FirstLoadedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the plugin was last reloaded.
    /// </summary>
    public DateTimeOffset? LastReloadedAt { get; set; }
}

/// <summary>
/// Result of a plugin management operation.
/// </summary>
public sealed record PluginManagementResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The managed plugin, if applicable.
    /// </summary>
    public ManagedPlugin? Plugin { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PluginManagementResult Succeeded(ManagedPlugin plugin) => new()
    {
        Success = true,
        Plugin = plugin
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static PluginManagementResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}

/// <summary>
/// Event data for plugin state changes.
/// </summary>
public sealed record PluginStateChange
{
    /// <summary>
    /// Plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// New plugin state.
    /// </summary>
    public required PluginState NewState { get; init; }

    /// <summary>
    /// Reason for the state change.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Overall status of the plugin system.
/// </summary>
public sealed record PluginSystemStatus
{
    /// <summary>
    /// Total number of managed plugins.
    /// </summary>
    public int TotalPlugins { get; init; }

    /// <summary>
    /// Number of enabled plugins.
    /// </summary>
    public int EnabledPlugins { get; init; }

    /// <summary>
    /// Number of active plugins.
    /// </summary>
    public int ActivePlugins { get; init; }

    /// <summary>
    /// Number of plugins in error state.
    /// </summary>
    public int ErrorPlugins { get; init; }

    /// <summary>
    /// Plugin directory path.
    /// </summary>
    public string? PluginDirectory { get; init; }

    /// <summary>
    /// Host version.
    /// </summary>
    public Version? HostVersion { get; init; }

    /// <summary>
    /// Whether the plugin manager is initialized.
    /// </summary>
    public bool IsInitialized { get; init; }

    /// <summary>
    /// Whether directory watching is enabled.
    /// </summary>
    public bool DirectoryWatchingEnabled { get; init; }
}

/// <summary>
/// Configuration options for the plugin manager.
/// </summary>
public sealed record PluginManagerOptions
{
    /// <summary>
    /// Primary plugin directory.
    /// </summary>
    public string PluginDirectory { get; init; } = Path.Combine(
        AppContext.BaseDirectory,
        "plugins");

    /// <summary>
    /// Additional plugin directories to scan.
    /// </summary>
    public IReadOnlyList<string> PluginDirectories { get; init; } = [];

    /// <summary>
    /// Whether to watch the plugin directory for changes.
    /// </summary>
    public bool EnableDirectoryWatching { get; init; } = true;

    /// <summary>
    /// Whether to enable hot reload when plugin files change.
    /// </summary>
    public bool EnableHotReload { get; init; } = true;

    /// <summary>
    /// Whether to automatically load new plugins added to the directory.
    /// </summary>
    public bool AutoLoadNewPlugins { get; init; } = true;

    /// <summary>
    /// Debounce time for hot reload file system events.
    /// </summary>
    public TimeSpan HotReloadDebounce { get; init; } = TimeSpan.FromSeconds(2);
}
