using System.Collections.Concurrent;
using MarketDataCollector.Infrastructure.Contracts;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Configuration for the degradation scoring algorithm.
/// </summary>
public sealed record DegradationScoringConfig
{
    /// <summary>Weight for latency component (0.0 - 1.0).</summary>
    public double LatencyWeight { get; init; } = 0.25;

    /// <summary>Weight for connection stability component (0.0 - 1.0).</summary>
    public double StabilityWeight { get; init; } = 0.30;

    /// <summary>Weight for data completeness component (0.0 - 1.0).</summary>
    public double CompletenessWeight { get; init; } = 0.25;

    /// <summary>Weight for data consistency component (0.0 - 1.0).</summary>
    public double ConsistencyWeight { get; init; } = 0.20;

    /// <summary>P95 latency threshold for "good" rating (ms).</summary>
    public double LatencyGoodP95Ms { get; init; } = 150;

    /// <summary>P95 latency threshold for "fair" rating (ms).</summary>
    public double LatencyFairP95Ms { get; init; } = 300;

    /// <summary>P95 latency threshold for "poor" rating (ms).</summary>
    public double LatencyPoorP95Ms { get; init; } = 500;

    /// <summary>Reconnects per hour threshold for "good" stability.</summary>
    public int StabilityGoodReconnectsPerHour { get; init; } = 1;

    /// <summary>Reconnects per hour threshold for "fair" stability.</summary>
    public int StabilityFairReconnectsPerHour { get; init; } = 3;

    /// <summary>Reconnects per hour threshold for "poor" stability.</summary>
    public int StabilityPoorReconnectsPerHour { get; init; } = 5;

    /// <summary>Completeness percentage for "good" rating.</summary>
    public double CompletenessGoodPercent { get; init; } = 99;

    /// <summary>Completeness percentage for "fair" rating.</summary>
    public double CompletenesFairPercent { get; init; } = 95;

    /// <summary>Error rate percentage for "good" consistency.</summary>
    public double ConsistencyGoodErrorRate { get; init; } = 0.1;

    /// <summary>Error rate percentage for "fair" consistency.</summary>
    public double ConsistencyFairErrorRate { get; init; } = 0.5;

    /// <summary>Score below which a provider is considered degraded.</summary>
    public double DegradationThreshold { get; init; } = 60.0;

    /// <summary>Score below which failover should be triggered.</summary>
    public double FailoverThreshold { get; init; } = 40.0;

    /// <summary>Default configuration.</summary>
    public static DegradationScoringConfig Default => new();
}

/// <summary>
/// Input metrics for scoring a provider's health.
/// </summary>
public sealed record ProviderHealthInput
{
    /// <summary>Provider identifier.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Whether the provider is currently connected.</summary>
    public bool IsConnected { get; init; }

    /// <summary>P95 latency in milliseconds. Null if no data.</summary>
    public double? P95LatencyMs { get; init; }

    /// <summary>P99 latency in milliseconds. Null if no data.</summary>
    public double? P99LatencyMs { get; init; }

    /// <summary>Mean latency in milliseconds. Null if no data.</summary>
    public double? MeanLatencyMs { get; init; }

    /// <summary>Number of reconnection attempts in the measurement window.</summary>
    public int ReconnectsInWindow { get; init; }

    /// <summary>Connection uptime as a fraction (0.0 - 1.0).</summary>
    public double UptimeFraction { get; init; } = 1.0;

    /// <summary>Data completeness as a percentage (0 - 100).</summary>
    public double CompletenessPercent { get; init; } = 100;

    /// <summary>Gap rate as a percentage of total events.</summary>
    public double GapRatePercent { get; init; }

    /// <summary>Duplicate rate as a percentage of total events.</summary>
    public double DuplicateRatePercent { get; init; }

    /// <summary>Out-of-order rate as a percentage of total events.</summary>
    public double OutOfOrderRatePercent { get; init; }

    /// <summary>Total events received in the measurement window.</summary>
    public long EventsReceived { get; init; }

    /// <summary>Total events dropped in the measurement window.</summary>
    public long EventsDropped { get; init; }
}

/// <summary>
/// Result of a degradation scoring calculation for a single provider.
/// </summary>
public sealed record DegradationScore
{
    /// <summary>Provider identifier.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Overall composite score (0-100). Higher is healthier.</summary>
    public double OverallScore { get; init; }

    /// <summary>Latency component score (0-100).</summary>
    public double LatencyScore { get; init; }

    /// <summary>Connection stability component score (0-100).</summary>
    public double StabilityScore { get; init; }

    /// <summary>Data completeness component score (0-100).</summary>
    public double CompletenessScore { get; init; }

    /// <summary>Data consistency component score (0-100).</summary>
    public double ConsistencyScore { get; init; }

    /// <summary>Health recommendation based on score.</summary>
    public ProviderHealthRecommendation Recommendation { get; init; }

    /// <summary>When the score was calculated.</summary>
    public DateTimeOffset CalculatedAt { get; init; }
}

/// <summary>
/// Health recommendation based on degradation score.
/// </summary>
public enum ProviderHealthRecommendation
{
    /// <summary>Provider is healthy and performing well.</summary>
    Healthy,

    /// <summary>Provider showing signs of degradation; monitor closely.</summary>
    Caution,

    /// <summary>Provider significantly degraded; consider failover.</summary>
    Degraded,

    /// <summary>Provider should be failed over to a backup.</summary>
    FailoverRecommended,

    /// <summary>Provider is disconnected or unavailable.</summary>
    Unavailable
}

/// <summary>
/// Ranking of all providers by their degradation scores.
/// </summary>
public sealed record ProviderScoreRanking
{
    /// <summary>Providers ranked by score (highest first).</summary>
    public required IReadOnlyList<RankedProvider> Providers { get; init; }

    /// <summary>The top-ranked (healthiest) provider.</summary>
    public string? RecommendedActiveProvider { get; init; }

    /// <summary>When the ranking was calculated.</summary>
    public DateTimeOffset CalculatedAt { get; init; }
}

/// <summary>
/// A provider with its rank position and score.
/// </summary>
public sealed record RankedProvider(
    string ProviderId,
    int Rank,
    double Score,
    ProviderHealthRecommendation Recommendation);

/// <summary>
/// Calculates degradation scores for data providers based on latency, stability,
/// completeness, and consistency metrics. Supports intelligent failover decisions
/// by replacing binary healthy/unhealthy logic with a continuous 0-100 score.
/// </summary>
/// <remarks>
/// This is the H4 (Graceful Provider Degradation Scoring) component from the project roadmap.
/// The scorer integrates with existing monitoring infrastructure:
/// <list type="bullet">
///   <item><see cref="ProviderLatencyService"/> — latency percentiles</item>
///   <item><see cref="ConnectionHealthMonitor"/> — connection stability</item>
///   <item><see cref="ProviderMetricsStatus"/> — operational metrics</item>
/// </list>
/// </remarks>
[ImplementsAdr("ADR-012", "Provider degradation scoring for intelligent failover")]
public sealed class ProviderDegradationScorer : IDisposable
{
    private readonly DegradationScoringConfig _config;
    private readonly ConcurrentDictionary<string, DegradationScore> _scores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DegradationScore?> _previousScores = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new provider degradation scorer with the specified configuration.
    /// </summary>
    public ProviderDegradationScorer(DegradationScoringConfig? config = null)
    {
        _config = config ?? DegradationScoringConfig.Default;
        ValidateConfig(_config);
    }

    /// <summary>
    /// Gets the active scoring configuration.
    /// </summary>
    public DegradationScoringConfig Config => _config;

    /// <summary>
    /// Calculates a degradation score for the given provider health input.
    /// </summary>
    public DegradationScore CalculateScore(ProviderHealthInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ProviderId);

        if (!input.IsConnected)
        {
            var unavailableScore = new DegradationScore
            {
                ProviderId = input.ProviderId,
                OverallScore = 0,
                LatencyScore = 0,
                StabilityScore = 0,
                CompletenessScore = 0,
                ConsistencyScore = 0,
                Recommendation = ProviderHealthRecommendation.Unavailable,
                CalculatedAt = DateTimeOffset.UtcNow
            };
            StoreScore(unavailableScore);
            return unavailableScore;
        }

        var latencyScore = ScoreLatency(input.P95LatencyMs, input.P99LatencyMs, input.MeanLatencyMs);
        var stabilityScore = ScoreStability(input.ReconnectsInWindow, input.UptimeFraction);
        var completenessScore = ScoreCompleteness(input.CompletenessPercent, input.EventsDropped, input.EventsReceived);
        var consistencyScore = ScoreConsistency(input.GapRatePercent, input.DuplicateRatePercent, input.OutOfOrderRatePercent);

        var overall =
            _config.LatencyWeight * latencyScore +
            _config.StabilityWeight * stabilityScore +
            _config.CompletenessWeight * completenessScore +
            _config.ConsistencyWeight * consistencyScore;

        // Clamp to [0, 100]
        overall = Math.Clamp(overall, 0, 100);

        var recommendation = ClassifyRecommendation(overall);

        var score = new DegradationScore
        {
            ProviderId = input.ProviderId,
            OverallScore = Math.Round(overall, 2),
            LatencyScore = Math.Round(latencyScore, 2),
            StabilityScore = Math.Round(stabilityScore, 2),
            CompletenessScore = Math.Round(completenessScore, 2),
            ConsistencyScore = Math.Round(consistencyScore, 2),
            Recommendation = recommendation,
            CalculatedAt = DateTimeOffset.UtcNow
        };

        StoreScore(score);
        return score;
    }

    /// <summary>
    /// Gets the most recently calculated score for a provider.
    /// </summary>
    public DegradationScore? GetScore(string providerId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scores.TryGetValue(providerId, out var score) ? score : null;
    }

    /// <summary>
    /// Gets scores for all known providers.
    /// </summary>
    public IReadOnlyDictionary<string, DegradationScore> GetAllScores()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Dictionary<string, DegradationScore>(_scores, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ranks all scored providers by their overall score.
    /// </summary>
    public ProviderScoreRanking RankProviders()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ranked = _scores.Values
            .OrderByDescending(s => s.OverallScore)
            .Select((s, i) => new RankedProvider(s.ProviderId, i + 1, s.OverallScore, s.Recommendation))
            .ToList();

        var recommended = ranked.FirstOrDefault(p =>
            p.Recommendation is ProviderHealthRecommendation.Healthy or ProviderHealthRecommendation.Caution);

        return new ProviderScoreRanking
        {
            Providers = ranked,
            RecommendedActiveProvider = recommended?.ProviderId,
            CalculatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Selects the best available provider from a set of candidates, excluding a specific provider.
    /// Returns null if no suitable provider exists.
    /// </summary>
    public string? SelectBestProvider(IEnumerable<string> candidates, string? excludeProviderId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return candidates
            .Where(c => !string.Equals(c, excludeProviderId, StringComparison.OrdinalIgnoreCase))
            .Select(c => (ProviderId: c, Score: _scores.TryGetValue(c, out var s) ? s.OverallScore : 50.0))
            .Where(c => c.Score >= _config.FailoverThreshold)
            .OrderByDescending(c => c.Score)
            .Select(c => c.ProviderId)
            .FirstOrDefault();
    }

    /// <summary>
    /// Determines whether a provider has degraded compared to its previous score.
    /// </summary>
    public bool HasDegraded(string providerId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_scores.TryGetValue(providerId, out var current)) return false;
        if (!_previousScores.TryGetValue(providerId, out var previous) || previous is null) return false;

        return current.OverallScore < previous.OverallScore &&
               current.OverallScore < _config.DegradationThreshold;
    }

    /// <summary>
    /// Determines whether a provider should trigger failover.
    /// </summary>
    public bool ShouldFailover(string providerId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_scores.TryGetValue(providerId, out var score)) return false;

        return score.Recommendation is ProviderHealthRecommendation.FailoverRecommended or
            ProviderHealthRecommendation.Unavailable;
    }

    /// <summary>
    /// Removes a provider's scores.
    /// </summary>
    public void RemoveProvider(string providerId)
    {
        _scores.TryRemove(providerId, out _);
        _previousScores.TryRemove(providerId, out _);
    }

    /// <summary>
    /// Clears all stored scores.
    /// </summary>
    public void Clear()
    {
        _scores.Clear();
        _previousScores.Clear();
    }

    #region Scoring Components

    internal double ScoreLatency(double? p95Ms, double? p99Ms, double? meanMs)
    {
        // No latency data — assume good (benefit of the doubt)
        if (!p95Ms.HasValue) return 100;

        var p95 = p95Ms.Value;

        double baseScore;
        if (p95 <= _config.LatencyGoodP95Ms)
        {
            // Linear interpolation: 0ms → 100, goodThreshold → 90
            baseScore = 100 - (p95 / _config.LatencyGoodP95Ms) * 10;
        }
        else if (p95 <= _config.LatencyFairP95Ms)
        {
            // Linear interpolation: good → 90, fair → 65
            var range = _config.LatencyFairP95Ms - _config.LatencyGoodP95Ms;
            var position = (p95 - _config.LatencyGoodP95Ms) / range;
            baseScore = 90 - position * 25;
        }
        else if (p95 <= _config.LatencyPoorP95Ms)
        {
            // Linear interpolation: fair → 65, poor → 30
            var range = _config.LatencyPoorP95Ms - _config.LatencyFairP95Ms;
            var position = (p95 - _config.LatencyFairP95Ms) / range;
            baseScore = 65 - position * 35;
        }
        else
        {
            // Exponential decay beyond poor threshold
            var overPoor = p95 - _config.LatencyPoorP95Ms;
            baseScore = Math.Max(0, 30 * Math.Exp(-overPoor / _config.LatencyPoorP95Ms));
        }

        // P99 penalty: if P99 is significantly higher than P95, add a penalty for inconsistency
        if (p99Ms.HasValue && p95 > 0)
        {
            var p99Ratio = p99Ms.Value / p95;
            if (p99Ratio > 3.0)
                baseScore *= 0.85; // 15% penalty for high tail latency
            else if (p99Ratio > 2.0)
                baseScore *= 0.92; // 8% penalty
        }

        return Math.Clamp(baseScore, 0, 100);
    }

    internal double ScoreStability(int reconnectsInWindow, double uptimeFraction)
    {
        // Uptime component (0-50 points)
        var uptimeScore = uptimeFraction * 50;

        // Reconnect component (0-50 points)
        double reconnectScore;
        if (reconnectsInWindow <= _config.StabilityGoodReconnectsPerHour)
        {
            reconnectScore = 50;
        }
        else if (reconnectsInWindow <= _config.StabilityFairReconnectsPerHour)
        {
            var range = _config.StabilityFairReconnectsPerHour - _config.StabilityGoodReconnectsPerHour;
            var position = (double)(reconnectsInWindow - _config.StabilityGoodReconnectsPerHour) / Math.Max(range, 1);
            reconnectScore = 50 - position * 20; // 50 → 30
        }
        else if (reconnectsInWindow <= _config.StabilityPoorReconnectsPerHour)
        {
            var range = _config.StabilityPoorReconnectsPerHour - _config.StabilityFairReconnectsPerHour;
            var position = (double)(reconnectsInWindow - _config.StabilityFairReconnectsPerHour) / Math.Max(range, 1);
            reconnectScore = 30 - position * 20; // 30 → 10
        }
        else
        {
            reconnectScore = Math.Max(0, 10 - (reconnectsInWindow - _config.StabilityPoorReconnectsPerHour) * 2);
        }

        return Math.Clamp(uptimeScore + reconnectScore, 0, 100);
    }

    internal double ScoreCompleteness(double completenessPercent, long eventsDropped, long eventsReceived)
    {
        var totalEvents = eventsReceived + eventsDropped;

        // If no events at all, assume perfect (no data yet)
        if (totalEvents == 0) return 100;

        // Actual completeness from drop rate
        var actualCompleteness = totalEvents > 0
            ? (double)eventsReceived / totalEvents * 100
            : completenessPercent;

        // Use the worse of reported completeness and calculated completeness
        var effectiveCompleteness = Math.Min(completenessPercent, actualCompleteness);

        if (effectiveCompleteness >= _config.CompletenessGoodPercent)
        {
            // 99%+ → 90-100
            var fraction = (effectiveCompleteness - _config.CompletenessGoodPercent) /
                           (100 - _config.CompletenessGoodPercent);
            return 90 + fraction * 10;
        }
        if (effectiveCompleteness >= _config.CompletenesFairPercent)
        {
            // 95-99% → 60-90
            var range = _config.CompletenessGoodPercent - _config.CompletenesFairPercent;
            var position = (effectiveCompleteness - _config.CompletenesFairPercent) / range;
            return 60 + position * 30;
        }

        // Below fair → 0-60
        return Math.Max(0, effectiveCompleteness / _config.CompletenesFairPercent * 60);
    }

    internal double ScoreConsistency(double gapRatePercent, double duplicateRatePercent, double outOfOrderRatePercent)
    {
        var totalErrorRate = gapRatePercent + duplicateRatePercent + outOfOrderRatePercent;

        if (totalErrorRate <= _config.ConsistencyGoodErrorRate)
        {
            return 100 - (totalErrorRate / _config.ConsistencyGoodErrorRate) * 5; // 95-100
        }
        if (totalErrorRate <= _config.ConsistencyFairErrorRate)
        {
            var range = _config.ConsistencyFairErrorRate - _config.ConsistencyGoodErrorRate;
            var position = (totalErrorRate - _config.ConsistencyGoodErrorRate) / range;
            return 95 - position * 30; // 65-95
        }

        // Beyond fair threshold: rapid degradation
        var overFair = totalErrorRate - _config.ConsistencyFairErrorRate;
        return Math.Max(0, 65 * Math.Exp(-overFair));
    }

    #endregion

    #region Helpers

    private ProviderHealthRecommendation ClassifyRecommendation(double score)
    {
        if (score >= 80) return ProviderHealthRecommendation.Healthy;
        if (score >= _config.DegradationThreshold) return ProviderHealthRecommendation.Caution;
        if (score >= _config.FailoverThreshold) return ProviderHealthRecommendation.Degraded;
        return ProviderHealthRecommendation.FailoverRecommended;
    }

    private void StoreScore(DegradationScore score)
    {
        if (_scores.TryGetValue(score.ProviderId, out var existing))
        {
            _previousScores[score.ProviderId] = existing;
        }
        _scores[score.ProviderId] = score;
    }

    private static void ValidateConfig(DegradationScoringConfig config)
    {
        var totalWeight = config.LatencyWeight + config.StabilityWeight +
                         config.CompletenessWeight + config.ConsistencyWeight;
        if (Math.Abs(totalWeight - 1.0) > 0.01)
        {
            throw new ArgumentException(
                $"Scoring weights must sum to 1.0 (got {totalWeight:F2}). " +
                $"Latency={config.LatencyWeight}, Stability={config.StabilityWeight}, " +
                $"Completeness={config.CompletenessWeight}, Consistency={config.ConsistencyWeight}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scores.Clear();
        _previousScores.Clear();
    }

    #endregion
}
