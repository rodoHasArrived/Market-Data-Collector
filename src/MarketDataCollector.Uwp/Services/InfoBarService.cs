using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for displaying InfoBar notifications with appropriate durations
/// based on severity. Errors stay visible longer to ensure users notice them.
/// </summary>
public sealed class InfoBarService
{
    private static InfoBarService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static InfoBarService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new InfoBarService();
                }
            }
            return _instance;
        }
    }

    private InfoBarService() { }

    /// <summary>
    /// Duration configuration for different severity levels.
    /// Errors and critical messages stay longer to ensure visibility.
    /// </summary>
    public static class Durations
    {
        /// <summary>Informational messages - 4 seconds (user acknowledgment optional)</summary>
        public const int Info = 4000;

        /// <summary>Success messages - 3 seconds (quick confirmation)</summary>
        public const int Success = 3000;

        /// <summary>Warning messages - 6 seconds (user should notice)</summary>
        public const int Warning = 6000;

        /// <summary>Error messages - 10 seconds (requires attention)</summary>
        public const int Error = 10000;

        /// <summary>Critical errors - no auto-dismiss (manual close required)</summary>
        public const int Critical = 0;
    }

    /// <summary>
    /// Shows an InfoBar with auto-dismiss based on severity.
    /// </summary>
    /// <param name="infoBar">The InfoBar control to show</param>
    /// <param name="severity">Severity level determining display duration</param>
    /// <param name="title">Message title</param>
    /// <param name="message">Detailed message</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Task that completes when the InfoBar is dismissed</returns>
    public async Task ShowAsync(
        InfoBar infoBar,
        InfoBarSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (infoBar == null) return;

        infoBar.Severity = severity;
        infoBar.Title = title;
        infoBar.Message = message;
        infoBar.IsOpen = true;

        var duration = GetDurationForSeverity(severity);

        if (duration > 0)
        {
            try
            {
                await Task.Delay(duration, cancellationToken);
                infoBar.IsOpen = false;
            }
            catch (OperationCanceledException)
            {
                // Cancellation expected - page may have unloaded
            }
        }
        // If duration is 0 (critical), don't auto-dismiss
    }

    /// <summary>
    /// Shows an InfoBar with custom duration.
    /// </summary>
    public async Task ShowAsync(
        InfoBar infoBar,
        InfoBarSeverity severity,
        string title,
        string message,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        if (infoBar == null) return;

        infoBar.Severity = severity;
        infoBar.Title = title;
        infoBar.Message = message;
        infoBar.IsOpen = true;

        if (durationMs > 0)
        {
            try
            {
                await Task.Delay(durationMs, cancellationToken);
                infoBar.IsOpen = false;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    /// <summary>
    /// Shows an error InfoBar with context and remedy information.
    /// </summary>
    public async Task ShowErrorAsync(
        InfoBar infoBar,
        string title,
        string message,
        string? context = null,
        string? remedy = null,
        CancellationToken cancellationToken = default)
    {
        if (infoBar == null) return;

        var fullMessage = message;
        if (!string.IsNullOrEmpty(context))
        {
            fullMessage += $"\n\nContext: {context}";
        }
        if (!string.IsNullOrEmpty(remedy))
        {
            fullMessage += $"\n\nSuggestion: {remedy}";
        }

        await ShowAsync(infoBar, InfoBarSeverity.Error, title, fullMessage, cancellationToken);
    }

    /// <summary>
    /// Creates a user-friendly error message with context and remedies.
    /// </summary>
    public static ErrorDetails CreateErrorDetails(Exception ex, string operation)
    {
        return ex switch
        {
            OperationCanceledException => new ErrorDetails
            {
                Title = "Operation Cancelled",
                Message = $"The {operation} was cancelled.",
                Context = "User cancelled the operation or the request timed out.",
                Remedy = "If this was unexpected, try the operation again.",
                Severity = InfoBarSeverity.Warning
            },
            TimeoutException => new ErrorDetails
            {
                Title = "Request Timeout",
                Message = $"The {operation} took too long to complete.",
                Context = "The server may be busy or unresponsive.",
                Remedy = "Wait a moment and try again. If the problem persists, check your connection.",
                Severity = InfoBarSeverity.Error
            },
            UnauthorizedAccessException => new ErrorDetails
            {
                Title = "Access Denied",
                Message = $"Permission denied for {operation}.",
                Context = "You may not have the required permissions.",
                Remedy = "Check your credentials or contact your administrator.",
                Severity = InfoBarSeverity.Error
            },
            System.Net.Http.HttpRequestException httpEx => new ErrorDetails
            {
                Title = "Connection Error",
                Message = $"Failed to connect while {operation}.",
                Context = httpEx.Message,
                Remedy = "Check your internet connection and ensure the collector service is running.",
                Severity = InfoBarSeverity.Error
            },
            System.IO.IOException ioEx => new ErrorDetails
            {
                Title = "File System Error",
                Message = $"Error accessing files during {operation}.",
                Context = ioEx.Message,
                Remedy = "Ensure you have proper permissions and sufficient disk space.",
                Severity = InfoBarSeverity.Error
            },
            ArgumentException argEx => new ErrorDetails
            {
                Title = "Invalid Input",
                Message = $"Invalid data provided for {operation}.",
                Context = argEx.Message,
                Remedy = "Check your input values and try again.",
                Severity = InfoBarSeverity.Warning
            },
            InvalidOperationException invEx => new ErrorDetails
            {
                Title = "Invalid Operation",
                Message = $"Cannot perform {operation} in the current state.",
                Context = invEx.Message,
                Remedy = "Ensure the application is in the correct state before retrying.",
                Severity = InfoBarSeverity.Warning
            },
            _ => new ErrorDetails
            {
                Title = "Unexpected Error",
                Message = $"An error occurred during {operation}.",
                Context = ex.Message,
                Remedy = "Try the operation again. If the problem persists, check the logs or restart the application.",
                Severity = InfoBarSeverity.Error
            }
        };
    }

    /// <summary>
    /// Gets the recommended duration for a severity level.
    /// </summary>
    public static int GetDurationForSeverity(InfoBarSeverity severity)
    {
        return severity switch
        {
            InfoBarSeverity.Informational => Durations.Info,
            InfoBarSeverity.Success => Durations.Success,
            InfoBarSeverity.Warning => Durations.Warning,
            InfoBarSeverity.Error => Durations.Error,
            _ => Durations.Info
        };
    }
}

/// <summary>
/// Contains detailed error information for user display.
/// </summary>
public class ErrorDetails
{
    /// <summary>Error title</summary>
    public string Title { get; set; } = "Error";

    /// <summary>Main error message</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Additional context about what happened</summary>
    public string? Context { get; set; }

    /// <summary>Suggested remedy or next steps</summary>
    public string? Remedy { get; set; }

    /// <summary>Severity level for the InfoBar</summary>
    public InfoBarSeverity Severity { get; set; } = InfoBarSeverity.Error;

    /// <summary>
    /// Gets the full formatted message including context and remedy.
    /// </summary>
    public string GetFormattedMessage()
    {
        var result = Message;
        if (!string.IsNullOrEmpty(Context))
        {
            result += $"\n\nDetails: {Context}";
        }
        if (!string.IsNullOrEmpty(Remedy))
        {
            result += $"\n\nSuggestion: {Remedy}";
        }
        return result;
    }
}
