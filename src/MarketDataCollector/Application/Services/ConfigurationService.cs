using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// Unified configuration service that coordinates wizard, auto-config, validation,
/// provider detection, and hot reload.
/// </summary>
public sealed class ConfigurationService : IAsyncDisposable
{
    private readonly ILogger _log;
    private readonly ConfigurationWizard _wizard;
    private readonly AutoConfigurationService _autoConfig;
    private ConfigWatcher? _watcher;

    public ConfigurationService(
        ILogger? log = null,
        ConfigurationWizard? wizard = null,
        AutoConfigurationService? autoConfig = null)
    {
        _log = log ?? LoggingSetup.ForContext<ConfigurationService>();
        _wizard = wizard ?? new ConfigurationWizard();
        _autoConfig = autoConfig ?? new AutoConfigurationService();
    }

    public Task<WizardResult> RunWizardAsync(CancellationToken ct = default) => _wizard.RunAsync(ct);

    public WizardResult RunAutoConfig() => _wizard.RunQuickSetup();

    public IReadOnlyList<DetectedProvider> DetectProviders() => _autoConfig.DetectAvailableProviders();

    public int ValidateConfig(string configPath)
    {
        var validator = new ConfigValidatorCli(_log);
        return validator.Validate(configPath);
    }

    public ConfigWatcher StartHotReload(
        string configPath,
        Action<AppConfig> onConfigChanged,
        Action<Exception>? onError = null)
    {
        StopHotReload();

        _watcher = new ConfigWatcher(configPath);
        _watcher.ConfigChanged += onConfigChanged;
        if (onError != null)
        {
            _watcher.Error += onError;
        }

        _watcher.Start();
        _log.Information("Configuration hot reload enabled for {ConfigPath}", configPath);
        return _watcher;
    }

    public void StopHotReload()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public ValueTask DisposeAsync()
    {
        StopHotReload();
        return ValueTask.CompletedTask;
    }
}
