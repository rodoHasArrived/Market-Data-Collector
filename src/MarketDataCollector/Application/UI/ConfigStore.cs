using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Storage;

namespace MarketDataCollector.Application.UI;

public sealed class ConfigStore
{
    public string ConfigPath { get; }

    public ConfigStore(string? configPath = null)
    {
        ConfigPath = configPath ?? "appsettings.json";
        if (!Path.IsPathRooted(ConfigPath))
        {
            ConfigPath = Path.GetFullPath(ConfigPath);
        }
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Console.WriteLine($"[Warning] Configuration file not found: {ConfigPath}");
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to load configuration: {ex.Message}");
            return new AppConfig();
        }
    }

    public async Task SaveAsync(AppConfig cfg)
    {
        try
        {
            var json = JsonSerializer.Serialize(cfg, AppConfigJsonOptions.Write);
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(ConfigPath, json);
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
