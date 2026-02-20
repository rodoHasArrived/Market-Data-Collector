using System;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF platform-specific analysis export service.
/// Extends <see cref="AnalysisExportServiceBase"/> using <see cref="ApiClientService"/> for API access.
/// Part of Phase 2 service extraction.
/// </summary>
public sealed class WpfAnalysisExportService : AnalysisExportServiceBase
{
    private static readonly Lazy<WpfAnalysisExportService> _instance = new(() => new WpfAnalysisExportService());
    private readonly ApiClientService _apiClient;

    public static WpfAnalysisExportService Instance => _instance.Value;

    private WpfAnalysisExportService()
    {
        _apiClient = ApiClientService.Instance;
    }

    protected override async Task<(bool Success, string? ErrorMessage, T? Data)> PostApiAsync<T>(string endpoint, object body, CancellationToken ct) where T : class
    {
        var response = await _apiClient.PostWithResponseAsync<T>(endpoint, body, ct);
        return (response.Success, response.ErrorMessage, response.Data);
    }

    protected override async Task<(bool Success, string? ErrorMessage, T? Data)> GetApiAsync<T>(string endpoint, CancellationToken ct) where T : class
    {
        var response = await _apiClient.GetWithResponseAsync<T>(endpoint, ct);
        return (response.Success, response.ErrorMessage, response.Data);
    }
}
