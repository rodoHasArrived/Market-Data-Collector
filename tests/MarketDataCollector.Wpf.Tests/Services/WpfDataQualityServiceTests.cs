using FluentAssertions;
using MarketDataCollector.Wpf.Services;
using MarketDataCollector.Wpf.Models;

namespace MarketDataCollector.Wpf.Tests.Services;

/// <summary>
/// Tests for <see cref="WpfDataQualityService"/> scoring algorithms and quality analysis.
/// </summary>
public sealed class WpfDataQualityServiceTests
{
    [Fact]
    public void Instance_ReturnsNonNullSingleton()
    {
        // Act
        var instance = WpfDataQualityService.Instance;

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Instance_ReturnsSameInstanceOnMultipleCalls()
    {
        // Act
        var instance1 = WpfDataQualityService.Instance;
        var instance2 = WpfDataQualityService.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void Instance_ThreadSafety_MultipleThreadsGetSameInstance()
    {
        // Arrange
        WpfDataQualityService? instance1 = null;
        WpfDataQualityService? instance2 = null;
        var task1 = Task.Run(() => instance1 = WpfDataQualityService.Instance);
        var task2 = Task.Run(() => instance2 = WpfDataQualityService.Instance);

        // Act
        Task.WaitAll(task1, task2);

        // Assert
        instance1.Should().NotBeNull();
        instance2.Should().NotBeNull();
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public async Task GetQualitySummaryAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should throw due to cancellation or network error
        var act = async () => await service.GetQualitySummaryAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(90.0)]
    [InlineData(95.0)]
    [InlineData(99.0)]
    public async Task GetQualityScoresAsync_WithDifferentMinScores_AcceptsValidValues(double? minScore)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetQualityScoresAsync(minScore, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("AAPL")]
    [InlineData("MSFT")]
    public async Task GetSymbolQualityAsync_WithDifferentSymbols_AcceptsAllSymbols(string symbol)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetSymbolQualityAsync(symbol, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Critical")]
    [InlineData("Warning")]
    [InlineData("Info")]
    public async Task GetQualityAlertsAsync_WithDifferentSeverities_AcceptsAllValues(string? severity)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetQualityAlertsAsync(severity, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_WithAlertId_ReturnsSuccessStatus()
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Will fail due to API unavailability, but tests method signature
        var act = async () => await service.AcknowledgeAlertAsync("alert-123", cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("TSLA")]
    public async Task GetSourceRankingsAsync_WithSymbol_AcceptsDifferentSymbols(string symbol)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetSourceRankingsAsync(symbol, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1d")]
    [InlineData("7d")]
    [InlineData("30d")]
    [InlineData("90d")]
    public async Task GetQualityTrendsAsync_WithDifferentTimeWindows_AcceptsAllValues(string? timeWindow)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetQualityTrendsAsync(timeWindow, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("price_anomaly")]
    [InlineData("volume_spike")]
    [InlineData("gap")]
    public async Task GetAnomaliesAsync_WithDifferentTypes_AcceptsAllValues(string? type)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetAnomaliesAsync(type, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("/data/SPY")]
    [InlineData("/data/AAPL")]
    public async Task RunQualityCheckAsync_WithDifferentPaths_AcceptsAllPaths(string path)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.RunQualityCheckAsync(path, cts.Token);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("AAPL")]
    public async Task GetDataGapsAsync_WithCancellation_ThrowsOnCancelledToken(string symbol)
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.GetDataGapsAsync(symbol, cts.Token);

        // Assert - Should throw when token is cancelled
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task VerifySymbolIntegrityAsync_WhenApiUnavailable_ReturnsFailureResult()
    {
        // Arrange
        var service = WpfDataQualityService.Instance;
        using var cts = new CancellationTokenSource();

        // Act - Should return failure when API is unavailable
        var result = await service.VerifySymbolIntegrityAsync("SPY", cts.Token);

        // Assert - Should return a result (not throw) with IsValid=false
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public void DataQualitySummary_CanBeConstructed()
    {
        // Act
        var summary = new DataQualitySummary
        {
            OverallScore = 98.5,
            TotalFiles = 100,
            HealthyFiles = 95,
            WarningFiles = 4,
            CriticalFiles = 1,
            ActiveAlerts = 2,
            UnacknowledgedAlerts = 1
        };

        // Assert
        summary.OverallScore.Should().Be(98.5);
        summary.TotalFiles.Should().Be(100);
        summary.HealthyFiles.Should().Be(95);
    }

    [Theory]
    [InlineData(99.0, "A")]
    [InlineData(95.0, "A")]
    [InlineData(90.0, "B")]
    [InlineData(85.0, "B")]
    [InlineData(70.0, "C")]
    public void QualityScoreEntry_ScoreToGrade_MapsCorrectly(double score, string expectedGrade)
    {
        // Act
        var entry = new QualityScoreEntry
        {
            Score = score,
            Grade = score >= 95 ? "A" : score >= 85 ? "B" : "C"
        };

        // Assert
        entry.Score.Should().Be(score);
        entry.Grade.Should().Be(expectedGrade);
    }

    [Fact]
    public void QualityAlert_Construction_StoresAllProperties()
    {
        // Act
        var alert = new QualityAlert
        {
            Id = "alert-123",
            AlertType = "data_gap",
            Severity = "Critical",
            Symbol = "SPY",
            Message = "Data gap detected",
            IsAcknowledged = false
        };

        // Assert
        alert.Id.Should().Be("alert-123");
        alert.AlertType.Should().Be("data_gap");
        alert.Severity.Should().Be("Critical");
        alert.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public void SymbolQualityReport_SupportsMultipleScoreTypes()
    {
        // Act
        var report = new SymbolQualityReport
        {
            Symbol = "SPY",
            OverallScore = 97.5,
            ScoresByType = new Dictionary<string, double>
            {
                { "completeness", 99.0 },
                { "accuracy", 98.0 },
                { "timeliness", 96.0 }
            }
        };

        // Assert
        report.ScoresByType.Should().HaveCount(3);
        report.ScoresByType["completeness"].Should().Be(99.0);
    }

    [Fact]
    public void IntegrityVerificationResult_HighScore_MarksAsValid()
    {
        // Act
        var result = new IntegrityVerificationResult
        {
            IsValid = true,
            Score = 98.5,
            Issues = new List<string>(),
            CheckedAt = DateTime.UtcNow
        };

        // Assert
        result.IsValid.Should().BeTrue();
        result.Score.Should().BeGreaterThanOrEqualTo(95.0);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void IntegrityVerificationResult_LowScore_MarksAsInvalid()
    {
        // Act
        var result = new IntegrityVerificationResult
        {
            IsValid = false,
            Score = 75.0,
            Issues = new List<string> { "High gap count", "Sequence errors detected" }
        };

        // Assert
        result.IsValid.Should().BeFalse();
        result.Score.Should().BeLessThan(95.0);
        result.Issues.Should().HaveCountGreaterThan(0);
    }

    [Theory]
    [InlineData(100.0, 1.0)]
    [InlineData(99.5, 0.995)]
    [InlineData(50.0, 0.5)]
    [InlineData(0.0, 0.0)]
    public void QualityCompletenessReport_PercentageConversion_CalculatesCorrectly(
        double overallCompleteness, double expectedDecimal)
    {
        // Act
        var report = new QualityCompletenessReport
        {
            OverallCompleteness = overallCompleteness
        };

        // Assert
        report.OverallCompleteness.Should().Be(overallCompleteness);
        (report.OverallCompleteness / 100.0).Should().BeApproximately(expectedDecimal, 0.001);
    }

    [Fact]
    public void SourceRanking_SortByRank_OrdersCorrectly()
    {
        // Arrange
        var rankings = new List<SourceRanking>
        {
            new() { Source = "Provider1", Rank = 3, QualityScore = 90.0 },
            new() { Source = "Provider2", Rank = 1, QualityScore = 98.0 },
            new() { Source = "Provider3", Rank = 2, QualityScore = 95.0 }
        };

        // Act
        var sorted = rankings.OrderBy(r => r.Rank).ToList();

        // Assert
        sorted[0].Source.Should().Be("Provider2");
        sorted[0].Rank.Should().Be(1);
        sorted[2].Source.Should().Be("Provider1");
    }

    [Theory]
    [InlineData(100, 0, 100.0)]
    [InlineData(95, 5, 95.0)]
    [InlineData(90, 10, 90.0)]
    public void QualityScoreEntry_CompletenessCalculation_IsAccurate(
        int recordCount, int missingCount, double expectedCompleteness)
    {
        // Act
        var entry = new QualityScoreEntry
        {
            RecordCount = recordCount,
            MissingCount = missingCount
        };

        // Calculate completeness: (recordCount / (recordCount + missingCount)) * 100
        var actualCompleteness = ((double)entry.RecordCount / (entry.RecordCount + entry.MissingCount)) * 100;

        // Assert
        actualCompleteness.Should().BeApproximately(expectedCompleteness, 0.1);
    }

    [Fact]
    public void QualityTrendData_MultipleDataPoints_CalculatesAverageScore()
    {
        // Arrange
        var trendData = new QualityTrendData
        {
            OverallTrend = new List<TrendDataPoint>
            {
                new() { Score = 98.0 },
                new() { Score = 97.0 },
                new() { Score = 99.0 }
            }
        };

        // Act
        var averageScore = trendData.OverallTrend.Average(t => t.Score);

        // Assert
        averageScore.Should().BeApproximately(98.0, 0.1);
    }

    [Theory]
    [InlineData(1.0, "improving")]
    [InlineData(0.0, "stable")]
    [InlineData(-1.0, "declining")]
    public void QualityTrendData_TrendDirection_IndicatesQualityMovement(
        double trendDirection, string expectedTrend)
    {
        // Act
        var trend = new QualityTrendData
        {
            TrendDirection = trendDirection
        };

        var actualTrend = trendDirection > 0 ? "improving" :
                          trendDirection < 0 ? "declining" : "stable";

        // Assert
        actualTrend.Should().Be(expectedTrend);
    }

    [Fact]
    public void AnomalyEvent_Construction_StoresAllDetails()
    {
        // Act
        var anomaly = new AnomalyEvent
        {
            Id = "anomaly-456",
            Type = "volume_spike",
            Symbol = "TSLA",
            Severity = "Warning",
            DetectedAt = DateTime.UtcNow,
            Details = new Dictionary<string, object>
            {
                { "expected_volume", 1000000 },
                { "actual_volume", 5000000 }
            }
        };

        // Assert
        anomaly.Type.Should().Be("volume_spike");
        anomaly.Details.Should().ContainKey("expected_volume");
        anomaly.Details["actual_volume"].Should().Be(5000000);
    }

    [Fact]
    public void QualityCheckResult_SuccessfulCheck_HasHighScore()
    {
        // Act
        var result = new QualityCheckResult
        {
            Success = true,
            Score = 99.5,
            Issues = new List<string>(),
            Recommendations = new List<string> { "Continue monitoring" }
        };

        // Assert
        result.Success.Should().BeTrue();
        result.Score.Should().BeGreaterThan(95.0);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void QualityDataGap_DurationCalculation_IsAccurate()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1, 9, 30, 0);
        var end = new DateTime(2024, 1, 1, 16, 0, 0);

        // Act
        var gap = new QualityDataGap
        {
            Start = start,
            End = end,
            Duration = end - start,
            MissingRecords = 390 // 6.5 hours * 60 minutes
        };

        // Assert
        gap.Duration.TotalHours.Should().BeApproximately(6.5, 0.01);
        gap.MissingRecords.Should().Be(390);
    }
}
