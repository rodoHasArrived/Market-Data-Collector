using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Severity levels for info bar notifications.
/// Replaces WinUI InfoBarSeverity for WPF compatibility.
/// </summary>
public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error
}

/// <summary>
/// Service for managing notification bar display with appropriate durations
/// based on severity. Errors stay visible longer to ensure users notice them.
/// In WPF, this uses an event-based approach instead of directly manipulating WinUI InfoBar controls.
/// </summary>
public sealed class InfoBarService
{
    private static InfoBarService? _instance;
    private static readonly object _lock = new();

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

    public static class Durations
    {
        public const int Info = 4000;
        public const int Success = 3000;
        public const int Warning = 6000;
        public const int Error = 10000;
        public const int Critical = 0;
    }

    /// <summary>
    /// Event raised when a notification should be shown.
    /// Pages can subscribe to this to display notifications in their status area.
    /// </summary>
    public event EventHandler<InfoBarNotificationEventArgs>? NotificationRequested;

    public async Task ShowAsync(
        InfoBarSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        var duration = GetDurationForSeverity(severity);

        NotificationRequested?.Invoke(this, new InfoBarNotificationEventArgs
        {
            Severity = severity,
            Title = title,
            Message = message,
            DurationMs = duration,
            IsOpen = true
        });

        if (duration > 0)
        {
            try
            {
                await Task.Delay(duration, cancellationToken);
                NotificationRequested?.Invoke(this, new InfoBarNotificationEventArgs { IsOpen = false });
            }
            catch (OperationCanceledException)
            {
                // Cancellation expected
            }
        }
    }

    public async Task ShowAsync(
        InfoBarSeverity severity,
        string title,
        string message,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        NotificationRequested?.Invoke(this, new InfoBarNotificationEventArgs
        {
            Severity = severity,
            Title = title,
            Message = message,
            DurationMs = durationMs,
            IsOpen = true
        });

        if (durationMs > 0)
        {
            try
            {
                await Task.Delay(durationMs, cancellationToken);
                NotificationRequested?.Invoke(this, new InfoBarNotificationEventArgs { IsOpen = false });
            }
            catch (OperationCanceledException) { /* Expected during shutdown or dismissal */ }
        }
    }

    public async Task ShowErrorAsync(
        string title,
        string message,
        string? context = null,
        string? remedy = null,
        CancellationToken cancellationToken = default)
    {
        var fullMessage = message;
        if (!string.IsNullOrEmpty(context))
            fullMessage += $"\n\nContext: {context}";
        if (!string.IsNullOrEmpty(remedy))
            fullMessage += $"\n\nSuggestion: {remedy}";

        await ShowAsync(InfoBarSeverity.Error, title, fullMessage, cancellationToken);
    }

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

public sealed class InfoBarNotificationEventArgs : EventArgs
{
    public InfoBarSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int DurationMs { get; init; }
    public bool IsOpen { get; init; }
}

public class ErrorDetails
{
    public string Title { get; set; } = "Error";
    public string Message { get; set; } = string.Empty;
    public string? Context { get; set; }
    public string? Remedy { get; set; }
    public InfoBarSeverity Severity { get; set; } = InfoBarSeverity.Error;

    public string GetFormattedMessage()
    {
        var result = Message;
        if (!string.IsNullOrEmpty(Context))
            result += $"\n\nDetails: {Context}";
        if (!string.IsNullOrEmpty(Remedy))
            result += $"\n\nSuggestion: {Remedy}";
        return result;
    }
}
