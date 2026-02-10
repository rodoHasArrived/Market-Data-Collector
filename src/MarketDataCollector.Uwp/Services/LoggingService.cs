using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Pipeline;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Centralized logging service that provides structured logging with
/// support for multiple outputs and log levels.
/// </summary>
public sealed class LoggingService : ILoggingService, IDisposable
{
    private static LoggingService? _instance;
    private static readonly object _lock = new();

    private readonly Channel<LogEntry> _logChannel;
    private readonly ConcurrentBag<ILogOutput> _outputs = [];
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task? _processingTask;
    private bool _disposed;

    public static LoggingService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LoggingService();
                }
            }
            return _instance;
        }
    }

    private LoggingService()
    {
        // Use shared constants from PipelinePolicyConstants to ensure consistency
        // with EventPipelinePolicy.Logging preset in the main application.
        // UWP cannot reference main project due to WinRT metadata constraints,
        // but can share constants via compile-include from Contracts.
        _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(PipelinePolicyConstants.LoggingCapacity)
        {
            FullMode = (BoundedChannelFullMode)PipelinePolicyConstants.LoggingFullMode,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        // Add default debug output
        AddOutput(new DebugLogOutput());

        // Start processing logs
        _processingTask = ProcessLogsAsync(_shutdownCts.Token);
    }

    /// <summary>
    /// Gets or sets the minimum log level to process.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Adds a log output destination.
    /// </summary>
    public void AddOutput(ILogOutput output)
    {
        _outputs.Add(output);
    }

    /// <summary>
    /// Logs a message at the specified level.
    /// </summary>
    public void Log(LogLevel level, string message, params (string Key, string Value)[] properties)
    {
        if (level < MinimumLevel) return;

        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Properties = properties.Length > 0 ? new Dictionary<string, string>(
                properties.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)))
                : null
        };

        _logChannel.Writer.TryWrite(entry);
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public void LogDebug(string message, params (string Key, string Value)[] properties)
        => Log(LogLevel.Debug, message, properties);

    /// <summary>
    /// Logs an info message.
    /// </summary>
    public void LogInfo(string message, params (string Key, string Value)[] properties)
        => Log(LogLevel.Info, message, properties);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void LogWarning(string message, params (string Key, string Value)[] properties)
        => Log(LogLevel.Warning, message, properties);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void LogError(string message, params (string Key, string Value)[] properties)
        => Log(LogLevel.Error, message, properties);

    /// <summary>
    /// Logs an error message with an exception.
    /// </summary>
    public void LogError(string message, Exception exception, params (string Key, string Value)[] properties)
    {
        var allProperties = new List<(string, string)>(properties)
        {
            ("exception", exception.GetType().Name),
            ("exceptionMessage", exception.Message)
        };

        if (exception.StackTrace != null)
        {
            allProperties.Add(("stackTrace", exception.StackTrace[..Math.Min(500, exception.StackTrace.Length)]));
        }

        Log(LogLevel.Error, message, allProperties.ToArray());
    }

    /// <summary>
    /// Logs a critical message.
    /// </summary>
    public void LogCritical(string message, params (string Key, string Value)[] properties)
        => Log(LogLevel.Critical, message, properties);

    // Explicit ILoggingService implementations for signature compatibility
    void ILoggingService.LogWarning(string message, Exception? exception)
    {
        if (exception != null)
            LogWarning(message, ("exception", exception.GetType().Name), ("exceptionMessage", exception.Message));
        else
            LogWarning(message);
    }

    void ILoggingService.LogError(string message, Exception? exception, params (string key, string value)[] properties)
    {
        if (exception != null)
            LogError(message, exception, properties);
        else
            LogError(message, properties);
    }

    private async Task ProcessLogsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var entry in _logChannel.Reader.ReadAllAsync(ct))
            {
                foreach (var output in _outputs)
                {
                    try
                    {
                        await output.WriteAsync(entry, ct);
                    }
                    catch
                    {
                        // Don't let output failures stop processing
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logChannel.Writer.Complete();
        _shutdownCts.Cancel();

        // Allow processing task to complete gracefully without blocking
        // Using fire-and-forget pattern with proper cancellation signal already sent
        if (_processingTask is { IsCompleted: false })
        {
            // Task will complete when cancellation is processed; don't block synchronous Dispose
            _ = _processingTask.ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously);
        }

        _shutdownCts.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Log levels.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// A log entry.
/// </summary>
public sealed class LogEntry
{
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public Dictionary<string, string>? Properties { get; init; }
}

/// <summary>
/// Interface for log output destinations.
/// </summary>
public interface ILogOutput
{
    Task WriteAsync(LogEntry entry, CancellationToken ct);
}

/// <summary>
/// Log output that writes to Debug.WriteLine.
/// </summary>
public sealed class DebugLogOutput : ILogOutput
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct)
    {
        var level = entry.Level.ToString().ToUpperInvariant();
        var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");

        var message = $"[{timestamp}] [{level}] {entry.Message}";

        if (entry.Properties?.Count > 0)
        {
            var props = string.Join(", ", entry.Properties.Select(p => $"{p.Key}={p.Value}"));
            message += $" {{ {props} }}";
        }

        Debug.WriteLine(message);
        return Task.CompletedTask;
    }
}
