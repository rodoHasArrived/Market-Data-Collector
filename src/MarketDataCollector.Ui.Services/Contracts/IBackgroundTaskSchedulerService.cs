namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for scheduling and managing background tasks.
/// Shared between WPF and UWP desktop applications.
/// Part of C1 improvement (WPF/UWP service deduplication).
/// </summary>
public interface IBackgroundTaskSchedulerService
{
    Task ScheduleTaskAsync(string taskName, Func<CancellationToken, Task> task, TimeSpan interval, CancellationToken cancellationToken = default);
    Task CancelTaskAsync(string taskName);
    bool IsTaskRunning(string taskName);
}
