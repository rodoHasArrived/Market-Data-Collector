using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public sealed class FirstRunService
{
    private readonly ILoggingService _logger;
    private readonly IConfigService _configService;
    private readonly string _firstRunMarkerPath;

    public FirstRunService(ILoggingService logger, IConfigService configService)
    {
        _logger = logger;
        _configService = configService;
        
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarketDataCollector");
        
        _firstRunMarkerPath = Path.Combine(appDataPath, ".firstrun");
    }

    public bool IsFirstRun()
    {
        var isFirst = !File.Exists(_firstRunMarkerPath);
        _logger.Log($"First run check: {isFirst}");
        return isFirst;
    }

    public async Task MarkFirstRunCompleteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_firstRunMarkerPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_firstRunMarkerPath, 
                DateTime.UtcNow.ToString("O"), 
                cancellationToken);
            
            _logger.Log("First run marker created");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to create first run marker: {ex.Message}", ex);
        }
    }

    public async Task CreateSampleConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Log("Creating sample configuration...");

            var sampleConfig = new
            {
                BackendUrl = "http://localhost:8080/api",
                Theme = "Light",
                AutoConnect = true,
                RefreshInterval = 5000,
                ShowNotifications = true,
                EnableLogging = true
            };

            foreach (var prop in sampleConfig.GetType().GetProperties())
            {
                var value = prop.GetValue(sampleConfig);
                if (value != null)
                {
                    await _configService.SaveConfigurationAsync(prop.Name, value, cancellationToken);
                }
            }

            _logger.Log("Sample configuration created");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to create sample configuration: {ex.Message}", ex);
        }
    }

    public async Task ShowWelcomeAsync(CancellationToken cancellationToken = default)
    {
        _logger.Log("First run detected - showing welcome");
        
        // Create sample config
        await CreateSampleConfigAsync(cancellationToken);
        
        // Mark first run complete
        await MarkFirstRunCompleteAsync(cancellationToken);
    }
}
