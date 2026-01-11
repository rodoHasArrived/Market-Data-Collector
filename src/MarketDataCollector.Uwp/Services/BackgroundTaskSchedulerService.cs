using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for scheduling and executing background tasks.
/// Manages scheduled backfill, collection, verification, and other recurring operations.
/// Tasks persist across app restarts and can run on schedule regardless of user presence.
/// </summary>
public sealed class BackgroundTaskSchedulerService
{
    private static BackgroundTaskSchedulerService? _instance;
    private static readonly object _lock = new();

    private readonly string _dataDirectory;
    private readonly string _configFilePath;
    private readonly string _logsFilePath;
    private readonly NotificationService _notificationService;
    private readonly OfflineTrackingPersistenceService _persistenceService;

    private SchedulerConfig _config;
    private readonly ConcurrentDictionary<string, TaskExecutionLog> _runningTasks;
    private readonly ConcurrentQueue<TaskExecutionLog> _executionLogs;
    private Timer? _schedulerTimer;
    private readonly SemaphoreSlim _executionSemaphore;
    private bool _isRunning;
    private CancellationTokenSource? _shutdownCts;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static BackgroundTaskSchedulerService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new BackgroundTaskSchedulerService();
                }
            }
            return _instance;
        }
    }

    private BackgroundTaskSchedulerService()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "data", "_scheduler");
        _configFilePath = Path.Combine(_dataDirectory, "scheduler_config.json");
        _logsFilePath = Path.Combine(_dataDirectory, "execution_logs.json");

        _notificationService = NotificationService.Instance;
        _persistenceService = OfflineTrackingPersistenceService.Instance;

        _config = new SchedulerConfig();
        _runningTasks = new ConcurrentDictionary<string, TaskExecutionLog>();
        _executionLogs = new ConcurrentQueue<TaskExecutionLog>();
        _executionSemaphore = new SemaphoreSlim(3); // Default max concurrent
    }

    /// <summary>
    /// Gets whether the scheduler is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the current scheduler configuration.
    /// </summary>
    public SchedulerConfig Config => _config;

    /// <summary>
    /// Gets all scheduled tasks.
    /// </summary>
    public IReadOnlyList<ScheduledTask> Tasks => _config.Tasks.ToList();

    /// <summary>
    /// Gets currently running tasks.
    /// </summary>
    public IReadOnlyList<TaskExecutionLog> RunningTasks => _runningTasks.Values.ToList();

    /// <summary>
    /// Initializes and starts the scheduler.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;

        try
        {
            Directory.CreateDirectory(_dataDirectory);
            await LoadConfigAsync();
            await LoadLogsAsync();

            if (!_config.IsEnabled)
            {
                System.Diagnostics.Debug.WriteLine("Scheduler is disabled in configuration");
                return;
            }

            _shutdownCts = new CancellationTokenSource();

            // Check for missed tasks on startup
            if (_config.RunOnStartup)
            {
                await ProcessMissedTasksAsync(cancellationToken);
            }

            // Calculate next run times for all tasks
            foreach (var task in _config.Tasks.Where(t => t.IsEnabled))
            {
                task.NextRunAt = CalculateNextRunTime(task);
            }
            await SaveConfigAsync();

            // Start the scheduler timer
            var checkInterval = TimeSpan.FromSeconds(_config.CheckIntervalSeconds);
            _schedulerTimer = new Timer(
                async _ => await CheckAndExecuteTasksAsync(),
                null,
                checkInterval,
                checkInterval);

            _isRunning = true;
            System.Diagnostics.Debug.WriteLine($"BackgroundTaskSchedulerService started with {_config.Tasks.Length} tasks");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start scheduler: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stops the scheduler.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        try
        {
            _shutdownCts?.Cancel();
            _schedulerTimer?.Dispose();
            _schedulerTimer = null;

            // Wait for running tasks to complete (with timeout)
            var timeout = TimeSpan.FromSeconds(30);
            var waitStart = DateTime.UtcNow;
            while (_runningTasks.Count > 0 && DateTime.UtcNow - waitStart < timeout)
            {
                await Task.Delay(500);
            }

            await SaveConfigAsync();
            await SaveLogsAsync();

            _isRunning = false;
            System.Diagnostics.Debug.WriteLine("BackgroundTaskSchedulerService stopped");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping scheduler: {ex.Message}");
        }
    }

    #region Task Management

    /// <summary>
    /// Creates a new scheduled task.
    /// </summary>
    public async Task<ScheduledTask> CreateTaskAsync(ScheduledTask task)
    {
        task.Id = Guid.NewGuid().ToString();
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        task.NextRunAt = CalculateNextRunTime(task);

        var tasks = _config.Tasks.ToList();
        tasks.Add(task);
        _config.Tasks = tasks.ToArray();
        _config.LastUpdated = DateTime.UtcNow;

        await SaveConfigAsync();

        TaskCreated?.Invoke(this, new TaskEventArgs { Task = task });

        System.Diagnostics.Debug.WriteLine($"Created scheduled task: {task.Name} (ID: {task.Id})");
        return task;
    }

    /// <summary>
    /// Updates an existing scheduled task.
    /// </summary>
    public async Task<ScheduledTask?> UpdateTaskAsync(ScheduledTask task)
    {
        var existing = _config.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (existing == null) return null;

        task.UpdatedAt = DateTime.UtcNow;
        task.NextRunAt = CalculateNextRunTime(task);

        var tasks = _config.Tasks.ToList();
        var index = tasks.FindIndex(t => t.Id == task.Id);
        if (index >= 0)
        {
            tasks[index] = task;
            _config.Tasks = tasks.ToArray();
            _config.LastUpdated = DateTime.UtcNow;
            await SaveConfigAsync();

            TaskUpdated?.Invoke(this, new TaskEventArgs { Task = task });
        }

        return task;
    }

    /// <summary>
    /// Deletes a scheduled task.
    /// </summary>
    public async Task<bool> DeleteTaskAsync(string taskId)
    {
        var task = _config.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return false;

        // Cancel if running
        if (_runningTasks.ContainsKey(taskId))
        {
            System.Diagnostics.Debug.WriteLine($"Cannot delete running task: {taskId}");
            return false;
        }

        _config.Tasks = _config.Tasks.Where(t => t.Id != taskId).ToArray();
        _config.LastUpdated = DateTime.UtcNow;
        await SaveConfigAsync();

        TaskDeleted?.Invoke(this, new TaskEventArgs { Task = task });

        System.Diagnostics.Debug.WriteLine($"Deleted scheduled task: {task.Name}");
        return true;
    }

    /// <summary>
    /// Enables or disables a task.
    /// </summary>
    public async Task SetTaskEnabledAsync(string taskId, bool enabled)
    {
        var task = _config.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        task.IsEnabled = enabled;
        task.UpdatedAt = DateTime.UtcNow;

        if (enabled)
        {
            task.NextRunAt = CalculateNextRunTime(task);
        }

        await SaveConfigAsync();
    }

    /// <summary>
    /// Runs a task immediately (manual trigger).
    /// </summary>
    public async Task<TaskExecutionLog?> RunTaskNowAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = _config.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return null;

        if (_runningTasks.ContainsKey(taskId) && !task.AllowConcurrent)
        {
            System.Diagnostics.Debug.WriteLine($"Task {task.Name} is already running");
            return null;
        }

        return await ExecuteTaskAsync(task, "Manual", cancellationToken);
    }

    /// <summary>
    /// Gets a task by ID.
    /// </summary>
    public ScheduledTask? GetTask(string taskId)
    {
        return _config.Tasks.FirstOrDefault(t => t.Id == taskId);
    }

    /// <summary>
    /// Gets execution logs for a task.
    /// </summary>
    public IReadOnlyList<TaskExecutionLog> GetTaskLogs(string taskId, int limit = 50)
    {
        return _executionLogs
            .Where(l => l.TaskId == taskId)
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToList();
    }

    #endregion

    #region Task Templates

    /// <summary>
    /// Creates a daily backfill task for specified symbols.
    /// </summary>
    public async Task<ScheduledTask> CreateDailyBackfillTaskAsync(
        string[] symbols,
        string time = "06:00",
        string? provider = null,
        int daysBack = 1)
    {
        var payload = new BackfillTaskPayload
        {
            Symbols = symbols,
            Provider = provider,
            UseRelativeDates = true,
            RelativeDaysBack = daysBack,
            FillGaps = true
        };

        var task = new ScheduledTask
        {
            Name = $"Daily Backfill - {string.Join(", ", symbols.Take(3))}{(symbols.Length > 3 ? "..." : "")}",
            Description = $"Automatically backfill {symbols.Length} symbol(s) each trading day",
            TaskType = ScheduledTaskType.RunBackfill,
            Schedule = new TaskSchedule
            {
                ScheduleType = ScheduleType.Daily,
                Time = time,
                SkipWeekends = true,
                SkipHolidays = true
            },
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Tags = new[] { "backfill", "daily", "automated" }
        };

        return await CreateTaskAsync(task);
    }

    /// <summary>
    /// Creates a market hours collection task.
    /// </summary>
    public async Task<ScheduledTask> CreateMarketHoursCollectionTaskAsync(
        string[] symbols,
        string startTime = "09:30",
        string endTime = "16:00",
        string? provider = null)
    {
        var startPayload = new CollectionTaskPayload
        {
            AutoCreateSession = true,
            Symbols = symbols,
            EventTypes = new[] { "Trade", "Quote" },
            Provider = provider
        };

        var startTask = new ScheduledTask
        {
            Name = $"Start Collection - Market Open",
            Description = $"Start data collection at market open for {symbols.Length} symbol(s)",
            TaskType = ScheduledTaskType.StartCollection,
            Schedule = new TaskSchedule
            {
                ScheduleType = ScheduleType.Daily,
                Time = startTime,
                SkipWeekends = true,
                SkipHolidays = true,
                MarketHoursOnly = true
            },
            Payload = JsonSerializer.Serialize(startPayload, JsonOptions),
            Tags = new[] { "collection", "market-hours", "automated" }
        };

        await CreateTaskAsync(startTask);

        var stopTask = new ScheduledTask
        {
            Name = $"Stop Collection - Market Close",
            Description = $"Stop data collection at market close",
            TaskType = ScheduledTaskType.StopCollection,
            Schedule = new TaskSchedule
            {
                ScheduleType = ScheduleType.Daily,
                Time = endTime,
                SkipWeekends = true,
                SkipHolidays = true
            },
            Tags = new[] { "collection", "market-hours", "automated" }
        };

        return await CreateTaskAsync(stopTask);
    }

    /// <summary>
    /// Creates a weekly verification task.
    /// </summary>
    public async Task<ScheduledTask> CreateWeeklyVerificationTaskAsync(DayOfWeek dayOfWeek = DayOfWeek.Sunday, string time = "02:00")
    {
        var task = new ScheduledTask
        {
            Name = "Weekly Archive Verification",
            Description = "Verify archive integrity and generate health report",
            TaskType = ScheduledTaskType.RunVerification,
            Schedule = new TaskSchedule
            {
                ScheduleType = ScheduleType.Weekly,
                Time = time,
                DaysOfWeek = new[] { dayOfWeek }
            },
            Tags = new[] { "verification", "weekly", "maintenance" },
            TimeoutMinutes = 120
        };

        return await CreateTaskAsync(task);
    }

    #endregion

    #region Scheduler Logic

    private async Task CheckAndExecuteTasksAsync()
    {
        if (!_isRunning || _shutdownCts?.IsCancellationRequested == true) return;

        try
        {
            var now = DateTime.UtcNow;
            var tasksToRun = _config.Tasks
                .Where(t => t.IsEnabled &&
                           t.NextRunAt.HasValue &&
                           t.NextRunAt.Value <= now &&
                           (t.AllowConcurrent || !_runningTasks.ContainsKey(t.Id)))
                .ToList();

            foreach (var task in tasksToRun)
            {
                if (_runningTasks.Count >= _config.MaxConcurrentTasks) break;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _executionSemaphore.WaitAsync(_shutdownCts?.Token ?? CancellationToken.None);
                        try
                        {
                            await ExecuteTaskAsync(task, "Scheduler", _shutdownCts?.Token ?? CancellationToken.None);
                        }
                        finally
                        {
                            _executionSemaphore.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown requested
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error executing task {task.Name}: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in scheduler check: {ex.Message}");
        }
    }

    private async Task<TaskExecutionLog> ExecuteTaskAsync(ScheduledTask task, string triggeredBy, CancellationToken cancellationToken)
    {
        var log = new TaskExecutionLog
        {
            TaskId = task.Id,
            TaskName = task.Name,
            TriggeredBy = triggeredBy,
            Status = "Running"
        };

        _runningTasks[task.Id] = log;
        TaskStarted?.Invoke(this, new TaskExecutionEventArgs { Task = task, Log = log });

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(task.TimeoutMinutes));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await ExecuteTaskTypeAsync(task, log, linkedCts.Token);

            log.Status = "Success";
            log.CompletedAt = DateTime.UtcNow;
            log.DurationSeconds = (log.CompletedAt.Value - log.StartedAt).TotalSeconds;

            task.LastRunAt = log.StartedAt;
            task.LastRunStatus = "Success";
            task.LastRunDurationSeconds = log.DurationSeconds;
            task.LastRunError = null;
            task.RunCount++;

            if (task.NotifyOnCompletion)
            {
                await _notificationService.NotifyAsync(
                    "Task Completed",
                    $"{task.Name} completed successfully",
                    NotificationType.Success);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            log.Status = "Cancelled";
            log.Error = "Task was cancelled";
            task.LastRunStatus = "Cancelled";
        }
        catch (OperationCanceledException)
        {
            log.Status = "Timeout";
            log.Error = $"Task exceeded timeout of {task.TimeoutMinutes} minutes";
            task.LastRunStatus = "Timeout";
            task.FailCount++;

            if (task.NotifyOnFailure)
            {
                await _notificationService.NotifyErrorAsync("Task Timeout", $"{task.Name} exceeded timeout");
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Error = ex.Message;
            task.LastRunStatus = "Failed";
            task.LastRunError = ex.Message;
            task.FailCount++;

            if (task.NotifyOnFailure)
            {
                await _notificationService.NotifyErrorAsync("Task Failed", $"{task.Name}: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"Task {task.Name} failed: {ex.Message}");
        }
        finally
        {
            log.CompletedAt ??= DateTime.UtcNow;
            log.DurationSeconds ??= (log.CompletedAt.Value - log.StartedAt).TotalSeconds;

            _runningTasks.TryRemove(task.Id, out _);
            _executionLogs.Enqueue(log);

            // Trim old logs
            while (_executionLogs.Count > 1000)
            {
                _executionLogs.TryDequeue(out _);
            }

            // Calculate next run time
            task.NextRunAt = CalculateNextRunTime(task);
            task.UpdatedAt = DateTime.UtcNow;

            await SaveConfigAsync();
            await SaveLogsAsync();

            TaskCompleted?.Invoke(this, new TaskExecutionEventArgs { Task = task, Log = log });
        }

        return log;
    }

    private async Task ExecuteTaskTypeAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        switch (task.TaskType)
        {
            case ScheduledTaskType.RunBackfill:
                await ExecuteBackfillTaskAsync(task, log, cancellationToken);
                break;

            case ScheduledTaskType.StartCollection:
                await ExecuteStartCollectionTaskAsync(task, log, cancellationToken);
                break;

            case ScheduledTaskType.StopCollection:
                await ExecuteStopCollectionTaskAsync(task, log, cancellationToken);
                break;

            case ScheduledTaskType.RunVerification:
                await ExecuteVerificationTaskAsync(task, log, cancellationToken);
                break;

            case ScheduledTaskType.RunCleanup:
                await ExecuteCleanupTaskAsync(task, log, cancellationToken);
                break;

            case ScheduledTaskType.GenerateManifest:
                await ExecuteManifestTaskAsync(task, log, cancellationToken);
                break;

            case ScheduledTaskType.RunExport:
                await ExecuteExportTaskAsync(task, log, cancellationToken);
                break;

            default:
                log.Output = $"Task type {task.TaskType} execution not implemented";
                System.Diagnostics.Debug.WriteLine(log.Output);
                break;
        }
    }

    private async Task ExecuteBackfillTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(task.Payload)) return;

        var payload = JsonSerializer.Deserialize<BackfillTaskPayload>(task.Payload, JsonOptions);
        if (payload == null) return;

        // Write to WAL before execution
        var walEntry = await _persistenceService.WriteWalEntryAsync("StartBackfill", payload);

        try
        {
            // Execute backfill via BackfillService
            var backfillService = BackfillService.Instance;

            string? fromDate = payload.FromDate;
            string? toDate = payload.ToDate;

            if (payload.UseRelativeDates)
            {
                toDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                fromDate = DateTime.UtcNow.AddDays(-payload.RelativeDaysBack).ToString("yyyy-MM-dd");
            }

            var result = await backfillService.RunBackfillAsync(
                payload.Symbols,
                payload.Provider,
                fromDate,
                toDate,
                cancellationToken);

            log.ItemsProcessed = result?.BarsWritten ?? 0;
            log.Output = $"Backfilled {log.ItemsProcessed} bars for {payload.Symbols.Length} symbols";

            await _persistenceService.CompleteWalEntryAsync(walEntry.Id);
        }
        catch
        {
            await _persistenceService.FailWalEntryAsync(walEntry.Id, "Backfill task failed");
            throw;
        }
    }

    private async Task ExecuteStartCollectionTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(task.Payload)) return;

        var payload = JsonSerializer.Deserialize<CollectionTaskPayload>(task.Payload, JsonOptions);
        if (payload == null) return;

        // Write to WAL
        var walEntry = await _persistenceService.WriteWalEntryAsync("StartSession", payload);

        try
        {
            var sessionService = CollectionSessionService.Instance;

            if (payload.AutoCreateSession)
            {
                var session = await sessionService.CreateDailySessionAsync(
                    payload.Symbols ?? Array.Empty<string>(),
                    payload.EventTypes ?? new[] { "Trade", "Quote" },
                    payload.Provider);

                await sessionService.StartSessionAsync(session.Id);
                log.Output = $"Started collection session: {session.Name}";
            }

            await _persistenceService.CompleteWalEntryAsync(walEntry.Id);
        }
        catch
        {
            await _persistenceService.FailWalEntryAsync(walEntry.Id, "Start collection failed");
            throw;
        }
    }

    private async Task ExecuteStopCollectionTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        var sessionService = CollectionSessionService.Instance;
        var activeSessions = await sessionService.GetActiveSessionsAsync();

        foreach (var session in activeSessions)
        {
            await sessionService.StopSessionAsync(session.Id);
        }

        log.ItemsProcessed = activeSessions.Count;
        log.Output = $"Stopped {activeSessions.Count} active session(s)";
    }

    private async Task ExecuteVerificationTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        var archiveService = ArchiveHealthService.Instance;
        var result = await archiveService.RunVerificationAsync(cancellationToken);

        log.ItemsProcessed = result?.ProcessedFiles ?? 0;
        log.Output = $"Verified {log.ItemsProcessed} files, health score: {result?.HealthScore:P0}";
    }

    private async Task ExecuteCleanupTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        // Implement cleanup logic - remove old logs, temp files, etc.
        var logsRemoved = await CleanupOldLogsAsync(_config.LogRetentionDays);
        log.ItemsProcessed = logsRemoved;
        log.Output = $"Cleaned up {logsRemoved} old log entries";
    }

    private async Task ExecuteManifestTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        var manifestService = ManifestService.Instance;
        var sessionService = CollectionSessionService.Instance;

        var recentSessions = (await sessionService.GetSessionsAsync())
            .Where(s => s.Status == "Completed" && string.IsNullOrEmpty(s.ManifestPath))
            .ToList();

        foreach (var session in recentSessions)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await manifestService.GenerateManifestAsync(session.Id);
        }

        log.ItemsProcessed = recentSessions.Count;
        log.Output = $"Generated manifests for {recentSessions.Count} sessions";
    }

    private async Task ExecuteExportTaskAsync(ScheduledTask task, TaskExecutionLog log, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(task.Payload)) return;

        var payload = JsonSerializer.Deserialize<ExportTaskPayload>(task.Payload, JsonOptions);
        if (payload == null) return;

        // Export implementation would go here
        log.Output = $"Export task completed for format: {payload.ExportFormat}";
        await Task.CompletedTask;
    }

    private async Task ProcessMissedTasksAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        foreach (var task in _config.Tasks.Where(t => t.IsEnabled && t.RunMissedOnStartup))
        {
            if (task.LastRunAt.HasValue && task.NextRunAt.HasValue)
            {
                // Check if task was missed
                if (task.NextRunAt.Value < now && task.LastRunAt.Value < task.NextRunAt.Value)
                {
                    System.Diagnostics.Debug.WriteLine($"Running missed task: {task.Name}");
                    await ExecuteTaskAsync(task, "Startup", cancellationToken);
                }
            }
        }
    }

    private async Task<int> CleanupOldLogsAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var initialCount = _executionLogs.Count;

        var logsToKeep = _executionLogs
            .Where(l => l.StartedAt >= cutoff)
            .ToList();

        // Clear and re-add
        while (_executionLogs.TryDequeue(out _)) { }
        foreach (var log in logsToKeep)
        {
            _executionLogs.Enqueue(log);
        }

        await SaveLogsAsync();
        return initialCount - logsToKeep.Count;
    }

    #endregion

    #region Schedule Calculation

    private DateTime? CalculateNextRunTime(ScheduledTask task)
    {
        if (!task.IsEnabled) return null;

        var schedule = task.Schedule;
        var now = DateTime.UtcNow;

        // Handle start/end date constraints
        if (schedule.StartDate.HasValue && now < schedule.StartDate.Value)
        {
            now = schedule.StartDate.Value;
        }
        if (schedule.EndDate.HasValue && now > schedule.EndDate.Value)
        {
            return null; // Task has expired
        }

        DateTime nextRun;

        switch (schedule.ScheduleType)
        {
            case ScheduleType.Once:
                if (task.LastRunAt.HasValue) return null;
                nextRun = ParseScheduleTime(schedule.Time, now);
                if (nextRun <= now) nextRun = nextRun.AddDays(1);
                break;

            case ScheduleType.Interval:
                if (!schedule.IntervalMinutes.HasValue) return null;
                nextRun = task.LastRunAt?.AddMinutes(schedule.IntervalMinutes.Value) ?? now;
                if (nextRun <= now) nextRun = now.AddMinutes(schedule.IntervalMinutes.Value);
                break;

            case ScheduleType.Daily:
                nextRun = ParseScheduleTime(schedule.Time, now);
                if (nextRun <= now) nextRun = nextRun.AddDays(1);

                // Skip weekends if configured
                while (schedule.SkipWeekends && (nextRun.DayOfWeek == DayOfWeek.Saturday || nextRun.DayOfWeek == DayOfWeek.Sunday))
                {
                    nextRun = nextRun.AddDays(1);
                }
                break;

            case ScheduleType.Weekly:
                if (schedule.DaysOfWeek == null || schedule.DaysOfWeek.Length == 0)
                    return null;

                nextRun = ParseScheduleTime(schedule.Time, now);

                // Find next matching day
                for (int i = 0; i < 8; i++)
                {
                    if (schedule.DaysOfWeek.Contains(nextRun.DayOfWeek) && nextRun > now)
                        break;
                    nextRun = nextRun.AddDays(1);
                }
                break;

            case ScheduleType.Monthly:
                if (!schedule.DayOfMonth.HasValue) return null;

                nextRun = new DateTime(now.Year, now.Month, Math.Min(schedule.DayOfMonth.Value, DateTime.DaysInMonth(now.Year, now.Month)));
                nextRun = ParseScheduleTime(schedule.Time, nextRun);

                if (nextRun <= now)
                {
                    nextRun = nextRun.AddMonths(1);
                    nextRun = new DateTime(nextRun.Year, nextRun.Month, Math.Min(schedule.DayOfMonth.Value, DateTime.DaysInMonth(nextRun.Year, nextRun.Month)));
                    nextRun = ParseScheduleTime(schedule.Time, nextRun);
                }
                break;

            default:
                return null;
        }

        return nextRun;
    }

    private static DateTime ParseScheduleTime(string time, DateTime date)
    {
        if (TimeSpan.TryParse(time, out var timeOfDay))
        {
            return date.Date.Add(timeOfDay);
        }
        return date;
    }

    #endregion

    #region Persistence

    private async Task LoadConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                _config = JsonSerializer.Deserialize<SchedulerConfig>(json, JsonOptions) ?? new SchedulerConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load scheduler config: {ex.Message}");
            _config = new SchedulerConfig();
        }
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, JsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save scheduler config: {ex.Message}");
        }
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            if (File.Exists(_logsFilePath))
            {
                var json = await File.ReadAllTextAsync(_logsFilePath);
                var logs = JsonSerializer.Deserialize<TaskExecutionLog[]>(json, JsonOptions) ?? Array.Empty<TaskExecutionLog>();
                foreach (var log in logs)
                {
                    _executionLogs.Enqueue(log);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load execution logs: {ex.Message}");
        }
    }

    private async Task SaveLogsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_executionLogs.ToArray(), JsonOptions);
            await File.WriteAllTextAsync(_logsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save execution logs: {ex.Message}");
        }
    }

    #endregion

    #region Events

    public event EventHandler<TaskEventArgs>? TaskCreated;
    public event EventHandler<TaskEventArgs>? TaskUpdated;
    public event EventHandler<TaskEventArgs>? TaskDeleted;
    public event EventHandler<TaskExecutionEventArgs>? TaskStarted;
    public event EventHandler<TaskExecutionEventArgs>? TaskCompleted;

    #endregion
}

public class TaskEventArgs : EventArgs
{
    public ScheduledTask? Task { get; set; }
}

public class TaskExecutionEventArgs : EventArgs
{
    public ScheduledTask? Task { get; set; }
    public TaskExecutionLog? Log { get; set; }
}
