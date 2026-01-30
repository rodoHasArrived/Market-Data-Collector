using MarketDataCollector.Application.Config.Credentials;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.UI;
using Serilog;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// Unified configuration pipeline that always produces a validated, normalized configuration.
/// This is the single entry point for all configuration loading regardless of source
/// (file, wizard, auto-config, hot reload, programmatic).
/// </summary>
/// <remarks>
/// <para><b>Pipeline Stages:</b></para>
/// <list type="number">
/// <item><description>Load base configuration from source</description></item>
/// <item><description>Apply environment-specific overlay (e.g., appsettings.Production.json)</description></item>
/// <item><description>Apply environment variable overrides</description></item>
/// <item><description>Resolve credentials from all sources</description></item>
/// <item><description>Apply self-healing fixes (optional)</description></item>
/// <item><description>Validate the final configuration</description></item>
/// <item><description>Return ValidatedConfig with full metadata</description></item>
/// </list>
/// </remarks>
public sealed class ConfigurationPipeline : IAsyncDisposable
{
    private readonly ILogger _log;
    private readonly ProviderCredentialResolver _credentialResolver;
    private readonly ConfigEnvironmentOverride _envOverride;
    private ConfigWatcher? _watcher;

    public ConfigurationPipeline(ILogger? log = null, ProviderCredentialResolver? credentialResolver = null)
    {
        _log = log ?? LoggingSetup.ForContext<ConfigurationPipeline>();
        _credentialResolver = credentialResolver ?? new ProviderCredentialResolver(_log);
        _envOverride = new ConfigEnvironmentOverride();
    }

    #region Pipeline Entry Points

    /// <summary>
    /// Loads and validates configuration from a file path.
    /// This is the primary entry point for file-based configuration.
    /// </summary>
    public ValidatedConfig LoadFromFile(string configPath, PipelineOptions? options = null)
    {
        options ??= PipelineOptions.Default;

        try
        {
            // Stage 1: Load base config
            var config = ConfigStore.LoadConfig(configPath);
            var environmentName = GetEnvironmentName();

            // Stage 2: Apply environment overlay
            config = ApplyEnvironmentOverlay(config, configPath);

            // Run through common pipeline
            return RunPipeline(config, configPath, environmentName, ConfigurationSource.File, options);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load configuration from {ConfigPath}", configPath);
            return ValidatedConfig.Failed(
                null,
                new[] { $"Failed to load configuration: {ex.Message}" },
                configPath,
                ConfigurationSource.File);
        }
    }

    /// <summary>
    /// Processes an existing AppConfig through the pipeline.
    /// Use this for programmatically created configurations.
    /// </summary>
    public ValidatedConfig Process(AppConfig config, PipelineOptions? options = null)
    {
        options ??= PipelineOptions.Default;
        var environmentName = GetEnvironmentName();

        return RunPipeline(config, null, environmentName, ConfigurationSource.Programmatic, options);
    }

    /// <summary>
    /// Processes configuration from the wizard result.
    /// </summary>
    public ValidatedConfig FromWizardResult(WizardResult result, PipelineOptions? options = null)
    {
        if (!result.Success || result.Config == null)
        {
            return ValidatedConfig.Failed(
                null,
                new[] { result.Message ?? "Wizard did not produce a valid configuration" },
                source: ConfigurationSource.Wizard);
        }

        options ??= PipelineOptions.Default;
        var environmentName = GetEnvironmentName();

        return RunPipeline(result.Config, result.ConfigPath, environmentName, ConfigurationSource.Wizard, options);
    }

    /// <summary>
    /// Processes configuration from auto-config result.
    /// </summary>
    public ValidatedConfig FromAutoConfigResult(AutoConfigurationService.AutoConfigResult result, PipelineOptions? options = null)
    {
        options ??= PipelineOptions.Default;
        var environmentName = GetEnvironmentName();

        var warnings = new List<string>(result.Warnings);
        if (result.Recommendations.Count > 0)
        {
            warnings.AddRange(result.Recommendations.Select(r => $"Recommendation: {r}"));
        }

        var validated = RunPipeline(result.Config, null, environmentName, ConfigurationSource.AutoConfig, options);

        // Merge auto-config specific info
        return validated with
        {
            AppliedFixes = validated.AppliedFixes.Concat(result.AppliedChanges).ToList(),
            Warnings = validated.Warnings.Concat(warnings).ToList()
        };
    }

    /// <summary>
    /// Processes configuration from hot reload.
    /// </summary>
    public ValidatedConfig FromHotReload(AppConfig config, string configPath, PipelineOptions? options = null)
    {
        options ??= PipelineOptions.Default with { ApplySelfHealing = true };
        var environmentName = GetEnvironmentName();

        return RunPipeline(config, configPath, environmentName, ConfigurationSource.HotReload, options);
    }

    #endregion

    #region Hot Reload Support

    /// <summary>
    /// Starts watching a configuration file for changes.
    /// The callback receives a ValidatedConfig that has been processed through the full pipeline.
    /// </summary>
    public ConfigWatcher StartHotReload(
        string configPath,
        Action<ValidatedConfig> onConfigChanged,
        Action<Exception>? onError = null,
        PipelineOptions? options = null)
    {
        StopHotReload();

        options ??= PipelineOptions.Default;

        _watcher = new ConfigWatcher(configPath);
        _watcher.ConfigChanged += rawConfig =>
        {
            var validated = FromHotReload(rawConfig, configPath, options);
            onConfigChanged(validated);
        };

        if (onError != null)
        {
            _watcher.Error += onError;
        }

        _watcher.Start();
        _log.Information("Configuration hot reload enabled for {ConfigPath}", configPath);
        return _watcher;
    }

    /// <summary>
    /// Stops hot reload monitoring.
    /// </summary>
    public void StopHotReload()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    #endregion

    #region Pipeline Core

    private ValidatedConfig RunPipeline(
        AppConfig config,
        string? sourcePath,
        string? environmentName,
        ConfigurationSource source,
        PipelineOptions options)
    {
        var appliedFixes = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Stage 3: Apply environment variable overrides
            config = _envOverride.ApplyOverrides(config);

            // Stage 4: Resolve credentials
            config = ResolveAllCredentials(config);

            // Stage 5: Apply self-healing fixes (if enabled)
            if (options.ApplySelfHealing)
            {
                var (healedConfig, fixes, healWarnings) = ApplySelfHealingFixes(config);
                config = healedConfig;
                appliedFixes.AddRange(fixes);
                warnings.AddRange(healWarnings);

                if (fixes.Count > 0)
                {
                    _log.Information("Applied {FixCount} self-healing configuration fixes", fixes.Count);
                }
            }

            // Stage 6: Validate
            var validationErrors = new List<string>();
            var isValid = true;

            if (options.ValidateConfig)
            {
                isValid = ConfigValidationHelper.ValidateAndLog(config, validationErrors);

                if (!isValid)
                {
                    _log.Warning("Configuration validation failed with {ErrorCount} errors", validationErrors.Count);
                }
            }

            // Stage 7: Return validated config
            return ValidatedConfig.FromConfig(
                config,
                isValid,
                sourcePath,
                validationErrors,
                appliedFixes,
                warnings,
                environmentName,
                source);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Configuration pipeline failed");
            return ValidatedConfig.Failed(
                config,
                new[] { $"Pipeline error: {ex.Message}" },
                sourcePath,
                source);
        }
    }

    #endregion

    #region Credential Resolution

    private AppConfig ResolveAllCredentials(AppConfig config)
    {
        // Resolve Alpaca credentials
        if (config.DataSource == DataSourceKind.Alpaca || config.Alpaca != null)
        {
            var (keyId, secretKey) = _credentialResolver.ResolveAlpaca(config.Alpaca?.KeyId, config.Alpaca?.SecretKey);
            if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(secretKey))
            {
                config = config with
                {
                    Alpaca = (config.Alpaca ?? new AlpacaOptions(keyId, secretKey)) with
                    {
                        KeyId = keyId,
                        SecretKey = secretKey
                    }
                };
            }
        }

        // Resolve Polygon credentials
        if (config.DataSource == DataSourceKind.Polygon || config.Polygon != null)
        {
            var apiKey = _credentialResolver.ResolvePolygon(config.Polygon?.ApiKey);
            if (!string.IsNullOrEmpty(apiKey) && config.Polygon != null)
            {
                config = config with
                {
                    Polygon = config.Polygon with { ApiKey = apiKey }
                };
            }
        }

        // Resolve backfill provider credentials
        if (config.Backfill?.Providers != null)
        {
            var providers = config.Backfill.Providers;
            var updated = false;

            if (providers.Tiingo != null)
            {
                var token = _credentialResolver.ResolveTiingo(providers.Tiingo.ApiToken);
                if (!string.IsNullOrEmpty(token))
                {
                    providers = providers with { Tiingo = providers.Tiingo with { ApiToken = token } };
                    updated = true;
                }
            }

            if (providers.Finnhub != null)
            {
                var key = _credentialResolver.ResolveFinnhub(providers.Finnhub.ApiKey);
                if (!string.IsNullOrEmpty(key))
                {
                    providers = providers with { Finnhub = providers.Finnhub with { ApiKey = key } };
                    updated = true;
                }
            }

            if (providers.Polygon != null)
            {
                var key = _credentialResolver.ResolvePolygon(providers.Polygon.ApiKey);
                if (!string.IsNullOrEmpty(key))
                {
                    providers = providers with { Polygon = providers.Polygon with { ApiKey = key } };
                    updated = true;
                }
            }

            if (providers.AlphaVantage != null)
            {
                var key = _credentialResolver.ResolveAlphaVantage(providers.AlphaVantage.ApiKey);
                if (!string.IsNullOrEmpty(key))
                {
                    providers = providers with { AlphaVantage = providers.AlphaVantage with { ApiKey = key } };
                    updated = true;
                }
            }

            if (providers.Nasdaq != null)
            {
                var key = _credentialResolver.ResolveNasdaq(providers.Nasdaq.ApiKey);
                if (!string.IsNullOrEmpty(key))
                {
                    providers = providers with { Nasdaq = providers.Nasdaq with { ApiKey = key } };
                    updated = true;
                }
            }

            if (updated)
            {
                config = config with { Backfill = config.Backfill with { Providers = providers } };
            }
        }

        return config;
    }

    #endregion

    #region Self-Healing

    private (AppConfig Config, IReadOnlyList<string> AppliedFixes, IReadOnlyList<string> Warnings) ApplySelfHealingFixes(AppConfig config)
    {
        var appliedFixes = new List<string>();
        var warnings = new List<string>();

        // Fix: Alpaca selected but no credentials
        if (config.DataSource == DataSourceKind.Alpaca)
        {
            if (config.Alpaca == null || string.IsNullOrEmpty(config.Alpaca.KeyId))
            {
                var (keyId, secretKey) = _credentialResolver.ResolveAlpaca();
                if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(secretKey))
                {
                    config = config with { Alpaca = new AlpacaOptions(keyId, secretKey) };
                    appliedFixes.Add("Fixed: Added Alpaca credentials from environment variables");
                }
                else
                {
                    warnings.Add("Alpaca selected but no credentials found. Set ALPACA_KEY_ID and ALPACA_SECRET_KEY environment variables.");
                }
            }
        }

        // Fix: IB selected but gateway not available
        if (config.DataSource == DataSourceKind.IB && !IsIBGatewayAvailable())
        {
            // Try to find an alternative provider
            var (keyId, secretKey) = _credentialResolver.ResolveAlpaca();
            if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(secretKey))
            {
                config = config with
                {
                    DataSource = DataSourceKind.Alpaca,
                    Alpaca = new AlpacaOptions(keyId, secretKey)
                };
                appliedFixes.Add("Fixed: Switched from IB to Alpaca (IB Gateway not detected)");
            }
            else
            {
                warnings.Add("IB Gateway not detected and no alternative real-time providers configured.");
            }
        }

        // Fix: Invalid storage naming convention
        if (config.Storage != null)
        {
            var validConventions = new[] { "flat", "bysymbol", "bydate", "bytype", "bysource", "byassetclass", "hierarchical", "canonical" };
            if (!validConventions.Contains(config.Storage.NamingConvention.ToLowerInvariant()))
            {
                var oldValue = config.Storage.NamingConvention;
                config = config with { Storage = config.Storage with { NamingConvention = "BySymbol" } };
                appliedFixes.Add($"Fixed: Invalid naming convention '{oldValue}' changed to 'BySymbol'");
            }

            // Fix: Invalid date partition
            var validPartitions = new[] { "none", "daily", "hourly", "monthly" };
            if (!validPartitions.Contains(config.Storage.DatePartition.ToLowerInvariant()))
            {
                var oldValue = config.Storage.DatePartition;
                config = config with { Storage = config.Storage with { DatePartition = "Daily" } };
                appliedFixes.Add($"Fixed: Invalid date partition '{oldValue}' changed to 'Daily'");
            }
        }

        // Fix: Empty symbols list
        if (config.Symbols == null || config.Symbols.Length == 0)
        {
            config = config with
            {
                Symbols = new[]
                {
                    new SymbolConfig("SPY", SubscribeTrades: true, SubscribeDepth: true, DepthLevels: 10)
                }
            };
            appliedFixes.Add("Fixed: Added default symbol (SPY) since none were configured");
        }

        // Fix: Invalid depth levels
        if (config.Symbols != null)
        {
            var fixedSymbols = config.Symbols.Select(s =>
            {
                if (s.SubscribeDepth && (s.DepthLevels < 1 || s.DepthLevels > 50))
                {
                    return s with { DepthLevels = Math.Clamp(s.DepthLevels, 1, 50) };
                }
                return s;
            }).ToArray();

            if (!config.Symbols.SequenceEqual(fixedSymbols))
            {
                config = config with { Symbols = fixedSymbols };
                appliedFixes.Add("Fixed: Adjusted depth levels to valid range (1-50)");
            }
        }

        // Fix: Backfill date range issues
        if (config.Backfill != null)
        {
            var backfill = config.Backfill;
            var needsFix = false;

            // Fix: From date after To date
            if (backfill.From.HasValue && backfill.To.HasValue && backfill.From > backfill.To)
            {
                backfill = backfill with { From = backfill.To, To = backfill.From };
                needsFix = true;
                appliedFixes.Add("Fixed: Swapped backfill From/To dates (From was after To)");
            }

            // Fix: Future end date
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (backfill.To.HasValue && backfill.To > today)
            {
                backfill = backfill with { To = today };
                needsFix = true;
                appliedFixes.Add("Fixed: Adjusted backfill To date to today (was in the future)");
            }

            if (needsFix)
            {
                config = config with { Backfill = backfill };
            }
        }

        _log.Debug("Self-healing applied {FixCount} fixes, {WarningCount} warnings",
            appliedFixes.Count, warnings.Count);

        return (config, appliedFixes, warnings);
    }

    private static bool IsIBGatewayAvailable()
    {
        try
        {
            var ports = new[] { 7496, 7497, 4001, 4002 };
            foreach (var port in ports)
            {
                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                if (success && client.Connected)
                {
                    client.EndConnect(result);
                    return true;
                }
            }
        }
        catch
        {
            // Ignore connection errors
        }
        return false;
    }

    #endregion

    #region Environment Handling

    private AppConfig ApplyEnvironmentOverlay(AppConfig baseConfig, string basePath)
    {
        var envName = GetEnvironmentName();
        if (string.IsNullOrWhiteSpace(envName))
            return baseConfig;

        var directory = Path.GetDirectoryName(basePath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        var envPath = Path.Combine(directory, $"{fileName}.{envName}{extension}");

        if (!File.Exists(envPath))
            return baseConfig;

        try
        {
            _log.Information("Loading environment-specific configuration: {EnvPath}", envPath);
            var envConfig = ConfigStore.LoadConfig(envPath);
            return MergeConfigs(baseConfig, envConfig);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to load environment config {EnvPath}", envPath);
            return baseConfig;
        }
    }

    private static string? GetEnvironmentName()
    {
        var env = Environment.GetEnvironmentVariable("MDC_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    }

    private static AppConfig MergeConfigs(AppConfig baseConfig, AppConfig overlay)
    {
        return baseConfig with
        {
            DataSource = overlay.DataSource != default ? overlay.DataSource : baseConfig.DataSource,
            DataRoot = !string.IsNullOrWhiteSpace(overlay.DataRoot) ? overlay.DataRoot : baseConfig.DataRoot,
            Compress = overlay.Compress ?? baseConfig.Compress,
            Symbols = overlay.Symbols?.Length > 0 ? overlay.Symbols : baseConfig.Symbols,
            Alpaca = overlay.Alpaca ?? baseConfig.Alpaca,
            IB = overlay.IB ?? baseConfig.IB,
            Polygon = overlay.Polygon ?? baseConfig.Polygon,
            Storage = overlay.Storage ?? baseConfig.Storage,
            Backfill = overlay.Backfill ?? baseConfig.Backfill
        };
    }

    #endregion

    public ValueTask DisposeAsync()
    {
        StopHotReload();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Options for controlling the configuration pipeline behavior.
/// </summary>
public sealed record PipelineOptions
{
    /// <summary>
    /// Whether to apply self-healing fixes to the configuration.
    /// </summary>
    public bool ApplySelfHealing { get; init; } = true;

    /// <summary>
    /// Whether to validate the configuration.
    /// </summary>
    public bool ValidateConfig { get; init; } = true;

    /// <summary>
    /// Default options - self-healing and validation enabled.
    /// </summary>
    public static PipelineOptions Default { get; } = new();

    /// <summary>
    /// Options for strict validation without self-healing.
    /// </summary>
    public static PipelineOptions Strict { get; } = new()
    {
        ApplySelfHealing = false,
        ValidateConfig = true
    };

    /// <summary>
    /// Options for lenient loading (no validation, no self-healing).
    /// </summary>
    public static PipelineOptions Lenient { get; } = new()
    {
        ApplySelfHealing = false,
        ValidateConfig = false
    };
}
