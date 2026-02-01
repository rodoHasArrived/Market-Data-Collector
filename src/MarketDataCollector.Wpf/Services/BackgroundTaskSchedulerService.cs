using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Represents a scheduled background task.
/// </summary>
public sealed class ScheduledTask
{
    /// <summary>
    /// Gets or sets the unique identifier for the task.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the task name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action to execute.
    /// </summary>
    public Func<CancellationToken, Task>? Action { get; set; }

    /// <summary>
    /// Gets or sets the interval between executions.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets whether the task is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Service for scheduling and managing background tasks.
/// Implements singleton pattern for application-wide task scheduling.
/// </summary>
public sealed class BackgroundTaskSchedulerService
{
    private static readonly Lazy<BackgroundTaskSchedulerService> _instance =
        new(() => new BackgroundTaskSchedulerService());

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    /// <summary>
    /// Gets the singleton instance of the BackgroundTaskSchedulerService.
    /// </summary>
    public static BackgroundTaskSchedulerService Instance => _instance.Value;

    /// <summary>
    /// Gets whether the scheduler is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    private BackgroundTaskSchedulerService()
    {
    }

    /// <summary>
    /// Starts the background task scheduler.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task StartAsync()
    {
        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _isRunning = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the background task scheduler.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public Task StopAsync()
    {
        if (!_isRunning)
        {
            return Task.CompletedTask;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isRunning = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Schedules a task for execution.
    /// </summary>
    /// <param name="task">The task to schedule.</param>
    public void ScheduleTask(ScheduledTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        // Stub: task scheduling not implemented
    }

    /// <summary>
    /// Schedules a task for execution with the specified parameters.
    /// </summary>
    /// <param name="name">The task name.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="interval">The interval between executions.</param>
    public void ScheduleTask(string name, Func<CancellationToken, Task> action, TimeSpan interval)
    {
        ScheduleTask(new ScheduledTask
        {
            Name = name,
            Action = action,
            Interval = interval
        });
    }

    /// <summary>
    /// Cancels a scheduled task.
    /// </summary>
    /// <param name="taskId">The task identifier to cancel.</param>
    public void CancelTask(string taskId)
    {
        // Stub: task cancellation not implemented
    }
}
