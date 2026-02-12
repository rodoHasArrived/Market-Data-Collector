using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for displaying InfoBar notifications with appropriate durations
/// based on severity. Errors stay visible longer to ensure users notice them.
/// Delegates shared logic to <see cref="InfoBarConstants"/> and <see cref="ErrorDetailsModel"/> in Ui.Services.
/// </summary>
public sealed class InfoBarService : IInfoBarService
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
        public const int Info = InfoBarConstants.InfoDurationMs;
        public const int Success = InfoBarConstants.SuccessDurationMs;
        public const int Warning = InfoBarConstants.WarningDurationMs;
        public const int Error = InfoBarConstants.ErrorDurationMs;
        public const int Critical = InfoBarConstants.CriticalDurationMs;
    }

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
    }

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
            fullMessage += $"\n\nContext: {context}";
        if (!string.IsNullOrEmpty(remedy))
            fullMessage += $"\n\nSuggestion: {remedy}";

        await ShowAsync(infoBar, InfoBarSeverity.Error, title, fullMessage, cancellationToken);
    }

    public static ErrorDetails CreateErrorDetails(Exception ex, string operation)
    {
        var shared = ErrorDetailsModel.CreateFromException(ex, operation);
        return new ErrorDetails
        {
            Title = shared.Title,
            Message = shared.Message,
            Context = shared.Context,
            Remedy = shared.Remedy,
            Severity = (InfoBarSeverity)shared.Severity
        };
    }

    public static int GetDurationForSeverity(InfoBarSeverity severity)
    {
        return InfoBarConstants.GetDurationForSeverity((InfoBarSeverityLevel)severity);
    }
}

/// <summary>
/// Contains detailed error information for user display.
/// </summary>
public sealed class ErrorDetails
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
