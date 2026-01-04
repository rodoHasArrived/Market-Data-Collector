namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Interface for data source plugins that can be dynamically loaded and managed.
/// Extends IDataSource with plugin-specific lifecycle and metadata.
/// </summary>
public interface IDataSourcePlugin : IDataSource
{
    /// <summary>
    /// Gets the plugin metadata including version and dependencies.
    /// </summary>
    PluginMetadata PluginInfo { get; }

    /// <summary>
    /// Called when the plugin is being loaded.
    /// Use this for any initialization that needs to happen before the plugin is used.
    /// </summary>
    Task OnLoadAsync(PluginContext context, CancellationToken ct = default);

    /// <summary>
    /// Called when the plugin is being unloaded.
    /// Use this for cleanup before the plugin is removed.
    /// </summary>
    Task OnUnloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Called when the plugin configuration has changed.
    /// </summary>
    Task OnConfigurationChangedAsync(PluginConfiguration newConfig, CancellationToken ct = default);
}

/// <summary>
/// Metadata describing a data source plugin.
/// </summary>
public sealed record PluginMetadata
{
    /// <summary>
    /// Unique plugin identifier.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Human-readable plugin name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Plugin version following semantic versioning.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Plugin author or organization.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// URL for plugin documentation or support.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Minimum host version required.
    /// </summary>
    public Version? MinHostVersion { get; init; }

    /// <summary>
    /// Maximum host version supported (exclusive).
    /// </summary>
    public Version? MaxHostVersion { get; init; }

    /// <summary>
    /// Required dependencies (plugin IDs and minimum versions).
    /// </summary>
    public IReadOnlyDictionary<string, Version>? Dependencies { get; init; }

    /// <summary>
    /// Tags for categorization and discovery.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// License identifier (e.g., "MIT", "Apache-2.0").
    /// </summary>
    public string? License { get; init; }

    /// <summary>
    /// Whether this plugin requires special permissions.
    /// </summary>
    public PluginPermissions RequiredPermissions { get; init; } = PluginPermissions.None;
}

/// <summary>
/// Permissions that a plugin may require.
/// </summary>
[Flags]
public enum PluginPermissions
{
    None = 0,

    /// <summary>Access to network resources.</summary>
    Network = 1 << 0,

    /// <summary>Access to file system.</summary>
    FileSystem = 1 << 1,

    /// <summary>Access to environment variables.</summary>
    Environment = 1 << 2,

    /// <summary>Access to native code.</summary>
    NativeCode = 1 << 3,

    /// <summary>Access to credentials/secrets.</summary>
    Credentials = 1 << 4,

    /// <summary>Full trust (all permissions).</summary>
    FullTrust = Network | FileSystem | Environment | NativeCode | Credentials
}

/// <summary>
/// Context provided to plugins during lifecycle events.
/// </summary>
public sealed class PluginContext
{
    /// <summary>
    /// Service provider for resolving dependencies.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public required PluginConfiguration Configuration { get; init; }

    /// <summary>
    /// Path to the plugin's assembly.
    /// </summary>
    public required string PluginPath { get; init; }

    /// <summary>
    /// Path to the plugin's data directory for storing state.
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>
    /// The plugin loader that loaded this plugin.
    /// </summary>
    public required IDataSourcePluginLoader Loader { get; init; }

    /// <summary>
    /// Host version for compatibility checks.
    /// </summary>
    public required Version HostVersion { get; init; }

    /// <summary>
    /// Permissions granted to this plugin.
    /// </summary>
    public PluginPermissions GrantedPermissions { get; init; } = PluginPermissions.FullTrust;

    /// <summary>
    /// Logger for the plugin to use.
    /// </summary>
    public Serilog.ILogger? Logger { get; init; }
}

/// <summary>
/// Configuration specific to a plugin instance.
/// </summary>
public sealed record PluginConfiguration
{
    /// <summary>
    /// Whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Priority override for the plugin.
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Plugin-specific settings.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Settings { get; init; }

    /// <summary>
    /// Data source options override.
    /// </summary>
    public DataSourceOptions? SourceOptions { get; init; }

    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    public T? GetSetting<T>(string key, T? defaultValue = default)
    {
        if (Settings == null || !Settings.TryGetValue(key, out var value))
            return defaultValue;

        if (value is T typedValue)
            return typedValue;

        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}

/// <summary>
/// Attribute to mark a class as a data source plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataSourcePluginAttribute : DataSourceAttribute
{
    /// <summary>
    /// Plugin version in semantic versioning format.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Minimum host version required.
    /// </summary>
    public string? MinHostVersion { get; set; }

    /// <summary>
    /// Maximum host version supported.
    /// </summary>
    public string? MaxHostVersion { get; set; }

    /// <summary>
    /// Required permissions.
    /// </summary>
    public PluginPermissions RequiredPermissions { get; set; } = PluginPermissions.Network;

    /// <summary>
    /// Creates a new DataSourcePluginAttribute.
    /// </summary>
    public DataSourcePluginAttribute(
        string id,
        string displayName,
        DataSourceType type,
        DataSourceCategory category)
        : base(id, displayName, type, category)
    {
    }
}

/// <summary>
/// Extension methods for plugin metadata.
/// </summary>
public static class PluginMetadataExtensions
{
    /// <summary>
    /// Gets PluginMetadata from a DataSourcePluginAttribute.
    /// </summary>
    public static PluginMetadata ToPluginMetadata(this DataSourcePluginAttribute attr, Type implementationType)
    {
        return new PluginMetadata
        {
            PluginId = attr.Id,
            Name = attr.DisplayName,
            Version = System.Version.Parse(attr.Version),
            Author = attr.Author,
            Description = attr.Description,
            MinHostVersion = attr.MinHostVersion != null ? System.Version.Parse(attr.MinHostVersion) : null,
            MaxHostVersion = attr.MaxHostVersion != null ? System.Version.Parse(attr.MaxHostVersion) : null,
            RequiredPermissions = attr.RequiredPermissions
        };
    }

    /// <summary>
    /// Checks if a plugin is compatible with the given host version.
    /// </summary>
    public static bool IsCompatibleWith(this PluginMetadata metadata, Version hostVersion)
    {
        if (metadata.MinHostVersion != null && hostVersion < metadata.MinHostVersion)
            return false;

        if (metadata.MaxHostVersion != null && hostVersion >= metadata.MaxHostVersion)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the DataSourcePluginAttribute from a type.
    /// </summary>
    public static DataSourcePluginAttribute? GetPluginAttribute(this Type type)
    {
        return Attribute.GetCustomAttribute(type, typeof(DataSourcePluginAttribute)) as DataSourcePluginAttribute;
    }

    /// <summary>
    /// Checks if a type is a data source plugin.
    /// </summary>
    public static bool IsDataSourcePlugin(this Type type)
    {
        return type.GetPluginAttribute() != null
            && typeof(IDataSourcePlugin).IsAssignableFrom(type)
            && !type.IsAbstract
            && !type.IsInterface;
    }
}
