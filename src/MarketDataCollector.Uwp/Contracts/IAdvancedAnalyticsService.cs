using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for advanced analytics including gap analysis, cross-provider comparison,
/// latency histograms, anomaly detection, and detailed quality reports.
/// Types are now sourced from the shared MarketDataCollector.Ui.Services library.
/// </summary>
public interface IAdvancedAnalyticsService
{
    Task<GapAnalysisResult> AnalyzeGapsAsync(GapAnalysisOptions options, CancellationToken ct = default);
    Task<AnalyticsGapRepairResult> RepairGapsAsync(GapRepairOptions options, CancellationToken ct = default);
    Task<CrossProviderComparisonResult> CompareProvidersAsync(CrossProviderComparisonOptions options, CancellationToken ct = default);
    Task<LatencyHistogramResult> GetLatencyHistogramAsync(LatencyHistogramOptions? options = null, CancellationToken ct = default);
    Task<LatencyStatisticsResult> GetLatencyStatisticsAsync(string? provider = null, CancellationToken ct = default);
    Task<AnomalyDetectionResult> DetectAnomaliesAsync(AnomalyDetectionOptions options, CancellationToken ct = default);
    Task<DataQualityReportResult> GetQualityReportAsync(DataQualityReportOptions options, CancellationToken ct = default);
    Task<CompletenessAnalysisResult> AnalyzeCompletenessAsync(CompletenessAnalysisOptions options, CancellationToken ct = default);
    Task<ThroughputAnalysisResult> GetThroughputAnalysisAsync(ThroughputAnalysisOptions? options = null, CancellationToken ct = default);
    Task<RateLimitStatusResult> GetRateLimitStatusAsync(CancellationToken ct = default);
}
