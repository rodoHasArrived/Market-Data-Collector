using System;
using System.Collections.Generic;
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

    public virtual async Task SaveDataSourceAsync(string dataSource, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.DataSource = dataSource;
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task SaveAlpacaOptionsAsync(AlpacaOptions options, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.Alpaca = options;
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.DataRoot = dataRoot;
        config.Compress = compress;
        config.Storage = storage;
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task AddOrUpdateSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();
        var existingIndex = symbols.FindIndex(s =>
            string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            symbols[existingIndex] = symbol;
        else
            symbols.Add(symbol);
        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config, ct);
    }

    public virtual Task AddSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
        => AddOrUpdateSymbolAsync(symbol, ct);

    public virtual async Task DeleteSymbolAsync(string symbol, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();
        symbols.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task<DataSourceConfig[]> GetDataSourcesAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct);
        return config?.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
    }

    public virtual async Task<DataSourcesConfig> GetDataSourcesConfigAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct);
        return config?.DataSources ?? new DataSourcesConfig();
    }

    public virtual async Task AddOrUpdateDataSourceAsync(DataSourceConfig dataSource, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();
        var existingIndex = sources.FindIndex(s =>
            string.Equals(s.Id, dataSource.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            sources[existingIndex] = dataSource;
        else
            sources.Add(dataSource);
        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task DeleteDataSourceAsync(string id, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();
        sources.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task SetDefaultDataSourceAsync(string id, bool isHistorical, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        if (isHistorical)
            dataSources.DefaultHistoricalSourceId = id;
        else
            dataSources.DefaultRealTimeSourceId = id;
        config.DataSources = dataSources;
        await SaveConfigAsync(config, ct);
    }

    public virtual async Task ToggleDataSourceAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var source = dataSources.Sources?.FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (source != null)
        {
            source.Enabled = enabled;
            await SaveConfigAsync(config, ct);
        }
    }

    public virtual async Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        dataSources.EnableFailover = enableFailover;
        dataSources.FailoverTimeoutSeconds = failoverTimeoutSeconds;
        config.DataSources = dataSources;
        await SaveConfigAsync(config, ct);
    }

    public virtual Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
        => Task.FromResult(new AppSettings());

    public virtual Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task UpdateServiceUrlAsync(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60, CancellationToken ct = default)
    {
        ApiClientService.Instance.Configure(serviceUrl, timeoutSeconds, backfillTimeoutMinutes);
        return Task.CompletedTask;
    }

    public virtual Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public virtual Task<ConfigValidationResult> ValidateConfigAsync(CancellationToken ct = default)
        => Task.FromResult(new ConfigValidationResult { IsValid = true });
}
