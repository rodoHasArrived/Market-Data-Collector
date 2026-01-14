using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for advanced analytics including gap analysis, cross-provider comparison,
/// latency histograms, anomaly detection, and detailed quality reports.
/// </summary>
public sealed class AdvancedAnalyticsService
{
    private static AdvancedAnalyticsService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;

    public static AdvancedAnalyticsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AdvancedAnalyticsService();
                }
            }
            return _instance;
        }
    }

    private AdvancedAnalyticsService()
    {
        _apiClient = ApiClientService.Instance;
    }

    #region Gap Analysis

    /// <summary>
    /// Analyzes data gaps for a symbol or all symbols.
    /// </summary>
    public async Task<GapAnalysisResult> AnalyzeGapsAsync(
        GapAnalysisOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<GapAnalysisResponse>(
            "/api/analytics/gaps",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new GapAnalysisResult
            {
                Success = true,
                AnalysisTime = response.Data.AnalysisTime,
                TotalGaps = response.Data.TotalGaps,
                TotalGapDuration = response.Data.TotalGapDuration,
                Gaps = response.Data.Gaps?.ToList() ?? new List<DataGap>(),
                SymbolSummaries = response.Data.SymbolSummaries?.ToList() ?? new List<SymbolGapSummary>()
            };
        }

        return new GapAnalysisResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Gap analysis failed"
        };
    }

    /// <summary>
    /// Attempts to repair data gaps by fetching from alternative sources.
    /// </summary>
    public async Task<GapRepairResult> RepairGapsAsync(
        GapRepairOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<GapRepairResponse>(
            "/api/analytics/gaps/repair",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new GapRepairResult
            {
                Success = true,
                GapsAttempted = response.Data.GapsAttempted,
                GapsRepaired = response.Data.GapsRepaired,
                GapsFailed = response.Data.GapsFailed,
                RecordsRecovered = response.Data.RecordsRecovered,
                Details = response.Data.Details?.ToList() ?? new List<GapRepairDetail>()
            };
        }

        return new GapRepairResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Gap repair failed"
        };
    }

    #endregion

    #region Cross-Provider Comparison

    /// <summary>
    /// Compares data across multiple providers for consistency.
    /// </summary>
    public async Task<CrossProviderComparisonResult> CompareProvidersAsync(
        CrossProviderComparisonOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<CrossProviderComparisonResponse>(
            "/api/analytics/compare",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new CrossProviderComparisonResult
            {
                Success = true,
                ComparisonTime = response.Data.ComparisonTime,
                OverallConsistencyScore = response.Data.OverallConsistencyScore,
                ProvidersCompared = response.Data.ProvidersCompared?.ToList() ?? new List<string>(),
                Comparisons = response.Data.Comparisons?.ToList() ?? new List<ProviderComparison>(),
                Discrepancies = response.Data.Discrepancies?.ToList() ?? new List<DataDiscrepancy>()
            };
        }

        return new CrossProviderComparisonResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Provider comparison failed"
        };
    }

    #endregion

    #region Latency Analysis

    /// <summary>
    /// Gets latency histogram data for providers.
    /// </summary>
    public async Task<LatencyHistogramResult> GetLatencyHistogramAsync(
        LatencyHistogramOptions? options = null,
        CancellationToken ct = default)
    {
        var url = "/api/analytics/latency";
        if (options?.Provider != null)
        {
            url = $"/api/analytics/latency?provider={Uri.EscapeDataString(options.Provider)}";
        }

        var response = await _apiClient.GetWithResponseAsync<LatencyHistogramResponse>(url, ct);

        if (response.Success && response.Data != null)
        {
            return new LatencyHistogramResult
            {
                Success = true,
                Period = response.Data.Period,
                Providers = response.Data.Providers?.ToList() ?? new List<ProviderLatencyData>()
            };
        }

        return new LatencyHistogramResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get latency data"
        };
    }

    /// <summary>
    /// Gets detailed latency statistics.
    /// </summary>
    public async Task<LatencyStatisticsResult> GetLatencyStatisticsAsync(
        string? provider = null,
        CancellationToken ct = default)
    {
        var url = "/api/analytics/latency/stats";
        if (provider != null)
        {
            url = $"{url}?provider={Uri.EscapeDataString(provider)}";
        }

        var response = await _apiClient.GetWithResponseAsync<LatencyStatisticsResponse>(url, ct);

        if (response.Success && response.Data != null)
        {
            return new LatencyStatisticsResult
            {
                Success = true,
                Statistics = response.Data.Statistics?.ToList() ?? new List<ProviderLatencyStatistics>()
            };
        }

        return new LatencyStatisticsResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion

    #region Anomaly Detection

    /// <summary>
    /// Detects anomalies in market data.
    /// </summary>
    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(
        AnomalyDetectionOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<AnomalyDetectionResponse>(
            "/api/analytics/anomalies",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new AnomalyDetectionResult
            {
                Success = true,
                AnalysisTime = response.Data.AnalysisTime,
                TotalAnomalies = response.Data.TotalAnomalies,
                Anomalies = response.Data.Anomalies?.ToList() ?? new List<DataAnomaly>(),
                Summary = response.Data.Summary
            };
        }

        return new AnomalyDetectionResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Anomaly detection failed"
        };
    }

    #endregion

    #region Quality Reports

    /// <summary>
    /// Gets detailed data quality report.
    /// </summary>
    public async Task<DataQualityReportResult> GetQualityReportAsync(
        DataQualityReportOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<DataQualityReportResponse>(
            "/api/analytics/quality-report",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new DataQualityReportResult
            {
                Success = true,
                ReportTime = response.Data.ReportTime,
                OverallScore = response.Data.OverallScore,
                Grade = response.Data.Grade,
                Metrics = response.Data.Metrics,
                SymbolReports = response.Data.SymbolReports?.ToList() ?? new List<SymbolQualityReport>(),
                Recommendations = response.Data.Recommendations?.ToList() ?? new List<string>()
            };
        }

        return new DataQualityReportResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to generate quality report"
        };
    }

    /// <summary>
    /// Gets data completeness analysis.
    /// </summary>
    public async Task<CompletenessAnalysisResult> AnalyzeCompletenessAsync(
        CompletenessAnalysisOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<CompletenessAnalysisResponse>(
            "/api/analytics/completeness",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new CompletenessAnalysisResult
            {
                Success = true,
                OverallCompleteness = response.Data.OverallCompleteness,
                TradingDaysCovered = response.Data.TradingDaysCovered,
                TradingDaysExpected = response.Data.TradingDaysExpected,
                SymbolCompleteness = response.Data.SymbolCompleteness?.ToList() ?? new List<SymbolCompleteness>()
            };
        }

        return new CompletenessAnalysisResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion

    #region Throughput Analysis

    /// <summary>
    /// Gets throughput statistics over time.
    /// </summary>
    public async Task<ThroughputAnalysisResult> GetThroughputAnalysisAsync(
        ThroughputAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<ThroughputAnalysisResponse>(
            "/api/analytics/throughput",
            options ?? new ThroughputAnalysisOptions(),
            ct);

        if (response.Success && response.Data != null)
        {
            return new ThroughputAnalysisResult
            {
                Success = true,
                CurrentThroughput = response.Data.CurrentThroughput,
                AverageThroughput = response.Data.AverageThroughput,
                PeakThroughput = response.Data.PeakThroughput,
                TotalEventsProcessed = response.Data.TotalEventsProcessed,
                Timeline = response.Data.Timeline?.ToList() ?? new List<ThroughputDataPoint>()
            };
        }

        return new ThroughputAnalysisResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion

    #region Provider Rate Limits

    /// <summary>
    /// Gets provider rate limit status and usage.
    /// </summary>
    public async Task<RateLimitStatusResult> GetRateLimitStatusAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<RateLimitStatusResponse>(
            "/api/analytics/rate-limits",
            ct);

        if (response.Success && response.Data != null)
        {
            return new RateLimitStatusResult
            {
                Success = true,
                Providers = response.Data.Providers?.ToList() ?? new List<ProviderRateLimitStatus>()
            };
        }

        return new RateLimitStatusResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion
}

#region Gap Analysis Models

public class GapAnalysisOptions
{
    public string? Symbol { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public List<string>? EventTypes { get; set; }
    public int MinGapMinutes { get; set; } = 5;
}

public class GapAnalysisResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime AnalysisTime { get; set; }
    public int TotalGaps { get; set; }
    public TimeSpan TotalGapDuration { get; set; }
    public List<DataGap> Gaps { get; set; } = new();
    public List<SymbolGapSummary> SymbolSummaries { get; set; } = new();
}

public class GapAnalysisResponse
{
    public DateTime AnalysisTime { get; set; }
    public int TotalGaps { get; set; }
    public TimeSpan TotalGapDuration { get; set; }
    public List<DataGap>? Gaps { get; set; }
    public List<SymbolGapSummary>? SymbolSummaries { get; set; }
}

public class DataGap
{
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? PossibleReason { get; set; }
    public bool IsRepairable { get; set; }
}

public class SymbolGapSummary
{
    public string Symbol { get; set; } = string.Empty;
    public int GapCount { get; set; }
    public TimeSpan TotalGapDuration { get; set; }
    public double CoveragePercent { get; set; }
}

public class GapRepairOptions
{
    public string? Symbol { get; set; }
    public List<string>? GapIds { get; set; }
    public bool UseAlternativeProviders { get; set; } = true;
    public bool DryRun { get; set; } = false;
}

public class GapRepairResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int GapsAttempted { get; set; }
    public int GapsRepaired { get; set; }
    public int GapsFailed { get; set; }
    public long RecordsRecovered { get; set; }
    public List<GapRepairDetail> Details { get; set; } = new();
}

public class GapRepairResponse
{
    public int GapsAttempted { get; set; }
    public int GapsRepaired { get; set; }
    public int GapsFailed { get; set; }
    public long RecordsRecovered { get; set; }
    public List<GapRepairDetail>? Details { get; set; }
}

public class GapRepairDetail
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime GapStart { get; set; }
    public DateTime GapEnd { get; set; }
    public bool Repaired { get; set; }
    public int RecordsRecovered { get; set; }
    public string? SourceProvider { get; set; }
    public string? Error { get; set; }
}

#endregion

#region Cross-Provider Comparison Models

public class CrossProviderComparisonOptions
{
    public string Symbol { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public List<string>? Providers { get; set; }
    public string EventType { get; set; } = "trades";
}

public class CrossProviderComparisonResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime ComparisonTime { get; set; }
    public double OverallConsistencyScore { get; set; }
    public List<string> ProvidersCompared { get; set; } = new();
    public List<ProviderComparison> Comparisons { get; set; } = new();
    public List<DataDiscrepancy> Discrepancies { get; set; } = new();
}

public class CrossProviderComparisonResponse
{
    public DateTime ComparisonTime { get; set; }
    public double OverallConsistencyScore { get; set; }
    public List<string>? ProvidersCompared { get; set; }
    public List<ProviderComparison>? Comparisons { get; set; }
    public List<DataDiscrepancy>? Discrepancies { get; set; }
}

public class ProviderComparison
{
    public string Provider1 { get; set; } = string.Empty;
    public string Provider2 { get; set; } = string.Empty;
    public double ConsistencyScore { get; set; }
    public long RecordsMatched { get; set; }
    public long RecordsMismatched { get; set; }
    public long RecordsOnlyIn1 { get; set; }
    public long RecordsOnlyIn2 { get; set; }
}

public class DataDiscrepancy
{
    public DateTime Timestamp { get; set; }
    public string DiscrepancyType { get; set; } = string.Empty;
    public string Provider1 { get; set; } = string.Empty;
    public string Provider2 { get; set; } = string.Empty;
    public string? Value1 { get; set; }
    public string? Value2 { get; set; }
    public double Difference { get; set; }
}

#endregion

#region Latency Analysis Models

public class LatencyHistogramOptions
{
    public string? Provider { get; set; }
    public int BucketCount { get; set; } = 20;
    public TimeSpan? Period { get; set; }
}

public class LatencyHistogramResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Period { get; set; }
    public List<ProviderLatencyData> Providers { get; set; } = new();
}

public class LatencyHistogramResponse
{
    public string? Period { get; set; }
    public List<ProviderLatencyData>? Providers { get; set; }
}

public class ProviderLatencyData
{
    public string Provider { get; set; } = string.Empty;
    public List<LatencyBucket> Buckets { get; set; } = new();
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
}

public class LatencyBucket
{
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public long Count { get; set; }
    public double Percentage { get; set; }
}

public class LatencyStatisticsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ProviderLatencyStatistics> Statistics { get; set; } = new();
}

public class LatencyStatisticsResponse
{
    public List<ProviderLatencyStatistics>? Statistics { get; set; }
}

public class ProviderLatencyStatistics
{
    public string Provider { get; set; } = string.Empty;
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double StdDevMs { get; set; }
    public double P50Ms { get; set; }
    public double P90Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public long SampleCount { get; set; }
}

#endregion

#region Anomaly Detection Models

public class AnomalyDetectionOptions
{
    public string? Symbol { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public List<string>? AnomalyTypes { get; set; }
    public double SensitivityThreshold { get; set; } = 0.95;
}

public class AnomalyDetectionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime AnalysisTime { get; set; }
    public int TotalAnomalies { get; set; }
    public List<DataAnomaly> Anomalies { get; set; } = new();
    public AnomalySummary? Summary { get; set; }
}

public class AnomalyDetectionResponse
{
    public DateTime AnalysisTime { get; set; }
    public int TotalAnomalies { get; set; }
    public List<DataAnomaly>? Anomalies { get; set; }
    public AnomalySummary? Summary { get; set; }
}

public class DataAnomaly
{
    public string Symbol { get; set; } = string.Empty;
    public string AnomalyType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public double ConfidenceScore { get; set; }
}

public class AnomalySummary
{
    public int NegativePrices { get; set; }
    public int FutureTimestamps { get; set; }
    public int SequenceGaps { get; set; }
    public int OutOfOrder { get; set; }
    public int PriceSpikes { get; set; }
    public int VolumeSpikes { get; set; }
    public int StaleQuotes { get; set; }
}

#endregion

#region Quality Report Models

public class DataQualityReportOptions
{
    public string? Symbol { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool IncludeDetails { get; set; } = true;
}

public class DataQualityReportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime ReportTime { get; set; }
    public double OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public QualityMetrics? Metrics { get; set; }
    public List<SymbolQualityReport> SymbolReports { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class DataQualityReportResponse
{
    public DateTime ReportTime { get; set; }
    public double OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public QualityMetrics? Metrics { get; set; }
    public List<SymbolQualityReport>? SymbolReports { get; set; }
    public List<string>? Recommendations { get; set; }
}

public class QualityMetrics
{
    public double CompletenessScore { get; set; }
    public double IntegrityScore { get; set; }
    public double TimelinessScore { get; set; }
    public double AccuracyScore { get; set; }
    public double ConsistencyScore { get; set; }
    public long TotalRecords { get; set; }
    public long ValidRecords { get; set; }
    public long InvalidRecords { get; set; }
}

public class SymbolQualityReport
{
    public string Symbol { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public double CompletenessScore { get; set; }
    public double IntegrityScore { get; set; }
    public int SequenceGaps { get; set; }
    public int Anomalies { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class CompletenessAnalysisOptions
{
    public string? Symbol { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class CompletenessAnalysisResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double OverallCompleteness { get; set; }
    public int TradingDaysCovered { get; set; }
    public int TradingDaysExpected { get; set; }
    public List<SymbolCompleteness> SymbolCompleteness { get; set; } = new();
}

public class CompletenessAnalysisResponse
{
    public double OverallCompleteness { get; set; }
    public int TradingDaysCovered { get; set; }
    public int TradingDaysExpected { get; set; }
    public List<SymbolCompleteness>? SymbolCompleteness { get; set; }
}

public class SymbolCompleteness
{
    public string Symbol { get; set; } = string.Empty;
    public double CompletenessPercent { get; set; }
    public int DaysCovered { get; set; }
    public int DaysExpected { get; set; }
    public List<DateOnly> MissingDays { get; set; } = new();
}

#endregion

#region Throughput Analysis Models

public class ThroughputAnalysisOptions
{
    public string? Provider { get; set; }
    public TimeSpan? Period { get; set; }
    public int IntervalSeconds { get; set; } = 60;
}

public class ThroughputAnalysisResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double CurrentThroughput { get; set; }
    public double AverageThroughput { get; set; }
    public double PeakThroughput { get; set; }
    public long TotalEventsProcessed { get; set; }
    public List<ThroughputDataPoint> Timeline { get; set; } = new();
}

public class ThroughputAnalysisResponse
{
    public double CurrentThroughput { get; set; }
    public double AverageThroughput { get; set; }
    public double PeakThroughput { get; set; }
    public long TotalEventsProcessed { get; set; }
    public List<ThroughputDataPoint>? Timeline { get; set; }
}

public class ThroughputDataPoint
{
    public DateTime Timestamp { get; set; }
    public double EventsPerSecond { get; set; }
    public long TotalEvents { get; set; }
}

#endregion

#region Rate Limit Models

public class RateLimitStatusResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ProviderRateLimitStatus> Providers { get; set; } = new();
}

public class RateLimitStatusResponse
{
    public List<ProviderRateLimitStatus>? Providers { get; set; }
}

public class ProviderRateLimitStatus
{
    public string Provider { get; set; } = string.Empty;
    public int RequestsPerMinute { get; set; }
    public int RequestsUsed { get; set; }
    public int RequestsRemaining { get; set; }
    public DateTime? ResetTime { get; set; }
    public double UsagePercent { get; set; }
    public bool IsThrottled { get; set; }
    public string Status { get; set; } = string.Empty;
}

#endregion
