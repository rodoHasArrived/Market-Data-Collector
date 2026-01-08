using FluentAssertions;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using NSubstitute;
using Xunit;

namespace MarketDataCollector.Tests.DataSources;

public class FallbackOrchestratorTests
{
    [Fact]
    public async Task GetHistoricalBarsWithFallbackAsync_ReturnsDataFromFirstHealthySource()
    {
        // Arrange
        var bars = new List<HistoricalBar>
        {
            CreateBar("SPY", new DateOnly(2024, 1, 1), 100, 105, 99, 104, 1000000)
        };

        var source = CreateMockHistoricalSource("yahoo", 10, bars);
        var manager = CreateMockManager(source);
        var orchestrator = new FallbackDataSourceOrchestrator(manager);

        // Act
        var result = await orchestrator.GetHistoricalBarsWithFallbackAsync("SPY");

        // Assert
        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("SPY");
    }

    [Fact]
    public async Task GetHistoricalBarsWithFallbackAsync_FallsBackOnFailure()
    {
        // Arrange
        var failingSource = CreateMockHistoricalSource("stooq", 5, throwException: true);
        var successfulSource = CreateMockHistoricalSource("yahoo", 10,
            new List<HistoricalBar> { CreateBar("SPY", new DateOnly(2024, 1, 1), 100, 105, 99, 104, 1000000) });

        var manager = CreateMockManager(failingSource, successfulSource);
        var orchestrator = new FallbackDataSourceOrchestrator(manager);

        // Act
        var result = await orchestrator.GetHistoricalBarsWithFallbackAsync("SPY");

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetHistoricalBarsWithFallbackAsync_ThrowsWhenAllSourcesFail()
    {
        // Arrange
        var failingSource1 = CreateMockHistoricalSource("stooq", 5, throwException: true);
        var failingSource2 = CreateMockHistoricalSource("yahoo", 10, throwException: true);

        var manager = CreateMockManager(failingSource1, failingSource2);
        var orchestrator = new FallbackDataSourceOrchestrator(manager);

        // Act & Assert
        await Assert.ThrowsAsync<AggregateException>(
            () => orchestrator.GetHistoricalBarsWithFallbackAsync("SPY"));
    }

    [Fact]
    public async Task GetHistoricalBarsWithFallbackAsync_SkipsSourcesInCooldown()
    {
        // Arrange
        var source1 = CreateMockHistoricalSource("stooq", 5,
            new List<HistoricalBar> { CreateBar("SPY", new DateOnly(2024, 1, 1), 100, 105, 99, 104, 1000000) });

        var manager = CreateMockManager(source1);
        var options = new FallbackOptions(CooldownDurationValue: TimeSpan.FromMinutes(5));
        var orchestrator = new FallbackDataSourceOrchestrator(manager, options);

        // Simulate putting source in cooldown (by making it fail first)
        var failingSource = CreateMockHistoricalSource("stooq", 5, throwException: true);
        var successSource = CreateMockHistoricalSource("yahoo", 10,
            new List<HistoricalBar> { CreateBar("AAPL", new DateOnly(2024, 1, 1), 100, 105, 99, 104, 1000000) });

        var failManager = CreateMockManager(failingSource, successSource);
        var orchWithFailure = new FallbackDataSourceOrchestrator(failManager, options);

        // First request fails on stooq, falls back to yahoo
        var result1 = await orchWithFailure.GetHistoricalBarsWithFallbackAsync("AAPL");
        result1.Should().HaveCount(1);

        // Check status shows stooq in cooldown
        var status = orchWithFailure.GetStatus();
        status.SourcesInCooldown.Should().Contain(c => c.SourceId == "stooq");
    }

    [Fact]
    public void GetStatus_ReturnsCorrectInformation()
    {
        // Arrange
        var source = CreateMockHistoricalSource("yahoo", 10);
        var manager = CreateMockManager(source);
        var options = new FallbackOptions(
            Strategy: FallbackStrategy.Priority,
            MaxFailoverAttempts: 3,
            CooldownDurationValue: TimeSpan.FromSeconds(60)
        );
        var orchestrator = new FallbackDataSourceOrchestrator(manager, options);

        // Act
        var status = orchestrator.GetStatus();

        // Assert
        status.Should().NotBeNull();
        status.Options.Should().Be(options);
        status.SourcesInCooldown.Should().BeEmpty();
        status.FailureCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRealtimeSourceWithFailoverAsync_ReturnsConnectedSource()
    {
        // Arrange
        var realtimeSource = CreateMockRealtimeSource("alpaca", 5);
        var manager = Substitute.For<IDataSourceManager>();
        manager.RealtimeSources.Returns(new[] { realtimeSource });

        var orchestrator = new FallbackDataSourceOrchestrator(manager);

        // Act
        var result = await orchestrator.GetRealtimeSourceWithFailoverAsync("SPY");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("alpaca");
    }

    [Fact]
    public async Task GetRealtimeSourceWithFailoverAsync_ReturnsNull_WhenNoSourcesAvailable()
    {
        // Arrange
        var manager = Substitute.For<IDataSourceManager>();
        manager.RealtimeSources.Returns(Array.Empty<IRealtimeDataSource>());

        var orchestrator = new FallbackDataSourceOrchestrator(manager);

        // Act
        var result = await orchestrator.GetRealtimeSourceWithFailoverAsync("SPY");

        // Assert
        result.Should().BeNull();
    }

    #region Helper Methods

    private static HistoricalBar CreateBar(string symbol, DateOnly date, decimal open, decimal high, decimal low, decimal close, long volume)
    {
        return new HistoricalBar(symbol, date, open, high, low, close, volume, "test");
    }

    private static IHistoricalDataSource CreateMockHistoricalSource(
        string id,
        int priority,
        IReadOnlyList<HistoricalBar>? bars = null,
        bool throwException = false)
    {
        var source = Substitute.For<IHistoricalDataSource>();
        source.Id.Returns(id);
        source.Priority.Returns(priority);
        source.Health.Returns(DataSourceHealth.Healthy());
        source.RateLimitState.Returns(RateLimitState.Available);

        if (throwException)
        {
            source.GetDailyBarsAsync(Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
                .Returns<IReadOnlyList<HistoricalBar>>(x => throw new HttpRequestException("Test failure"));
        }
        else
        {
            source.GetDailyBarsAsync(Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
                .Returns(bars ?? Array.Empty<HistoricalBar>());
        }

        return source;
    }

    private static IRealtimeDataSource CreateMockRealtimeSource(string id, int priority)
    {
        var source = Substitute.For<IRealtimeDataSource>();
        source.Id.Returns(id);
        source.Priority.Returns(priority);
        source.Status.Returns(DataSourceStatus.Connected);
        source.Health.Returns(DataSourceHealth.Healthy());
        source.RateLimitState.Returns(RateLimitState.Available);
        source.HealthChanges.Returns(System.Reactive.Linq.Observable.Empty<DataSourceHealthChanged>());
        return source;
    }

    private static IDataSourceManager CreateMockManager(params IHistoricalDataSource[] sources)
    {
        var manager = Substitute.For<IDataSourceManager>();
        manager.HistoricalSources.Returns(sources.ToList());
        manager.RealtimeSources.Returns(Array.Empty<IRealtimeDataSource>());
        return manager;
    }

    #endregion
}
