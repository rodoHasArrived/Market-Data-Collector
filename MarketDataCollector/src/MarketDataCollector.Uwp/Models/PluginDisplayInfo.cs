using CommunityToolkit.Mvvm.ComponentModel;
using MarketDataCollector.Infrastructure.DataSources.Plugins;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// UI-friendly display model for a loaded plugin.
/// </summary>
public partial class PluginDisplayInfo : ObservableObject
{
    /// <summary>
    /// Unique plugin ID.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Version string.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Path to the plugin assembly.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Data source type (Realtime, Historical, Hybrid).
    /// </summary>
    public string DataSourceType { get; set; } = "Unknown";

    /// <summary>
    /// Data source category (Exchange, Broker, Free, etc.).
    /// </summary>
    public string Category { get; set; } = "Unknown";

    /// <summary>
    /// Current plugin state.
    /// </summary>
    [ObservableProperty]
    private string _status = "Unknown";

    /// <summary>
    /// Whether the plugin is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Color/style for the status badge.
    /// </summary>
    public string StatusColor => Status switch
    {
        "Active" => "#48BB78",      // Green
        "Paused" => "#ED8936",      // Orange
        "Error" => "#F56565",       // Red
        "Loading" => "#4299E1",     // Blue
        "Unloaded" => "#A0AEC0",    // Gray
        _ => "#A0AEC0"
    };

    /// <summary>
    /// Required permissions display text.
    /// </summary>
    public string PermissionsText { get; set; } = "None";

    /// <summary>
    /// When the plugin was loaded.
    /// </summary>
    public DateTimeOffset LoadedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Priority for data source selection.
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Creates a PluginDisplayInfo from a ManagedPlugin.
    /// </summary>
    public static PluginDisplayInfo FromManagedPlugin(ManagedPlugin plugin)
    {
        var loaded = plugin.LoadedPlugin;
        var metadata = loaded?.Metadata;

        return new PluginDisplayInfo
        {
            PluginId = plugin.PluginId,
            Name = metadata?.Name ?? plugin.PluginId,
            Version = metadata?.Version?.ToString() ?? "1.0.0",
            Author = metadata?.Author,
            Description = metadata?.Description,
            AssemblyPath = plugin.AssemblyPath,
            DataSourceType = loaded?.Instance?.Type.ToString() ?? "Unknown",
            Category = loaded?.Instance?.Category.ToString() ?? "Unknown",
            Status = loaded?.State.ToString() ?? "Unknown",
            IsEnabled = plugin.IsEnabled,
            PermissionsText = FormatPermissions(metadata?.RequiredPermissions ?? PluginPermissions.None),
            LoadedAt = loaded?.LoadedAt ?? plugin.FirstLoadedAt,
            Priority = loaded?.Instance?.Priority ?? 100
        };
    }

    private static string FormatPermissions(PluginPermissions permissions)
    {
        if (permissions == PluginPermissions.None)
            return "None";

        if (permissions == PluginPermissions.FullTrust)
            return "Full Trust";

        var parts = new List<string>();

        if (permissions.HasFlag(PluginPermissions.Network))
            parts.Add("Network");
        if (permissions.HasFlag(PluginPermissions.FileSystem))
            parts.Add("File System");
        if (permissions.HasFlag(PluginPermissions.Environment))
            parts.Add("Environment");
        if (permissions.HasFlag(PluginPermissions.Credentials))
            parts.Add("Credentials");
        if (permissions.HasFlag(PluginPermissions.NativeCode))
            parts.Add("Native Code");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Result of a plugin installation attempt.
/// </summary>
public record PluginInstallResult
{
    public bool Success { get; init; }
    public string? PluginId { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FilePath { get; init; }
}
