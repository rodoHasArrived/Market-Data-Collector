using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public interface IBackgroundTaskSchedulerService
{
    Task ScheduleTaskAsync(string taskName, Func<CancellationToken, Task> task, TimeSpan interval, CancellationToken cancellationToken = default);
    Task CancelTaskAsync(string taskName);
    bool IsTaskRunning(string taskName);
}
