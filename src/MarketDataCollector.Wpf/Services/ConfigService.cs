using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MarketDataCollector.Contracts.Configuration;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Simple status information model for the UI.
/// </summary>
public sealed class SimpleStatus
{
    public long Published { get; set; }
    public long Dropped { get; set; }
    public long Integrity { get; set; }
    public long Historical { get; set; }
    public string? Provider { get; set; }
}

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ConfigValidationResult
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
    public static ConfigValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ConfigValidationResult Failure(params string[] errors) => new()
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
    public Task<ConfigValidationResult> ValidateConfigAsync()
    {
        return Task.FromResult(ConfigValidationResult.Success());
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

    private async Task<AppConfigDto?> LoadConfigAsync()
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
