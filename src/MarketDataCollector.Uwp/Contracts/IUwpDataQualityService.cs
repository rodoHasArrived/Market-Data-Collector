using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Views;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for UWP-specific data quality monitoring and analysis.
/// </summary>
public interface IUwpDataQualityService
{
    Task<DataQualitySummary?> GetQualitySummaryAsync(CancellationToken ct = default);
    Task<List<QualityScoreEntry>?> GetQualityScoresAsync(double? minScore = null, CancellationToken ct = default);
    Task<SymbolQualityReport?> GetSymbolQualityAsync(string symbol, CancellationToken ct = default);
    Task<List<QualityAlert>?> GetQualityAlertsAsync(string? severity = null, CancellationToken ct = default);
    Task<bool> AcknowledgeAlertAsync(string alertId, CancellationToken ct = default);
    Task<List<SourceRanking>?> GetSourceRankingsAsync(string symbol, CancellationToken ct = default);
    Task<QualityTrendData?> GetQualityTrendsAsync(string? timeWindow = "7d", CancellationToken ct = default);
    Task<List<AnomalyEvent>?> GetAnomaliesAsync(string? type = null, CancellationToken ct = default);
    Task<QualityCheckResult?> RunQualityCheckAsync(string path, CancellationToken ct = default);
    Task<List<DataGapInfo>> GetDataGapsAsync(string symbol, CancellationToken ct = default);
    Task<IntegrityVerificationResult> VerifySymbolIntegrityAsync(string symbol, CancellationToken ct = default);
}
