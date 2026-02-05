using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for advanced analytics including gap analysis, cross-provider comparison,
/// latency histograms, anomaly detection, and detailed quality reports.
/// </summary>
public sealed class AdvancedAnalyticsService
{
    private static readonly Lazy<AdvancedAnalyticsService> _instance = new(() => new AdvancedAnalyticsService());
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
    private string _baseUrl = "http://localhost:8080";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AdvancedAnalyticsService Instance => _instance.Value;

    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value;
    }

    private AdvancedAnalyticsService() { }

    #region Gap Analysis

    public async Task<GapAnalysisResult> AnalyzeGapsAsync(GapAnalysisOptions options, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/analytics/gaps", options, _jsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<GapAnalysisResponse>(_jsonOptions, ct);
                return new GapAnalysisResult
                {
                    Success = true,
                    AnalysisTime = data?.AnalysisTime ?? DateTime.UtcNow,
                    TotalGaps = data?.TotalGaps ?? 0,
                    TotalGapDuration = data?.TotalGapDuration ?? TimeSpan.Zero,
                    Gaps = data?.Gaps?.ToList() ?? new List<DataGap>()
                };
            }
            return new GapAnalysisResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new GapAnalysisResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<GapRepairResult> RepairGapsAsync(GapRepairOptions options, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/analytics/gaps/repair", options, _jsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<GapRepairResponse>(_jsonOptions, ct);
                return new GapRepairResult
                {
                    Success = true,
                    GapsAttempted = data?.GapsAttempted ?? 0,
                    GapsRepaired = data?.GapsRepaired ?? 0,
                    RecordsRecovered = data?.RecordsRecovered ?? 0
                };
            }
            return new GapRepairResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new GapRepairResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Cross-Provider Comparison

    public async Task<CrossProviderComparisonResult> CompareProvidersAsync(CrossProviderComparisonOptions options, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/analytics/compare", options, _jsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<CrossProviderComparisonResponse>(_jsonOptions, ct);
                return new CrossProviderComparisonResult
                {
                    Success = true,
                    OverallConsistencyScore = data?.OverallConsistencyScore ?? 0,
                    Discrepancies = data?.Discrepancies?.ToList() ?? new List<DataDiscrepancy>()
                };
            }
            return new CrossProviderComparisonResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new CrossProviderComparisonResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Latency Analysis

    public async Task<LatencyHistogramResult> GetLatencyHistogramAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/analytics/latency", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<LatencyHistogramResponse>(_jsonOptions, ct);
                return new LatencyHistogramResult
                {
                    Success = true,
                    Providers = data?.Providers?.ToList() ?? new List<ProviderLatencyData>()
                };
            }
            return new LatencyHistogramResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new LatencyHistogramResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Quality Reports

    public async Task<DataQualityReportResult> GetQualityReportAsync(DataQualityReportOptions options, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/analytics/quality-report", options, _jsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<DataQualityReportResponse>(_jsonOptions, ct);
                return new DataQualityReportResult
                {
                    Success = true,
                    OverallScore = data?.OverallScore ?? 0,
                    Grade = data?.Grade ?? "N/A",
                    Metrics = data?.Metrics,
                    SymbolReports = data?.SymbolReports?.ToList() ?? new List<SymbolQualityReport>(),
                    Recommendations = data?.Recommendations?.ToList() ?? new List<string>()
                };
            }
            return new DataQualityReportResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new DataQualityReportResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Rate Limits

    public async Task<RateLimitStatusResult> GetRateLimitStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/analytics/rate-limits", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<RateLimitStatusResponse>(_jsonOptions, ct);
                return new RateLimitStatusResult
                {
                    Success = true,
                    Providers = data?.Providers?.ToList() ?? new List<ProviderRateLimitStatus>()
                };
            }
            return new RateLimitStatusResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new RateLimitStatusResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Symbols

    public async Task<SymbolsResult> GetAllSymbolsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/symbols", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<SymbolsResponse>(_jsonOptions, ct);
                return new SymbolsResult
                {
                    Success = true,
                    Symbols = data?.Symbols?.ToList() ?? new List<SymbolInfo>()
                };
            }
            return new SymbolsResult { Success = false, Error = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new SymbolsResult { Success = false, Error = ex.Message };
        }
    }

    #endregion
}

#region Gap Analysis Models

public class GapAnalysisOptions
{
    public string? Symbol { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
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
}

public class GapAnalysisResponse
{
    public DateTime AnalysisTime { get; set; }
    public int TotalGaps { get; set; }
    public TimeSpan TotalGapDuration { get; set; }
    public List<DataGap>? Gaps { get; set; }
}

public class DataGap
{
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsRepairable { get; set; }
}

public class GapRepairOptions
{
    public bool UseAlternativeProviders { get; set; } = true;
}

public class GapRepairResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int GapsAttempted { get; set; }
    public int GapsRepaired { get; set; }
    public long RecordsRecovered { get; set; }
}

public class GapRepairResponse
{
    public int GapsAttempted { get; set; }
    public int GapsRepaired { get; set; }
    public long RecordsRecovered { get; set; }
}

#endregion

#region Cross-Provider Models

public class CrossProviderComparisonOptions
{
    public string Symbol { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

public class CrossProviderComparisonResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double OverallConsistencyScore { get; set; }
    public List<DataDiscrepancy> Discrepancies { get; set; } = new();
}

public class CrossProviderComparisonResponse
{
    public double OverallConsistencyScore { get; set; }
    public List<DataDiscrepancy>? Discrepancies { get; set; }
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

#region Latency Models

public class LatencyHistogramResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ProviderLatencyData> Providers { get; set; } = new();
}

public class LatencyHistogramResponse
{
    public List<ProviderLatencyData>? Providers { get; set; }
}

public class ProviderLatencyData
{
    public string Provider { get; set; } = string.Empty;
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
}

#endregion

#region Quality Report Models

public class DataQualityReportOptions
{
    public bool IncludeDetails { get; set; } = true;
}

public class DataQualityReportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public QualityMetrics? Metrics { get; set; }
    public List<SymbolQualityReport> SymbolReports { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class DataQualityReportResponse
{
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
}

public class SymbolQualityReport
{
    public string Symbol { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public double CompletenessScore { get; set; }
    public double IntegrityScore { get; set; }
    public List<string> Issues { get; set; } = new();
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
    public double UsagePercent { get; set; }
    public bool IsThrottled { get; set; }
}

#endregion

#region Symbol Models

public class SymbolsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<SymbolInfo> Symbols { get; set; } = new();
}

public class SymbolsResponse
{
    public List<SymbolInfo>? Symbols { get; set; }
}

public class SymbolInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
}

#endregion
