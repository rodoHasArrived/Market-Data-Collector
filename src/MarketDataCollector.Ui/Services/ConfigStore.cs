using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Monitoring;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Service for loading and persisting application configuration.
/// Provides access to config files, status files, and provider metrics.
/// </summary>
public sealed class ConfigStore
{
    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigPath { get; }

    public ConfigStore()
    {
        // Config lives at solution root by convention.
        ConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
    }

    /// <summary>
    /// Loads the application configuration from disk.
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <summary>
    /// Saves the application configuration to disk.
    /// </summary>
    public async Task SaveAsync(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, AppConfigJsonOptions.Write);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        await File.WriteAllTextAsync(ConfigPath, json);
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
