// Re-export the consolidated ConfigStore from the core project.
// This preserves backward compatibility for existing code referencing this namespace.
using CoreConfigStore = MarketDataCollector.Application.UI.ConfigStore;

namespace MarketDataCollector.Ui.Shared.Services;

/// <summary>
/// ConfigStore for web dashboard and shared UI services.
/// This is a wrapper around the consolidated core ConfigStore implementation.
/// </summary>
/// <remarks>
/// <para><b>Migration Note:</b> This wrapper exists for backward compatibility.
/// New code should reference <see cref="MarketDataCollector.Application.UI.ConfigStore"/> directly.</para>
/// <para>The web-specific default path resolver is registered via <see cref="ConfigStoreExtensions.UseWebDefaultPath"/>.</para>
/// </remarks>
public sealed class ConfigStore
{
    private readonly CoreConfigStore _core;

    /// <summary>
    /// Creates a new ConfigStore with the web dashboard default path.
    /// The default path resolves to appsettings.json at solution root (4 directories up from BaseDirectory).
    /// </summary>
    public ConfigStore() : this(GetWebDefaultPath())
    {
    }

    /// <summary>
    /// Creates a new ConfigStore with a custom configuration path.
    /// </summary>
    /// <param name="configPath">Full path to the configuration file.</param>
    public ConfigStore(string? configPath)
    {
        _core = new CoreConfigStore(configPath);
    }

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigPath => _core.ConfigPath;

    /// <summary>
    /// Loads configuration from the config file.
    /// </summary>
    public static MarketDataCollector.Storage.AppConfig LoadConfig(string path) => CoreConfigStore.LoadConfig(path);
    {
    }

    /// <summary>
    /// Gets the default configuration path for web dashboard hosting.
    /// Config lives at solution root by convention (4 directories up from bin output).
    /// </summary>
    private static string GetWebDefaultPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
    }
}

/// <summary>
/// Extension methods for ConfigStore web-specific configuration.
/// </summary>
public static class ConfigStoreExtensions
{
    /// <summary>
    /// Configures the default path resolver for web dashboard hosting.
    /// Call this at startup before any ConfigStore instances are created.
    /// </summary>
    public static void UseWebDefaultPath()
    {
        CoreConfigStore.DefaultPathResolver = () =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
    }
}
