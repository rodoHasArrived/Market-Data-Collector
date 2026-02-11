using FluentAssertions;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Tests.Services;

/// <summary>
/// Tests for <see cref="BackfillService"/> business logic.
/// </summary>
public sealed class BackfillServiceTests
{
    [Fact]
    public void Instance_ReturnsNonNullSingleton()
    {
        // Act
        var instance = BackfillService.Instance;

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Instance_ReturnsSameInstanceOnMultipleCalls()
    {
        // Act
        var instance1 = BackfillService.Instance;
        var instance2 = BackfillService.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void CurrentProgress_InitiallyNull()
    {
        // Arrange
        var service = BackfillService.Instance;

        // Act
        var progress = service.CurrentProgress;

        // Assert
        progress.Should().BeNull("no backfill has been started");
    }

    [Fact]
    public void IsRunning_WhenNoBackfill_ReturnsFalse()
    {
        // Arrange
        var service = BackfillService.Instance;

        // Act
        var isRunning = service.IsRunning;

        // Assert
        isRunning.Should().BeFalse("no backfill is running");
    }

    [Fact]
    public void IsPaused_WhenNoBackfill_ReturnsFalse()
    {
        // Arrange
        var service = BackfillService.Instance;

        // Act
        var isPaused = service.IsPaused;

        // Assert
        isPaused.Should().BeFalse("no backfill is paused");
    }

    [Fact]
    public void BarsPerSecond_WhenNoBackfill_ReturnsZero()
    {
        // Arrange
        var service = BackfillService.Instance;

        // Act
        var bps = service.BarsPerSecond;

        // Assert
        bps.Should().Be(0, "no bars have been downloaded");
    }

    [Fact]
    public void EstimatedTimeRemaining_WhenNoBackfill_ReturnsNull()
    {
        // Arrange
        var service = BackfillService.Instance;

        // Act
        var estimate = service.EstimatedTimeRemaining;

        // Assert
        estimate.Should().BeNull("no backfill is running");
    }

    [Fact]
    public void Constructor_WithUseInstanceTrue_ThrowsException()
    {
        // Act
        var act = () => new BackfillService(useInstance: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*singleton*");
    }

    [Fact]
    public void Constructor_WithUseInstanceFalse_DoesNotThrow()
    {
        // Act
        var act = () => new BackfillService(useInstance: false);

        // Assert
        act.Should().NotThrow();
    }
}
