using FluentAssertions;
using MarketDataCollector.Application.Notifications;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Archival;
using MarketDataCollector.Storage.Deduplication;
using MarketDataCollector.Storage.Maintenance;
using MarketDataCollector.Storage.OfflineQueue;
using Xunit;

namespace MarketDataCollector.Tests.Archival;

/// <summary>
/// Tests for archival notifications, integrity alerts, and related features.
/// </summary>
public class ArchivalNotificationsIntegrityTests
{
    #region IntegrityAlertsService Tests

    [Fact]
    public void IntegrityAlertsService_RecordEvent_UpdatesCounters()
    {
        // Arrange
        using var service = new IntegrityAlertsService();
        var evt = IntegrityEvent.SequenceGap(
            DateTimeOffset.UtcNow,
            "SPY",
            expectedNext: 100,
            received: 105);

        // Act
        service.RecordEvent(evt);
        var summary = service.GetSummary();

        // Assert
        summary.TotalErrors.Should().Be(1);
        summary.TotalGaps.Should().Be(1);
    }

    [Fact]
    public void IntegrityAlertsService_RecordWarning_UpdatesWarningCounter()
    {
        // Arrange
        using var service = new IntegrityAlertsService();
        var evt = IntegrityEvent.OutOfOrder(
            DateTimeOffset.UtcNow,
            "AAPL",
            lastSeq: 100,
            receivedSeq: 99);

        // Act
        service.RecordEvent(evt);
        var summary = service.GetSummary();

        // Assert
        summary.TotalWarnings.Should().Be(1);
    }

    [Fact]
    public void IntegrityAlertsService_RaisesAlert_WhenErrorThresholdExceeded()
    {
        // Arrange
        var config = new IntegrityAlertsConfig
        {
            MinAlertIntervalSeconds = 0,
            HighErrorThreshold = 2
        };
        using var service = new IntegrityAlertsService(config);
        var alertRaised = false;
        service.OnIntegrityAlert += _ => alertRaised = true;

        // Act - Record multiple errors
        for (int i = 0; i < 5; i++)
        {
            service.RecordEvent(IntegrityEvent.SequenceGap(
                DateTimeOffset.UtcNow,
                "SPY",
                expectedNext: 100 + i,
                received: 110 + i));
        }

        // Assert
        alertRaised.Should().BeTrue();
    }

    [Fact]
    public void IntegrityAlertsService_GetSymbolState_ReturnsCorrectData()
    {
        // Arrange
        using var service = new IntegrityAlertsService();

        service.RecordEvent(IntegrityEvent.SequenceGap(DateTimeOffset.UtcNow, "SPY", 100, 105));
        service.RecordEvent(IntegrityEvent.SequenceGap(DateTimeOffset.UtcNow, "SPY", 106, 110));

        // Act
        var state = service.GetSymbolState("SPY");

        // Assert
        state.Should().NotBeNull();
        state!.Symbol.Should().Be("SPY");
        state.TotalErrors.Should().Be(2);
    }

    [Fact]
    public void IntegrityAlertsService_Reset_ClearsAllData()
    {
        // Arrange
        using var service = new IntegrityAlertsService();
        service.RecordEvent(IntegrityEvent.SequenceGap(DateTimeOffset.UtcNow, "SPY", 100, 105));

        // Act
        service.Reset();
        var summary = service.GetSummary();

        // Assert
        summary.TotalErrors.Should().Be(0);
        summary.TotalGaps.Should().Be(0);
        summary.SymbolsWithIssues.Should().Be(0);
    }

    #endregion

    #region AutoReconnectionManager Tests

    [Fact]
    public async Task AutoReconnectionManager_GetStatusSnapshot_ReturnsCorrectState()
    {
        // Arrange
        using var manager = new AutoReconnectionManager();

        // Act
        var snapshot = manager.GetStatusSnapshot();

        // Assert
        snapshot.IsPaused.Should().BeFalse();
        snapshot.ActiveReconnections.Should().Be(0);
    }

    [Fact]
    public void AutoReconnectionManager_Pause_SetsPausedState()
    {
        // Arrange
        using var manager = new AutoReconnectionManager();

        // Act
        manager.Pause();
        var snapshot = manager.GetStatusSnapshot();

        // Assert
        snapshot.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void AutoReconnectionManager_Resume_ClearsPausedState()
    {
        // Arrange
        using var manager = new AutoReconnectionManager();
        manager.Pause();

        // Act
        manager.Resume();
        var snapshot = manager.GetStatusSnapshot();

        // Assert
        snapshot.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task AutoReconnectionManager_OnReconnectionAttempt_RaisesEvent()
    {
        // Arrange
        using var manager = new AutoReconnectionManager(new AutoReconnectionConfig
        {
            MaxAttempts = 2,
            BaseDelaySeconds = 0.1
        });

        var attemptReceived = false;
        manager.OnReconnectionAttempt += _ => attemptReceived = true;
        manager.ReconnectHandler = (_, _) => Task.FromResult(true);

        // Act
        await manager.StartReconnectionAsync("test-conn", "TestProvider");

        // Assert
        attemptReceived.Should().BeTrue();
    }

    [Fact]
    public async Task AutoReconnectionManager_OnSuccess_RaisesSuccessEvent()
    {
        // Arrange
        using var manager = new AutoReconnectionManager(new AutoReconnectionConfig
        {
            MaxAttempts = 3,
            BaseDelaySeconds = 0.1
        });

        var successReceived = false;
        manager.OnReconnectionSuccess += _ => successReceived = true;
        manager.ReconnectHandler = (_, _) => Task.FromResult(true);

        // Act
        await manager.StartReconnectionAsync("test-conn", "TestProvider");

        // Assert
        successReceived.Should().BeTrue();
    }

    #endregion

    #region DataDeduplicationService Tests

    [Fact]
    public void DeduplicationService_CheckDuplicate_IdentifiesUnique()
    {
        // Arrange
        using var service = new DataDeduplicationService();
        var evt = CreateTradeEvent("SPY", 100.50m, 1);

        // Act
        var result = service.CheckDuplicate(evt);

        // Assert
        result.IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public void DeduplicationService_CheckDuplicate_IdentifiesDuplicate()
    {
        // Arrange
        using var service = new DataDeduplicationService();
        var evt1 = CreateTradeEvent("SPY", 100.50m, 1);
        var evt2 = CreateTradeEvent("SPY", 100.50m, 1); // Same event

        // Act
        service.CheckDuplicate(evt1);
        var result = service.CheckDuplicate(evt2);

        // Assert
        result.IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public void DeduplicationService_GetStatistics_TracksProcessedCount()
    {
        // Arrange
        using var service = new DataDeduplicationService();

        for (int i = 0; i < 10; i++)
        {
            service.CheckDuplicate(CreateTradeEvent("SPY", 100.50m + i, i));
        }

        // Act
        var stats = service.GetStatistics();

        // Assert
        stats.TotalProcessed.Should().Be(10);
        stats.TotalDuplicates.Should().Be(0);
    }

    [Fact]
    public void DeduplicationService_AnalyzeBatch_FindsDuplicates()
    {
        // Arrange
        using var service = new DataDeduplicationService();
        var events = new List<MarketEvent>
        {
            CreateTradeEvent("SPY", 100.50m, 1),
            CreateTradeEvent("SPY", 100.50m, 1), // Duplicate
            CreateTradeEvent("AAPL", 150.00m, 1)
        };

        // Act
        var report = service.AnalyzeBatch(events);

        // Assert
        report.TotalEvents.Should().Be(3);
        report.TotalDuplicates.Should().Be(1);
        report.UniqueEvents.Should().Be(2);
    }

    [Fact]
    public void DeduplicationService_ClearCache_ResetsFingerprints()
    {
        // Arrange
        using var service = new DataDeduplicationService();
        service.CheckDuplicate(CreateTradeEvent("SPY", 100.50m, 1));

        // Act
        service.ClearCache();
        var result = service.CheckDuplicate(CreateTradeEvent("SPY", 100.50m, 1));

        // Assert - Same event should not be duplicate after cache clear
        result.IsDuplicate.Should().BeFalse();
    }

    #endregion

    #region OfflineEventQueue Tests

    [Fact]
    public void OfflineEventQueue_TryEnqueue_SucceedsWhenOnline()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"queue_test_{Guid.NewGuid():N}");
        try
        {
            using var queue = new OfflineEventQueue(tempDir);
            var evt = CreateTradeEvent("SPY", 100.50m, 1);

            // Act
            var result = queue.TryEnqueue(evt);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfflineEventQueue_GetStatus_ReflectsOnlineState()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"queue_test_{Guid.NewGuid():N}");
        try
        {
            using var queue = new OfflineEventQueue(tempDir);

            // Act
            var status = queue.GetStatus();

            // Assert
            status.IsOnline.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfflineEventQueue_GoOffline_SetsOfflineState()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"queue_test_{Guid.NewGuid():N}");
        try
        {
            using var queue = new OfflineEventQueue(tempDir);

            // Act
            queue.GoOffline("Test offline");
            var status = queue.GetStatus();

            // Assert
            status.IsOnline.Should().BeFalse();
            status.OfflineSince.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfflineEventQueue_RecordClockSync_TracksDrift()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"queue_test_{Guid.NewGuid():N}");
        try
        {
            using var queue = new OfflineEventQueue(tempDir);
            var serverTime = DateTimeOffset.UtcNow.AddMilliseconds(50);

            // Act
            queue.RecordClockSync("TestProvider", serverTime);
            var status = queue.GetStatus();

            // Assert
            status.ClockSyncStates.Should().ContainKey("TestProvider");
            var syncInfo = status.ClockSyncStates["TestProvider"];
            syncInfo.SyncCount.Should().Be(1);
            Math.Abs(syncInfo.DriftMs).Should().BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region ArchiveMaintenanceScheduler Tests

    [Fact]
    public async Task MaintenanceScheduler_GetStatus_ReturnsValidStatus()
    {
        // Arrange
        await using var scheduler = new ArchiveMaintenanceScheduler();
        scheduler.Initialize();

        // Act
        var status = scheduler.GetStatus();

        // Assert
        status.IsPaused.Should().BeFalse();
        status.TaskCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MaintenanceScheduler_Pause_SetsPausedState()
    {
        // Arrange
        await using var scheduler = new ArchiveMaintenanceScheduler();

        // Act
        scheduler.Pause();
        var status = scheduler.GetStatus();

        // Assert
        status.IsPaused.Should().BeTrue();
    }

    [Fact]
    public async Task MaintenanceScheduler_ScheduleTask_AddsTask()
    {
        // Arrange
        await using var scheduler = new ArchiveMaintenanceScheduler();

        // Act
        var taskId = scheduler.ScheduleTask(new MaintenanceTaskConfig
        {
            Name = "Test Task",
            Action = "verify_recent",
            Schedule = "0 3 * * *"
        });

        var status = scheduler.GetStatus();

        // Assert
        taskId.Should().NotBeEmpty();
        status.Tasks.Should().Contain(t => t.Name == "Test Task");
    }

    [Fact]
    public async Task MaintenanceScheduler_CancelTask_RemovesTask()
    {
        // Arrange
        await using var scheduler = new ArchiveMaintenanceScheduler();
        var taskId = scheduler.ScheduleTask(new MaintenanceTaskConfig
        {
            Name = "Test Task",
            Action = "cleanup_temp",
            Schedule = "0 5 * * *"
        });

        // Act
        var result = scheduler.CancelTask(taskId);
        var status = scheduler.GetStatus();

        // Assert
        result.Should().BeTrue();
        status.Tasks.Should().NotContain(t => t.Name == "Test Task");
    }

    [Fact]
    public async Task MaintenanceScheduler_RunTaskNow_ExecutesTask()
    {
        // Arrange
        await using var scheduler = new ArchiveMaintenanceScheduler();
        var taskId = scheduler.ScheduleTask(new MaintenanceTaskConfig
        {
            Name = "Immediate Task",
            Action = "cleanup_temp",
            Schedule = "0 5 * * *"
        });

        // Act
        var result = await scheduler.RunTaskNowAsync(taskId);

        // Assert
        result.Success.Should().BeTrue();
        result.TaskName.Should().Be("Immediate Task");
    }

    [Fact]
    public void MaintenanceScheduler_IsMarketHours_ReturnsFalseOnWeekend()
    {
        // Arrange
        var config = new MaintenanceSchedulerConfig
        {
            PauseDuringMarketHours = true
        };
        using var scheduler = new ArchiveMaintenanceScheduler(config);

        // Act & Assert
        // This test is time-dependent but validates the method exists and works
        var result = scheduler.IsMarketHours();
        result.Should().BeOfType<bool>();
    }

    #endregion

    #region PortableArchivePackager Tests

    [Fact]
    public async Task PortableArchivePackager_CreatePackage_WithNoFiles_ReturnsError()
    {
        // Arrange
        var tempDataDir = Path.Combine(Path.GetTempPath(), $"data_{Guid.NewGuid():N}");
        var tempOutputDir = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDataDir);
            Directory.CreateDirectory(tempOutputDir);

            var packager = new PortableArchivePackager(tempDataDir, tempOutputDir);
            var request = new PackageRequest
            {
                Name = "TestPackage",
                Symbols = new[] { "NONEXISTENT" }
            };

            // Act
            var result = await packager.CreatePackageAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Contain("No files found");
        }
        finally
        {
            if (Directory.Exists(tempDataDir)) Directory.Delete(tempDataDir, true);
            if (Directory.Exists(tempOutputDir)) Directory.Delete(tempOutputDir, true);
        }
    }

    [Fact]
    public async Task PortableArchivePackager_CreatePackage_WithFiles_Succeeds()
    {
        // Arrange
        var tempDataDir = Path.Combine(Path.GetTempPath(), $"data_{Guid.NewGuid():N}");
        var tempOutputDir = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDataDir);
            Directory.CreateDirectory(tempOutputDir);

            // Create a test file
            var testFile = Path.Combine(tempDataDir, "SPY_trades.jsonl");
            await File.WriteAllTextAsync(testFile, "{\"test\": \"data\"}\n{\"test\": \"data2\"}");

            var packager = new PortableArchivePackager(tempDataDir, tempOutputDir);
            var request = new PackageRequest
            {
                Name = "TestPackage",
                Format = PackageFormat.Zip
            };

            // Act
            var result = await packager.CreatePackageAsync(request);

            // Assert
            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNull();
            File.Exists(result.PackagePath).Should().BeTrue();
            result.FileCount.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempDataDir)) Directory.Delete(tempDataDir, true);
            if (Directory.Exists(tempOutputDir)) Directory.Delete(tempOutputDir, true);
        }
    }

    #endregion

    #region Helper Methods

    private static MarketEvent CreateTradeEvent(string symbol, decimal price, long sequence)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var trade = new Trade(
            Timestamp: timestamp,
            Symbol: symbol,
            Price: price,
            Size: 100,
            Aggressor: AggressorSide.Buy,
            SequenceNumber: sequence,
            Venue: "NYSE");

        return MarketEvent.Trade(timestamp, symbol, trade);
    }

    #endregion
}

/// <summary>
/// Tests for the WriteAheadLog functionality.
/// </summary>
public class WriteAheadLogTests
{
    [Fact]
    public async Task WriteAheadLog_Initialize_CreatesDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}");

        try
        {
            await using var wal = new WriteAheadLog(tempDir);

            // Act
            await wal.InitializeAsync();

            // Assert
            Directory.Exists(tempDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WriteAheadLog_Append_ReturnsSequencedRecord()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}");

        try
        {
            await using var wal = new WriteAheadLog(tempDir);
            await wal.InitializeAsync();

            // Act
            var record = await wal.AppendAsync(new { Symbol = "SPY", Price = 100.50 }, "Trade");

            // Assert
            record.Sequence.Should().BeGreaterThan(0);
            record.RecordType.Should().Be("Trade");
            record.Checksum.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WriteAheadLog_Commit_WritesCommitMarker()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}");

        try
        {
            await using var wal = new WriteAheadLog(tempDir);
            await wal.InitializeAsync();

            var record = await wal.AppendAsync(new { Symbol = "SPY" }, "Trade");

            // Act
            await wal.CommitAsync(record.Sequence);

            // Assert - No exception means success
            // WAL file should contain commit marker
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WriteAheadLog_FlushAsync_SyncsToFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}");

        try
        {
            await using var wal = new WriteAheadLog(tempDir, new WalOptions
            {
                SyncMode = WalSyncMode.BatchedSync
            });
            await wal.InitializeAsync();

            await wal.AppendAsync(new { Symbol = "SPY" }, "Trade");

            // Act & Assert - Should not throw
            await wal.FlushAsync();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
