using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Storage;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// Configuration store for UI components. Delegates to ConfigurationService for consistent behavior.
/// </summary>
/// <remarks>
/// This is a thin wrapper around ConfigurationService that provides UI-specific helper methods
/// for accessing status files and data paths. All configuration loading and saving is delegated
/// to the unified ConfigurationService.
/// </remarks>
public sealed class ConfigStore
{
    private readonly ConfigurationService _configService;

    /// <summary>
    /// Gets the configuration file path.
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// Creates a new ConfigStore instance.
    /// </summary>
    /// <param name="configPath">Path to the configuration file. If null, uses default resolution.</param>
    /// <param name="configService">Optional ConfigurationService instance. If null, creates a new one.</param>
    public ConfigStore(string? configPath = null, ConfigurationService? configService = null)
    {
        _configService = configService ?? new ConfigurationService();

        ConfigPath = configPath ?? _configService.ResolveConfigPath();
        if (!Path.IsPathRooted(ConfigPath))
        {
            ConfigPath = Path.GetFullPath(ConfigPath);
        }
    }

    /// <summary>
    /// Loads the configuration using ConfigurationService.
    /// Applies environment overlays and MDC_* environment variable overrides.
    /// </summary>
    /// <returns>The loaded configuration.</returns>
    public AppConfig Load()
    {
        try
        {
            return _configService.Load(ConfigPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to load configuration: {ex.Message}");
            return new AppConfig();
        }
    }

    /// <summary>
    /// Saves the configuration using ConfigurationService.
    /// </summary>
    /// <param name="cfg">Configuration to save.</param>
    public async Task SaveAsync(AppConfig cfg)
    {
        try
        {
            await _configService.SaveAsync(cfg, ConfigPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    public string? TryLoadStatusJson()
    {
        try
        {
            var statusPath = GetStatusPath();
            return File.Exists(statusPath) ? File.ReadAllText(statusPath) : null;
        }
        catch
        {
            return null;
        }
    }

    public string GetStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "status.json");
    }

    public string GetBackfillStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "backfill.json");
    }

    public BackfillResult? TryLoadBackfillStatus()
    {
        var cfg = Load();
        var store = new BackfillStatusStore(GetDataRoot(cfg));
        return store.TryRead();
    }

    public string GetDataRoot(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = string.IsNullOrWhiteSpace(cfg.DataRoot) ? "data" : cfg.DataRoot;
        var baseDir = Path.GetDirectoryName(ConfigPath)!;
        return Path.IsPathRooted(root) ? root : Path.GetFullPath(Path.Combine(baseDir, root));
    }

    public string GetProviderMetricsPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "providers.json");
    }

    public ProviderMetricsStatus? TryLoadProviderMetrics()
    {
        try
        {
            var path = GetProviderMetricsPath();
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProviderMetricsStatus>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return null;
        }
    }
}
