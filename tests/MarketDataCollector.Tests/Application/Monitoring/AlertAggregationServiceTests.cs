using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using Xunit;

namespace MarketDataCollector.Tests.Application.Monitoring;

/// <summary>
/// Tests for AlertAggregationService (improvement 9.9).
/// Verifies alert grouping, deduplication, and batching behavior.
/// </summary>
public sealed class AlertAggregationServiceTests : IAsyncDisposable
{
    private readonly AlertAggregationService _sut;
    private readonly List<AlertBatch> _receivedBatches = new();

    public AlertAggregationServiceTests()
    {
        _sut = new AlertAggregationService(new AlertAggregationConfig
        {
            AggregationWindowSeconds = 1, // Short window for fast tests
            MaxBatchSize = 5,
            DeduplicationCooldownSeconds = 10
        });
        _sut.OnAlertBatchReady += batch => _receivedBatches.Add(batch);
    }

    public async ValueTask DisposeAsync() => await _sut.DisposeAsync();

    [Fact]
    public void Submit_SingleAlert_PendingUntilFlush()
    {
        _sut.Submit("connection_lost", AlertSeverity.Warning,
            "Connection lost", "Lost connection to Alpaca", "Alpaca");

        _sut.PendingAlertCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task Submit_MaxBatchSize_FlushesImmediately()
    {
        for (var i = 0; i < 5; i++)
        {
            _sut.Submit("high_latency", AlertSeverity.Warning,
                $"High latency {i}", $"Latency spike #{i}", $"Provider{i}");
        }

        // Allow time for flush
        await Task.Delay(100);

        _receivedBatches.Should().NotBeEmpty("max batch size reached, should flush immediately");
        _receivedBatches[0].AlertCount.Should().Be(5);
        _receivedBatches[0].Category.Should().Be("high_latency");
    }

    [Fact]
    public void Submit_DuplicateAlert_SuppressedByCooldown()
    {
        // Submit first alert
        _sut.Submit("sla_violation", AlertSeverity.Error,
            "SLA breached", "AAPL data stale", "AAPL", "sla:AAPL");

        var pending1 = _sut.PendingAlertCount;

        // Submit duplicate within cooldown
        _sut.Submit("sla_violation", AlertSeverity.Error,
            "SLA breached", "AAPL data stale", "AAPL", "sla:AAPL");

        // Should not increase pending count (suppressed)
        _sut.PendingAlertCount.Should().Be(pending1,
            "duplicate alert within cooldown should be suppressed");
    }

    [Fact]
    public void Submit_DifferentCategories_GroupedSeparately()
    {
        _sut.Submit("connection_lost", AlertSeverity.Warning,
            "Connection lost", "Lost connection", "Alpaca");
        _sut.Submit("high_latency", AlertSeverity.Warning,
            "High latency", "Latency spike", "Polygon");

        // Both should be pending as separate groups
        _sut.PendingAlertCount.Should().Be(2);
    }

    [Fact]
    public async Task Submit_AlertBatch_ContainsCorrectMetadata()
    {
        for (var i = 0; i < 5; i++)
        {
            _sut.Submit("data_gap", AlertSeverity.Error,
                $"Gap detected for Symbol{i}", $"Missing data for Symbol{i}",
                $"Symbol{i}", $"gap:Symbol{i}");
        }

        await Task.Delay(100);

        _receivedBatches.Should().NotBeEmpty();
        var batch = _receivedBatches[0];
        batch.Severity.Should().Be(AlertSeverity.Error);
        batch.Sources.Should().HaveCount(5);
        batch.WindowStart.Should().BeBefore(batch.WindowEnd);
    }
}
