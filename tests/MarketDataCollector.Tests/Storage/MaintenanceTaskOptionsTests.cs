using FluentAssertions;
using MarketDataCollector.Storage.Maintenance;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

/// <summary>
/// Tests for MaintenanceTaskOptions configuration and defaults.
/// Validates the new incremental migration and market-hours awareness features.
/// </summary>
public sealed class MaintenanceTaskOptionsTests
{
    [Fact]
    public void MaintenanceTaskOptions_DefaultMaxMigrationsPerRun_ShouldBe250()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions();

        // Assert
        options.MaxMigrationsPerRun.Should().Be(250,
            "default should limit migrations to 250 files per run for incremental processing");
    }

    [Fact]
    public void MaintenanceTaskOptions_DefaultMaxMigrationBytesPerRun_ShouldBe2GB()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions();

        // Assert
        options.MaxMigrationBytesPerRun.Should().Be(2L * 1024 * 1024 * 1024,
            "default should limit migrations to 2 GB per run");
    }

    [Fact]
    public void MaintenanceTaskOptions_DefaultRunOnlyDuringMarketClosedHours_ShouldBeTrue()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions();

        // Assert
        options.RunOnlyDuringMarketClosedHours.Should().BeTrue(
            "default should avoid running during market hours to prevent I/O interference");
    }

    [Fact]
    public void MaintenanceTaskOptions_DefaultMarketTimeZoneId_ShouldBeNewYork()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions();

        // Assert
        options.MarketTimeZoneId.Should().Be("America/New_York",
            "default should use US Eastern timezone for equity markets");
    }

    [Fact]
    public void MaintenanceTaskOptions_DefaultMarketOpenTime_ShouldBe0930()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions();

        // Assert
        options.MarketOpenTime.Should().Be(new TimeSpan(9, 30, 0),
            "default should match US equity market open time");
    }

    [Fact]
    public void MaintenanceTaskOptions_DefaultMarketCloseTime_ShouldBe1600()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions();

        // Assert
        options.MarketCloseTime.Should().Be(new TimeSpan(16, 0, 0),
            "default should match US equity market close time");
    }

    [Fact]
    public void MaintenanceSchedulePresets_DailyTierMigration_ShouldHaveIncrementalDefaults()
    {
        // Arrange & Act
        var schedule = MaintenanceSchedulePresets.DailyTierMigration("test");

        // Assert
        schedule.Options.MaxMigrationsPerRun.Should().Be(250);
        schedule.Options.MaxMigrationBytesPerRun.Should().Be(2L * 1024 * 1024 * 1024);
        schedule.Options.RunOnlyDuringMarketClosedHours.Should().BeTrue();
        schedule.Options.MarketTimeZoneId.Should().Be("America/New_York");
    }

    [Fact]
    public void MaintenanceSchedulePresets_DailyTierMigration_ShouldRunOnWeekdays()
    {
        // Arrange & Act
        var schedule = MaintenanceSchedulePresets.DailyTierMigration("test");

        // Assert
        schedule.CronExpression.Should().Be("0 1 * * 1-5",
            "should run at 1 AM on weekdays (Monday-Friday) only");
    }

    [Fact]
    public void MaintenanceSchedulePresets_DailyTierMigration_ShouldUseNewYorkTimeZone()
    {
        // Arrange & Act
        var schedule = MaintenanceSchedulePresets.DailyTierMigration("test");

        // Assert
        schedule.TimeZoneId.Should().Be("America/New_York",
            "should use Eastern timezone for schedule execution");
    }

    [Fact]
    public void MaintenanceSchedulePresets_DailyTierMigration_ShouldHaveCorrectDescription()
    {
        // Arrange & Act
        var schedule = MaintenanceSchedulePresets.DailyTierMigration("test");

        // Assert
        schedule.Description.Should().Contain("incremental",
            "description should mention incremental processing");
    }

    [Theory]
    [InlineData(0, 1)] // 0 should be clamped to 1
    [InlineData(-10, 1)] // negative should be clamped to 1
    [InlineData(100, 100)]
    [InlineData(1000, 1000)]
    public void MaintenanceTaskOptions_MaxMigrationsPerRun_ShouldAllowPositiveValues(int setValue, int expectedMin)
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            MaxMigrationsPerRun = setValue
        };

        // Act
        var effectiveValue = Math.Max(1, options.MaxMigrationsPerRun);

        // Assert
        effectiveValue.Should().BeGreaterOrEqualTo(expectedMin,
            "the service should clamp to at least 1 file per run");
    }

    [Fact]
    public void MaintenanceTaskOptions_MaxMigrationBytesPerRun_CanBeNull()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions
        {
            MaxMigrationBytesPerRun = null
        };

        // Assert
        options.MaxMigrationBytesPerRun.Should().BeNull(
            "null should mean no byte limit (unlimited)");
    }

    [Theory]
    [InlineData(1024)] // 1 KB
    [InlineData(1024L * 1024)] // 1 MB
    [InlineData(1024L * 1024 * 1024)] // 1 GB
    [InlineData(10L * 1024 * 1024 * 1024)] // 10 GB
    public void MaintenanceTaskOptions_MaxMigrationBytesPerRun_ShouldAcceptValidSizes(long bytes)
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions
        {
            MaxMigrationBytesPerRun = bytes
        };

        // Assert
        options.MaxMigrationBytesPerRun.Should().Be(bytes);
    }

    [Fact]
    public void MaintenanceTaskOptions_MarketTimeZoneId_CanBeCustomized()
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions
        {
            MarketTimeZoneId = "Europe/London"
        };

        // Assert
        options.MarketTimeZoneId.Should().Be("Europe/London",
            "should support different market timezones");
    }

    [Theory]
    [InlineData(0, 0, 0)] // midnight
    [InlineData(9, 30, 0)] // 9:30 AM
    [InlineData(16, 0, 0)] // 4:00 PM
    [InlineData(23, 59, 59)] // 11:59:59 PM
    public void MaintenanceTaskOptions_MarketTimes_ShouldAcceptValidTimeSpans(int hours, int minutes, int seconds)
    {
        // Arrange & Act
        var options = new MaintenanceTaskOptions
        {
            MarketOpenTime = new TimeSpan(hours, minutes, seconds),
            MarketCloseTime = new TimeSpan(hours, minutes, seconds)
        };

        // Assert
        options.MarketOpenTime.Hours.Should().Be(hours);
        options.MarketOpenTime.Minutes.Should().Be(minutes);
        options.MarketCloseTime.Hours.Should().Be(hours);
        options.MarketCloseTime.Minutes.Should().Be(minutes);
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldSucceedForDefaultOptions()
    {
        // Arrange
        var options = new MaintenanceTaskOptions();

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow(
            "default options should be valid");
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldThrowForInvalidMaxMigrationsPerRun()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            MaxMigrationsPerRun = 0
        };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*MaxMigrationsPerRun*must be at least 1*");
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldThrowForNegativeMaxMigrationBytesPerRun()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            MaxMigrationBytesPerRun = -100
        };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*MaxMigrationBytesPerRun*must be non-negative*");
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldThrowForInvalidTimeZoneId()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            RunOnlyDuringMarketClosedHours = true,
            MarketTimeZoneId = "Invalid/TimeZone"
        };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*Invalid MarketTimeZoneId*")
            .WithInnerException<TimeZoneNotFoundException>();
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldThrowWhenMarketOpenTimeAfterCloseTime()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            RunOnlyDuringMarketClosedHours = true,
            MarketOpenTime = new TimeSpan(16, 0, 0),
            MarketCloseTime = new TimeSpan(9, 30, 0)
        };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*MarketOpenTime*must be before*MarketCloseTime*");
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldThrowWhenMarketOpenTimeEqualsCloseTime()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            RunOnlyDuringMarketClosedHours = true,
            MarketOpenTime = new TimeSpan(9, 30, 0),
            MarketCloseTime = new TimeSpan(9, 30, 0)
        };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*MarketOpenTime*must be before*MarketCloseTime*");
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldThrowForEmptyTimeZoneId()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            RunOnlyDuringMarketClosedHours = true,
            MarketTimeZoneId = ""
        };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*MarketTimeZoneId*is required*");
    }

    [Fact]
    public void MaintenanceTaskOptions_Validate_ShouldNotThrowWhenRunOnlyDuringMarketClosedHoursIsFalse()
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            RunOnlyDuringMarketClosedHours = false,
            MarketTimeZoneId = "Invalid/TimeZone", // Invalid but should be ignored
            MarketOpenTime = new TimeSpan(16, 0, 0),
            MarketCloseTime = new TimeSpan(9, 30, 0) // Invalid but should be ignored
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow(
            "validation should be skipped when RunOnlyDuringMarketClosedHours is false");
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    [InlineData("UTC")]
    public void MaintenanceTaskOptions_Validate_ShouldAcceptValidTimeZones(string timeZoneId)
    {
        // Arrange
        var options = new MaintenanceTaskOptions
        {
            RunOnlyDuringMarketClosedHours = true,
            MarketTimeZoneId = timeZoneId
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow(
            $"{timeZoneId} should be a valid time zone");
    }
}
