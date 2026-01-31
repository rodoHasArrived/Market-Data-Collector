using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public sealed class BackgroundTaskSchedulerService : IBackgroundTaskSchedulerService
{
    private readonly ILoggingService _logger;

    public BackgroundTaskSchedulerService(ILoggingService logger)
    {
        _logger = logger;
        _logger.Log("BackgroundTaskSchedulerService initialized (stub implementation)");
    }

    public Task ScheduleTaskAsync(string taskName, Func<CancellationToken, Task> task, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        _logger.Log($"ScheduleTaskAsync called for task: {taskName} with interval: {interval} (not implemented)");
        return Task.CompletedTask;
    }

    public Task CancelTaskAsync(string taskName)
    {
        _logger.Log($"CancelTaskAsync called for task: {taskName} (not implemented)");
        return Task.CompletedTask;
    }

    public bool IsTaskRunning(string taskName)
    {
        _logger.Log($"IsTaskRunning called for task: {taskName} (not implemented)");
        return false;
    }
}
