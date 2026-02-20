using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Services.Contracts;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default configuration service for the shared UI services layer.
/// Provides basic config loading/saving from the standard appsettings path.
/// Platform-specific projects may override this by setting the Instance property.
/// </summary>
public class ConfigService : IConfigService
{
    private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());

    public static ConfigService Instance => _instance.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ConfigPath { get; }

    public ConfigService()
    {
        ConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
    }

    public virtual async Task<AppConfig?> LoadConfigAsync(CancellationToken ct = default)
    {
        if (!File.Exists(ConfigPath)) return null;
        var json = await File.ReadAllTextAsync(ConfigPath, ct);
        return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
    }

    public virtual async Task SaveConfigAsync(AppConfig config, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (dir != null) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json, ct);
    }

    public virtual Task SaveDataSourceAsync(string dataSource, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task SaveAlpacaOptionsAsync(AlpacaOptions options, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task AddOrUpdateSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task AddSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
        => AddOrUpdateSymbolAsync(symbol, ct);

    public virtual Task DeleteSymbolAsync(string symbol, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task<DataSourceConfig[]> GetDataSourcesAsync(CancellationToken ct = default)
        => Task.FromResult(Array.Empty<DataSourceConfig>());

    public virtual Task<DataSourcesConfig> GetDataSourcesConfigAsync(CancellationToken ct = default)
        => Task.FromResult(new DataSourcesConfig());

    public virtual Task AddOrUpdateDataSourceAsync(DataSourceConfig dataSource, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task DeleteDataSourceAsync(string id, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task SetDefaultDataSourceAsync(string id, bool isHistorical, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task ToggleDataSourceAsync(string id, bool enabled, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
        => Task.FromResult(new AppSettings());

    public virtual Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task UpdateServiceUrlAsync(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task<ConfigValidationResult> ValidateConfigAsync(CancellationToken ct = default)
        => Task.FromResult(new ConfigValidationResult { IsValid = true });
}
