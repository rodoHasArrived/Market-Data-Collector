using FluentAssertions;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Tests.Services;

/// <summary>
/// Tests for <see cref="IntegrityEventsService"/> — integrity event tracking,
/// filtering, summary aggregation, and event notification.
/// </summary>
public sealed class IntegrityEventsServiceTests
{
    // ── Singleton ────────────────────────────────────────────────────

    [Fact]
    public void Instance_ReturnsNonNullSingleton()
    {
        // Act
        var instance = IntegrityEventsService.Instance;

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Instance_ReturnsSameInstanceOnMultipleCalls()
    {
        // Act
        var instance1 = IntegrityEventsService.Instance;
        var instance2 = IntegrityEventsService.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void Instance_ThreadSafety_MultipleThreadsGetSameInstance()
    {
        // Arrange
        IntegrityEventsService? instance1 = null;
        IntegrityEventsService? instance2 = null;
        var task1 = Task.Run(() => instance1 = IntegrityEventsService.Instance);
        var task2 = Task.Run(() => instance2 = IntegrityEventsService.Instance);

        // Act
        Task.WaitAll(task1, task2);

        // Assert
        instance1.Should().NotBeNull();
        instance2.Should().NotBeNull();
        instance1.Should().BeSameAs(instance2);
    }

    // ── IntegrityEvent model ────────────────────────────────────────

    [Fact]
    public void IntegrityEvent_DefaultValues_ShouldBeValid()
    {
        // Act
        var evt = new IntegrityEvent();

        // Assert
        evt.Id.Should().NotBeNull();
        evt.Symbol.Should().NotBeNull();
        evt.Message.Should().NotBeNull();
        evt.Details.Should().NotBeNull();
        evt.Source.Should().NotBeNull();
    }

    [Fact]
    public void IntegrityEvent_CanStoreAllProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var evt = new IntegrityEvent
        {
            Id = "evt-001",
            Timestamp = timestamp,
            Symbol = "SPY",
            EventType = IntegrityEventType.Gap,
            Severity = IntegrityEventSeverity.Warning,
            Message = "Sequence gap detected",
            Details = "Missing ticks between 10:30 and 10:31",
            Source = "Alpaca"
        };

        // Assert
        evt.Id.Should().Be("evt-001");
        evt.Timestamp.Should().Be(timestamp);
        evt.Symbol.Should().Be("SPY");
        evt.EventType.Should().Be(IntegrityEventType.Gap);
        evt.Severity.Should().Be(IntegrityEventSeverity.Warning);
        evt.Message.Should().Be("Sequence gap detected");
        evt.Details.Should().Be("Missing ticks between 10:30 and 10:31");
        evt.Source.Should().Be("Alpaca");
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("AAPL")]
    [InlineData("MSFT")]
    [InlineData("TSLA")]
    public void IntegrityEvent_AcceptsDifferentSymbols(string symbol)
    {
        // Act
        var evt = new IntegrityEvent { Symbol = symbol };

        // Assert
        evt.Symbol.Should().Be(symbol);
    }

    // ── IntegrityEventType enum ─────────────────────────────────────

    [Theory]
    [InlineData(IntegrityEventType.Gap)]
    [InlineData(IntegrityEventType.OutOfOrder)]
    [InlineData(IntegrityEventType.Duplicate)]
    [InlineData(IntegrityEventType.Invalid)]
    public void IntegrityEventType_AllValues_ShouldBeDefined(IntegrityEventType eventType)
    {
        // Assert
        Enum.IsDefined(typeof(IntegrityEventType), eventType).Should().BeTrue();
    }

    [Fact]
    public void IntegrityEventType_ShouldHaveFourValues()
    {
        // Act
        var values = Enum.GetValues<IntegrityEventType>();

        // Assert
        values.Should().HaveCountGreaterThanOrEqualTo(4);
        values.Should().Contain(IntegrityEventType.Gap);
        values.Should().Contain(IntegrityEventType.OutOfOrder);
        values.Should().Contain(IntegrityEventType.Duplicate);
        values.Should().Contain(IntegrityEventType.Invalid);
    }

    // ── IntegrityEventSeverity enum ─────────────────────────────────

    [Theory]
    [InlineData(IntegrityEventSeverity.Info)]
    [InlineData(IntegrityEventSeverity.Warning)]
    [InlineData(IntegrityEventSeverity.Error)]
    [InlineData(IntegrityEventSeverity.Critical)]
    public void IntegrityEventSeverity_AllValues_ShouldBeDefined(IntegrityEventSeverity severity)
    {
        // Assert
        Enum.IsDefined(typeof(IntegrityEventSeverity), severity).Should().BeTrue();
    }

    [Fact]
    public void IntegrityEventSeverity_ShouldHaveFourValues()
    {
        // Act
        var values = Enum.GetValues<IntegrityEventSeverity>();

        // Assert
        values.Should().HaveCountGreaterThanOrEqualTo(4);
        values.Should().Contain(IntegrityEventSeverity.Info);
        values.Should().Contain(IntegrityEventSeverity.Warning);
        values.Should().Contain(IntegrityEventSeverity.Error);
        values.Should().Contain(IntegrityEventSeverity.Critical);
    }

    // ── IntegrityEventFilter model ──────────────────────────────────

    [Fact]
    public void IntegrityEventFilter_DefaultValues_ShouldBeNull()
    {
        // Act
        var filter = new IntegrityEventFilter();

        // Assert
        filter.Symbol.Should().BeNull();
        filter.FromDate.Should().BeNull();
        filter.ToDate.Should().BeNull();
    }

    [Fact]
    public void IntegrityEventFilter_CanSetAllProperties()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        // Act
        var filter = new IntegrityEventFilter
        {
            Symbol = "AAPL",
            EventTypes = new List<IntegrityEventType> { IntegrityEventType.Gap, IntegrityEventType.Duplicate },
            FromDate = from,
            ToDate = to,
            MaxResults = 50
        };

        // Assert
        filter.Symbol.Should().Be("AAPL");
        filter.EventTypes.Should().HaveCount(2);
        filter.EventTypes.Should().Contain(IntegrityEventType.Gap);
        filter.EventTypes.Should().Contain(IntegrityEventType.Duplicate);
        filter.FromDate.Should().Be(from);
        filter.ToDate.Should().Be(to);
        filter.MaxResults.Should().Be(50);
    }

    [Fact]
    public void IntegrityEventFilter_EventTypes_CanBeEmpty()
    {
        // Act
        var filter = new IntegrityEventFilter
        {
            EventTypes = new List<IntegrityEventType>()
        };

        // Assert
        filter.EventTypes.Should().NotBeNull();
        filter.EventTypes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void IntegrityEventFilter_MaxResults_AcceptsVariousLimits(int maxResults)
    {
        // Act
        var filter = new IntegrityEventFilter { MaxResults = maxResults };

        // Assert
        filter.MaxResults.Should().Be(maxResults);
    }

    // ── IntegrityEventSummary model ─────────────────────────────────

    [Fact]
    public void IntegrityEventSummary_DefaultValues_ShouldBeZero()
    {
        // Act
        var summary = new IntegrityEventSummary();

        // Assert
        summary.TotalEvents.Should().Be(0);
        summary.GapCount.Should().Be(0);
        summary.OutOfOrderCount.Should().Be(0);
        summary.DuplicateCount.Should().Be(0);
        summary.InvalidCount.Should().Be(0);
    }

    [Fact]
    public void IntegrityEventSummary_CanSetAllProperties()
    {
        // Arrange
        var lastEvent = DateTimeOffset.UtcNow;

        // Act
        var summary = new IntegrityEventSummary
        {
            TotalEvents = 42,
            GapCount = 10,
            OutOfOrderCount = 15,
            DuplicateCount = 12,
            InvalidCount = 5,
            LastEventAt = lastEvent,
            SeverityCounts = new Dictionary<string, int>
            {
                ["Info"] = 20,
                ["Warning"] = 15,
                ["Error"] = 5,
                ["Critical"] = 2
            }
        };

        // Assert
        summary.TotalEvents.Should().Be(42);
        summary.GapCount.Should().Be(10);
        summary.OutOfOrderCount.Should().Be(15);
        summary.DuplicateCount.Should().Be(12);
        summary.InvalidCount.Should().Be(5);
        summary.LastEventAt.Should().Be(lastEvent);
        summary.SeverityCounts.Should().HaveCount(4);
        summary.SeverityCounts["Info"].Should().Be(20);
        summary.SeverityCounts["Critical"].Should().Be(2);
    }

    [Fact]
    public void IntegrityEventSummary_SeverityCounts_CanBeEmpty()
    {
        // Act
        var summary = new IntegrityEventSummary
        {
            SeverityCounts = new Dictionary<string, int>()
        };

        // Assert
        summary.SeverityCounts.Should().NotBeNull();
        summary.SeverityCounts.Should().BeEmpty();
    }

    [Fact]
    public void IntegrityEventSummary_LastEventAt_CanBeNull()
    {
        // Act
        var summary = new IntegrityEventSummary();

        // Assert
        summary.LastEventAt.Should().BeNull();
    }

    // ── GetEventsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetEventsAsync_WithNullFilter_ReturnsEvents()
    {
        // Arrange
        var service = IntegrityEventsService.Instance;

        // Act
        var events = await service.GetEventsAsync(null, CancellationToken.None);

        // Assert
        events.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEventsAsync_WithEmptyFilter_ReturnsEvents()
    {
        // Arrange
        var service = IntegrityEventsService.Instance;
        var filter = new IntegrityEventFilter();

        // Act
        var events = await service.GetEventsAsync(filter, CancellationToken.None);

        // Assert
        events.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEventsAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = IntegrityEventsService.Instance;
        using var cts = new CancellationTokenSource();

        // Act
        var events = await service.GetEventsAsync(null, cts.Token);

        // Assert
        events.Should().NotBeNull();
    }

    // ── GetSummaryAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_ReturnsNonNullSummary()
    {
        // Arrange
        var service = IntegrityEventsService.Instance;

        // Act
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalEvents.Should().BeGreaterThanOrEqualTo(0);
        summary.GapCount.Should().BeGreaterThanOrEqualTo(0);
        summary.OutOfOrderCount.Should().BeGreaterThanOrEqualTo(0);
        summary.DuplicateCount.Should().BeGreaterThanOrEqualTo(0);
        summary.InvalidCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSummaryAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = IntegrityEventsService.Instance;
        using var cts = new CancellationTokenSource();

        // Act
        var summary = await service.GetSummaryAsync(cts.Token);

        // Assert
        summary.Should().NotBeNull();
    }
}
