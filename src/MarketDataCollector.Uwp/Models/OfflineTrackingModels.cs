using System.Text.Json.Serialization;

namespace MarketDataCollector.Uwp.Models;

// ============================================================================
// Offline Tracking Persistence Models
// Enables symbol tracking persistence and background task scheduling
// that survives user logout, app restart, and system restart.
// ============================================================================

/// <summary>
/// Write-Ahead Log entry for tracking operations.
/// Ensures operations are persisted before execution and can be recovered.
/// </summary>
public class WalEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty; // Subscribe, Unsubscribe, StartBackfill, StopBackfill, StartSession, StopSession

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty; // JSON-serialized operation data

    [JsonPropertyName("status")]
    public WalEntryStatus Status { get; set; } = WalEntryStatus.Pending;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [JsonPropertyName("failedAt")]
    public DateTime? FailedAt { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 5;

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100; // Lower = higher priority

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Status of a WAL entry.
/// </summary>
public enum WalEntryStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Expired,
    Cancelled
}

/// <summary>
/// Persisted subscription state for recovery after app restart.
/// </summary>
public class PersistedSubscription
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("subscriptionType")]
    public string SubscriptionType { get; set; } = "Trades"; // Trades, Depth, Quotes, Bars

    [JsonPropertyName("config")]
    public SymbolConfig? Config { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastActiveAt")]
    public DateTime? LastActiveAt { get; set; }

    [JsonPropertyName("shouldAutoRecover")]
    public bool ShouldAutoRecover { get; set; } = true;

    [JsonPropertyName("recoveryPriority")]
    public int RecoveryPriority { get; set; } = 100;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Configuration for persisted subscriptions.
/// </summary>
public class SubscriptionPersistenceConfig
{
    [JsonPropertyName("subscriptions")]
    public PersistedSubscription[] Subscriptions { get; set; } = Array.Empty<PersistedSubscription>();

    [JsonPropertyName("lastCheckpointAt")]
    public DateTime? LastCheckpointAt { get; set; }

    [JsonPropertyName("autoRecoveryEnabled")]
    public bool AutoRecoveryEnabled { get; set; } = true;

    [JsonPropertyName("recoveryDelaySeconds")]
    public int RecoveryDelaySeconds { get; set; } = 5;

    [JsonPropertyName("maxConcurrentRecoveries")]
    public int MaxConcurrentRecoveries { get; set; } = 10;
}

/// <summary>
/// Scheduled background task definition.
/// </summary>
public class ScheduledTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("taskType")]
    public ScheduledTaskType TaskType { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("schedule")]
    public TaskSchedule Schedule { get; set; } = new();

    [JsonPropertyName("payload")]
    public string? Payload { get; set; } // JSON-serialized task-specific configuration

    [JsonPropertyName("lastRunAt")]
    public DateTime? LastRunAt { get; set; }

    [JsonPropertyName("nextRunAt")]
    public DateTime? NextRunAt { get; set; }

    [JsonPropertyName("lastRunStatus")]
    public string? LastRunStatus { get; set; } // Success, Failed, Skipped, Cancelled

    [JsonPropertyName("lastRunDurationSeconds")]
    public double? LastRunDurationSeconds { get; set; }

    [JsonPropertyName("lastRunError")]
    public string? LastRunError { get; set; }

    [JsonPropertyName("runCount")]
    public int RunCount { get; set; }

    [JsonPropertyName("failCount")]
    public int FailCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("notifyOnCompletion")]
    public bool NotifyOnCompletion { get; set; } = true;

    [JsonPropertyName("notifyOnFailure")]
    public bool NotifyOnFailure { get; set; } = true;

    [JsonPropertyName("runMissedOnStartup")]
    public bool RunMissedOnStartup { get; set; } = true;

    [JsonPropertyName("allowConcurrent")]
    public bool AllowConcurrent { get; set; } = false;

    [JsonPropertyName("timeoutMinutes")]
    public int TimeoutMinutes { get; set; } = 60;
}

/// <summary>
/// Types of scheduled tasks.
/// </summary>
public enum ScheduledTaskType
{
    /// <summary>Start data collection for configured symbols.</summary>
    StartCollection,

    /// <summary>Stop data collection.</summary>
    StopCollection,

    /// <summary>Run historical backfill for specified symbols.</summary>
    RunBackfill,

    /// <summary>Run archive verification.</summary>
    RunVerification,

    /// <summary>Run data cleanup/retention.</summary>
    RunCleanup,

    /// <summary>Export data to specified format.</summary>
    RunExport,

    /// <summary>Generate manifest for collected data.</summary>
    GenerateManifest,

    /// <summary>Sync data to remote storage.</summary>
    SyncToRemote,

    /// <summary>Custom script execution.</summary>
    Custom
}

/// <summary>
/// Schedule configuration for a task.
/// </summary>
public class TaskSchedule
{
    [JsonPropertyName("scheduleType")]
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;

    [JsonPropertyName("time")]
    public string Time { get; set; } = "09:30"; // HH:mm format (local time)

    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; } = "America/New_York"; // IANA timezone

    [JsonPropertyName("daysOfWeek")]
    public DayOfWeek[]? DaysOfWeek { get; set; } // For Weekly schedule

    [JsonPropertyName("dayOfMonth")]
    public int? DayOfMonth { get; set; } // For Monthly schedule (1-31)

    [JsonPropertyName("intervalMinutes")]
    public int? IntervalMinutes { get; set; } // For Interval schedule

    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; } // For Cron schedule

    [JsonPropertyName("skipWeekends")]
    public bool SkipWeekends { get; set; } = true;

    [JsonPropertyName("skipHolidays")]
    public bool SkipHolidays { get; set; } = true;

    [JsonPropertyName("holidayCalendar")]
    public string HolidayCalendar { get; set; } = "US"; // US, NYSE, etc.

    [JsonPropertyName("marketHoursOnly")]
    public bool MarketHoursOnly { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Types of schedules.
/// </summary>
public enum ScheduleType
{
    Once,
    Interval,
    Daily,
    Weekly,
    Monthly,
    Cron
}

/// <summary>
/// Configuration for the background task scheduler.
/// </summary>
public class SchedulerConfig
{
    [JsonPropertyName("tasks")]
    public ScheduledTask[] Tasks { get; set; } = Array.Empty<ScheduledTask>();

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("runOnStartup")]
    public bool RunOnStartup { get; set; } = true;

    [JsonPropertyName("checkIntervalSeconds")]
    public int CheckIntervalSeconds { get; set; } = 60;

    [JsonPropertyName("maxConcurrentTasks")]
    public int MaxConcurrentTasks { get; set; } = 3;

    [JsonPropertyName("logRetentionDays")]
    public int LogRetentionDays { get; set; } = 30;

    [JsonPropertyName("pauseDuringBackfill")]
    public bool PauseDuringBackfill { get; set; }

    [JsonPropertyName("defaultTimeZone")]
    public string DefaultTimeZone { get; set; } = "America/New_York";

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Task execution log entry.
/// </summary>
public class TaskExecutionLog
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("taskName")]
    public string TaskName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Running"; // Running, Success, Failed, Cancelled, Timeout

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("durationSeconds")]
    public double? DurationSeconds { get; set; }

    [JsonPropertyName("triggeredBy")]
    public string TriggeredBy { get; set; } = "Scheduler"; // Scheduler, Manual, Startup, Recovery

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("itemsProcessed")]
    public int? ItemsProcessed { get; set; }

    [JsonPropertyName("bytesProcessed")]
    public long? BytesProcessed { get; set; }
}

/// <summary>
/// Pending operation for offline queue.
/// </summary>
public class PendingOperation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("operationType")]
    public PendingOperationType OperationType { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public PendingOperationStatus Status { get; set; } = PendingOperationStatus.Queued;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }

    [JsonPropertyName("requiresConnection")]
    public bool RequiresConnection { get; set; } = true;

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("dependsOnOperationId")]
    public string? DependsOnOperationId { get; set; }
}

/// <summary>
/// Types of pending operations.
/// </summary>
public enum PendingOperationType
{
    Subscribe,
    Unsubscribe,
    StartBackfill,
    StopBackfill,
    StartSession,
    StopSession,
    SyncData,
    VerifyArchive,
    ExportData
}

/// <summary>
/// Status of a pending operation.
/// </summary>
public enum PendingOperationStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Expired,
    Cancelled,
    WaitingForConnection
}

/// <summary>
/// Configuration for the offline operations queue.
/// </summary>
public class OfflineQueueConfig
{
    [JsonPropertyName("operations")]
    public PendingOperation[] Operations { get; set; } = Array.Empty<PendingOperation>();

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("processOnReconnect")]
    public bool ProcessOnReconnect { get; set; } = true;

    [JsonPropertyName("maxQueueSize")]
    public int MaxQueueSize { get; set; } = 1000;

    [JsonPropertyName("defaultExpirationHours")]
    public int DefaultExpirationHours { get; set; } = 24;

    [JsonPropertyName("batchSize")]
    public int BatchSize { get; set; } = 10;

    [JsonPropertyName("processIntervalSeconds")]
    public int ProcessIntervalSeconds { get; set; } = 5;

    [JsonPropertyName("lastProcessedAt")]
    public DateTime? LastProcessedAt { get; set; }
}

/// <summary>
/// State of the offline tracking persistence system.
/// </summary>
public class OfflineTrackingState
{
    [JsonPropertyName("walSequenceNumber")]
    public long WalSequenceNumber { get; set; }

    [JsonPropertyName("lastCheckpointAt")]
    public DateTime? LastCheckpointAt { get; set; }

    [JsonPropertyName("lastRecoveryAt")]
    public DateTime? LastRecoveryAt { get; set; }

    [JsonPropertyName("recoveryCount")]
    public int RecoveryCount { get; set; }

    [JsonPropertyName("isRecoveryInProgress")]
    public bool IsRecoveryInProgress { get; set; }

    [JsonPropertyName("activeSubscriptionCount")]
    public int ActiveSubscriptionCount { get; set; }

    [JsonPropertyName("pendingOperationCount")]
    public int PendingOperationCount { get; set; }

    [JsonPropertyName("scheduledTaskCount")]
    public int ScheduledTaskCount { get; set; }

    [JsonPropertyName("lastHeartbeatAt")]
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isServiceRunning")]
    public bool IsServiceRunning { get; set; }

    [JsonPropertyName("serviceStartedAt")]
    public DateTime? ServiceStartedAt { get; set; }

    [JsonPropertyName("cleanShutdown")]
    public bool CleanShutdown { get; set; }
}

/// <summary>
/// Backfill task payload for scheduled backfill operations.
/// </summary>
public class BackfillTaskPayload
{
    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("fromDate")]
    public string? FromDate { get; set; }

    [JsonPropertyName("toDate")]
    public string? ToDate { get; set; }

    [JsonPropertyName("useRelativeDates")]
    public bool UseRelativeDates { get; set; }

    [JsonPropertyName("relativeDaysBack")]
    public int RelativeDaysBack { get; set; } = 30;

    [JsonPropertyName("overwriteExisting")]
    public bool OverwriteExisting { get; set; }

    [JsonPropertyName("fillGaps")]
    public bool FillGaps { get; set; } = true;
}

/// <summary>
/// Collection task payload for scheduled collection operations.
/// </summary>
public class CollectionTaskPayload
{
    [JsonPropertyName("sessionName")]
    public string? SessionName { get; set; }

    [JsonPropertyName("useExistingSession")]
    public bool UseExistingSession { get; set; }

    [JsonPropertyName("autoCreateSession")]
    public bool AutoCreateSession { get; set; } = true;

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("eventTypes")]
    public string[]? EventTypes { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Export task payload for scheduled export operations.
/// </summary>
public class ExportTaskPayload
{
    [JsonPropertyName("exportFormat")]
    public string ExportFormat { get; set; } = "Parquet"; // Parquet, CSV, JSON

    [JsonPropertyName("symbols")]
    public string[]? Symbols { get; set; }

    [JsonPropertyName("dateRange")]
    public DateRangeInfo? DateRange { get; set; }

    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }

    [JsonPropertyName("compress")]
    public bool Compress { get; set; } = true;

    [JsonPropertyName("includeManifest")]
    public bool IncludeManifest { get; set; } = true;
}

/// <summary>
/// Represents a recovery attempt after system restart.
/// </summary>
public class RecoveryAttempt
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "AppRestart"; // AppRestart, SystemRestart, CrashRecovery, Manual

    [JsonPropertyName("subscriptionsRecovered")]
    public int SubscriptionsRecovered { get; set; }

    [JsonPropertyName("subscriptionsFailed")]
    public int SubscriptionsFailed { get; set; }

    [JsonPropertyName("operationsProcessed")]
    public int OperationsProcessed { get; set; }

    [JsonPropertyName("operationsFailed")]
    public int OperationsFailed { get; set; }

    [JsonPropertyName("tasksRescheduled")]
    public int TasksRescheduled { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errors")]
    public string[]? Errors { get; set; }
}
