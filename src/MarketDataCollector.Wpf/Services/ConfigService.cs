using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Services;

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
public sealed class ConfigService
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
        var config = await LoadConfigAsync() ?? new AppConfigDto();
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
    public async Task<DataSourcesConfigDto> GetDataSourcesConfigAsync()
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        return config.DataSources ?? new DataSourcesConfigDto();
    }

    /// <summary>
    /// Adds or updates a data source configuration.
    /// </summary>
    public async Task AddOrUpdateDataSourceAsync(DataSourceConfigDto dataSource)
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
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
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Deletes a data source by ID.
    /// </summary>
    public async Task DeleteDataSourceAsync(string id)
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();
        var sources = dataSources.Sources?.ToList() ?? new List<DataSourceConfigDto>();

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
        var config = await LoadConfigAsync() ?? new AppConfigDto();
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
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Updates failover settings for data sources.
    /// </summary>
    public async Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds)
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        var dataSources = config.DataSources ?? new DataSourcesConfigDto();

        dataSources.EnableFailover = enableFailover;
        dataSources.FailoverTimeoutSeconds = failoverTimeoutSeconds;

        config.DataSources = dataSources;
        await SaveConfigAsync(config);
    }


    /// <summary>
    /// Gets backfill provider configuration, creating defaults when missing.
    /// </summary>
    public async Task<BackfillProvidersConfigDto> GetBackfillProvidersConfigAsync()
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        config.Backfill ??= new BackfillConfigDto();
        config.Backfill.Providers ??= new BackfillProvidersConfigDto();
        return config.Backfill.Providers;
    }

    /// <summary>
    /// Adds or updates a single backfill provider configuration entry.
    /// Records the change in the audit trail for operator confidence.
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

        var config = await LoadConfigAsync() ?? new AppConfigDto();
        config.Backfill ??= new BackfillConfigDto();
        config.Backfill.Providers ??= new BackfillProvidersConfigDto();

        // Capture previous state for audit trail
        var previousOptions = GetProviderOptions(config.Backfill.Providers, NormalizeProviderId(providerId));
        var previousJson = previousOptions != null
            ? System.Text.Json.JsonSerializer.Serialize(previousOptions, _jsonOptions)
            : null;
        var newJson = System.Text.Json.JsonSerializer.Serialize(options, _jsonOptions);

        SetProviderOptions(config.Backfill.Providers, providerId, options);

        await SaveConfigAsync(config);

        // Record audit entry
        Ui.Services.BackfillProviderConfigService.Instance.RecordAuditEntry(
            NormalizeProviderId(providerId),
            "update",
            previousJson,
            newJson);
    }

    /// <summary>
    /// Resets a provider's configuration back to defaults.
    /// </summary>
    /// <param name="providerId">Provider identifier to reset.</param>
    public async Task ResetBackfillProviderOptionsAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id is required", nameof(providerId));
        }

        var defaultOptions = await Ui.Services.BackfillProviderConfigService.Instance
            .GetDefaultOptionsAsync(NormalizeProviderId(providerId));

        var config = await LoadConfigAsync() ?? new AppConfigDto();
        config.Backfill ??= new BackfillConfigDto();
        config.Backfill.Providers ??= new BackfillProvidersConfigDto();

        var previousOptions = GetProviderOptions(config.Backfill.Providers, NormalizeProviderId(providerId));
        var previousJson = previousOptions != null
            ? System.Text.Json.JsonSerializer.Serialize(previousOptions, _jsonOptions)
            : null;

        SetProviderOptions(config.Backfill.Providers, providerId, defaultOptions);
        await SaveConfigAsync(config);

        Ui.Services.BackfillProviderConfigService.Instance.RecordAuditEntry(
            NormalizeProviderId(providerId),
            "reset",
            previousJson,
            System.Text.Json.JsonSerializer.Serialize(defaultOptions, _jsonOptions));
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


    /// <summary>
    /// Gets the configured symbols from appsettings.json.
    /// </summary>
    /// <returns>Array of configured symbols, or empty array if none configured.</returns>
    public async Task<SymbolConfigDto[]> GetConfiguredSymbolsAsync()
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        return config.Symbols ?? Array.Empty<SymbolConfigDto>();
    }

    /// <summary>
    /// Saves symbols to the configuration file.
    /// </summary>
    /// <param name="symbols">The symbols to save.</param>
    public async Task SaveSymbolsAsync(SymbolConfigDto[] symbols)
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        config.Symbols = symbols;
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Adds a symbol to the configuration.
    /// </summary>
    /// <param name="symbol">The symbol configuration to add.</param>
    public async Task AddSymbolAsync(SymbolConfigDto symbol)
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        var existing = config.Symbols?.ToList() ?? new List<SymbolConfigDto>();
        if (existing.All(s => !string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase)))
        {
            existing.Add(symbol);
            config.Symbols = existing.ToArray();
            await SaveConfigAsync(config);
        }
    }

    /// <summary>
    /// Removes a symbol from the configuration.
    /// </summary>
    /// <param name="symbolName">The symbol name to remove.</param>
    public async Task RemoveSymbolAsync(string symbolName)
    {
        var config = await LoadConfigAsync() ?? new AppConfigDto();
        var existing = config.Symbols?.ToList() ?? new List<SymbolConfigDto>();
        existing.RemoveAll(s => string.Equals(s.Symbol, symbolName, StringComparison.OrdinalIgnoreCase));
        config.Symbols = existing.ToArray();
        await SaveConfigAsync(config);
    }

    internal async Task<AppConfigDto?> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfigDto();
            }

            var json = await File.ReadAllTextAsync(ConfigPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppConfigDto();
            }

            return JsonSerializer.Deserialize<AppConfigDto>(json, _jsonOptions) ?? new AppConfigDto();
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load configuration", ex);
            return new AppConfigDto();
        }
    }

    private async Task SaveConfigAsync(AppConfigDto config)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to save configuration", ex);
            throw;
        }
    }
}
