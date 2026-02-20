using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NotificationType = MarketDataCollector.Ui.Services.NotificationType;

namespace MarketDataCollector.Ui.Services.Services;

/// <summary>
/// Notification settings.
/// </summary>
public sealed class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public bool NotifyConnectionStatus { get; set; } = true;
    public bool NotifyErrors { get; set; } = true;
    public bool NotifyBackfillComplete { get; set; } = true;
    public bool NotifyDataGaps { get; set; } = true;
    public bool NotifyStorageWarnings { get; set; } = true;
    public string SoundType { get; set; } = "Default";
    public bool QuietHoursEnabled { get; set; }
    public TimeSpan QuietHoursStart { get; set; } = new TimeSpan(22, 0, 0);
    public TimeSpan QuietHoursEnd { get; set; } = new TimeSpan(7, 0, 0);
}

/// <summary>
/// Notification history item.
/// </summary>
public sealed class NotificationHistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string Tag { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

/// <summary>
/// Event args for notification received.
/// </summary>
public sealed class NotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Tag { get; set; } = string.Empty;
    public int DurationMs { get; set; }
}

/// <summary>
/// Abstract base class for notification management shared between platforms.
/// Provides smart suppression (deduplication + rate limiting), history tracking,
/// settings management, and domain-specific notification methods.
/// Part of Phase 2 service extraction.
/// </summary>
public abstract class NotificationServiceBase
{
    private NotificationSettings _settings = new();
    private readonly List<NotificationHistoryItem> _history = new();
    protected readonly object HistoryLock = new();
    private const int MaxHistoryItems = 100;

    // Smart suppression: deduplication and rate limiting
    private readonly Dictionary<string, DateTime> _recentNotifications = new();
    private readonly Dictionary<string, int> _groupedCounts = new();
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(30);
    private const int MaxNotificationsPerMinute = 10;
    private int _notificationsThisMinute;
    private DateTime _minuteWindowStart = DateTime.UtcNow;

    public event EventHandler<NotificationEventArgs>? NotificationReceived;

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000)
    {
        if (!_settings.Enabled) return;
        if (IsQuietHours()) return;

        var dedupeKey = $"{title}:{type}";

        lock (HistoryLock)
        {
            if (_recentNotifications.TryGetValue(dedupeKey, out var lastSeen))
            {
                if (DateTime.UtcNow - lastSeen < DeduplicationWindow)
                {
                    _groupedCounts.TryGetValue(dedupeKey, out var count);
                    _groupedCounts[dedupeKey] = count + 1;
                    Debug.WriteLine($"[NotificationService] Suppressed duplicate: {title} (count: {count + 1})");
                    return;
                }
            }

            var now = DateTime.UtcNow;
            if (now - _minuteWindowStart > TimeSpan.FromMinutes(1))
            {
                _minuteWindowStart = now;
                _notificationsThisMinute = 0;
            }

            if (_notificationsThisMinute >= MaxNotificationsPerMinute && type < NotificationType.Error)
            {
                Debug.WriteLine($"[NotificationService] Rate limited: {title}");
                return;
            }

            _notificationsThisMinute++;
            _recentNotifications[dedupeKey] = now;

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

        lock (HistoryLock)
        {
            _history.Insert(0, historyItem);
            while (_history.Count > MaxHistoryItems)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        NotificationReceived?.Invoke(this, new NotificationEventArgs
        {
            Title = title,
            Message = message,
            Type = type,
            Tag = historyItem.Tag,
            DurationMs = durationMs
        });

        Debug.WriteLine($"[NotificationService] {type}: {title} - {message}");
    }

    public async Task NotifyErrorAsync(string title, string message, Exception? exception = null)
    {
        if (!_settings.Enabled || !_settings.NotifyErrors) return;
        if (IsQuietHours()) return;

        var fullMessage = exception != null ? $"{message}: {exception.Message}" : message;
        await Task.Run(() => ShowNotification(title, fullMessage, NotificationType.Error, 0));
    }

    public void NotifySuccess(string title, string message)
        => ShowNotification(title, message, NotificationType.Success, 3000);

    public void NotifyWarning(string title, string message)
    {
        if (!_settings.Enabled || !_settings.NotifyErrors) return;
        ShowNotification(title, message, NotificationType.Warning, 5000);
    }

    public async Task NotifyWarningAsync(string title, string message)
        => await Task.Run(() => NotifyWarning(title, message));

    public async Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info)
    {
        await Task.Run(() => ShowNotification(title, message, type));
    }

    public void NotifyInfo(string title, string message)
        => ShowNotification(title, message, NotificationType.Info, 4000);

    public async Task NotifyConnectionStatusAsync(bool connected, string providerName, string? details = null)
    {
        if (!_settings.Enabled || !_settings.NotifyConnectionStatus) return;
        if (IsQuietHours()) return;

        var title = connected ? "Connected" : "Connection Lost";
        var message = connected
            ? $"Successfully connected to {providerName}"
            : $"Lost connection to {providerName}";

        if (!string.IsNullOrEmpty(details)) message += $". {details}";

        await Task.Run(() => ShowNotification(title, message,
            connected ? NotificationType.Success : NotificationType.Error,
            connected ? 3000 : 0));
    }

    public async Task NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration)
    {
        if (!_settings.Enabled || !_settings.NotifyBackfillComplete) return;
        if (IsQuietHours()) return;

        var title = success ? "Backfill Complete" : "Backfill Failed";
        var message = success
            ? $"Downloaded {barsWritten:N0} bars for {symbolCount} symbol(s) in {FormatDuration(duration)}"
            : $"Backfill failed after {FormatDuration(duration)}";

        await Task.Run(() => ShowNotification(title, message, success ? NotificationType.Success : NotificationType.Error));
    }

    public async Task NotifyScheduledJobAsync(string jobName, bool started, bool success = true)
    {
        if (!_settings.Enabled) return;
        if (IsQuietHours()) return;

        var title = started ? "Scheduled Job Started" : (success ? "Scheduled Job Complete" : "Scheduled Job Failed");
        var message = started ? $"Job '{jobName}' is now running" : $"Job '{jobName}' has completed";
        var type = started ? NotificationType.Info : (success ? NotificationType.Success : NotificationType.Error);

        await Task.Run(() => ShowNotification(title, message, type, started ? 3000 : 5000));
    }

    public async Task NotifyDataGapAsync(string symbol, DateTime gapStart, DateTime gapEnd, int missingBars)
    {
        if (!_settings.Enabled || !_settings.NotifyDataGaps) return;
        if (IsQuietHours()) return;

        await Task.Run(() => ShowNotification(
            "Data Gap Detected",
            $"{symbol}: {missingBars} missing bars from {gapStart:yyyy-MM-dd} to {gapEnd:yyyy-MM-dd}",
            NotificationType.Warning));
    }

    public async Task NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes)
    {
        if (!_settings.Enabled || !_settings.NotifyStorageWarnings) return;
        if (IsQuietHours()) return;

        var freeSpaceFormatted = FormatHelpers.FormatBytes(freeSpaceBytes);
        var title = usedPercent >= 95 ? "Critical: Storage Almost Full" : "Storage Warning";
        var type = usedPercent >= 95 ? NotificationType.Error : NotificationType.Warning;

        await Task.Run(() => ShowNotification(title,
            $"Data drive is {usedPercent:F1}% full. Only {freeSpaceFormatted} remaining.", type));
    }

    public void SendTestNotification()
        => ShowNotification("Test Notification", "Notifications are working correctly!", NotificationType.Info);

    public void UpdateSettings(NotificationSettings settings)
        => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public NotificationSettings GetSettings() => _settings;

    public IReadOnlyList<NotificationHistoryItem> GetHistory()
    {
        lock (HistoryLock) { return _history.ToArray(); }
    }

    public void ClearHistory()
    {
        lock (HistoryLock) { _history.Clear(); }
    }

    public void MarkAsRead(int index)
    {
        lock (HistoryLock)
        {
            if (index >= 0 && index < _history.Count) _history[index].IsRead = true;
        }
    }

    public int GetUnreadCount()
    {
        lock (HistoryLock)
        {
            var count = 0;
            foreach (var item in _history) { if (!item.IsRead) count++; }
            return count;
        }
    }

    private bool IsQuietHours()
    {
        if (!_settings.QuietHoursEnabled) return false;

        var now = DateTime.Now.TimeOfDay;
        var start = _settings.QuietHoursStart;
        var end = _settings.QuietHoursEnd;

        return start > end ? now >= start || now <= end : now >= start && now <= end;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1) return $"{duration.Hours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1) return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }
}
