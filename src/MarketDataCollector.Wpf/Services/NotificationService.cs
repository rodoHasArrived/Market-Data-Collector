using System;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Services;
using Contracts = MarketDataCollector.Ui.Services.Contracts;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF platform-specific notification service.
/// Extends <see cref="NotificationServiceBase"/> implementing the <see cref="INotificationService"/> contract.
/// Part of Phase 2 service extraction.
/// </summary>
public sealed class NotificationService : NotificationServiceBase, Contracts.INotificationService
{
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());

    public static NotificationService Instance => _instance.Value;

    private NotificationService()
    {
    }

    // INotificationService explicit implementations delegate to base class methods
    async Task Contracts.INotificationService.NotifyErrorAsync(string title, string message, Exception? exception)
        => await NotifyErrorAsync(title, message, exception);

    async Task Contracts.INotificationService.NotifyWarningAsync(string title, string message)
        => await NotifyWarningAsync(title, message);

    async Task Contracts.INotificationService.NotifyAsync(string title, string message, MarketDataCollector.Ui.Services.NotificationType type)
        => await NotifyAsync(title, message, type);

    async Task Contracts.INotificationService.NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration)
        => await NotifyBackfillCompleteAsync(success, symbolCount, barsWritten, duration);

    async Task Contracts.INotificationService.NotifyScheduledJobAsync(string jobName, bool started, bool success)
        => await NotifyScheduledJobAsync(jobName, started, success);

    async Task Contracts.INotificationService.NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes)
        => await NotifyStorageWarningAsync(usedPercent, freeSpaceBytes);
}
