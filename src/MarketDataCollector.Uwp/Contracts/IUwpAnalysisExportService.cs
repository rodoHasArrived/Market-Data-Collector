using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for UWP-specific analysis data export operations.
/// </summary>
public interface IUwpAnalysisExportService
{
    event EventHandler<ExportProgressEventArgs>? ProgressChanged;

    Task<AnalysisExportResult> ExportAsync(AnalysisExportOptions options, CancellationToken ct = default);
    Task<ExportFormatsResult> GetAvailableFormatsAsync(CancellationToken ct = default);
    Task<List<AggregationOption>> GetAggregationOptionsAsync(CancellationToken ct = default);
    Task<QualityReportResult> GenerateQualityReportAsync(QualityReportOptions options, CancellationToken ct = default);
    Task<AnalysisExportResult> ExportOrderFlowAsync(OrderFlowExportOptions options, CancellationToken ct = default);
    Task<AnalysisExportResult> ExportIntegrityEventsAsync(IntegrityExportOptions options, CancellationToken ct = default);
    Task<ResearchPackageResult> CreateResearchPackageAsync(ResearchPackageOptions options, CancellationToken ct = default);
    Task<List<ExportTemplate>> GetExportTemplatesAsync(CancellationToken ct = default);
}
