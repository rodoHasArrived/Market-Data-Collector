using System;
using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Notification type levels.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Default no-op notification service for the shared UI services layer.
/// Platform-specific projects (WPF) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public class NotificationService
{
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());

    public static NotificationService Instance => _instance.Value;

    public virtual Task NotifyErrorAsync(string title, string message, Exception? exception = null)
        => Task.CompletedTask;

    public virtual Task NotifyWarningAsync(string title, string message)
        => Task.CompletedTask;

    public virtual Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info)
        => Task.CompletedTask;

    public virtual Task NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration)
        => Task.CompletedTask;

    public virtual Task NotifyScheduledJobAsync(string jobName, bool started, bool success = true)
        => Task.CompletedTask;

    public virtual Task NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes)
        => Task.CompletedTask;
}
