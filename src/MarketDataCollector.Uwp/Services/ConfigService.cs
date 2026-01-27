using System.Text.Json;
using System.Threading;
using MarketDataCollector.Uwp.Contracts;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing application configuration.
/// Implements <see cref="IConfigService"/> for testability.
/// </summary>
public sealed class ConfigService : IConfigService
{
    private static ConfigService? _instance;
    private static readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static ConfigService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ConfigService();
                }
            }
            return _instance;
        }
    }

    public string ConfigPath { get; }

    private ConfigService()
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

    public async Task<AppConfig?> LoadConfigAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = await File.ReadAllTextAsync(ConfigPath, ct);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveConfigAsync(AppConfig config, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(ConfigPath, json, ct);
    }

    public async Task SaveDataSourceAsync(string dataSource, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.DataSource = dataSource;
        await SaveConfigAsync(config, ct);
    }

    public async Task SaveAlpacaOptionsAsync(AlpacaOptions options, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.Alpaca = options;
        await SaveConfigAsync(config, ct);
    }

    public async Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.DataRoot = dataRoot;
        config.Compress = compress;
        config.Storage = storage;
        await SaveConfigAsync(config, ct);
    }

    public async Task AddOrUpdateSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
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
        await SaveConfigAsync(config, ct);
    }

    public async Task DeleteSymbolAsync(string symbol, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();

        symbols.RemoveAll(s =>
            string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config, ct);
    }

    /// <summary>
    /// Adds a symbol configuration (interface compatibility).
    /// </summary>
    public Task AddSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
        => AddOrUpdateSymbolAsync(symbol, ct);

    /// <summary>
    /// Gets all configured data sources.
    /// </summary>
    public async Task<DataSourceConfig[]> GetDataSourcesAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        return config.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();
    }

    /// <summary>
    /// Gets the data sources configuration.
    /// </summary>
    public async Task<DataSourcesConfig> GetDataSourcesConfigAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        return config.DataSources ?? new DataSourcesConfig();
    }

    /// <summary>
    /// Adds or updates a data source configuration.
    /// </summary>
    public async Task AddOrUpdateDataSourceAsync(DataSourceConfig dataSource, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
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
        await SaveConfigAsync(config, ct);
    }

    /// <summary>
    /// Deletes a data source by ID.
    /// </summary>
    public async Task DeleteDataSourceAsync(string id, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();

        sources.RemoveAll(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigAsync(config, ct);
    }

    /// <summary>
    /// Sets the default data source for real-time or historical data.
    /// </summary>
    public async Task SetDefaultDataSourceAsync(string id, bool isHistorical, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
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
        await SaveConfigAsync(config, ct);
    }

    /// <summary>
    /// Toggles a data source's enabled state.
    /// </summary>
    public async Task ToggleDataSourceAsync(string id, bool enabled, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfig>();

        var source = sources.FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        if (source != null)
        {
            source.Enabled = enabled;
            dataSources.Sources = sources.ToArray();
            config.DataSources = dataSources;
            await SaveConfigAsync(config, ct);
        }
    }

    /// <summary>
    /// Updates failover settings for data sources.
    /// </summary>
    public async Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var dataSources = config.DataSources ?? new DataSourcesConfig();

        dataSources.EnableFailover = enableFailover;
        dataSources.FailoverTimeoutSeconds = failoverTimeoutSeconds;

        config.DataSources = dataSources;
        await SaveConfigAsync(config, ct);
    }

    /// <summary>
    /// Gets the app settings including service URL configuration.
    /// </summary>
    public async Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        return config.Settings ?? new AppSettings();
    }

    /// <summary>
    /// Saves app settings including service URL configuration.
    /// </summary>
    public async Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        config.Settings = settings;
        await SaveConfigAsync(config, ct);

        // Configure the API client with new settings
        ApiClientService.Instance.Configure(settings);
    }

    /// <summary>
    /// Updates the service URL configuration.
    /// </summary>
    public async Task UpdateServiceUrlAsync(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfig();
        var settings = config.Settings ?? new AppSettings();

        settings.ServiceUrl = serviceUrl;
        settings.ServiceTimeoutSeconds = timeoutSeconds;
        settings.BackfillTimeoutMinutes = backfillTimeoutMinutes;

        config.Settings = settings;
        await SaveConfigAsync(config, ct);

        // Configure the API client with new settings
        ApiClientService.Instance.Configure(settings);
    }

    /// <summary>
    /// Loads configuration and initializes services with configured URLs.
    /// Should be called during app startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct);
        if (config?.Settings != null)
        {
            // Configure the API client with loaded settings
            ApiClientService.Instance.Configure(config.Settings);
        }
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    public async Task<ConfigValidationResult> ValidateConfigAsync(CancellationToken ct = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var config = await LoadConfigAsync(ct);

            if (config == null)
            {
                errors.Add("Configuration file could not be loaded");
                return new ConfigValidationResult { IsValid = false, Errors = errors.ToArray(), Warnings = warnings.ToArray() };
            }

            // Validate service URL
            var settings = config.Settings ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(settings.ServiceUrl))
            {
                warnings.Add("Service URL is not configured - using default localhost:8080");
            }
            else if (!Uri.TryCreate(settings.ServiceUrl, UriKind.Absolute, out var uri))
            {
                errors.Add($"Service URL '{settings.ServiceUrl}' is not a valid URL");
            }
            else if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                errors.Add($"Service URL scheme must be http or https, got: {uri.Scheme}");
            }

            // Validate timeout settings
            if (settings.ServiceTimeoutSeconds <= 0)
            {
                errors.Add("Service timeout must be greater than 0");
            }
            else if (settings.ServiceTimeoutSeconds > 300)
            {
                warnings.Add("Service timeout is unusually high (> 5 minutes)");
            }

            if (settings.BackfillTimeoutMinutes <= 0)
            {
                errors.Add("Backfill timeout must be greater than 0");
            }

            // Validate data root path
            if (!string.IsNullOrWhiteSpace(config.DataRoot) && !Directory.Exists(config.DataRoot))
            {
                warnings.Add($"Data root directory does not exist: {config.DataRoot}");
            }

            // Validate symbols
            if (config.Symbols != null)
            {
                foreach (var symbol in config.Symbols)
                {
                    if (string.IsNullOrWhiteSpace(symbol.Symbol))
                    {
                        errors.Add("Symbol configuration contains empty symbol");
                    }

                    if (symbol.DepthLevels < 1 || symbol.DepthLevels > 50)
                    {
                        warnings.Add($"Symbol {symbol.Symbol} has unusual depth levels: {symbol.DepthLevels}");
                    }
                }
            }

            // Validate data sources
            if (config.DataSources?.Sources != null)
            {
                foreach (var source in config.DataSources.Sources)
                {
                    if (string.IsNullOrWhiteSpace(source.Id))
                    {
                        errors.Add("Data source configuration contains empty ID");
                    }
                }
            }

            return new ConfigValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray(),
                Warnings = warnings.ToArray()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add($"Validation failed with exception: {ex.Message}");
            return new ConfigValidationResult { IsValid = false, Errors = errors.ToArray(), Warnings = warnings.ToArray() };
        }
    }
}
