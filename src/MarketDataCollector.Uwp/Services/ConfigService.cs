using System.Text.Json;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigService()
    {
        // Look for config in app directory or parent directories
        var baseDir = AppContext.BaseDirectory;
        var configName = "appsettings.json";

        // Try multiple locations
        var paths = new[]
        {
            Path.Combine(baseDir, configName),
            Path.Combine(baseDir, "..", configName),
            Path.Combine(baseDir, "..", "..", configName),
            Path.Combine(baseDir, "..", "..", "..", "..", configName)
        };

        ConfigPath = paths.FirstOrDefault(File.Exists) ?? paths[0];
    }

    public async Task<AppConfig?> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = await File.ReadAllTextAsync(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    public async Task SaveDataSourceAsync(string dataSource)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.DataSource = dataSource;
        await SaveConfigAsync(config);
    }

    public async Task SaveAlpacaOptionsAsync(AlpacaOptions options)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.Alpaca = options;
        await SaveConfigAsync(config);
    }

    public async Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.DataRoot = dataRoot;
        config.Compress = compress;
        config.Storage = storage;
        await SaveConfigAsync(config);
    }

    public async Task AddOrUpdateSymbolAsync(SymbolConfig symbol)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();

        var existingIndex = symbols.FindIndex(s =>
            string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            symbols[existingIndex] = symbol;
        }
        else
        {
            symbols.Add(symbol);
        }

        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config);
    }

    public async Task DeleteSymbolAsync(string symbol)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();

        symbols.RemoveAll(s =>
            string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Gets all configured data sources.
    /// </summary>
    public async Task<DataSourceConfig[]> GetDataSourcesAsync()
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        return config.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
    }

    /// <summary>
    /// Gets the data sources configuration.
    /// </summary>
    public async Task<DataSourcesConfig> GetDataSourcesConfigAsync()
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        return config.DataSources ?? new DataSourcesConfig();
    }

    /// <summary>
    /// Adds or updates a data source configuration.
    /// </summary>
    public async Task AddOrUpdateDataSourceAsync(DataSourceConfig dataSource)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();

        var existingIndex = sources.FindIndex(s =>
            string.Equals(s.Id, dataSource.Id, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            sources[existingIndex] = dataSource;
        }
        else
        {
            sources.Add(dataSource);
        }

        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Deletes a data source by ID.
    /// </summary>
    public async Task DeleteDataSourceAsync(string id)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();

        sources.RemoveAll(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Sets the default data source for real-time or historical data.
    /// </summary>
    public async Task SetDefaultDataSourceAsync(string id, bool isHistorical)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();

        if (isHistorical)
        {
            dataSources.DefaultHistoricalSourceId = id;
        }
        else
        {
            dataSources.DefaultRealTimeSourceId = id;
        }

        config.DataSources = dataSources;
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Toggles a data source's enabled state.
    /// </summary>
    public async Task ToggleDataSourceAsync(string id, bool enabled)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();

        var source = sources.FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        if (source != null)
        {
            source.Enabled = enabled;
            dataSources.Sources = sources.ToArray();
            config.DataSources = dataSources;
            await SaveConfigAsync(config);
        }
    }

    /// <summary>
    /// Updates failover settings for data sources.
    /// </summary>
    public async Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();

        dataSources.EnableFailover = enableFailover;
        dataSources.FailoverTimeoutSeconds = failoverTimeoutSeconds;

        config.DataSources = dataSources;
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Gets the app settings including service URL configuration.
    /// </summary>
    public async Task<AppSettings> GetAppSettingsAsync()
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        return config.Settings ?? new AppSettings();
    }

    /// <summary>
    /// Saves app settings including service URL configuration.
    /// </summary>
    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.Settings = settings;
        await SaveConfigAsync(config);

        // Configure the API client with new settings
        ApiClientService.Instance.Configure(settings);
    }

    /// <summary>
    /// Updates the service URL configuration.
    /// </summary>
    public async Task UpdateServiceUrlAsync(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var settings = config.Settings ?? new AppSettings();

        settings.ServiceUrl = serviceUrl;
        settings.ServiceTimeoutSeconds = timeoutSeconds;
        settings.BackfillTimeoutMinutes = backfillTimeoutMinutes;

        config.Settings = settings;
        await SaveConfigAsync(config);

        // Configure the API client with new settings
        ApiClientService.Instance.Configure(settings);
    }

    /// <summary>
    /// Loads configuration and initializes services with configured URLs.
    /// Should be called during app startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        var config = await LoadConfigAsync();
        if (config?.Settings != null)
        {
            // Configure the API client with loaded settings
            ApiClientService.Instance.Configure(config.Settings);
        }
    }
}
