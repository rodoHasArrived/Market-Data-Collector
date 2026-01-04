using System.Reflection;

namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Interface for loading data source plugins from assemblies.
/// </summary>
public interface IDataSourcePluginLoader
{
    /// <summary>
    /// Gets the host version for compatibility checks.
    /// </summary>
    Version HostVersion { get; }

    /// <summary>
    /// Loads a plugin from the specified assembly path.
    /// </summary>
    /// <param name="assemblyPath">Path to the plugin assembly.</param>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded plugin result.</returns>
    Task<PluginLoadResult> LoadPluginAsync(
        string assemblyPath,
        PluginConfiguration? configuration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Loads all plugins from the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin assemblies.</param>
    /// <param name="searchPattern">File search pattern (default: "*.dll").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Results for all discovered plugins.</returns>
    Task<IReadOnlyList<PluginLoadResult>> LoadPluginsFromDirectoryAsync(
        string pluginDirectory,
        string searchPattern = "*.dll",
        CancellationToken ct = default);

    /// <summary>
    /// Unloads a plugin by its ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the plugin was unloaded successfully.</returns>
    Task<bool> UnloadPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Reloads a plugin by its ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID to reload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reload result.</returns>
    Task<PluginLoadResult> ReloadPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    IReadOnlyList<LoadedPlugin> LoadedPlugins { get; }

    /// <summary>
    /// Gets a loaded plugin by ID.
    /// </summary>
    LoadedPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// Validates a plugin assembly without loading it.
    /// </summary>
    /// <param name="assemblyPath">Path to the plugin assembly.</param>
    /// <returns>Validation result.</returns>
    PluginValidationResult ValidatePlugin(string assemblyPath);

    /// <summary>
    /// Event raised when a plugin is loaded.
    /// </summary>
    event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <summary>
    /// Event raised when a plugin is unloaded.
    /// </summary>
    event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <summary>
    /// Event raised when plugin loading fails.
    /// </summary>
    event EventHandler<PluginLoadFailedEventArgs>? PluginLoadFailed;
}

/// <summary>
/// Result of a plugin load operation.
/// </summary>
public sealed record PluginLoadResult
{
    /// <summary>
    /// Whether the load was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The loaded plugin, if successful.
    /// </summary>
    public LoadedPlugin? Plugin { get; init; }

    /// <summary>
    /// Error message if the load failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception that occurred during loading.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Validation result.
    /// </summary>
    public PluginValidationResult? ValidationResult { get; init; }

    /// <summary>
    /// Assembly path that was loaded.
    /// </summary>
    public string? AssemblyPath { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PluginLoadResult Succeeded(LoadedPlugin plugin, string assemblyPath) => new()
    {
        Success = true,
        Plugin = plugin,
        AssemblyPath = assemblyPath
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static PluginLoadResult Failed(string message, Exception? ex = null, string? assemblyPath = null) => new()
    {
        Success = false,
        ErrorMessage = message,
        Exception = ex,
        AssemblyPath = assemblyPath
    };

    /// <summary>
    /// Creates a failed result from validation.
    /// </summary>
    public static PluginLoadResult FailedValidation(PluginValidationResult validation, string assemblyPath) => new()
    {
        Success = false,
        ErrorMessage = $"Plugin validation failed: {string.Join(", ", validation.Errors)}",
        ValidationResult = validation,
        AssemblyPath = assemblyPath
    };
}

/// <summary>
/// Represents a loaded plugin.
/// </summary>
public sealed class LoadedPlugin
{
    /// <summary>
    /// The plugin instance.
    /// </summary>
    public required IDataSourcePlugin Instance { get; init; }

    /// <summary>
    /// Plugin metadata.
    /// </summary>
    public required PluginMetadata Metadata { get; init; }

    /// <summary>
    /// Plugin context.
    /// </summary>
    public required PluginContext Context { get; init; }

    /// <summary>
    /// The assembly containing the plugin.
    /// </summary>
    public required Assembly Assembly { get; init; }

    /// <summary>
    /// Path to the plugin assembly.
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// When the plugin was loaded.
    /// </summary>
    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Current plugin state.
    /// </summary>
    public PluginState State { get; set; } = PluginState.Loaded;

    /// <summary>
    /// Plugin ID shorthand.
    /// </summary>
    public string PluginId => Metadata.PluginId;
}

/// <summary>
/// State of a loaded plugin.
/// </summary>
public enum PluginState
{
    /// <summary>Plugin is loaded but not yet initialized.</summary>
    Loaded,

    /// <summary>Plugin is initializing.</summary>
    Initializing,

    /// <summary>Plugin is active and ready for use.</summary>
    Active,

    /// <summary>Plugin is paused/disabled temporarily.</summary>
    Paused,

    /// <summary>Plugin encountered an error.</summary>
    Error,

    /// <summary>Plugin is being unloaded.</summary>
    Unloading,

    /// <summary>Plugin has been unloaded.</summary>
    Unloaded
}

/// <summary>
/// Result of plugin validation.
/// </summary>
public sealed record PluginValidationResult
{
    /// <summary>
    /// Whether the plugin is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Discovered plugin types in the assembly.
    /// </summary>
    public IReadOnlyList<Type> DiscoveredPluginTypes { get; init; } = [];

    /// <summary>
    /// Plugin metadata if successfully extracted.
    /// </summary>
    public IReadOnlyList<PluginMetadata> DiscoveredMetadata { get; init; } = [];

    /// <summary>
    /// Assembly name.
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>
    /// Assembly version.
    /// </summary>
    public Version? AssemblyVersion { get; init; }

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static PluginValidationResult Valid(
        IReadOnlyList<Type> types,
        IReadOnlyList<PluginMetadata> metadata,
        string assemblyName,
        Version? version,
        IReadOnlyList<string>? warnings = null) => new()
    {
        DiscoveredPluginTypes = types,
        DiscoveredMetadata = metadata,
        AssemblyName = assemblyName,
        AssemblyVersion = version,
        Warnings = warnings ?? []
    };

    /// <summary>
    /// Creates an invalid result.
    /// </summary>
    public static PluginValidationResult Invalid(IReadOnlyList<string> errors, string? assemblyName = null) => new()
    {
        Errors = errors,
        AssemblyName = assemblyName
    };
}

#region Event Args

/// <summary>
/// Event args for plugin loaded event.
/// </summary>
public sealed class PluginLoadedEventArgs : EventArgs
{
    /// <summary>
    /// The loaded plugin.
    /// </summary>
    public required LoadedPlugin Plugin { get; init; }

    /// <summary>
    /// Time taken to load the plugin.
    /// </summary>
    public TimeSpan LoadTime { get; init; }
}

/// <summary>
/// Event args for plugin unloaded event.
/// </summary>
public sealed class PluginUnloadedEventArgs : EventArgs
{
    /// <summary>
    /// ID of the unloaded plugin.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Reason for unloading.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Event args for plugin load failed event.
/// </summary>
public sealed class PluginLoadFailedEventArgs : EventArgs
{
    /// <summary>
    /// Path to the assembly that failed to load.
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Exception that occurred.
    /// </summary>
    public Exception? Exception { get; init; }
}

#endregion
