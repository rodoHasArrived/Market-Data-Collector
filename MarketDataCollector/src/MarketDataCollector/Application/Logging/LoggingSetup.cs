using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace MarketDataCollector.Application.Logging;

/// <summary>
/// Configures Serilog logging infrastructure for the MarketDataCollector.
/// Supports structured logging with console and file sinks.
/// </summary>
public static class LoggingSetup
{
    private static ILogger? _logger;

    /// <summary>
    /// Gets the configured logger instance.
    /// </summary>
    public static ILogger Logger => _logger ?? Log.Logger;

    /// <summary>
    /// Initializes the logging infrastructure with default settings.
    /// Call this early in application startup.
    /// </summary>
    /// <param name="configuration">Optional configuration for customizing log settings.</param>
    /// <param name="dataRoot">Root directory for log files.</param>
    public static void Initialize(IConfiguration? configuration = null, string dataRoot = "data")
    {
        var logPath = Path.Combine(dataRoot, "_logs", "mdc-.log");

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
            .Enrich.WithProperty("Application", "MarketDataCollector")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        // Allow configuration override if provided
        if (configuration != null)
        {
            loggerConfig = loggerConfig.ReadFrom.Configuration(configuration);
        }

        // Check for debug mode
        var debugEnv = Environment.GetEnvironmentVariable("MDC_DEBUG");
        if (debugEnv?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            loggerConfig = loggerConfig.MinimumLevel.Debug();
        }

        _logger = loggerConfig.CreateLogger();
        Log.Logger = _logger;
    }

    /// <summary>
    /// Creates a contextual logger for a specific component.
    /// </summary>
    public static ILogger ForContext<T>() => Logger.ForContext<T>();

    /// <summary>
    /// Creates a contextual logger for a specific type.
    /// </summary>
    public static ILogger ForContext(Type sourceContext) => Logger.ForContext(sourceContext);

    /// <summary>
    /// Creates a contextual logger with a custom source context.
    /// </summary>
    public static ILogger ForContext(string sourceContext) => Logger.ForContext("SourceContext", sourceContext);

    /// <summary>
    /// Flushes any buffered log entries and closes the logger.
    /// Call this during application shutdown.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
