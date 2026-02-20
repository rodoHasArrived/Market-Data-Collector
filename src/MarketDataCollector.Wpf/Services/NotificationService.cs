using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;
using ContractNotificationType = MarketDataCollector.Ui.Services.NotificationType;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing in-app notifications.
/// Provides notification display, history tracking, and settings management.
/// Implements <see cref="INotificationService"/> for testability and consistency across platforms.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static NotificationService? _instance;
    private static readonly object _lock = new();

    private NotificationSettings _settings = new();
    private readonly List<NotificationHistoryItem> _history = new();
    private readonly object _historyLock = new();
    private const int MaxHistoryItems = 100;

    // Smart suppression: deduplication and rate limiting
    private readonly Dictionary<string, DateTime> _recentNotifications = new();
    private readonly Dictionary<string, int> _groupedCounts = new();
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(30);
    private const int MaxNotificationsPerMinute = 10;
    private int _notificationsThisMinute;
    private DateTime _minuteWindowStart = DateTime.UtcNow;

    /// <summary>
    /// Gets the singleton instance of the NotificationService.
    /// </summary>
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
    }

    /// <summary>
    /// Event raised when a notification is received.
    /// </summary>
    public event EventHandler<NotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Shows a notification with the specified parameters.
    /// Applies smart suppression: deduplication (same title within 30s window)
    /// and rate limiting (max 10 notifications per minute).
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="durationMs">Display duration in milliseconds (0 for persistent).</param>
    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000)
    {
        if (!_settings.Enabled) return;
        if (IsQuietHours()) return;

        var dedupeKey = $"{title}:{type}";

        lock (_historyLock)
        {
            // Deduplication: suppress identical notifications within the window
            if (_recentNotifications.TryGetValue(dedupeKey, out var lastSeen))
            {
                if (DateTime.UtcNow - lastSeen < DeduplicationWindow)
                {
                    // Update grouped count instead of showing duplicate
                    _groupedCounts.TryGetValue(dedupeKey, out var count);
                    _groupedCounts[dedupeKey] = count + 1;

                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] Suppressed duplicate: {title} (count: {count + 1})");
                    return;
                }
            }

            // Rate limiting: prevent notification storms
            var now = DateTime.UtcNow;
            if (now - _minuteWindowStart > TimeSpan.FromMinutes(1))
            {
                _minuteWindowStart = now;
                _notificationsThisMinute = 0;
            }

            if (_notificationsThisMinute >= MaxNotificationsPerMinute && type < NotificationType.Error)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] Rate limited: {title}");
                return;
            }

            _notificationsThisMinute++;
            _recentNotifications[dedupeKey] = now;

            // Check if this is a grouped notification (had duplicates)
            if (_groupedCounts.TryGetValue(dedupeKey, out var groupedCount) && groupedCount > 0)
            {
                message = $"{message} (+{groupedCount} similar)";
                _groupedCounts.Remove(dedupeKey);
            }
        }

        var historyItem = new NotificationHistoryItem
        {
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.Now,
            Tag = type.ToString().ToLowerInvariant()
        };

        // Add to history with thread-safe access
        lock (_historyLock)
        {
            _history.Insert(0, historyItem);

            // Trim history if needed
            while (_history.Count > MaxHistoryItems)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        // Raise event for UI handling
        NotificationReceived?.Invoke(this, new NotificationEventArgs
        {
            Title = title,
            Message = message,
            Type = type,
            Tag = historyItem.Tag,
            DurationMs = durationMs
        });

        System.Diagnostics.Debug.WriteLine($"[NotificationService] {type}: {title} - {message}");
    }

    /// <summary>
    /// Shows an error notification asynchronously.
    /// </summary>
    /// <param name="title">The error title.</param>
    /// <param name="message">The error message.</param>
    /// <param name="exception">Optional exception for additional context.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task NotifyErrorAsync(string title, string message, Exception? exception = null)
    {
        if (!_settings.Enabled || !_settings.NotifyErrors) return;
        if (IsQuietHours()) return;

        var fullMessage = exception != null
            ? $"{message}: {exception.Message}"
            : message;

        await Task.Run(() =>
        {
            ShowNotification(title, fullMessage, NotificationType.Error, 0); // Persistent for errors
        });
    }

    /// <summary>
    /// Shows a success notification.
    /// </summary>
    public void NotifySuccess(string title, string message)
    {
        ShowNotification(title, message, NotificationType.Success, 3000);
    }

    /// <summary>
    /// Shows a warning notification.
    /// </summary>
    public void NotifyWarning(string title, string message)
    {
        if (!_settings.Enabled || !_settings.NotifyErrors) return;
        ShowNotification(title, message, NotificationType.Warning, 5000);
    }

    /// <summary>
    /// Shows a warning notification asynchronously (INotificationService implementation).
    /// </summary>
    /// <param name="title">The warning title.</param>
    /// <param name="message">The warning message.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task NotifyWarningAsync(string title, string message)
    {
        await Task.Run(() => NotifyWarning(title, message));
    }

    /// <summary>
    /// Shows a general notification asynchronously (INotificationService implementation).
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="type">The notification type.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task NotifyAsync(string title, string message, ContractNotificationType type = ContractNotificationType.Info)
    {
        // Convert contract type to local type
        var localType = type switch
        {
            ContractNotificationType.Success => NotificationType.Success,
            ContractNotificationType.Warning => NotificationType.Warning,
            ContractNotificationType.Error => NotificationType.Error,
            _ => NotificationType.Info
        };
        await Task.Run(() => ShowNotification(title, message, localType));
    }

    /// <summary>
    /// Shows an info notification.
    /// </summary>
    public void NotifyInfo(string title, string message)
    {
        ShowNotification(title, message, NotificationType.Info, 4000);
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

        await Task.Run(() =>
        {
            ShowNotification(
                title,
                message,
                connected ? NotificationType.Success : NotificationType.Error,
                connected ? 3000 : 0);
        });
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

        await Task.Run(() =>
        {
            ShowNotification(title, message, success ? NotificationType.Success : NotificationType.Error);
        });
    }

    /// <summary>
    /// Shows a scheduled job notification (INotificationService implementation).
    /// </summary>
    /// <param name="jobName">The name of the scheduled job.</param>
    /// <param name="started">Whether the job started (true) or completed (false).</param>
    /// <param name="success">Whether the job completed successfully (only relevant when started=false).</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task NotifyScheduledJobAsync(string jobName, bool started, bool success = true)
    {
        if (!_settings.Enabled) return;
        if (IsQuietHours()) return;

        var title = started ? "Scheduled Job Started" : (success ? "Scheduled Job Complete" : "Scheduled Job Failed");
        var message = started ? $"Job '{jobName}' is now running" : $"Job '{jobName}' has completed";
        var type = started ? NotificationType.Info : (success ? NotificationType.Success : NotificationType.Error);

        await Task.Run(() =>
        {
            ShowNotification(title, message, type, started ? 3000 : 5000);
        });
    }

    /// <summary>
    /// Shows a data gap detection notification.
    /// </summary>
    public async Task NotifyDataGapAsync(string symbol, DateTime gapStart, DateTime gapEnd, int missingBars)
    {
        if (!_settings.Enabled || !_settings.NotifyDataGaps) return;
        if (IsQuietHours()) return;

        await Task.Run(() =>
        {
            ShowNotification(
                "Data Gap Detected",
                $"{symbol}: {missingBars} missing bars from {gapStart:yyyy-MM-dd} to {gapEnd:yyyy-MM-dd}",
                NotificationType.Warning);
        });
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
        var type = usedPercent >= 95 ? NotificationType.Error : NotificationType.Warning;

        await Task.Run(() =>
        {
            ShowNotification(
                title,
                $"Data drive is {usedPercent:F1}% full. Only {freeSpaceFormatted} remaining.",
                type);
        });
    }

    /// <summary>
    /// Sends a test notification.
    /// </summary>
    public void SendTestNotification()
    {
        ShowNotification(
            "Test Notification",
            "Notifications are working correctly!",
            NotificationType.Info);
    }

    /// <summary>
    /// Updates notification settings.
    /// </summary>
    public void UpdateSettings(NotificationSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
            return _history.ToArray();
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
    /// Marks a notification as read by index.
    /// </summary>
    public void MarkAsRead(int index)
    {
        lock (_historyLock)
        {
            if (index >= 0 && index < _history.Count)
            {
                _history[index].IsRead = true;
            }
        }
    }

    /// <summary>
    /// Gets the count of unread notifications.
    /// </summary>
    public int GetUnreadCount()
    {
        lock (_historyLock)
        {
            var count = 0;
            foreach (var item in _history)
            {
                if (!item.IsRead) count++;
            }
            return count;
        }
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

    private static string FormatBytes(long bytes) => FormatHelpers.FormatBytes(bytes);
}

/// <summary>
/// Notification types.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Informational notification.
    /// </summary>
    Info,

    /// <summary>
    /// Success notification.
    /// </summary>
    Success,

    /// <summary>
    /// Warning notification.
    /// </summary>
    Warning,

    /// <summary>
    /// Error notification.
    /// </summary>
    Error
}

/// <summary>
/// Notification settings.
/// </summary>
public sealed class NotificationSettings
{
    /// <summary>
    /// Gets or sets whether notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to notify on connection status changes.
    /// </summary>
    public bool NotifyConnectionStatus { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to notify on errors.
    /// </summary>
    public bool NotifyErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to notify on backfill completion.
    /// </summary>
    public bool NotifyBackfillComplete { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to notify on data gaps.
    /// </summary>
    public bool NotifyDataGaps { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to notify on storage warnings.
    /// </summary>
    public bool NotifyStorageWarnings { get; set; } = true;

    /// <summary>
    /// Gets or sets the sound type (Default, Subtle, None).
    /// </summary>
    public string SoundType { get; set; } = "Default";

    /// <summary>
    /// Gets or sets whether quiet hours are enabled.
    /// </summary>
    public bool QuietHoursEnabled { get; set; }

    /// <summary>
    /// Gets or sets the quiet hours start time.
    /// </summary>
    public TimeSpan QuietHoursStart { get; set; } = new TimeSpan(22, 0, 0);

    /// <summary>
    /// Gets or sets the quiet hours end time.
    /// </summary>
    public TimeSpan QuietHoursEnd { get; set; } = new TimeSpan(7, 0, 0);
}

/// <summary>
/// Notification history item.
/// </summary>
public sealed class NotificationHistoryItem
{
    /// <summary>
    /// Gets or sets the notification title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification type.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the notification timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the notification tag.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the notification has been read.
    /// </summary>
    public bool IsRead { get; set; }
}

/// <summary>
/// Event args for notification received.
/// </summary>
public sealed class NotificationEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the notification title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification type.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the notification tag.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display duration in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }
}
