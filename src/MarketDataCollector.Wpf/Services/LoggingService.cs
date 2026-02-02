using System;
using System.Diagnostics;
using System.Text;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Represents the severity level of a log entry.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Structured logging service for WPF applications.
/// Implements singleton pattern for application-wide logging.
/// </summary>
public sealed class LoggingService
{
    private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());

    private readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of the LoggingService.
    /// </summary>
    public static LoggingService Instance => _instance.Value;

    /// <summary>
    /// Gets or sets the minimum log level to output.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Occurs when a log entry is written.
    /// </summary>
    public event EventHandler<LogEntryEventArgs>? LogWritten;

    private LoggingService()
    {
    }

    /// <summary>
    /// Logs an informational message with optional structured properties.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="properties">Optional key-value pairs for structured logging.</param>
    public void LogInfo(string message, params (string key, string value)[] properties)
    {
        Log(LogLevel.Info, message, null, properties);
    }

    /// <summary>
    /// Logs a warning message with optional structured properties.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="properties">Optional key-value pairs for structured logging.</param>
    public void LogWarning(string message, params (string key, string value)[] properties)
    {
        Log(LogLevel.Warning, message, null, properties);
    }

    /// <summary>
    /// Logs an error message with optional exception details.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="ex">Optional exception associated with the error.</param>
    public void LogError(string message, Exception? ex = null)
    {
        Log(LogLevel.Error, message, ex, []);
    }

    /// <summary>
    /// Logs a debug message with optional structured properties.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="properties">Optional key-value pairs for structured logging.</param>
    public void LogDebug(string message, params (string key, string value)[] properties)
    {
        Log(LogLevel.Debug, message, null, properties);
    }

    private void Log(LogLevel level, string message, Exception? exception, (string key, string value)[] properties)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var timestamp = DateTime.UtcNow;
        var formattedMessage = FormatLogEntry(level, timestamp, message, exception, properties);

        lock (_lock)
        {
            Debug.WriteLine(formattedMessage);
        }

        OnLogWritten(new LogEntryEventArgs(level, timestamp, message, exception, properties));
    }

    private static string FormatLogEntry(
        LogLevel level,
        DateTime timestamp,
        string message,
        Exception? exception,
        (string key, string value)[] properties)
    {
        var sb = new StringBuilder();

        // Format: [YYYY-MM-DD HH:mm:ss.fff] [LEVEL] Message {key=value, ...}
        sb.Append('[');
        sb.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.Append("] [");
        sb.Append(GetLevelString(level));
        sb.Append("] ");
        sb.Append(message);

        if (properties.Length > 0)
        {
            sb.Append(" {");
            for (var i = 0; i < properties.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(properties[i].key);
                sb.Append('=');
                sb.Append(properties[i].value);
            }
            sb.Append('}');
        }

        if (exception is not null)
        {
            sb.AppendLine();
            sb.Append("  Exception: ");
            sb.Append(exception.GetType().Name);
            sb.Append(" - ");
            sb.Append(exception.Message);

            if (exception.StackTrace is not null)
            {
                sb.AppendLine();
                sb.Append("  StackTrace: ");
                sb.Append(exception.StackTrace);
            }

            if (exception.InnerException is not null)
            {
                sb.AppendLine();
                sb.Append("  InnerException: ");
                sb.Append(exception.InnerException.GetType().Name);
                sb.Append(" - ");
                sb.Append(exception.InnerException.Message);
            }
        }

        return sb.ToString();
    }

    private static string GetLevelString(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        _ => "UNKN "
    };

    /// <summary>
    /// Raises the LogWritten event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected void OnLogWritten(LogEntryEventArgs e)
    {
        LogWritten?.Invoke(this, e);
    }
}

/// <summary>
/// Event arguments for log entry events.
/// </summary>
public sealed class LogEntryEventArgs : EventArgs
{
    /// <summary>
    /// Gets the log level.
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// Gets the timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the log message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception associated with the log entry, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the structured properties associated with the log entry.
    /// </summary>
    public (string key, string value)[] Properties { get; }

    /// <summary>
    /// Initializes a new instance of the LogEntryEventArgs class.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception.</param>
    /// <param name="properties">Structured properties.</param>
    public LogEntryEventArgs(
        LogLevel level,
        DateTime timestamp,
        string message,
        Exception? exception,
        (string key, string value)[] properties)
    {
        Level = level;
        Timestamp = timestamp;
        Message = message;
        Exception = exception;
        Properties = properties;
    }
}
