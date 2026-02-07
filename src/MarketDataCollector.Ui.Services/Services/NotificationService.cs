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
/// Platform-specific projects (WPF, UWP) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public class NotificationService
{
    private static NotificationService? _instance;
    private static readonly object _lock = new();

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
        set
        {
            lock (_lock)
            {
                _instance = value;
            }
        }
    }

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
