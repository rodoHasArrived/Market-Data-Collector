using System;
using System.Collections.Generic;

namespace MarketDataCollector.Ui.Services;

// =====================================================================================
// Advanced Analytics DTOs — shared between WPF and UWP desktop applications.
// Uses the UWP model (superset) as the canonical definition.
// =====================================================================================

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
    public List<AnalyticsDataGap> Gaps { get; set; } = new();
    public List<SymbolGapSummary> SymbolSummaries { get; set; } = new();
}

public class GapAnalysisResponse
{
    public DateTime AnalysisTime { get; set; }
    public int TotalGaps { get; set; }
    public TimeSpan TotalGapDuration { get; set; }
    public List<AnalyticsDataGap>? Gaps { get; set; }
    public List<SymbolGapSummary>? SymbolSummaries { get; set; }
}

public class AnalyticsDataGap
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
    public bool DryRun { get; set; }
}

public class AnalyticsGapRepairResult
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
    public AnalyticsQualityMetrics? Metrics { get; set; }
    public List<AnalyticsSymbolQualityReport> SymbolReports { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class DataQualityReportResponse
{
    public DateTime ReportTime { get; set; }
    public double OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public AnalyticsQualityMetrics? Metrics { get; set; }
    public List<AnalyticsSymbolQualityReport>? SymbolReports { get; set; }
    public List<string>? Recommendations { get; set; }
}

public class AnalyticsQualityMetrics
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

public class AnalyticsSymbolQualityReport
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

#region Symbol Models

public class AnalyticsSymbolsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<AnalyticsSymbolInfo> Symbols { get; set; } = new();
}

public class AnalyticsSymbolsResponse
{
    public List<AnalyticsSymbolInfo>? Symbols { get; set; }
}

public class AnalyticsSymbolInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
}

#endregion
