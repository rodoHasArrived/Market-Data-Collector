using System.Text.Json;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for handling first-run initialization and setup.
/// </summary>
public sealed class FirstRunService : IFirstRunService
{
    private const string FirstRunMarkerFile = ".firstrun_complete";
    private const string DefaultConfigFileName = "appsettings.json";
    private const string SampleConfigFileName = "appsettings.sample.json";

    private readonly string _appDirectory;
    private readonly string _dataDirectory;
    private readonly string _logsDirectory;
    private readonly string _firstRunMarkerPath;
    private readonly string _configPath;
    private readonly string _sampleConfigPath;

    public FirstRunService()
    {
        _appDirectory = AppContext.BaseDirectory;
        _dataDirectory = Path.Combine(_appDirectory, "data");
        _logsDirectory = Path.Combine(_appDirectory, "logs");
        _firstRunMarkerPath = Path.Combine(_appDirectory, FirstRunMarkerFile);
        _configPath = Path.Combine(_appDirectory, DefaultConfigFileName);
        _sampleConfigPath = Path.Combine(_appDirectory, SampleConfigFileName);
    }

    /// <summary>
    /// Checks if this is the first run of the application.
    /// </summary>
    public Task<bool> IsFirstRunAsync()
    {
        // First run if marker file doesn't exist
        var isFirstRun = !File.Exists(_firstRunMarkerPath);
        return Task.FromResult(isFirstRun);
    }

    /// <summary>
    /// Performs first-run initialization.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create required directories
        EnsureDirectoriesExist();

        // Create default configuration if needed
        await EnsureConfigurationExistsAsync();

        // Mark first run as complete
        await MarkFirstRunCompleteAsync();
    }

    /// <summary>
    /// Creates required directories for the application.
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        // Create data directory
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }

        // Create logs directory
        if (!Directory.Exists(_logsDirectory))
        {
            Directory.CreateDirectory(_logsDirectory);
        }

        // Create subdirectories for data organization
        var subDirs = new[]
        {
            Path.Combine(_dataDirectory, "_status"),
            Path.Combine(_dataDirectory, "_logs"),
            Path.Combine(_dataDirectory, "_backfill_jobs")
        };

        foreach (var dir in subDirs)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    /// <summary>
    /// Ensures a configuration file exists, creating from sample if needed.
    /// </summary>
    private async Task EnsureConfigurationExistsAsync()
    {
        // If config already exists, nothing to do
        if (File.Exists(_configPath))
        {
            return;
        }

        // Try to copy from sample config
        if (File.Exists(_sampleConfigPath))
        {
            File.Copy(_sampleConfigPath, _configPath);
            return;
        }

        // Create minimal default configuration
        var defaultConfig = CreateDefaultConfiguration();
        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(_configPath, json);
    }

    /// <summary>
    /// Creates the default configuration object.
    /// </summary>
    private static object CreateDefaultConfiguration()
    {
        return new
        {
            DataRoot = "data",
            Compress = false,
            DataSource = "IB",
            Backfill = new
            {
                Enabled = false,
                Provider = "composite",
                Symbols = new[] { "SPY", "QQQ", "AAPL" },
                From = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"),
                To = DateTime.Today.ToString("yyyy-MM-dd"),
                Granularity = "daily",
                EnableFallback = true
            },
            Alpaca = new
            {
                KeyId = "__SET_ME__",
                SecretKey = "__SET_ME__",
                Feed = "iex",
                UseSandbox = false
            },
            Storage = new
            {
                NamingConvention = "BySymbol",
                DatePartition = "Daily",
                IncludeProvider = false
            },
            Symbols = new[]
            {
                new
                {
                    Symbol = "SPY",
                    SubscribeTrades = true,
                    SubscribeDepth = true,
                    DepthLevels = 10,
                    SecurityType = "STK",
                    Exchange = "SMART",
                    Currency = "USD",
                    PrimaryExchange = "ARCA"
                }
            },
            Serilog = new
            {
                MinimumLevel = new
                {
                    Default = "Information",
                    Override = new
                    {
                        Microsoft = "Warning",
                        System = "Warning"
                    }
                },
                WriteTo = new[]
                {
                    new
                    {
                        Name = "Console",
                        Args = new
                        {
                            outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                        }
                    },
                    new
                    {
                        Name = "File",
                        Args = new
                        {
                            path = "data/_logs/mdc-.log",
                            rollingInterval = "Day",
                            retainedFileCountLimit = 30
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Marks first run as complete by creating a marker file.
    /// </summary>
    private async Task MarkFirstRunCompleteAsync()
    {
        var content = new
        {
            CompletedAt = DateTime.UtcNow.ToString("O"),
            Version = GetApplicationVersion(),
            Platform = Environment.OSVersion.ToString()
        };

        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_firstRunMarkerPath, json);
    }

    /// <summary>
    /// Gets the application version.
    /// </summary>
    private static string GetApplicationVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    /// <summary>
    /// Resets the first-run state (for testing or reconfiguration).
    /// </summary>
    public Task ResetFirstRunAsync()
    {
        if (File.Exists(_firstRunMarkerPath))
        {
            File.Delete(_firstRunMarkerPath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigurationPath => _configPath;

    /// <summary>
    /// Gets the path to the data directory.
    /// </summary>
    public string DataDirectory => _dataDirectory;

    /// <summary>
    /// Gets the path to the logs directory.
    /// </summary>
    public string LogsDirectory => _logsDirectory;
}
