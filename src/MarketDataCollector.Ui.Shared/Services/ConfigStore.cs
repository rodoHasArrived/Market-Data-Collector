using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;

namespace MarketDataCollector.Ui.Shared.Services;

/// <summary>
/// Service for loading and persisting application configuration.
/// Provides access to config files, status files, and provider metrics.
/// Shared between web dashboard and desktop applications.
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
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// Creates a new ConfigStore with the default configuration path.
    /// Config lives at solution root by convention.
    /// </summary>
    public ConfigStore()
    {
        _configService = new ConfigurationService();
        ConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
    }

    /// <summary>
    /// Creates a new ConfigStore with a custom configuration path.
    /// </summary>
    /// <param name="configPath">Full path to the configuration file.</param>
    public ConfigStore(string configPath)
    {
        _configService = new ConfigurationService();
        ConfigPath = configPath;
    }

    /// <summary>
    /// Creates a new ConfigStore with a custom configuration path and service.
    /// </summary>
    /// <param name="configPath">Full path to the configuration file.</param>
    /// <param name="configService">ConfigurationService instance to use.</param>
    public ConfigStore(string configPath, ConfigurationService configService)
    {
        _configService = configService;
        ConfigPath = configPath;
    }

    /// <summary>
    /// Loads the application configuration from disk using ConfigurationService.
    /// Applies environment overlays and MDC_* environment variable overrides.
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            return _configService.Load(ConfigPath);
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <summary>
    /// Saves the application configuration to disk using ConfigurationService.
    /// </summary>
    public async Task SaveAsync(AppConfig cfg)
    {
        await _configService.SaveAsync(cfg, ConfigPath);
    }

    /// <summary>
    /// Tries to load the status JSON file contents.
    /// </summary>
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

    /// <summary>
    /// Gets the path to the status file.
    /// </summary>
    public string GetStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "status.json");
    }

    /// <summary>
    /// Gets the path to the backfill status file.
    /// </summary>
    public string GetBackfillStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "backfill.json");
    }

    /// <summary>
    /// Tries to load the backfill status from disk.
    /// </summary>
    public BackfillResult? TryLoadBackfillStatus()
    {
        var cfg = Load();
        var store = new BackfillStatusStore(GetDataRoot(cfg));
        return store.TryRead();
    }

    /// <summary>
    /// Gets the data root directory path.
    /// </summary>
    public string GetDataRoot(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = string.IsNullOrWhiteSpace(cfg.DataRoot) ? "data" : cfg.DataRoot;
        var baseDir = Path.GetDirectoryName(ConfigPath)!;
        return Path.GetFullPath(Path.Combine(baseDir, root));
    }

    /// <summary>
    /// Tries to load provider metrics from the status file.
    /// </summary>
    public ProviderMetricsStatus? TryLoadProviderMetrics()
    {
        try
        {
            var cfg = Load();
            var root = GetDataRoot(cfg);
            var metricsPath = Path.Combine(root, "_status", "provider_metrics.json");

            if (!File.Exists(metricsPath)) return null;

            var json = File.ReadAllText(metricsPath);
            return JsonSerializer.Deserialize<ProviderMetricsStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
