using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Services.Contracts;
using AlpacaOptions = MarketDataCollector.Contracts.Configuration.AlpacaOptionsDto;
using AppConfig = MarketDataCollector.Contracts.Configuration.AppConfigDto;
using AppSettings = MarketDataCollector.Contracts.Configuration.AppSettingsDto;
using DataSourceConfig = MarketDataCollector.Contracts.Configuration.DataSourceConfigDto;
using DataSourcesConfig = MarketDataCollector.Contracts.Configuration.DataSourcesConfigDto;
using StorageConfig = MarketDataCollector.Contracts.Configuration.StorageConfigDto;
using SymbolConfig = MarketDataCollector.Contracts.Configuration.SymbolConfigDto;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ConfigServiceValidationResult
{
    /// <summary>
    /// Gets or sets whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public string[] Errors { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the validation warnings.
    /// </summary>
    public string[] Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ConfigServiceValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ConfigServiceValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}

/// <summary>
/// Service for managing application configuration.
/// Implements singleton pattern for application-wide configuration management.
/// </summary>
public sealed class ConfigService : IConfigService
{
    private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private bool _initialized;

    /// <summary>
    /// Gets the singleton instance of the ConfigService.
    /// </summary>
    public static ConfigService Instance => _instance.Value;

    /// <summary>
    /// Gets whether the service has been initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigPath => FirstRunService.Instance.ConfigFilePath;

    private ConfigService()
    {
    }

    /// <summary>
    /// Initializes the configuration service.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <returns>A task containing the validation result.</returns>
    public async Task<ConfigServiceValidationResult> ValidateConfigAsync()
    {
        var config = await LoadConfigCoreAsync(CancellationToken.None) ?? new AppConfigDto();
        var errors = new List<string>();
        var warnings = new List<string>();

        var backfill = config.Backfill;
        var providers = backfill?.Providers;

        if (backfill?.Enabled == true && providers == null)
        {
            warnings.Add("Backfill is enabled but no per-provider settings are configured. Defaults will be used.");
        }

        if (providers != null)
        {
            var providerEntries = EnumerateProviders(providers).ToList();
            var enabledEntries = providerEntries.Where(p => p.Options?.Enabled ?? false).ToList();

            if (backfill?.Enabled == true && enabledEntries.Count == 0)
            {
                errors.Add("Backfill is enabled but all historical providers are disabled.");
            }

            foreach (var (providerId, options) in providerEntries)
            {
                if (options == null)
                {
                    continue;
                }

                if (options.Priority is < 0)
                {
                    errors.Add($"Provider '{providerId}' has invalid priority {options.Priority}. Priority must be >= 0.");
                }

                if (options.RateLimitPerMinute is <= 0)
                {
                    errors.Add($"Provider '{providerId}' has invalid rateLimitPerMinute {options.RateLimitPerMinute}. Value must be > 0.");
                }

                if (options.RateLimitPerHour is <= 0)
                {
                    errors.Add($"Provider '{providerId}' has invalid rateLimitPerHour {options.RateLimitPerHour}. Value must be > 0.");
                }
            }

            var duplicatePriorityGroups = enabledEntries
                .Where(p => p.Options?.Priority != null)
                .GroupBy(p => p.Options!.Priority!.Value)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in duplicatePriorityGroups)
            {
                var providerList = string.Join(", ", group.Select(g => g.ProviderId));
                warnings.Add($"Enabled providers share priority {group.Key}: {providerList}. Fallback order may be ambiguous.");
            }
        }

        return new ConfigServiceValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.ToArray(),
            Warnings = warnings.ToArray()
        };
    }

    /// <summary>
    /// Gets the data sources configuration.
    /// </summary>
    public Task<DataSourcesConfigDto> GetDataSourcesConfigAsync()
        => GetDataSourcesConfigAsync(CancellationToken.None);

    /// <summary>
    /// Adds or updates a data source configuration.
    /// </summary>
    public Task AddOrUpdateDataSourceAsync(DataSourceConfigDto dataSource)
        => AddOrUpdateDataSourceAsync(dataSource, CancellationToken.None);

    /// <summary>
    /// Deletes a data source by ID.
    /// </summary>
    public Task DeleteDataSourceAsync(string id)
        => DeleteDataSourceAsync(id, CancellationToken.None);

    /// <summary>
    /// Sets the default data source for real-time or historical data.
    /// </summary>
    public Task SetDefaultDataSourceAsync(string id, bool isHistorical)
        => SetDefaultDataSourceAsync(id, isHistorical, CancellationToken.None);

    /// <summary>
    /// Updates failover settings for data sources.
    /// </summary>
    public Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds)
        => UpdateFailoverSettingsAsync(enableFailover, failoverTimeoutSeconds, CancellationToken.None);



    /// <summary>
    /// Gets backfill provider configuration, creating defaults when missing.
    /// </summary>
    public async Task<BackfillProvidersConfigDto> GetBackfillProvidersConfigAsync()
    {
        var config = await LoadConfigCoreAsync(CancellationToken.None) ?? new AppConfigDto();
        config.Backfill ??= new BackfillConfigDto();
        config.Backfill.Providers ??= new BackfillProvidersConfigDto();
        return config.Backfill.Providers;
    }

    /// <summary>
    /// Adds or updates a single backfill provider configuration entry.
    /// </summary>
    /// <param name="providerId">Known provider identifier (e.g., alpaca, polygon, yahoo).</param>
    /// <param name="options">Provider options to persist.</param>
    public async Task SetBackfillProviderOptionsAsync(string providerId, BackfillProviderOptionsDto options)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id is required", nameof(providerId));
        }

        ArgumentNullException.ThrowIfNull(options);
        ValidateProviderOptions(providerId, options);

        var config = await LoadConfigCoreAsync(CancellationToken.None) ?? new AppConfigDto();
        config.Backfill ??= new BackfillConfigDto();
        config.Backfill.Providers ??= new BackfillProvidersConfigDto();

        SetProviderOptions(config.Backfill.Providers, providerId, options);

        await SaveConfigCoreAsync(config, CancellationToken.None);
    }

    /// <summary>
    /// Gets options for a single historical backfill provider.
    /// </summary>
    public async Task<BackfillProviderOptionsDto?> GetBackfillProviderOptionsAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id is required", nameof(providerId));
        }

        var providers = await GetBackfillProvidersConfigAsync();
        return GetProviderOptions(providers, providerId);
    }

    private static IEnumerable<(string ProviderId, BackfillProviderOptionsDto? Options)> EnumerateProviders(BackfillProvidersConfigDto providers)
    {
        yield return ("alpaca", providers.Alpaca);
        yield return ("polygon", providers.Polygon);
        yield return ("tiingo", providers.Tiingo);
        yield return ("finnhub", providers.Finnhub);
        yield return ("stooq", providers.Stooq);
        yield return ("yahoo", providers.Yahoo);
        yield return ("alphavantage", providers.AlphaVantage);
        yield return ("nasdaqdatalink", providers.NasdaqDataLink);
    }

    private static BackfillProviderOptionsDto? GetProviderOptions(BackfillProvidersConfigDto providers, string providerId)
    {
        return NormalizeProviderId(providerId) switch
        {
            "alpaca" => providers.Alpaca,
            "polygon" => providers.Polygon,
            "tiingo" => providers.Tiingo,
            "finnhub" => providers.Finnhub,
            "stooq" => providers.Stooq,
            "yahoo" => providers.Yahoo,
            "alphavantage" => providers.AlphaVantage,
            "nasdaqdatalink" => providers.NasdaqDataLink,
            _ => throw new ArgumentOutOfRangeException(nameof(providerId), providerId, "Unknown backfill provider id")
        };
    }

    private static void SetProviderOptions(BackfillProvidersConfigDto providers, string providerId, BackfillProviderOptionsDto options)
    {
        switch (NormalizeProviderId(providerId))
        {
            case "alpaca":
                providers.Alpaca = options;
                break;
            case "polygon":
                providers.Polygon = options;
                break;
            case "tiingo":
                providers.Tiingo = options;
                break;
            case "finnhub":
                providers.Finnhub = options;
                break;
            case "stooq":
                providers.Stooq = options;
                break;
            case "yahoo":
                providers.Yahoo = options;
                break;
            case "alphavantage":
                providers.AlphaVantage = options;
                break;
            case "nasdaqdatalink":
                providers.NasdaqDataLink = options;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(providerId), providerId, "Unknown backfill provider id");
        }
    }

    private static string NormalizeProviderId(string providerId)
    {
        var normalized = providerId.Trim().ToLowerInvariant();
        return normalized switch
        {
            "yahoofinance" => "yahoo",
            "nasdaq" => "nasdaqdatalink",
            _ => normalized
        };
    }

    private static void ValidateProviderOptions(string providerId, BackfillProviderOptionsDto options)
    {
        if (options.Priority is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Priority, $"Priority for provider '{providerId}' must be >= 0.");
        }

        if (options.RateLimitPerMinute is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.RateLimitPerMinute, $"RateLimitPerMinute for provider '{providerId}' must be > 0.");
        }

        if (options.RateLimitPerHour is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.RateLimitPerHour, $"RateLimitPerHour for provider '{providerId}' must be > 0.");
        }
    }

    public async Task<AppConfig?> LoadConfigAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await LoadConfigCoreAsync(ct);
    }

    public async Task SaveConfigAsync(AppConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ct.ThrowIfCancellationRequested();
        await SaveConfigCoreAsync(config, ct);
    }

    public async Task SaveDataSourceAsync(string dataSource, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfigDto();
        config.DataSource = dataSource;
        await SaveConfigAsync(config, ct);
    }

    public async Task SaveAlpacaOptionsAsync(AlpacaOptions options, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfigDto();
        config.Alpaca = options;
        await SaveConfigAsync(config, ct);
    }

    public async Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfigDto();
        config.DataRoot = dataRoot;
        config.Compress = compress;
        config.Storage = storage;
        await SaveConfigAsync(config, ct);
    }

    public async Task AddOrUpdateSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfigDto();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfigDto>();
        var idx = symbols.FindIndex(s => string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) symbols[idx] = symbol; else symbols.Add(symbol);
        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config, ct);
    }

    public Task AddSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)
        => AddOrUpdateSymbolAsync(symbol, ct);

    public async Task DeleteSymbolAsync(string symbol, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(ct) ?? new AppConfigDto();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfigDto>();
        symbols.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config, ct);
    }

    public async Task<DataSourceConfig[]> GetDataSourcesAsync(CancellationToken ct = default)
    {
        var cfg = await GetDataSourcesConfigAsync(ct);
        return cfg.Sources ?? Array.Empty<DataSourceConfigDto>();
    }

    public async Task<DataSourcesConfig> GetDataSourcesConfigAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var config = await LoadConfigCoreAsync(ct) ?? new AppConfigDto();
        return config.DataSources ?? new DataSourcesConfigDto();
    }

    public async Task AddOrUpdateDataSourceAsync(DataSourceConfig dataSource, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ct.ThrowIfCancellationRequested();

        var config = await LoadConfigCoreAsync(ct) ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfigDto>();

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
        await SaveConfigCoreAsync(config, ct);
    }

    public async Task DeleteDataSourceAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ct.ThrowIfCancellationRequested();

        var config = await LoadConfigCoreAsync(ct) ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfigDto>();

        sources.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigCoreAsync(config, ct);
    }

    public async Task SetDefaultDataSourceAsync(string id, bool isHistorical, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ct.ThrowIfCancellationRequested();

        var config = await LoadConfigCoreAsync(ct) ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();

        if (isHistorical)
        {
            dataSources.DefaultHistoricalSourceId = id;
        }
        else
        {
            dataSources.DefaultRealTimeSourceId = id;
        }

        config.DataSources = dataSources;
        await SaveConfigCoreAsync(config, ct);
    }

    public async Task ToggleDataSourceAsync(string id, bool enabled, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ct.ThrowIfCancellationRequested();

        var config = await LoadConfigCoreAsync(ct) ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfigDto>();

        var source = sources.FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        if (source == null)
        {
            return;
        }

        source.Enabled = enabled;
        dataSources.Sources = sources.ToArray();
        config.DataSources = dataSources;
        await SaveConfigCoreAsync(config, ct);
    }

    public async Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var config = await LoadConfigCoreAsync(ct) ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();
        dataSources.EnableFailover = enableFailover;
        dataSources.FailoverTimeoutSeconds = failoverTimeoutSeconds;

        config.DataSources = dataSources;
        await SaveConfigCoreAsync(config, ct);
    }

    public async Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
    {
        var cfg = await LoadConfigAsync(ct) ?? new AppConfigDto();
        return cfg.Settings ?? new AppSettingsDto();
    }

    public async Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        var cfg = await LoadConfigAsync(ct) ?? new AppConfigDto();
        cfg.Settings = settings;
        await SaveConfigAsync(cfg, ct);
    }

    public async Task UpdateServiceUrlAsync(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60, CancellationToken ct = default)
    {
        var cfg = await LoadConfigAsync(ct) ?? new AppConfigDto();
        var settings = cfg.Settings ?? new AppSettingsDto();
        settings.ServiceUrl = serviceUrl;
        settings.ServiceTimeoutSeconds = timeoutSeconds;
        settings.BackfillTimeoutMinutes = backfillTimeoutMinutes;
        cfg.Settings = settings;
        await SaveConfigAsync(cfg, ct);
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return InitializeAsync();
    }

    public async Task<ConfigValidationResult> ValidateConfigAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = await ValidateConfigAsync();
        return new ConfigValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Errors,
            Warnings = result.Warnings
        };
    }

    /// <summary>
    /// Gets configuration of the specified type.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <returns>A task containing the configuration instance.</returns>
    public Task<T?> GetConfigAsync<T>() where T : class, new()
    {
        return Task.FromResult<T?>(new T());
    }

    /// <summary>
    /// Saves the current configuration.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task SaveConfigAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves the specified configuration.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <param name="config">The configuration to save.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task SaveConfigAsync<T>(T config) where T : class
    {
        return Task.CompletedTask;
    }

    private async Task<AppConfigDto?> LoadConfigCoreAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfigDto();
            }

            var json = await File.ReadAllTextAsync(ConfigPath, ct);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppConfigDto();
            }

            return JsonSerializer.Deserialize<AppConfigDto>(json, _jsonOptions) ?? new AppConfigDto();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load configuration", ex);
            return new AppConfigDto();
        }
    }

    private async Task SaveConfigCoreAsync(AppConfigDto config, CancellationToken ct)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(ConfigPath, json, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to save configuration", ex);
            throw;
        }
    }
}
