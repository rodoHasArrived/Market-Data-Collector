using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.Models;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing data quality monitoring, alerts, and quality scoring.
/// </summary>
public sealed class WpfDataQualityService
{
    private static WpfDataQualityService? _instance;
    private static readonly object _lock = new();

    public static WpfDataQualityService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new WpfDataQualityService();
                }
            }
            return _instance;
        }
    }

    private WpfDataQualityService() { }

    /// <summary>
    /// Gets the overall data quality summary.
    /// </summary>
    public async Task<DataQualitySummary?> GetQualitySummaryAsync(CancellationToken ct = default)
    {
        var response = await ApiClientService.Instance.GetAsync<DataQualitySummary>("/api/storage/quality/summary", ct);
        return response;
    }

    /// <summary>
    /// Gets quality scores for all monitored files/paths.
    /// </summary>
    public async Task<List<QualityScoreEntry>?> GetQualityScoresAsync(double? minScore = null, CancellationToken ct = default)
    {
        var endpoint = minScore.HasValue
            ? $"/api/storage/quality/scores?minScore={minScore}"
            : "/api/storage/quality/scores";
        return await ApiClientService.Instance.GetAsync<List<QualityScoreEntry>>(endpoint, ct);
    }

    /// <summary>
    /// Gets quality scores for a specific symbol.
    /// </summary>
    public async Task<SymbolQualityReport?> GetSymbolQualityAsync(string symbol, CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<SymbolQualityReport>($"/api/storage/quality/symbol/{symbol}", ct);
    }

    /// <summary>
    /// Gets quality alerts with optional severity filter.
    /// </summary>
    public async Task<List<QualityAlert>?> GetQualityAlertsAsync(string? severity = null, CancellationToken ct = default)
    {
        var endpoint = !string.IsNullOrEmpty(severity)
            ? $"/api/storage/quality/alerts?severity={severity}"
            : "/api/storage/quality/alerts";
        return await ApiClientService.Instance.GetAsync<List<QualityAlert>>(endpoint, ct);
    }

    /// <summary>
    /// Acknowledges a quality alert.
    /// </summary>
    public async Task<bool> AcknowledgeAlertAsync(string alertId, CancellationToken ct = default)
    {
        var response = await ApiClientService.Instance.PostWithResponseAsync<AcknowledgeResponse>(
            $"/api/storage/quality/alerts/{alertId}/acknowledge", null, ct);
        return response.Success;
    }

    /// <summary>
    /// Gets source rankings by quality for a specific symbol.
    /// </summary>
    public async Task<List<SourceRanking>?> GetSourceRankingsAsync(string symbol, CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<List<SourceRanking>>($"/api/storage/quality/rankings/{symbol}", ct);
    }

    /// <summary>
    /// Gets quality trend analysis over time.
    /// </summary>
    public async Task<QualityTrendData?> GetQualityTrendsAsync(string? timeWindow = "7d", CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<QualityTrendData>($"/api/storage/quality/trends?window={timeWindow}", ct);
    }

    /// <summary>
    /// Gets anomaly detection results.
    /// </summary>
    public async Task<List<AnomalyEvent>?> GetAnomaliesAsync(string? type = null, CancellationToken ct = default)
    {
        var endpoint = !string.IsNullOrEmpty(type)
            ? $"/api/storage/quality/anomalies?type={type}"
            : "/api/storage/quality/anomalies";
        return await ApiClientService.Instance.GetAsync<List<AnomalyEvent>>(endpoint, ct);
    }

    /// <summary>
    /// Runs a data quality check on specified path or symbol.
    /// </summary>
    public async Task<QualityCheckResult?> RunQualityCheckAsync(string path, CancellationToken ct = default)
    {
        return await ApiClientService.Instance.PostAsync<QualityCheckResult>(
            "/api/storage/quality/check",
            new { path },
            ct);
    }

    /// <summary>
    /// Gets data gaps for a specific symbol.
    /// </summary>
    public async Task<List<DataGapInfo>> GetDataGapsAsync(string symbol, CancellationToken ct = default)
    {
        var report = await GetSymbolQualityAsync(symbol, ct);
        if (report?.Gaps == null) return new List<DataGapInfo>();

        var gaps = new List<DataGapInfo>();
        foreach (var gap in report.Gaps)
        {
            gaps.Add(new DataGapInfo
            {
                StartDate = gap.Start,
                EndDate = gap.End,
                MissingBars = gap.MissingRecords
            });
        }
        return gaps;
    }

    /// <summary>
    /// Verifies data integrity for a specific symbol.
    /// </summary>
    public async Task<IntegrityVerificationResult> VerifySymbolIntegrityAsync(string symbol, CancellationToken ct = default)
    {
        var checkResult = await RunQualityCheckAsync(symbol, ct);

        if (checkResult == null)
        {
            return new IntegrityVerificationResult
            {
                IsValid = false,
                Issues = new List<string> { "Failed to run integrity check" }
            };
        }

        return new IntegrityVerificationResult
        {
            IsValid = checkResult.Score >= 95.0 && checkResult.Issues.Count == 0,
            Score = checkResult.Score,
            Issues = checkResult.Issues,
            Recommendations = checkResult.Recommendations,
            CheckedAt = checkResult.CheckedAt
        };
    }
}

/// <summary>
/// Result of an integrity verification check.
/// </summary>
public class IntegrityVerificationResult
{
    public bool IsValid { get; set; }
    public double Score { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

// DTO classes for data quality API responses

public class DataQualitySummary
{
    public double OverallScore { get; set; }
    public int TotalFiles { get; set; }
    public int HealthyFiles { get; set; }
    public int WarningFiles { get; set; }
    public int CriticalFiles { get; set; }
    public int ActiveAlerts { get; set; }
    public int UnacknowledgedAlerts { get; set; }
    public DateTime LastChecked { get; set; }
    public List<SymbolQualitySummary> SymbolSummaries { get; set; } = new();
}

public class SymbolQualitySummary
{
    public string Symbol { get; set; } = string.Empty;
    public double QualityScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public int GapCount { get; set; }
    public DateTime LastUpdate { get; set; }
}

public class QualityScoreEntry
{
    public string Path { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Grade { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int MissingCount { get; set; }
    public int DuplicateCount { get; set; }
    public int AnomalyCount { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public DateTime LastAnalyzed { get; set; }
}

public class SymbolQualityReport
{
    public string Symbol { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public Dictionary<string, double> ScoresByType { get; set; } = new();
    public List<QualityIssue> Issues { get; set; } = new();
    public List<QualityDataGap> Gaps { get; set; } = new();
    public QualityCompletenessReport Completeness { get; set; } = new();
}

public class QualityIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string AffectedPath { get; set; } = string.Empty;
}

public class QualityDataGap
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public TimeSpan Duration { get; set; }
    public string DataType { get; set; } = string.Empty;
    public int MissingRecords { get; set; }
}

public class QualityCompletenessReport
{
    public double OverallCompleteness { get; set; }
    public Dictionary<string, double> ByDataType { get; set; } = new();
    public Dictionary<string, double> ByDate { get; set; } = new();
}

public class QualityAlert
{
    public string Id { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public class AcknowledgeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SourceRanking
{
    public string Source { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public double QualityScore { get; set; }
    public double Latency { get; set; }
    public double Completeness { get; set; }
    public int Rank { get; set; }
}

public class QualityTrendData
{
    public List<TrendDataPoint> OverallTrend { get; set; } = new();
    public Dictionary<string, List<TrendDataPoint>> BySymbol { get; set; } = new();
    public string TimeWindow { get; set; } = string.Empty;
    public double AverageScore { get; set; }
    public double TrendDirection { get; set; }
}

public class TrendDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Score { get; set; }
    public int EventCount { get; set; }
    public int AlertCount { get; set; }
}

public class AnomalyEvent
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public class QualityCheckResult
{
    public bool Success { get; set; }
    public double Score { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}
