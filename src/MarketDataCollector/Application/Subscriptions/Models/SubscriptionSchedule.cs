namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Schedule for enabling/disabling symbol subscriptions by time/date.
/// </summary>
public sealed record SubscriptionSchedule(
    /// <summary>Unique schedule identifier.</summary>
    string Id,

    /// <summary>Display name for the schedule.</summary>
    string Name,

    /// <summary>Symbols affected by this schedule.</summary>
    string[] Symbols,

    /// <summary>Action to perform: Enable or Disable subscriptions.</summary>
    ScheduleAction Action,

    /// <summary>Schedule timing configuration.</summary>
    ScheduleTiming Timing,

    /// <summary>Whether the schedule is currently active.</summary>
    bool IsEnabled = true,

    /// <summary>Optional description of the schedule.</summary>
    string? Description = null
);

/// <summary>
/// Action to perform when schedule triggers.
/// </summary>
public enum ScheduleAction
{
    /// <summary>Enable subscriptions for the specified symbols.</summary>
    Enable,

    /// <summary>Disable subscriptions for the specified symbols.</summary>
    Disable
}

/// <summary>
/// Timing configuration for a subscription schedule.
/// </summary>
public sealed record ScheduleTiming(
    /// <summary>Type of schedule: OneTime, Daily, Weekly, Custom.</summary>
    ScheduleType Type,

    /// <summary>Time of day to trigger (UTC). Format: HH:mm.</summary>
    string TimeUtc,

    /// <summary>For OneTime: specific date. For Weekly: not used.</summary>
    DateOnly? Date = null,

    /// <summary>For Weekly: days of week (0=Sunday, 6=Saturday).</summary>
    int[]? DaysOfWeek = null,

    /// <summary>Optional end date for recurring schedules.</summary>
    DateOnly? EndDate = null,

    /// <summary>Timezone for display purposes (schedule runs in UTC).</summary>
    string Timezone = "UTC"
);

/// <summary>
/// Type of schedule frequency.
/// </summary>
public enum ScheduleType
{
    /// <summary>Runs once at a specific date and time.</summary>
    OneTime,

    /// <summary>Runs daily at the specified time.</summary>
    Daily,

    /// <summary>Runs on specific days of the week.</summary>
    Weekly,

    /// <summary>Custom cron-like expression (future).</summary>
    Custom
}

/// <summary>
/// Status of a schedule execution.
/// </summary>
public sealed record ScheduleExecutionStatus(
    string ScheduleId,
    DateTimeOffset LastRun,
    DateTimeOffset? NextRun,
    bool LastRunSuccess,
    string? LastError = null,
    int SymbolsAffected = 0
);

/// <summary>
/// Request to create or update a schedule.
/// </summary>
public sealed record CreateScheduleRequest(
    string Name,
    string[] Symbols,
    ScheduleAction Action,
    ScheduleTiming Timing,
    string? Description = null
);
