namespace MarketDataCollector.Storage.Maintenance;

/// <summary>
/// Interface for archive maintenance service that orchestrates scheduled and on-demand maintenance operations.
/// </summary>
public interface IArchiveMaintenanceService
{
    /// <summary>
    /// Whether the maintenance service is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Number of maintenance executions currently queued.
    /// </summary>
    int QueuedExecutions { get; }

    /// <summary>
    /// Currently running execution (if any).
    /// </summary>
    MaintenanceExecution? CurrentExecution { get; }

    /// <summary>
    /// Execute a maintenance task immediately.
    /// </summary>
    /// <param name="taskType">Type of maintenance to perform.</param>
    /// <param name="options">Options for the maintenance task.</param>
    /// <param name="targetPaths">Specific paths to target (null = use defaults).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The execution record with results.</returns>
    Task<MaintenanceExecution> ExecuteMaintenanceAsync(
        MaintenanceTaskType taskType,
        MaintenanceTaskOptions? options = null,
        string[]? targetPaths = null,
        CancellationToken ct = default);

    /// <summary>
    /// Trigger a scheduled maintenance to run immediately.
    /// </summary>
    /// <param name="scheduleId">ID of the schedule to trigger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The execution record.</returns>
    Task<MaintenanceExecution> TriggerScheduleAsync(string scheduleId, CancellationToken ct = default);

    /// <summary>
    /// Cancel a running or queued maintenance execution.
    /// </summary>
    /// <param name="executionId">ID of the execution to cancel.</param>
    /// <returns>True if cancellation was successful.</returns>
    Task<bool> CancelExecutionAsync(string executionId);

    /// <summary>
    /// Get the current status of the maintenance service.
    /// </summary>
    MaintenanceServiceStatus GetStatus();

    /// <summary>
    /// Event raised when a maintenance execution starts.
    /// </summary>
    event EventHandler<MaintenanceExecution>? ExecutionStarted;

    /// <summary>
    /// Event raised when a maintenance execution completes.
    /// </summary>
    event EventHandler<MaintenanceExecution>? ExecutionCompleted;

    /// <summary>
    /// Event raised when a maintenance execution fails.
    /// </summary>
    event EventHandler<MaintenanceExecution>? ExecutionFailed;
}

/// <summary>
/// Status of the maintenance service.
/// </summary>
public sealed record MaintenanceServiceStatus(
    bool IsRunning,
    int QueuedExecutions,
    MaintenanceExecution? CurrentExecution,
    DateTimeOffset? NextScheduledExecution,
    int ActiveSchedules,
    long TotalExecutionsToday,
    TimeSpan Uptime
);

/// <summary>
/// Interface for managing archive maintenance schedules.
/// </summary>
public interface IArchiveMaintenanceScheduleManager
{
    /// <summary>
    /// Get all maintenance schedules.
    /// </summary>
    IReadOnlyList<ArchiveMaintenanceSchedule> GetAllSchedules();

    /// <summary>
    /// Get a specific schedule by ID.
    /// </summary>
    ArchiveMaintenanceSchedule? GetSchedule(string scheduleId);

    /// <summary>
    /// Create a new maintenance schedule.
    /// </summary>
    Task<ArchiveMaintenanceSchedule> CreateScheduleAsync(ArchiveMaintenanceSchedule schedule, CancellationToken ct = default);

    /// <summary>
    /// Create a schedule from a preset.
    /// </summary>
    Task<ArchiveMaintenanceSchedule> CreateFromPresetAsync(string presetName, string name, CancellationToken ct = default);

    /// <summary>
    /// Update an existing schedule.
    /// </summary>
    Task<ArchiveMaintenanceSchedule> UpdateScheduleAsync(ArchiveMaintenanceSchedule schedule, CancellationToken ct = default);

    /// <summary>
    /// Delete a schedule.
    /// </summary>
    Task<bool> DeleteScheduleAsync(string scheduleId, CancellationToken ct = default);

    /// <summary>
    /// Enable or disable a schedule.
    /// </summary>
    Task<bool> SetScheduleEnabledAsync(string scheduleId, bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Get schedules that are due for execution.
    /// </summary>
    IReadOnlyList<ArchiveMaintenanceSchedule> GetDueSchedules(DateTimeOffset asOf);

    /// <summary>
    /// Get an overview of all schedules.
    /// </summary>
    MaintenanceScheduleSummary GetStatusSummary();

    /// <summary>
    /// Event raised when a schedule is created.
    /// </summary>
    event EventHandler<ArchiveMaintenanceSchedule>? ScheduleCreated;

    /// <summary>
    /// Event raised when a schedule is updated.
    /// </summary>
    event EventHandler<ArchiveMaintenanceSchedule>? ScheduleUpdated;

    /// <summary>
    /// Event raised when a schedule is deleted.
    /// </summary>
    event EventHandler<string>? ScheduleDeleted;
}

/// <summary>
/// Summary of maintenance schedules.
/// </summary>
public sealed record MaintenanceScheduleSummary(
    int TotalSchedules,
    int EnabledSchedules,
    int DisabledSchedules,
    Dictionary<MaintenanceTaskType, int> ByTaskType,
    DateTimeOffset? NextDueSchedule,
    string? NextDueScheduleName
);

/// <summary>
/// Interface for tracking maintenance execution history.
/// </summary>
public interface IMaintenanceExecutionHistory
{
    /// <summary>
    /// Record a new execution.
    /// </summary>
    void RecordExecution(MaintenanceExecution execution);

    /// <summary>
    /// Update an existing execution record.
    /// </summary>
    void UpdateExecution(MaintenanceExecution execution);

    /// <summary>
    /// Get a specific execution by ID.
    /// </summary>
    MaintenanceExecution? GetExecution(string executionId);

    /// <summary>
    /// Get recent executions.
    /// </summary>
    IReadOnlyList<MaintenanceExecution> GetRecentExecutions(int limit = 50);

    /// <summary>
    /// Get executions for a specific schedule.
    /// </summary>
    IReadOnlyList<MaintenanceExecution> GetExecutionsForSchedule(string scheduleId, int limit = 50);

    /// <summary>
    /// Get failed executions.
    /// </summary>
    IReadOnlyList<MaintenanceExecution> GetFailedExecutions(int limit = 50);

    /// <summary>
    /// Get executions within a time range.
    /// </summary>
    IReadOnlyList<MaintenanceExecution> GetExecutionsByTimeRange(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Get summary for a specific schedule.
    /// </summary>
    ScheduleExecutionSummary GetScheduleSummary(string scheduleId, int recentCount = 10);

    /// <summary>
    /// Get overall maintenance statistics.
    /// </summary>
    MaintenanceStatistics GetStatistics(TimeSpan? period = null);

    /// <summary>
    /// Clean up old execution records.
    /// </summary>
    Task<int> CleanupOldRecordsAsync(int maxAgeDays = 90, CancellationToken ct = default);
}
