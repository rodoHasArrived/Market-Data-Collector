using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using Xunit;

namespace MarketDataCollector.Tests.Application.Monitoring;

/// <summary>
/// Unit tests for ProviderDegradationScorer — the H4 (Graceful Provider Degradation Scoring)
/// component from the project roadmap. Tests multi-factor scoring algorithm, recommendation
/// classification, provider ranking, degradation detection, and failover decisions.
/// </summary>
public sealed class ProviderDegradationScorerTests : IDisposable
{
    private ProviderDegradationScorer? _scorer;

    public void Dispose()
    {
        _scorer?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultConfig_Succeeds()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.Config.Should().NotBeNull();
        _scorer.Config.LatencyWeight.Should().Be(0.25);
        _scorer.Config.StabilityWeight.Should().Be(0.30);
        _scorer.Config.CompletenessWeight.Should().Be(0.25);
        _scorer.Config.ConsistencyWeight.Should().Be(0.20);
    }

    [Fact]
    public void Constructor_WithCustomConfig_UsesCustomValues()
    {
        var config = new DegradationScoringConfig
        {
            LatencyWeight = 0.40,
            StabilityWeight = 0.20,
            CompletenessWeight = 0.20,
            ConsistencyWeight = 0.20,
            DegradationThreshold = 50
        };

        _scorer = new ProviderDegradationScorer(config);

        _scorer.Config.LatencyWeight.Should().Be(0.40);
        _scorer.Config.DegradationThreshold.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithInvalidWeights_ThrowsArgumentException()
    {
        var config = new DegradationScoringConfig
        {
            LatencyWeight = 0.50,
            StabilityWeight = 0.50,
            CompletenessWeight = 0.50,
            ConsistencyWeight = 0.50
        };

        var act = () => new ProviderDegradationScorer(config);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*weights must sum to 1.0*");
    }

    #endregion

    #region CalculateScore — Overall Tests

    [Fact]
    public void CalculateScore_PerfectProvider_ReturnsHighScore()
    {
        _scorer = new ProviderDegradationScorer();

        var input = new ProviderHealthInput
        {
            ProviderId = "alpaca",
            IsConnected = true,
            P95LatencyMs = 50,
            P99LatencyMs = 80,
            MeanLatencyMs = 30,
            ReconnectsInWindow = 0,
            UptimeFraction = 1.0,
            CompletenessPercent = 100,
            GapRatePercent = 0,
            DuplicateRatePercent = 0,
            OutOfOrderRatePercent = 0,
            EventsReceived = 10000,
            EventsDropped = 0
        };

        var score = _scorer.CalculateScore(input);

        score.ProviderId.Should().Be("alpaca");
        score.OverallScore.Should().BeGreaterOrEqualTo(90);
        score.Recommendation.Should().Be(ProviderHealthRecommendation.Healthy);
    }

    [Fact]
    public void CalculateScore_DisconnectedProvider_ReturnsZero()
    {
        _scorer = new ProviderDegradationScorer();

        var input = new ProviderHealthInput
        {
            ProviderId = "polygon",
            IsConnected = false
        };

        var score = _scorer.CalculateScore(input);

        score.OverallScore.Should().Be(0);
        score.LatencyScore.Should().Be(0);
        score.StabilityScore.Should().Be(0);
        score.Recommendation.Should().Be(ProviderHealthRecommendation.Unavailable);
    }

    [Fact]
    public void CalculateScore_DegradedProvider_ReturnsMediumScore()
    {
        _scorer = new ProviderDegradationScorer();

        var input = new ProviderHealthInput
        {
            ProviderId = "tiingo",
            IsConnected = true,
            P95LatencyMs = 350,
            P99LatencyMs = 600,
            MeanLatencyMs = 200,
            ReconnectsInWindow = 4,
            UptimeFraction = 0.85,
            CompletenessPercent = 93,
            GapRatePercent = 0.3,
            DuplicateRatePercent = 0.1,
            OutOfOrderRatePercent = 0.2,
            EventsReceived = 9000,
            EventsDropped = 500
        };

        var score = _scorer.CalculateScore(input);

        score.OverallScore.Should().BeInRange(30, 70);
        score.Recommendation.Should().BeOneOf(
            ProviderHealthRecommendation.Caution,
            ProviderHealthRecommendation.Degraded);
    }

    [Fact]
    public void CalculateScore_SeverelyDegraded_ReturnsFailoverRecommendation()
    {
        _scorer = new ProviderDegradationScorer();

        var input = new ProviderHealthInput
        {
            ProviderId = "bad-provider",
            IsConnected = true,
            P95LatencyMs = 2000,
            P99LatencyMs = 5000,
            MeanLatencyMs = 1500,
            ReconnectsInWindow = 10,
            UptimeFraction = 0.5,
            CompletenessPercent = 70,
            GapRatePercent = 5.0,
            DuplicateRatePercent = 2.0,
            OutOfOrderRatePercent = 3.0,
            EventsReceived = 5000,
            EventsDropped = 3000
        };

        var score = _scorer.CalculateScore(input);

        score.OverallScore.Should().BeLessThan(40);
        score.Recommendation.Should().Be(ProviderHealthRecommendation.FailoverRecommended);
    }

    [Fact]
    public void CalculateScore_NullInput_ThrowsArgumentNullException()
    {
        _scorer = new ProviderDegradationScorer();

        var act = () => _scorer.CalculateScore(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateScore_StoresScoreInternally()
    {
        _scorer = new ProviderDegradationScorer();

        var input = CreateHealthyInput("alpaca");
        _scorer.CalculateScore(input);

        var retrieved = _scorer.GetScore("alpaca");
        retrieved.Should().NotBeNull();
        retrieved!.ProviderId.Should().Be("alpaca");
    }

    #endregion

    #region Latency Scoring Tests

    [Theory]
    [InlineData(0, 100)]     // Zero latency → perfect
    [InlineData(50, 96.67)]  // Well within good threshold → near perfect
    [InlineData(150, 90)]    // At good threshold → 90
    [InlineData(300, 65)]    // At fair threshold → 65
    [InlineData(500, 30)]    // At poor threshold → 30
    [InlineData(1000, 9.10)] // Way beyond poor → very low
    public void ScoreLatency_VaryingP95_ScoresCorrectly(double p95, double expectedApprox)
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreLatency(p95, null, null);

        score.Should().BeApproximately(expectedApprox, 2.0);
    }

    [Fact]
    public void ScoreLatency_NullP95_Returns100()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreLatency(null, null, null);

        score.Should().Be(100);
    }

    [Fact]
    public void ScoreLatency_HighP99Ratio_AppliesPenalty()
    {
        _scorer = new ProviderDegradationScorer();

        var scoreNormal = _scorer.ScoreLatency(100, 150, null);
        var scoreHighTail = _scorer.ScoreLatency(100, 400, null);

        scoreHighTail.Should().BeLessThan(scoreNormal);
    }

    #endregion

    #region Stability Scoring Tests

    [Fact]
    public void ScoreStability_PerfectUptime_NoReconnects_Returns100()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreStability(0, 1.0);

        score.Should().Be(100);
    }

    [Fact]
    public void ScoreStability_ManyReconnects_LowUptime_ReturnsLow()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreStability(10, 0.5);

        score.Should().BeLessThan(30);
    }

    [Fact]
    public void ScoreStability_ModerateReconnects_FullUptime_ReturnsMedium()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreStability(3, 1.0);

        score.Should().BeInRange(60, 90);
    }

    #endregion

    #region Completeness Scoring Tests

    [Fact]
    public void ScoreCompleteness_PerfectCompleteness_Returns100()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreCompleteness(100, 0, 10000);

        score.Should().Be(100);
    }

    [Fact]
    public void ScoreCompleteness_HighDropRate_ReturnsLow()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreCompleteness(100, 5000, 5000);

        score.Should().BeLessThan(60);
    }

    [Fact]
    public void ScoreCompleteness_NoEvents_Returns100()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreCompleteness(100, 0, 0);

        score.Should().Be(100);
    }

    #endregion

    #region Consistency Scoring Tests

    [Fact]
    public void ScoreConsistency_NoErrors_ReturnsNear100()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreConsistency(0, 0, 0);

        score.Should().Be(100);
    }

    [Fact]
    public void ScoreConsistency_HighErrorRate_ReturnsLow()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreConsistency(2.0, 1.0, 1.0);

        score.Should().BeLessThan(30);
    }

    [Fact]
    public void ScoreConsistency_SmallErrors_ReturnsHigh()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.ScoreConsistency(0.05, 0.02, 0.01);

        score.Should().BeGreaterThan(90);
    }

    #endregion

    #region GetScore and GetAllScores Tests

    [Fact]
    public void GetScore_UnknownProvider_ReturnsNull()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.GetScore("unknown").Should().BeNull();
    }

    [Fact]
    public void GetAllScores_MultiplProviders_ReturnsAll()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));
        _scorer.CalculateScore(CreateHealthyInput("polygon"));
        _scorer.CalculateScore(CreateHealthyInput("tiingo"));

        var all = _scorer.GetAllScores();

        all.Should().HaveCount(3);
        all.Should().ContainKey("alpaca");
        all.Should().ContainKey("polygon");
        all.Should().ContainKey("tiingo");
    }

    #endregion

    #region RankProviders Tests

    [Fact]
    public void RankProviders_MultipleProviders_RankedByScore()
    {
        _scorer = new ProviderDegradationScorer();

        // Create providers with different health levels
        _scorer.CalculateScore(CreateHealthyInput("best"));
        _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "medium",
            IsConnected = true,
            P95LatencyMs = 250,
            ReconnectsInWindow = 2,
            UptimeFraction = 0.9,
            CompletenessPercent = 96,
            EventsReceived = 9600,
            EventsDropped = 400
        });
        _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "worst",
            IsConnected = true,
            P95LatencyMs = 800,
            ReconnectsInWindow = 8,
            UptimeFraction = 0.6,
            CompletenessPercent = 80,
            GapRatePercent = 2.0,
            EventsReceived = 8000,
            EventsDropped = 2000
        });

        var ranking = _scorer.RankProviders();

        ranking.Providers.Should().HaveCount(3);
        ranking.Providers[0].ProviderId.Should().Be("best");
        ranking.Providers[0].Rank.Should().Be(1);
        ranking.Providers[2].ProviderId.Should().Be("worst");
        ranking.Providers[2].Rank.Should().Be(3);
        ranking.RecommendedActiveProvider.Should().Be("best");
    }

    [Fact]
    public void RankProviders_AllUnavailable_ReturnsNullRecommendation()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(new ProviderHealthInput { ProviderId = "p1", IsConnected = false });
        _scorer.CalculateScore(new ProviderHealthInput { ProviderId = "p2", IsConnected = false });

        var ranking = _scorer.RankProviders();

        ranking.RecommendedActiveProvider.Should().BeNull();
    }

    [Fact]
    public void RankProviders_NoProviders_ReturnsEmpty()
    {
        _scorer = new ProviderDegradationScorer();

        var ranking = _scorer.RankProviders();

        ranking.Providers.Should().BeEmpty();
        ranking.RecommendedActiveProvider.Should().BeNull();
    }

    #endregion

    #region SelectBestProvider Tests

    [Fact]
    public void SelectBestProvider_ReturnsHighestScored()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));
        _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "polygon",
            IsConnected = true,
            P95LatencyMs = 400,
            ReconnectsInWindow = 5,
            UptimeFraction = 0.7,
            CompletenessPercent = 90,
            EventsReceived = 9000,
            EventsDropped = 1000
        });

        var best = _scorer.SelectBestProvider(new[] { "alpaca", "polygon" });

        best.Should().Be("alpaca");
    }

    [Fact]
    public void SelectBestProvider_ExcludesSpecifiedProvider()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));
        _scorer.CalculateScore(CreateHealthyInput("polygon"));

        var best = _scorer.SelectBestProvider(new[] { "alpaca", "polygon" }, excludeProviderId: "alpaca");

        best.Should().Be("polygon");
    }

    [Fact]
    public void SelectBestProvider_AllBelowThreshold_ReturnsNull()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(new ProviderHealthInput { ProviderId = "p1", IsConnected = false });
        _scorer.CalculateScore(new ProviderHealthInput { ProviderId = "p2", IsConnected = false });

        var best = _scorer.SelectBestProvider(new[] { "p1", "p2" });

        best.Should().BeNull();
    }

    [Fact]
    public void SelectBestProvider_UnknownProvider_UsesDefaultScore()
    {
        _scorer = new ProviderDegradationScorer();

        var best = _scorer.SelectBestProvider(new[] { "unknown" });

        // Unknown providers get a default score of 50 which is above the 40 failover threshold
        best.Should().Be("unknown");
    }

    #endregion

    #region HasDegraded Tests

    [Fact]
    public void HasDegraded_ScoreDropsBelowThreshold_ReturnsTrue()
    {
        _scorer = new ProviderDegradationScorer();

        // First score: healthy
        _scorer.CalculateScore(CreateHealthyInput("alpaca"));

        // Second score: degraded
        _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "alpaca",
            IsConnected = true,
            P95LatencyMs = 1000,
            ReconnectsInWindow = 10,
            UptimeFraction = 0.4,
            CompletenessPercent = 60,
            GapRatePercent = 5.0,
            EventsReceived = 3000,
            EventsDropped = 5000
        });

        _scorer.HasDegraded("alpaca").Should().BeTrue();
    }

    [Fact]
    public void HasDegraded_NoPreviousScore_ReturnsFalse()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));

        _scorer.HasDegraded("alpaca").Should().BeFalse();
    }

    [Fact]
    public void HasDegraded_UnknownProvider_ReturnsFalse()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.HasDegraded("unknown").Should().BeFalse();
    }

    #endregion

    #region ShouldFailover Tests

    [Fact]
    public void ShouldFailover_FailoverRecommended_ReturnsTrue()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "bad",
            IsConnected = true,
            P95LatencyMs = 3000,
            ReconnectsInWindow = 20,
            UptimeFraction = 0.3,
            CompletenessPercent = 50,
            GapRatePercent = 10,
            EventsReceived = 2000,
            EventsDropped = 8000
        });

        _scorer.ShouldFailover("bad").Should().BeTrue();
    }

    [Fact]
    public void ShouldFailover_Unavailable_ReturnsTrue()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "down",
            IsConnected = false
        });

        _scorer.ShouldFailover("down").Should().BeTrue();
    }

    [Fact]
    public void ShouldFailover_HealthyProvider_ReturnsFalse()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));

        _scorer.ShouldFailover("alpaca").Should().BeFalse();
    }

    [Fact]
    public void ShouldFailover_UnknownProvider_ReturnsFalse()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.ShouldFailover("unknown").Should().BeFalse();
    }

    #endregion

    #region RemoveProvider and Clear Tests

    [Fact]
    public void RemoveProvider_RemovesScoreAndHistory()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));
        _scorer.RemoveProvider("alpaca");

        _scorer.GetScore("alpaca").Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesAllScores()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.CalculateScore(CreateHealthyInput("alpaca"));
        _scorer.CalculateScore(CreateHealthyInput("polygon"));

        _scorer.Clear();

        _scorer.GetAllScores().Should().BeEmpty();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        _scorer = new ProviderDegradationScorer();

        _scorer.Dispose();
        var act = () => _scorer.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void CalculateScore_AfterDispose_ThrowsObjectDisposedException()
    {
        _scorer = new ProviderDegradationScorer();
        _scorer.Dispose();

        var act = () => _scorer.CalculateScore(CreateHealthyInput("alpaca"));

        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Recommendation Classification Tests

    [Fact]
    public void Recommendation_ScoreAbove80_IsHealthy()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.CalculateScore(CreateHealthyInput("test"));

        score.Recommendation.Should().Be(ProviderHealthRecommendation.Healthy);
    }

    [Fact]
    public void Recommendation_ScoreBetween60And80_IsCaution()
    {
        _scorer = new ProviderDegradationScorer();

        var score = _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "test",
            IsConnected = true,
            P95LatencyMs = 250,
            ReconnectsInWindow = 2,
            UptimeFraction = 0.9,
            CompletenessPercent = 96,
            EventsReceived = 9600,
            EventsDropped = 400
        });

        score.OverallScore.Should().BeInRange(60, 80);
        score.Recommendation.Should().Be(ProviderHealthRecommendation.Caution);
    }

    #endregion

    #region Score Update Tests

    [Fact]
    public void CalculateScore_UpdatesExistingScore()
    {
        _scorer = new ProviderDegradationScorer();

        // First score
        var first = _scorer.CalculateScore(CreateHealthyInput("alpaca"));
        var firstScore = first.OverallScore;

        // Update with degraded input
        var second = _scorer.CalculateScore(new ProviderHealthInput
        {
            ProviderId = "alpaca",
            IsConnected = true,
            P95LatencyMs = 500,
            ReconnectsInWindow = 5,
            UptimeFraction = 0.7,
            CompletenessPercent = 90,
            EventsReceived = 9000,
            EventsDropped = 1000
        });

        second.OverallScore.Should().BeLessThan(firstScore);
        _scorer.GetScore("alpaca")!.OverallScore.Should().Be(second.OverallScore);
    }

    #endregion

    #region Helpers

    private static ProviderHealthInput CreateHealthyInput(string providerId)
    {
        return new ProviderHealthInput
        {
            ProviderId = providerId,
            IsConnected = true,
            P95LatencyMs = 50,
            P99LatencyMs = 80,
            MeanLatencyMs = 30,
            ReconnectsInWindow = 0,
            UptimeFraction = 1.0,
            CompletenessPercent = 100,
            GapRatePercent = 0,
            DuplicateRatePercent = 0,
            OutOfOrderRatePercent = 0,
            EventsReceived = 10000,
            EventsDropped = 0
        };
    }

    #endregion
}
