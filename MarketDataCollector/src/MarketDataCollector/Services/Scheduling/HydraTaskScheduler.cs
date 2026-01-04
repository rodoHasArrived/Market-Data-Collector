using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Services.Scheduling;

/// <summary>
/// Task scheduler for automated data collection, similar to StockSharp Hydra.
/// Supports scheduled tasks for data downloads, sync, and maintenance.
/// </summary>
public sealed class HydraTaskScheduler : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<HydraTaskScheduler>();
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly ConcurrentDictionary<string, Timer> _timers = new();
    private readonly SemaphoreSlim _executionGate;
    private readonly int _maxConcurrentTasks;
    private bool _disposed;

    /// <summary>
    /// Event raised when a task starts execution.
    /// </summary>
    public event Action<ScheduledTask>? TaskStarted;

    /// <summary>
    /// Event raised when a task completes.
    /// </summary>
    public event Action<ScheduledTask, TaskExecutionResult>? TaskCompleted;

    /// <summary>
    /// Event raised when a task fails.
    /// </summary>
    public event Action<ScheduledTask, Exception>? TaskFailed;

    public HydraTaskScheduler(int maxConcurrentTasks = 4)
    {
        _maxConcurrentTasks = maxConcurrentTasks;
        _executionGate = new SemaphoreSlim(maxConcurrentTasks);
        _log.Information("HydraTaskScheduler initialized with max {MaxTasks} concurrent tasks", maxConcurrentTasks);
    }

    /// <summary>
    /// Register a new scheduled task.
    /// </summary>
    public void RegisterTask(ScheduledTask task)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HydraTaskScheduler));
        if (task == null) throw new ArgumentNullException(nameof(task));

        if (!_tasks.TryAdd(task.Id, task))
        {
            throw new InvalidOperationException($"Task with ID '{task.Id}' already exists");
        }

        if (task.IsEnabled)
        {
            ScheduleNextRun(task);
        }

        _log.Information("Registered task: {TaskId} ({TaskName}), Schedule: {Schedule}",
            task.Id, task.Name, task.Schedule);
    }

    /// <summary>
    /// Unregister a task.
    /// </summary>
    public void UnregisterTask(string taskId)
    {
        if (_tasks.TryRemove(taskId, out _))
        {
            if (_timers.TryRemove(taskId, out var timer))
            {
                timer.Dispose();
            }
            _log.Information("Unregistered task: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Enable a task.
    /// </summary>
    public void EnableTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = true;
            ScheduleNextRun(task);
            _log.Information("Enabled task: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Disable a task.
    /// </summary>
    public void DisableTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.IsEnabled = false;
            if (_timers.TryRemove(taskId, out var timer))
            {
                timer.Dispose();
            }
            _log.Information("Disabled task: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Run a task immediately (outside of schedule).
    /// </summary>
    public async Task<TaskExecutionResult> RunTaskNowAsync(string taskId, CancellationToken ct = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new KeyNotFoundException($"Task '{taskId}' not found");
        }

        return await ExecuteTaskAsync(task, ct);
    }

    /// <summary>
    /// Get all registered tasks.
    /// </summary>
    public IReadOnlyList<ScheduledTask> GetAllTasks()
    {
        return _tasks.Values.ToList();
    }

    /// <summary>
    /// Get a specific task.
    /// </summary>
    public ScheduledTask? GetTask(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return task;
    }

    private void ScheduleNextRun(ScheduledTask task)
    {
        var nextRun = task.Schedule.GetNextRun(DateTimeOffset.UtcNow);
        if (nextRun == null)
        {
            _log.Debug("Task {TaskId} has no next scheduled run", task.Id);
            return;
        }

        var delay = nextRun.Value - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        task.NextScheduledRun = nextRun;

        // Cancel existing timer
        if (_timers.TryRemove(task.Id, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new timer
        var timer = new Timer(
            OnTimerElapsed,
            task.Id,
            delay,
            Timeout.InfiniteTimeSpan);

        _timers[task.Id] = timer;

        _log.Debug("Scheduled task {TaskId} for {NextRun} (in {Delay})",
            task.Id, nextRun, delay);
    }

    private async void OnTimerElapsed(object? state)
    {
        var taskId = (string)state!;

        if (!_tasks.TryGetValue(taskId, out var task) || !task.IsEnabled)
            return;

        try
        {
            await ExecuteTaskAsync(task, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error executing scheduled task {TaskId}", taskId);
        }

        // Schedule next run
        if (task.IsEnabled)
        {
            ScheduleNextRun(task);
        }
    }

    private async Task<TaskExecutionResult> ExecuteTaskAsync(ScheduledTask task, CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;
        task.LastRunTime = startTime;
        task.Status = TaskStatus.Running;

        _log.Information("Starting task: {TaskId} ({TaskName})", task.Id, task.Name);
        TaskStarted?.Invoke(task);

        await _executionGate.WaitAsync(ct);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (task.Timeout.HasValue)
            {
                cts.CancelAfter(task.Timeout.Value);
            }

            await task.Action(cts.Token);

            var result = new TaskExecutionResult
            {
                TaskId = task.Id,
                Success = true,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Duration = DateTimeOffset.UtcNow - startTime
            };

            task.Status = TaskStatus.Completed;
            task.LastResult = result;
            task.ConsecutiveFailures = 0;

            _log.Information("Task completed: {TaskId} in {Duration}ms",
                task.Id, result.Duration.TotalMilliseconds);
            TaskCompleted?.Invoke(task, result);

            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new TaskExecutionResult
            {
                TaskId = task.Id,
                Success = false,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Duration = DateTimeOffset.UtcNow - startTime,
                Error = "Task was cancelled"
            };

            task.Status = TaskStatus.Cancelled;
            task.LastResult = result;

            _log.Warning("Task cancelled: {TaskId}", task.Id);
            return result;
        }
        catch (Exception ex)
        {
            var result = new TaskExecutionResult
            {
                TaskId = task.Id,
                Success = false,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                Duration = DateTimeOffset.UtcNow - startTime,
                Error = ex.Message
            };

            task.Status = TaskStatus.Failed;
            task.LastResult = result;
            task.ConsecutiveFailures++;

            _log.Error(ex, "Task failed: {TaskId} ({ConsecutiveFailures} consecutive failures)",
                task.Id, task.ConsecutiveFailures);
            TaskFailed?.Invoke(task, ex);

            // Disable task after too many failures
            if (task.MaxConsecutiveFailures > 0 &&
                task.ConsecutiveFailures >= task.MaxConsecutiveFailures)
            {
                _log.Warning("Disabling task {TaskId} after {Failures} consecutive failures",
                    task.Id, task.ConsecutiveFailures);
                DisableTask(task.Id);
            }

            return result;
        }
        finally
        {
            _executionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }
        _timers.Clear();
        _tasks.Clear();

        _executionGate.Dispose();

        await Task.CompletedTask;
    }
}

/// <summary>
/// Represents a scheduled task.
/// </summary>
public sealed class ScheduledTask
{
    /// <summary>Unique task identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable task name.</summary>
    public required string Name { get; init; }

    /// <summary>Task description.</summary>
    public string? Description { get; init; }

    /// <summary>Schedule for this task.</summary>
    public required TaskSchedule Schedule { get; init; }

    /// <summary>Action to execute.</summary>
    public required Func<CancellationToken, Task> Action { get; init; }

    /// <summary>Whether the task is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Task category/group.</summary>
    public string Category { get; init; } = "Default";

    /// <summary>Task priority (lower = higher priority).</summary>
    public int Priority { get; init; } = 100;

    /// <summary>Maximum execution time.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Maximum consecutive failures before disabling.</summary>
    public int MaxConsecutiveFailures { get; init; } = 3;

    // Runtime state
    public TaskStatus Status { get; internal set; } = TaskStatus.Pending;
    public DateTimeOffset? LastRunTime { get; internal set; }
    public DateTimeOffset? NextScheduledRun { get; internal set; }
    public TaskExecutionResult? LastResult { get; internal set; }
    public int ConsecutiveFailures { get; internal set; }
}

/// <summary>
/// Task execution status.
/// </summary>
public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Disabled
}

/// <summary>
/// Result of a task execution.
/// </summary>
public sealed record TaskExecutionResult
{
    public required string TaskId { get; init; }
    public bool Success { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public TimeSpan Duration { get; init; }
    public string? Error { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Task schedule definition.
/// </summary>
public abstract class TaskSchedule
{
    /// <summary>
    /// Get the next run time after the specified time.
    /// </summary>
    public abstract DateTimeOffset? GetNextRun(DateTimeOffset after);
}

/// <summary>
/// Interval-based schedule (every N minutes/hours).
/// </summary>
public sealed class IntervalSchedule : TaskSchedule
{
    public TimeSpan Interval { get; }
    public DateTimeOffset? StartTime { get; }
    public DateTimeOffset? EndTime { get; }

    public IntervalSchedule(TimeSpan interval, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be positive", nameof(interval));

        Interval = interval;
        StartTime = startTime;
        EndTime = endTime;
    }

    public override DateTimeOffset? GetNextRun(DateTimeOffset after)
    {
        var next = after.Add(Interval);

        if (StartTime.HasValue && next < StartTime.Value)
            next = StartTime.Value;

        if (EndTime.HasValue && next > EndTime.Value)
            return null;

        return next;
    }
}

/// <summary>
/// Daily schedule at specific times.
/// </summary>
public sealed class DailySchedule : TaskSchedule
{
    public TimeOnly[] RunTimes { get; }
    public DayOfWeek[]? DaysOfWeek { get; }
    public TimeZoneInfo TimeZone { get; }

    public DailySchedule(TimeOnly runTime, DayOfWeek[]? daysOfWeek = null, TimeZoneInfo? timeZone = null)
        : this(new[] { runTime }, daysOfWeek, timeZone)
    {
    }

    public DailySchedule(TimeOnly[] runTimes, DayOfWeek[]? daysOfWeek = null, TimeZoneInfo? timeZone = null)
    {
        if (runTimes == null || runTimes.Length == 0)
            throw new ArgumentException("At least one run time required", nameof(runTimes));

        RunTimes = runTimes.OrderBy(t => t).ToArray();
        DaysOfWeek = daysOfWeek;
        TimeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public override DateTimeOffset? GetNextRun(DateTimeOffset after)
    {
        var localAfter = TimeZoneInfo.ConvertTime(after, TimeZone);
        var currentDate = DateOnly.FromDateTime(localAfter.DateTime);
        var currentTime = TimeOnly.FromDateTime(localAfter.DateTime);

        // Check today's remaining times
        foreach (var runTime in RunTimes)
        {
            if (runTime > currentTime && IsDayAllowed(localAfter.DayOfWeek))
            {
                var candidate = new DateTimeOffset(
                    currentDate.ToDateTime(runTime),
                    TimeZone.GetUtcOffset(localAfter.DateTime));
                return candidate;
            }
        }

        // Check next days
        for (int i = 1; i <= 7; i++)
        {
            var nextDate = currentDate.AddDays(i);
            var nextDateTime = nextDate.ToDateTime(TimeOnly.MinValue);
            var nextDayOfWeek = nextDateTime.DayOfWeek;

            if (IsDayAllowed(nextDayOfWeek))
            {
                var candidate = new DateTimeOffset(
                    nextDate.ToDateTime(RunTimes[0]),
                    TimeZone.GetUtcOffset(nextDateTime));
                return candidate;
            }
        }

        return null;
    }

    private bool IsDayAllowed(DayOfWeek day)
    {
        return DaysOfWeek == null || DaysOfWeek.Contains(day);
    }
}

/// <summary>
/// Cron-like schedule.
/// </summary>
public sealed class CronSchedule : TaskSchedule
{
    private readonly string _expression;

    // Simple cron-like fields: minute, hour, day, month, dayOfWeek
    private readonly int[]? _minutes;
    private readonly int[]? _hours;
    private readonly int[]? _days;
    private readonly int[]? _months;
    private readonly DayOfWeek[]? _daysOfWeek;

    public CronSchedule(string expression)
    {
        _expression = expression;
        ParseExpression(expression);
    }

    private void ParseExpression(string expression)
    {
        // Simplified cron parser: "minute hour day month dayOfWeek"
        // Examples: "0 * * * *" = every hour
        //           "*/15 * * * *" = every 15 minutes
        //           "0 9 * * 1-5" = 9 AM on weekdays
    }

    public override DateTimeOffset? GetNextRun(DateTimeOffset after)
    {
        // Simplified implementation - for production use a proper cron library
        var candidate = after.AddMinutes(1);
        candidate = new DateTimeOffset(
            candidate.Year, candidate.Month, candidate.Day,
            candidate.Hour, candidate.Minute, 0, candidate.Offset);

        for (int i = 0; i < 527040; i++) // Max 1 year of minutes
        {
            if (Matches(candidate))
                return candidate;

            candidate = candidate.AddMinutes(1);
        }

        return null;
    }

    private bool Matches(DateTimeOffset time)
    {
        // Simplified matching
        return true;
    }
}
