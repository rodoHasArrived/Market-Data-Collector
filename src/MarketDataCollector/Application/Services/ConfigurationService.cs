using System.Text.Json;
using FluentValidation;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Contracts;
using Serilog;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// Unified configuration service that serves as the single entry point for all configuration flows.
/// Consolidates wizard, auto-config, validation, hot reload, loading, saving, and environment overrides.
/// </summary>
/// <remarks>
/// All configuration entry points (CLI, UI, programmatic) should route through this service.
/// This ensures consistent handling of:
/// - Configuration file loading with environment-specific overlays
/// - Environment variable overrides (MDC_* prefix)
/// - Validation and self-healing fixes
/// - Hot reload with change notifications
/// </remarks>
[ImplementsAdr("ADR-001", "Unified configuration provider")]
public sealed class ConfigurationService : IAsyncDisposable
{
    private const string DefaultConfigFileName = "appsettings.json";
    private const string ConfigPathEnvVar = "MDC_CONFIG_PATH";
    private const string EnvironmentEnvVar = "MDC_ENVIRONMENT";
    private const string DotnetEnvironmentEnvVar = "DOTNET_ENVIRONMENT";

    private readonly ILogger _log;
    private readonly ConfigurationWizard _wizard;
    private readonly AutoConfigurationService _autoConfig;
    private readonly ConfigEnvironmentOverride _envOverride;
    private ConfigWatcher? _watcher;
    private AppConfig? _currentConfig;
    private string? _currentConfigPath;

    /// <summary>
    /// Creates a new ConfigurationService instance.
    /// </summary>
    /// <param name="log">Logger instance.</param>
    /// <param name="wizard">Configuration wizard (optional, created if null).</param>
    /// <param name="autoConfig">Auto-configuration service (optional, created if null).</param>
    /// <param name="envOverride">Environment override service (optional, created if null).</param>
    public ConfigurationService(
        ILogger? log = null,
        ConfigurationWizard? wizard = null,
        AutoConfigurationService? autoConfig = null,
        ConfigEnvironmentOverride? envOverride = null)
    {
        _log = log ?? LoggingSetup.ForContext<ConfigurationService>();
        _wizard = wizard ?? new ConfigurationWizard();
        _autoConfig = autoConfig ?? new AutoConfigurationService();
        _envOverride = envOverride ?? new ConfigEnvironmentOverride();
    }

    /// <summary>
    /// Gets the currently loaded configuration.
    /// Returns null if no configuration has been loaded yet.
    /// </summary>
    public AppConfig? CurrentConfig => _currentConfig;

    /// <summary>
    /// Gets the path of the currently loaded configuration file.
    /// Returns null if no configuration has been loaded yet.
    /// </summary>
    public string? CurrentConfigPath => _currentConfigPath;

    /// <summary>
    /// Gets the environment override service for direct access to override information.
    /// </summary>
    public ConfigEnvironmentOverride EnvironmentOverrides => _envOverride;

    #region Configuration Loading

    /// <summary>
    /// Resolves the configuration file path from command line arguments, environment variables, or defaults.
    /// Priority: --config argument > MDC_CONFIG_PATH env var > appsettings.json
    /// </summary>
    /// <param name="args">Command line arguments (optional).</param>
    /// <returns>Resolved configuration file path.</returns>
    public string ResolveConfigPath(string[]? args = null)
    {
        // 1. Check command line argument (highest priority)
        if (args != null)
        {
            var argValue = GetArgValue(args, "--config");
            if (!string.IsNullOrWhiteSpace(argValue))
            {
                _log.Debug("Configuration path resolved from --config argument: {Path}", argValue);
                return argValue;
            }
        }

        // 2. Check environment variable
        var envValue = Environment.GetEnvironmentVariable(ConfigPathEnvVar);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            _log.Debug("Configuration path resolved from {EnvVar}: {Path}", ConfigPathEnvVar, envValue);
            return envValue;
        }

        // 3. Default to appsettings.json
        _log.Debug("Using default configuration path: {Path}", DefaultConfigFileName);
        return DefaultConfigFileName;
    }

    /// <summary>
    /// Loads configuration from the specified path, applying environment overlays and variable overrides.
    /// This is the primary method for loading configuration and should be used instead of direct file loading.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="applyEnvironmentOverrides">Whether to apply MDC_* environment variable overrides (default: true).</param>
    /// <returns>The loaded and processed configuration.</returns>
    public AppConfig Load(string? configPath = null, bool applyEnvironmentOverrides = true)
    {
        var path = configPath ?? ResolveConfigPath();
        _currentConfigPath = Path.GetFullPath(path);

        _log.Debug("Loading configuration from {Path}", _currentConfigPath);

        // Load base configuration with environment-specific overlay
        var config = LoadWithEnvironmentOverlay(_currentConfigPath);

        // Apply environment variable overrides (MDC_* variables)
        if (applyEnvironmentOverrides)
        {
            config = _envOverride.ApplyOverrides(config);
        }

        _currentConfig = config;
        _log.Information("Configuration loaded from {Path}", _currentConfigPath);

        return config;
    }

    /// <summary>
    /// Asynchronously loads configuration from the specified path.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="applyEnvironmentOverrides">Whether to apply MDC_* environment variable overrides.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded and processed configuration.</returns>
    public async Task<AppConfig> LoadAsync(string? configPath = null, bool applyEnvironmentOverrides = true, CancellationToken ct = default)
    {
        var path = configPath ?? ResolveConfigPath();
        _currentConfigPath = Path.GetFullPath(path);

        _log.Debug("Loading configuration from {Path}", _currentConfigPath);

        // Load base configuration with environment-specific overlay
        var config = await LoadWithEnvironmentOverlayAsync(_currentConfigPath, ct);

        // Apply environment variable overrides (MDC_* variables)
        if (applyEnvironmentOverrides)
        {
            config = _envOverride.ApplyOverrides(config);
        }

        _currentConfig = config;
        _log.Information("Configuration loaded from {Path}", _currentConfigPath);

        return config;
    }

    /// <summary>
    /// Reloads the current configuration from disk, re-applying all overlays and overrides.
    /// </summary>
    /// <returns>The reloaded configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no configuration has been loaded yet.</exception>
    public AppConfig Reload()
    {
        if (_currentConfigPath == null)
        {
            throw new InvalidOperationException("No configuration has been loaded. Call Load() first.");
        }

        return Load(_currentConfigPath);
    }

    #endregion

    #region Configuration Saving

    /// <summary>
    /// Saves the configuration to the specified path.
    /// </summary>
    /// <param name="config">Configuration to save.</param>
    /// <param name="configPath">Path to save to (uses current path if null).</param>
    public void Save(AppConfig config, string? configPath = null)
    {
        var path = configPath ?? _currentConfigPath ?? ResolveConfigPath();
        path = Path.GetFullPath(path);

        _log.Debug("Saving configuration to {Path}", path);

        var json = JsonSerializer.Serialize(config, AppConfigJsonOptions.Write);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, json);
        _currentConfig = config;
        _currentConfigPath = path;

        _log.Information("Configuration saved to {Path}", path);
    }

    /// <summary>
    /// Asynchronously saves the configuration to the specified path.
    /// </summary>
    /// <param name="config">Configuration to save.</param>
    /// <param name="configPath">Path to save to (uses current path if null).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveAsync(AppConfig config, string? configPath = null, CancellationToken ct = default)
    {
        var path = configPath ?? _currentConfigPath ?? ResolveConfigPath();
        path = Path.GetFullPath(path);

        _log.Debug("Saving configuration to {Path}", path);

        var json = JsonSerializer.Serialize(config, AppConfigJsonOptions.Write);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(path, json, ct);
        _currentConfig = config;
        _currentConfigPath = path;

        _log.Information("Configuration saved to {Path}", path);
    }

    #endregion

    #region Wizard and Auto-Configuration

    /// <summary>
    /// Runs the interactive configuration wizard.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Wizard result with the generated configuration.</returns>
    public Task<WizardResult> RunWizardAsync(CancellationToken ct = default) => _wizard.RunAsync(ct);

    /// <summary>
    /// Runs quick auto-configuration based on detected environment and providers.
    /// </summary>
    /// <returns>Wizard result with the auto-generated configuration.</returns>
    public WizardResult RunAutoConfig() => _wizard.RunQuickSetup();

    /// <summary>
    /// Runs auto-configuration and returns detailed results including detected providers and recommendations.
    /// </summary>
    /// <param name="existingConfig">Existing configuration to enhance (optional).</param>
    /// <returns>Auto-configuration result with detailed information.</returns>
    public AutoConfigurationService.AutoConfigResult AutoConfigure(AppConfig? existingConfig = null)
    {
        var result = _autoConfig.AutoConfigure(existingConfig);

        // Apply environment variable overrides to the auto-configured config
        var configWithOverrides = _envOverride.ApplyOverrides(result.Config);

        return new AutoConfigurationService.AutoConfigResult(
            Success: result.Success,
            Config: configWithOverrides,
            DetectedProviders: result.DetectedProviders,
            AppliedFixes: result.AppliedFixes,
            Recommendations: result.Recommendations,
            Warnings: result.Warnings
        );
    }

    /// <summary>
    /// Generates configuration for first-time users with specified options.
    /// </summary>
    /// <param name="options">First-time configuration options.</param>
    /// <returns>Generated configuration.</returns>
    public AppConfig GenerateFirstTimeConfig(FirstTimeConfigOptions options)
    {
        var config = _autoConfig.GenerateFirstTimeConfig(options);
        return _envOverride.ApplyOverrides(config);
    }

    #endregion

    #region Provider Detection

    /// <summary>
    /// Detects all available data providers based on environment variables and configuration.
    /// </summary>
    /// <returns>List of detected providers with their status and capabilities.</returns>
    public IReadOnlyList<DetectedProvider> DetectProviders() => _autoConfig.DetectAvailableProviders();

    #endregion

    #region Validation

    /// <summary>
    /// Validates the configuration file at the specified path.
    /// </summary>
    /// <param name="configPath">Path to the configuration file to validate.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public int ValidateConfig(string configPath)
    {
        var validator = new ConfigValidatorCli(_log);
        return validator.Validate(configPath);
    }

    /// <summary>
    /// Validates a configuration object.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    public ConfigurationValidationResult ValidateConfiguration(AppConfig config)
    {
        var errors = new List<ConfigurationValidationError>();
        var warnings = new List<ConfigurationValidationWarning>();

        // Use FluentValidation validator
        var validator = new ConfigValidationHelper.AppConfigValidator();
        var result = validator.Validate(config);

        foreach (var failure in result.Errors)
        {
            if (failure.Severity == FluentValidation.Severity.Error)
            {
                errors.Add(new ConfigurationValidationError(failure.PropertyName, failure.ErrorMessage, failure.AttemptedValue));
            }
            else
            {
                warnings.Add(new ConfigurationValidationWarning(failure.PropertyName, failure.ErrorMessage));
            }
        }

        return new ConfigurationValidationResult(result.IsValid, errors, warnings);
    }

    #endregion

    #region Hot Reload

    /// <summary>
    /// Starts hot reload monitoring for the specified configuration file.
    /// When the file changes, the configuration is reloaded and the callback is invoked.
    /// </summary>
    /// <param name="configPath">Path to the configuration file to watch.</param>
    /// <param name="onConfigChanged">Callback invoked when configuration changes.</param>
    /// <param name="onError">Callback invoked on errors (optional).</param>
    /// <returns>The ConfigWatcher instance.</returns>
    public ConfigWatcher StartHotReload(
        string configPath,
        Action<AppConfig> onConfigChanged,
        Action<Exception>? onError = null)
    {
        StopHotReload();

        _watcher = new ConfigWatcher(configPath);

        // Wrap the callback to apply environment overrides
        _watcher.ConfigChanged += cfg =>
        {
            var configWithOverrides = _envOverride.ApplyOverrides(cfg);
            _currentConfig = configWithOverrides;
            onConfigChanged(configWithOverrides);
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

    #region Environment Information

    /// <summary>
    /// Gets all recognized environment variables and their current values.
    /// </summary>
    /// <returns>List of environment variable information.</returns>
    public IReadOnlyList<EnvironmentOverrideInfo> GetRecognizedEnvironmentVariables()
        => _envOverride.GetRecognizedVariables();

    /// <summary>
    /// Gets the current environment name (from MDC_ENVIRONMENT or DOTNET_ENVIRONMENT).
    /// </summary>
    /// <returns>Environment name or null if not set.</returns>
    public string? GetEnvironmentName()
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        return Environment.GetEnvironmentVariable(DotnetEnvironmentEnvVar);
    }

    /// <summary>
    /// Gets documentation for all supported environment variables.
    /// </summary>
    /// <returns>Markdown-formatted documentation string.</returns>
    public string GetEnvironmentVariableDocumentation()
        => _envOverride.GetDocumentation();

    #endregion

    #region Helper Methods

    private AppConfig LoadWithEnvironmentOverlay(string basePath)
    {
        // Load base configuration
        var baseConfig = LoadFromFile(basePath);

        // Check for environment-specific overlay
        var envName = GetEnvironmentName();
        if (string.IsNullOrWhiteSpace(envName))
            return baseConfig;

        // Build environment-specific path (e.g., appsettings.Production.json)
        var directory = Path.GetDirectoryName(basePath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        var envPath = Path.Combine(directory, $"{fileName}.{envName}{extension}");

        // If environment-specific file doesn't exist, return base config
        if (!File.Exists(envPath))
            return baseConfig;

        // Load and merge environment-specific config
        try
        {
            _log.Information("Loading environment-specific configuration: {Path}", envPath);
            var envJson = File.ReadAllText(envPath);
            var envConfig = JsonSerializer.Deserialize<AppConfig>(envJson, AppConfigJsonOptions.Read);

            if (envConfig == null)
                return baseConfig;

            return MergeConfigs(baseConfig, envConfig);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to load environment config {Path}", envPath);
            return baseConfig;
        }
    }

    private async Task<AppConfig> LoadWithEnvironmentOverlayAsync(string basePath, CancellationToken ct)
    {
        // Load base configuration
        var baseConfig = await LoadFromFileAsync(basePath, ct);

        // Check for environment-specific overlay
        var envName = GetEnvironmentName();
        if (string.IsNullOrWhiteSpace(envName))
            return baseConfig;

        // Build environment-specific path (e.g., appsettings.Production.json)
        var directory = Path.GetDirectoryName(basePath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        var envPath = Path.Combine(directory, $"{fileName}.{envName}{extension}");

        // If environment-specific file doesn't exist, return base config
        if (!File.Exists(envPath))
            return baseConfig;

        // Load and merge environment-specific config
        try
        {
            _log.Information("Loading environment-specific configuration: {Path}", envPath);
            var envJson = await File.ReadAllTextAsync(envPath, ct);
            var envConfig = JsonSerializer.Deserialize<AppConfig>(envJson, AppConfigJsonOptions.Read);

            if (envConfig == null)
                return baseConfig;

            return MergeConfigs(baseConfig, envConfig);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to load environment config {Path}", envPath);
            return baseConfig;
        }
    }

    private AppConfig LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                _log.Warning("Configuration file not found: {Path}. Using default configuration.", path);
                return new AppConfig();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Invalid JSON in configuration file: {Path}", path);
            throw new Application.Exceptions.ConfigurationException(
                $"Invalid JSON in configuration file: {path}. {ex.Message}",
                path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new Application.Exceptions.ConfigurationException(
                $"Access denied reading configuration file: {path}. Check file permissions.",
                path, ex);
        }
        catch (IOException ex)
        {
            throw new Application.Exceptions.ConfigurationException(
                $"I/O error reading configuration file: {path}. {ex.Message}",
                path, ex);
        }
    }

    private async Task<AppConfig> LoadFromFileAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
            {
                _log.Warning("Configuration file not found: {Path}. Using default configuration.", path);
                return new AppConfig();
            }

            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Invalid JSON in configuration file: {Path}", path);
            throw new Application.Exceptions.ConfigurationException(
                $"Invalid JSON in configuration file: {path}. {ex.Message}",
                path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new Application.Exceptions.ConfigurationException(
                $"Access denied reading configuration file: {path}. Check file permissions.",
                path, ex);
        }
        catch (IOException ex)
        {
            throw new Application.Exceptions.ConfigurationException(
                $"I/O error reading configuration file: {path}. {ex.Message}",
                path, ex);
        }
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
            StockSharp = overlay.StockSharp ?? baseConfig.StockSharp,
            Storage = overlay.Storage ?? baseConfig.Storage,
            Backfill = overlay.Backfill ?? baseConfig.Backfill,
            Sources = overlay.Sources ?? baseConfig.Sources,
            DataSources = overlay.DataSources ?? baseConfig.DataSources
        };
    }

    private static string? GetArgValue(string[] args, string argName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    #endregion

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        StopHotReload();
        return ValueTask.CompletedTask;
    }
}
