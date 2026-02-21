using System;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Contracts;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF platform-specific notification service.
/// Extends <see cref="NotificationServiceBase"/> implementing the <see cref="INotificationService"/> contract.
/// Part of Phase 2 service extraction.
/// </summary>
public sealed class NotificationService : NotificationServiceBase, INotificationService
{
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());

    public static NotificationService Instance => _instance.Value;

    private NotificationService()
    {
    }

    // INotificationService explicit implementations delegate to base class methods
    async Task INotificationService.NotifyErrorAsync(string title, string message, Exception? exception)
        => await NotifyErrorAsync(title, message, exception);

    async Task INotificationService.NotifyWarningAsync(string title, string message)
        => await NotifyWarningAsync(title, message);

    async Task INotificationService.NotifyAsync(string title, string message, NotificationType type)
        => await NotifyAsync(title, message, type);

    async Task INotificationService.NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration)
        => await NotifyBackfillCompleteAsync(success, symbolCount, barsWritten, duration);

    async Task INotificationService.NotifyScheduledJobAsync(string jobName, bool started, bool success)
        => await NotifyScheduledJobAsync(jobName, started, success);

    async Task INotificationService.NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes)
        => await NotifyStorageWarningAsync(usedPercent, freeSpaceBytes);
}
