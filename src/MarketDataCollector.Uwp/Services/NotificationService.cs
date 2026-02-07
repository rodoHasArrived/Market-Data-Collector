using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using MarketDataCollector.Ui.Services.Contracts;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing Windows toast notifications.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static NotificationService? _instance;
    private static readonly object _lock = new();

    private NotificationSettings _settings = new();
    private readonly BoundedObservableCollection<NotificationHistoryItem> _history;
    private readonly object _historyLock = new();
    private const int MaxHistoryItems = 50;

    public static NotificationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new NotificationService();
                }
            }
            return _instance;
        }
    }

    private NotificationService()
    {
        _history = new BoundedObservableCollection<NotificationHistoryItem>(MaxHistoryItems);

        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            // Notifications may not be available in all environments (e.g., headless, containers)
            System.Diagnostics.Debug.WriteLine($"[NotificationService] Failed to register for notifications: {ex.GetType().Name} - {ex.Message}");
        }
    }

    /// <summary>
    /// Updates notification settings.
    /// </summary>
    public void UpdateSettings(NotificationSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Gets the current notification settings.
    /// </summary>
    public NotificationSettings GetSettings() => _settings;

    /// <summary>
    /// Gets notification history.
    /// </summary>
    public IReadOnlyList<NotificationHistoryItem> GetHistory()
    {
        lock (_historyLock)
        {
            return _history.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Clears notification history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _history.Clear();
        }
    }

    /// <summary>
    /// Shows a connection status notification.
    /// </summary>
    public async Task NotifyConnectionStatusAsync(bool connected, string providerName, string? details = null)
    {
        if (!_settings.Enabled || !_settings.NotifyConnectionStatus) return;
        if (IsQuietHours()) return;

        var title = connected ? "Connected" : "Connection Lost";
        var message = connected
            ? $"Successfully connected to {providerName}"
            : $"Lost connection to {providerName}";

        if (!string.IsNullOrEmpty(details))
        {
            message += $". {details}";
        }

        await ShowNotificationAsync(
            title,
            message,
            connected ? NotificationType.Success : NotificationType.Error,
            "connection");
    }

    /// <summary>
    /// Shows a reconnection attempt notification.
    /// </summary>
    public async Task NotifyReconnectionAttemptAsync(string providerName, int attempt, int maxAttempts)
    {
        if (!_settings.Enabled || !_settings.NotifyConnectionStatus) return;
        if (IsQuietHours()) return;

        await ShowNotificationAsync(
            "Reconnecting...",
            $"Attempting to reconnect to {providerName} ({attempt}/{maxAttempts})",
            NotificationType.Warning,
            "reconnect");
    }

    /// <summary>
    /// Shows an error notification.
    /// </summary>
    public async Task NotifyErrorAsync(string title, string message, string? actionUrl = null)
    {
        if (!_settings.Enabled || !_settings.NotifyErrors) return;
        if (IsQuietHours()) return;

        await ShowNotificationAsync(title, message, NotificationType.Error, "error", actionUrl);
    }

    /// <summary>
    /// Shows a warning notification.
    /// </summary>
    public async Task NotifyWarningAsync(string title, string message)
    {
        if (!_settings.Enabled || !_settings.NotifyErrors) return;
        if (IsQuietHours()) return;

        await ShowNotificationAsync(title, message, NotificationType.Warning, "warning");
    }

    /// <summary>
    /// Shows a backfill completion notification.
    /// </summary>
    public async Task NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration)
    {
        if (!_settings.Enabled || !_settings.NotifyBackfillComplete) return;
        if (IsQuietHours()) return;

        var title = success ? "Backfill Complete" : "Backfill Failed";
        var message = success
            ? $"Downloaded {barsWritten:N0} bars for {symbolCount} symbol(s) in {FormatDuration(duration)}"
            : $"Backfill failed after {FormatDuration(duration)}";

        await ShowNotificationAsync(
            title,
            message,
            success ? NotificationType.Success : NotificationType.Error,
            "backfill");
    }

    /// <summary>
    /// Shows a data gap detection notification.
    /// </summary>
    public async Task NotifyDataGapAsync(string symbol, DateTime gapStart, DateTime gapEnd, int missingBars)
    {
        if (!_settings.Enabled || !_settings.NotifyDataGaps) return;
        if (IsQuietHours()) return;

        await ShowNotificationAsync(
            "Data Gap Detected",
            $"{symbol}: {missingBars} missing bars from {gapStart:yyyy-MM-dd} to {gapEnd:yyyy-MM-dd}",
            NotificationType.Warning,
            "datagap");
    }

    /// <summary>
    /// Shows a storage warning notification.
    /// </summary>
    public async Task NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes)
    {
        if (!_settings.Enabled || !_settings.NotifyStorageWarnings) return;
        if (IsQuietHours()) return;

        var freeSpaceFormatted = FormatBytes(freeSpaceBytes);
        var title = usedPercent >= 95 ? "Critical: Storage Almost Full" : "Storage Warning";

        await ShowNotificationAsync(
            title,
            $"Data drive is {usedPercent:F1}% full. Only {freeSpaceFormatted} remaining.",
            usedPercent >= 95 ? NotificationType.Error : NotificationType.Warning,
            "storage");
    }

    /// <summary>
    /// Shows a scheduled job notification.
    /// </summary>
    public async Task NotifyScheduledJobAsync(string jobName, bool started, bool? success = null)
    {
        if (!_settings.Enabled || !_settings.NotifyBackfillComplete) return;
        if (IsQuietHours()) return;

        string title, message;
        NotificationType type;

        if (started)
        {
            title = "Scheduled Job Started";
            message = $"{jobName} is now running";
            type = NotificationType.Info;
        }
        else if (success == true)
        {
            title = "Scheduled Job Complete";
            message = $"{jobName} completed successfully";
            type = NotificationType.Success;
        }
        else
        {
            title = "Scheduled Job Failed";
            message = $"{jobName} failed to complete";
            type = NotificationType.Error;
        }

        await ShowNotificationAsync(title, message, type, "schedule");
    }

    /// <summary>
    /// Shows a test notification.
    /// </summary>
    public async Task SendTestNotificationAsync()
    {
        await ShowNotificationAsync(
            "Test Notification",
            "Notifications are working correctly!",
            NotificationType.Info,
            "test");
    }

    /// <summary>
    /// Shows a generic notification with specified type.
    /// </summary>
    public async Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info, string? actionUrl = null)
    {
        if (!_settings.Enabled) return;
        if (IsQuietHours()) return;

        var tag = type.ToString().ToLower();
        await ShowNotificationAsync(title, message, type, tag, actionUrl);
    }

    private async Task ShowNotificationAsync(
        string title,
        string message,
        NotificationType type,
        string tag,
        string? actionUrl = null)
    {
        // Add to history with thread-safe access using efficient Prepend
        lock (_historyLock)
        {
            _history.Prepend(new NotificationHistoryItem
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.Now,
                Tag = tag
            });
            // No manual trimming needed - BoundedObservableCollection handles capacity automatically
        }

        // Raise event for in-app notification handling
        NotificationReceived?.Invoke(this, new NotificationEventArgs
        {
            Title = title,
            Message = message,
            Type = type,
            Tag = tag
        });

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .SetTag(tag)
                .SetGroup("MarketDataCollector");

            // Add action button if URL provided
            if (!string.IsNullOrEmpty(actionUrl))
            {
                builder.AddButton(new AppNotificationButton("View Details")
                    .AddArgument("action", "open")
                    .AddArgument("url", actionUrl));
            }

            // Set attribution for branding
            builder.SetAttributionText("Market Data Collector");

            var notification = builder.BuildNotification();

            await Task.Run(() =>
            {
                AppNotificationManager.Default.Show(notification);
            });
        }
        catch (Exception ex)
        {
            // Notification display failed - already logged to history, log for diagnostics
            System.Diagnostics.Debug.WriteLine($"[NotificationService] Failed to display notification '{title}': {ex.GetType().Name} - {ex.Message}");
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // Handle notification click actions
        NotificationActivated?.Invoke(this, new NotificationActivatedEventArgs
        {
            Arguments = args.Arguments
        });
    }

    private bool IsQuietHours()
    {
        if (!_settings.QuietHoursEnabled) return false;

        var now = DateTime.Now.TimeOfDay;
        var start = _settings.QuietHoursStart;
        var end = _settings.QuietHoursEnd;

        // Handle overnight quiet hours (e.g., 22:00 - 07:00)
        if (start > end)
        {
            return now >= start || now <= end;
        }

        return now >= start && now <= end;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F1} {sizes[order]}";
    }

    /// <summary>
    /// Event raised when a notification is received (for in-app display).
    /// </summary>
    public event EventHandler<NotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Event raised when a notification is activated (clicked).
    /// </summary>
    public event EventHandler<NotificationActivatedEventArgs>? NotificationActivated;
}

/// <summary>
/// Notification settings.
/// </summary>
public class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public bool NotifyConnectionStatus { get; set; } = true;
    public bool NotifyErrors { get; set; } = true;
    public bool NotifyBackfillComplete { get; set; } = true;
    public bool NotifyDataGaps { get; set; } = true;
    public bool NotifyStorageWarnings { get; set; } = true;
    public string SoundType { get; set; } = "Default"; // Default, Subtle, None
    public bool QuietHoursEnabled { get; set; }
    public TimeSpan QuietHoursStart { get; set; } = new TimeSpan(22, 0, 0);
    public TimeSpan QuietHoursEnd { get; set; } = new TimeSpan(7, 0, 0);
}

/// <summary>
/// Notification types.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Notification history item.
/// </summary>
public class NotificationHistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string Tag { get; set; } = string.Empty;
}

/// <summary>
/// Event args for notification received.
/// </summary>
public class NotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Tag { get; set; } = string.Empty;
}

/// <summary>
/// Event args for notification activation.
/// </summary>
public class NotificationActivatedEventArgs : EventArgs
{
    public IDictionary<string, string>? Arguments { get; set; }
}
