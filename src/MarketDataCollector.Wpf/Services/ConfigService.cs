using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ConfigServiceValidationResult
{
    public bool IsValid { get; set; } = true;
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();

    public static ConfigServiceValidationResult Success() => new() { IsValid = true };
    public static ConfigServiceValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}

/// <summary>
/// WPF platform-specific configuration service.
/// Extends <see cref="ConfigServiceBase"/> with file I/O and audit trail integration.
/// Part of Phase 2 service extraction.
/// </summary>
public sealed class ConfigService : ConfigServiceBase
{
    private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());

    private bool _initialized;

    public static ConfigService Instance => _instance.Value;

    public bool IsInitialized => _initialized;

    public override string ConfigPath => FirstRunService.Instance.ConfigFilePath;

    private ConfigService()
    {
    }

    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the current configuration (WPF-specific result type).
    /// </summary>
    public async Task<ConfigServiceValidationResult> ValidateConfigAsync()
    {
        var result = await ValidateConfigDetailAsync();
        return new ConfigServiceValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Errors,
            Warnings = result.Warnings
        };
    }

    /// <summary>
    /// Gets the data sources configuration (WPF-specific alias).
    /// </summary>
    public Task<DataSourcesConfigDto> GetDataSourcesConfigAsync()
        => GetDataSourcesConfigDtoAsync();

    /// <summary>
    /// Adds or updates a single backfill provider configuration entry.
    /// Records the change in the audit trail.
    /// </summary>
    public async Task SetBackfillProviderOptionsAsync(string providerId, BackfillProviderOptionsDto options)
    {
        var (previousJson, newJson) = await base.SetBackfillProviderOptionsAsync(providerId, options);

        Ui.Services.BackfillProviderConfigService.Instance.RecordAuditEntry(
            NormalizeProviderId(providerId),
            "update",
            previousJson,
            newJson);
    }

    /// <summary>
    /// Resets a provider's configuration back to defaults.
    /// </summary>
    public async Task ResetBackfillProviderOptionsAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id is required", nameof(providerId));
        }

        var normalizedId = NormalizeProviderId(providerId);
        var defaultOptions = await Ui.Services.BackfillProviderConfigService.Instance
            .GetDefaultOptionsAsync(normalizedId);

        var previousJson = await base.ResetBackfillProviderOptionsAsync(providerId, defaultOptions);

        Ui.Services.BackfillProviderConfigService.Instance.RecordAuditEntry(
            normalizedId,
            "reset",
            previousJson,
            JsonSerializer.Serialize(defaultOptions, SharedJsonOptions));
    }

    protected override async Task<AppConfigDto?> LoadConfigCoreAsync(CancellationToken ct)
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

            return JsonSerializer.Deserialize<AppConfigDto>(json, SharedJsonOptions) ?? new AppConfigDto();
        }
        catch (Exception ex)
        {
            LogError("Failed to load configuration", ex);
            return new AppConfigDto();
        }
    }

    protected override async Task SaveConfigCoreAsync(AppConfigDto config, CancellationToken ct)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, SharedJsonOptions);
            await File.WriteAllTextAsync(ConfigPath, json, ct);
        }
        catch (Exception ex)
        {
            LogError("Failed to save configuration", ex);
            throw;
        }
    }

    protected override void LogError(string message, Exception? exception)
    {
        LoggingService.Instance.LogError(message, exception);
    }

    // Keep backward-compatible internal method for existing callers
    internal Task<AppConfigDto?> LoadConfigAsync()
        => LoadConfigCoreAsync();
}
