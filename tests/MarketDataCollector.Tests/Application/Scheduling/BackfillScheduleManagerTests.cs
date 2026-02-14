using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MarketDataCollector.Application.Scheduling;
using Xunit;

namespace MarketDataCollector.Tests.Application.Scheduling;

public sealed class BackfillScheduleManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BackfillScheduleManager _manager;
    private readonly ILogger<BackfillScheduleManager> _logger;

    public BackfillScheduleManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"mdc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logger = NullLoggerFactory.Instance.CreateLogger<BackfillScheduleManager>();
        _manager = new BackfillScheduleManager(_logger, _tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* cleanup best-effort */ }
    }

    // ── CreateScheduleAsync ─────────────────────────────────────────

    [Fact]
    public async Task CreateSchedule_WithValidSchedule_AddsToCollection()
    {
        var schedule = CreateTestSchedule("Daily Gap Fill");

        var result = await _manager.CreateScheduleAsync(schedule);

        result.Should().NotBeNull();
        result.Name.Should().Be("Daily Gap Fill");
        result.NextExecutionAt.Should().NotBeNull();
        _manager.GetAllSchedules().Should().ContainSingle();
    }

    [Fact]
    public async Task CreateSchedule_PersistsToFile()
    {
        var schedule = CreateTestSchedule("Persist Test");
        await _manager.CreateScheduleAsync(schedule);

        var schedulesDir = Path.Combine(_tempDir, "_backfill_schedules");
        Directory.Exists(schedulesDir).Should().BeTrue();
        Directory.GetFiles(schedulesDir, "schedule_*.json").Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSchedule_WithInvalidCron_ThrowsArgumentException()
    {
        var schedule = CreateTestSchedule("Bad Cron");
        schedule.CronExpression = "invalid cron";

        var act = () => _manager.CreateScheduleAsync(schedule);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cron*");
    }

    [Fact]
    public async Task CreateSchedule_WithEmptyName_ThrowsArgumentException()
    {
        var schedule = CreateTestSchedule("");

        var act = () => _manager.CreateScheduleAsync(schedule);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public async Task CreateSchedule_WithNull_ThrowsArgumentNullException()
    {
        var act = () => _manager.CreateScheduleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateSchedule_RaisesCreatedEvent()
    {
        BackfillSchedule? eventSchedule = null;
        _manager.ScheduleCreated += (_, s) => eventSchedule = s;

        var schedule = CreateTestSchedule("Event Test");
        await _manager.CreateScheduleAsync(schedule);

        eventSchedule.Should().NotBeNull();
        eventSchedule!.Name.Should().Be("Event Test");
    }

    // ── GetSchedule ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSchedule_ExistingId_ReturnsSchedule()
    {
        var schedule = CreateTestSchedule("Lookup Test");
        await _manager.CreateScheduleAsync(schedule);

        var result = _manager.GetSchedule(schedule.ScheduleId);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Lookup Test");
    }

    [Fact]
    public void GetSchedule_NonExistentId_ReturnsNull()
    {
        _manager.GetSchedule("nonexistent").Should().BeNull();
    }

    // ── GetAllSchedules ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllSchedules_ReturnsOrderedByName()
    {
        await _manager.CreateScheduleAsync(CreateTestSchedule("Zebra"));
        await _manager.CreateScheduleAsync(CreateTestSchedule("Alpha"));
        await _manager.CreateScheduleAsync(CreateTestSchedule("Middle"));

        var all = _manager.GetAllSchedules();
        all.Should().HaveCount(3);
        all[0].Name.Should().Be("Alpha");
        all[1].Name.Should().Be("Middle");
        all[2].Name.Should().Be("Zebra");
    }

    // ── UpdateScheduleAsync ─────────────────────────────────────────

    [Fact]
    public async Task UpdateSchedule_ChangesScheduleProperties()
    {
        var schedule = CreateTestSchedule("Original");
        await _manager.CreateScheduleAsync(schedule);

        schedule.Name = "Updated";
        schedule.LookbackDays = 60;
        var result = await _manager.UpdateScheduleAsync(schedule);

        result.Name.Should().Be("Updated");
        result.LookbackDays.Should().Be(60);
        result.ModifiedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateSchedule_NonExistent_ThrowsKeyNotFound()
    {
        var schedule = CreateTestSchedule("Ghost");

        var act = () => _manager.UpdateScheduleAsync(schedule);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateSchedule_RaisesUpdatedEvent()
    {
        BackfillSchedule? eventSchedule = null;
        _manager.ScheduleUpdated += (_, s) => eventSchedule = s;

        var schedule = CreateTestSchedule("Update Event");
        await _manager.CreateScheduleAsync(schedule);

        schedule.Name = "Updated Name";
        await _manager.UpdateScheduleAsync(schedule);

        eventSchedule.Should().NotBeNull();
        eventSchedule!.Name.Should().Be("Updated Name");
    }

    // ── DeleteScheduleAsync ─────────────────────────────────────────

    [Fact]
    public async Task DeleteSchedule_ExistingId_ReturnsTrue()
    {
        var schedule = CreateTestSchedule("To Delete");
        await _manager.CreateScheduleAsync(schedule);

        var deleted = await _manager.DeleteScheduleAsync(schedule.ScheduleId);
        deleted.Should().BeTrue();
        _manager.GetSchedule(schedule.ScheduleId).Should().BeNull();
    }

    [Fact]
    public async Task DeleteSchedule_NonExistentId_ReturnsFalse()
    {
        var result = await _manager.DeleteScheduleAsync("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSchedule_RaisesDeletedEvent()
    {
        string? deletedId = null;
        _manager.ScheduleDeleted += (_, id) => deletedId = id;

        var schedule = CreateTestSchedule("Delete Event");
        await _manager.CreateScheduleAsync(schedule);
        await _manager.DeleteScheduleAsync(schedule.ScheduleId);

        deletedId.Should().Be(schedule.ScheduleId);
    }

    // ── GetEnabledSchedules / GetDueSchedules ───────────────────────

    [Fact]
    public async Task GetEnabledSchedules_FiltersDisabled()
    {
        var enabled = CreateTestSchedule("Enabled");
        var disabled = CreateTestSchedule("Disabled");
        disabled.Enabled = false;

        await _manager.CreateScheduleAsync(enabled);
        await _manager.CreateScheduleAsync(disabled);

        _manager.GetEnabledSchedules().Should().ContainSingle()
            .Which.Name.Should().Be("Enabled");
    }

    [Fact]
    public async Task GetDueSchedules_ReturnsPastDueOnly()
    {
        var schedule = CreateTestSchedule("Past Due");
        await _manager.CreateScheduleAsync(schedule);

        // Set next execution to the past
        schedule.NextExecutionAt = DateTimeOffset.UtcNow.AddHours(-1);

        var due = _manager.GetDueSchedules();
        due.Should().ContainSingle().Which.Name.Should().Be("Past Due");
    }

    [Fact]
    public async Task GetDueSchedules_ExcludesFutureSchedules()
    {
        var schedule = CreateTestSchedule("Future");
        await _manager.CreateScheduleAsync(schedule);

        // NextExecutionAt was calculated from cron, should be in the future
        var due = _manager.GetDueSchedules(DateTimeOffset.UtcNow.AddMinutes(-10));
        due.Should().BeEmpty();
    }

    // ── SetScheduleEnabledAsync ─────────────────────────────────────

    [Fact]
    public async Task SetEnabled_DisablesSchedule()
    {
        var schedule = CreateTestSchedule("Toggle Test");
        await _manager.CreateScheduleAsync(schedule);

        var result = await _manager.SetScheduleEnabledAsync(schedule.ScheduleId, false);
        result.Should().BeTrue();

        var updated = _manager.GetSchedule(schedule.ScheduleId);
        updated!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabled_EnablesAndRecalculatesNextExecution()
    {
        var schedule = CreateTestSchedule("Re-enable");
        schedule.Enabled = false;
        await _manager.CreateScheduleAsync(schedule);

        await _manager.SetScheduleEnabledAsync(schedule.ScheduleId, true);

        var updated = _manager.GetSchedule(schedule.ScheduleId);
        updated!.Enabled.Should().BeTrue();
        updated.NextExecutionAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetEnabled_NonExistent_ReturnsFalse()
    {
        var result = await _manager.SetScheduleEnabledAsync("nonexistent", true);
        result.Should().BeFalse();
    }

    // ── RecordExecutionAsync ────────────────────────────────────────

    [Fact]
    public async Task RecordExecution_UpdatesScheduleCounters()
    {
        var schedule = CreateTestSchedule("Execution Record");
        await _manager.CreateScheduleAsync(schedule);

        var execution = new BackfillExecutionLog
        {
            ScheduleId = schedule.ScheduleId,
            ScheduleName = schedule.Name,
            Status = ExecutionStatus.Completed,
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            ToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        };

        await _manager.RecordExecutionAsync(schedule, execution);

        schedule.ExecutionCount.Should().Be(1);
        schedule.SuccessfulExecutions.Should().Be(1);
        schedule.FailedExecutions.Should().Be(0);
        schedule.LastExecutedAt.Should().NotBeNull();
        schedule.LastJobId.Should().Be(execution.JobId);
    }

    [Fact]
    public async Task RecordExecution_IncreasesFailureCount()
    {
        var schedule = CreateTestSchedule("Failure Record");
        await _manager.CreateScheduleAsync(schedule);

        var execution = new BackfillExecutionLog
        {
            ScheduleId = schedule.ScheduleId,
            ScheduleName = schedule.Name,
            Status = ExecutionStatus.Failed,
            ErrorMessage = "Test failure"
        };

        await _manager.RecordExecutionAsync(schedule, execution);

        schedule.FailedExecutions.Should().Be(1);
        schedule.SuccessfulExecutions.Should().Be(0);
    }

    // ── CreateFromPresetAsync ───────────────────────────────────────

    [Theory]
    [InlineData("daily")]
    [InlineData("dailygapfill")]
    [InlineData("weekly")]
    [InlineData("eod")]
    [InlineData("monthly")]
    public async Task CreateFromPreset_CreatesScheduleWithValidCron(string preset)
    {
        var result = await _manager.CreateFromPresetAsync(preset, $"Test {preset}");

        result.Should().NotBeNull();
        result.Name.Should().Be($"Test {preset}");
        result.NextExecutionAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateFromPreset_WithSymbols_IncludesSymbols()
    {
        var symbols = new[] { "SPY", "AAPL" };
        var result = await _manager.CreateFromPresetAsync("daily", "With Symbols", symbols);

        result.Symbols.Should().BeEquivalentTo(symbols);
    }

    [Fact]
    public async Task CreateFromPreset_UnknownPreset_ThrowsArgumentException()
    {
        var act = () => _manager.CreateFromPresetAsync("nonexistent", "Bad Preset");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*preset*");
    }

    // ── CreateManualExecution ────────────────────────────────────────

    [Fact]
    public async Task CreateManualExecution_SetsCorrectTrigger()
    {
        var schedule = CreateTestSchedule("Manual Exec");
        await _manager.CreateScheduleAsync(schedule);

        var execution = _manager.CreateManualExecution(schedule);

        execution.Trigger.Should().Be(ExecutionTrigger.Manual);
        execution.ScheduleId.Should().Be(schedule.ScheduleId);
        execution.ScheduleName.Should().Be("Manual Exec");
        execution.Symbols.Should().BeEquivalentTo(schedule.Symbols);
    }

    // ── GetSchedulesByTag ───────────────────────────────────────────

    [Fact]
    public async Task GetSchedulesByTag_FiltersCorrectly()
    {
        var tagged = CreateTestSchedule("Tagged");
        tagged.Tags.Add("production");
        var untagged = CreateTestSchedule("Untagged");

        await _manager.CreateScheduleAsync(tagged);
        await _manager.CreateScheduleAsync(untagged);

        _manager.GetSchedulesByTag("production").Should().ContainSingle()
            .Which.Name.Should().Be("Tagged");
    }

    [Fact]
    public async Task GetSchedulesByTag_IsCaseInsensitive()
    {
        var schedule = CreateTestSchedule("Case Test");
        schedule.Tags.Add("PRODUCTION");

        await _manager.CreateScheduleAsync(schedule);

        _manager.GetSchedulesByTag("production").Should().ContainSingle();
    }

    // ── GetStatusSummary ────────────────────────────────────────────

    [Fact]
    public async Task GetStatusSummary_ReturnsCorrectCounts()
    {
        var enabled1 = CreateTestSchedule("E1");
        var enabled2 = CreateTestSchedule("E2");
        var disabled = CreateTestSchedule("D1");
        disabled.Enabled = false;

        await _manager.CreateScheduleAsync(enabled1);
        await _manager.CreateScheduleAsync(enabled2);
        await _manager.CreateScheduleAsync(disabled);

        var summary = _manager.GetStatusSummary();
        summary.TotalSchedules.Should().Be(3);
        summary.EnabledSchedules.Should().Be(2);
        summary.DisabledSchedules.Should().Be(1);
    }

    // ── LoadSchedulesAsync ──────────────────────────────────────────

    [Fact]
    public async Task LoadSchedules_CreatesDirectoryIfMissing()
    {
        var freshDir = Path.Combine(_tempDir, "fresh");
        var manager = new BackfillScheduleManager(_logger, freshDir);

        await manager.LoadSchedulesAsync();

        Directory.Exists(Path.Combine(freshDir, "_backfill_schedules")).Should().BeTrue();
    }

    [Fact]
    public async Task LoadSchedules_RestoresPersistedSchedules()
    {
        // Create and persist a schedule
        var schedule = CreateTestSchedule("Persist Restore");
        await _manager.CreateScheduleAsync(schedule);

        // Create a new manager pointing to the same directory
        var newManager = new BackfillScheduleManager(_logger, _tempDir);
        await newManager.LoadSchedulesAsync();

        var restored = newManager.GetSchedule(schedule.ScheduleId);
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("Persist Restore");
    }

    [Fact]
    public async Task LoadSchedules_OnlyLoadsOnce()
    {
        await _manager.LoadSchedulesAsync();
        await _manager.LoadSchedulesAsync(); // Second call should be no-op

        // No exception = test passes
    }

    // ── HasRunningSchedules ─────────────────────────────────────────

    [Fact]
    public void HasRunningSchedules_WithNoExecutions_ReturnsFalse()
    {
        _manager.HasRunningSchedules().Should().BeFalse();
    }

    [Fact]
    public async Task HasRunningSchedules_WithRunningExecution_ReturnsTrue()
    {
        var schedule = CreateTestSchedule("Running");
        await _manager.CreateScheduleAsync(schedule);

        var execution = new BackfillExecutionLog
        {
            ScheduleId = schedule.ScheduleId,
            ScheduleName = schedule.Name,
            Status = ExecutionStatus.Running
        };

        _manager.ExecutionHistory.AddExecution(execution);

        _manager.HasRunningSchedules().Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static BackfillSchedule CreateTestSchedule(string name)
    {
        return new BackfillSchedule
        {
            Name = name,
            CronExpression = "0 2 * * *", // Daily at 2 AM
            Symbols = new List<string> { "SPY", "AAPL" },
            LookbackDays = 7
        };
    }
}
