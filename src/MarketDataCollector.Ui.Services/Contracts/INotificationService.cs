using System;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for notification services used by shared UI services.
/// Implemented by platform-specific notification services (WPF, UWP).
/// </summary>
public interface INotificationService
{
    Task NotifyErrorAsync(string title, string message, Exception? exception = null);
    Task NotifyWarningAsync(string title, string message);
    Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info);
    Task NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration);
    Task NotifyScheduledJobAsync(string jobName, bool started, bool success = true);
    Task NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes);
}
