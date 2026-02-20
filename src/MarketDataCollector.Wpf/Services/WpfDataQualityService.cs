using System;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF platform-specific data quality service.
/// Extends <see cref="DataQualityServiceBase"/> using <see cref="ApiClientService"/> for API access.
/// Part of Phase 2 service extraction.
/// </summary>
public sealed class WpfDataQualityService : DataQualityServiceBase
{
    private static readonly Lazy<WpfDataQualityService> _instance = new(() => new WpfDataQualityService());
    public static WpfDataQualityService Instance => _instance.Value;

    private WpfDataQualityService() { }

    protected override Task<T?> GetAsync<T>(string endpoint, CancellationToken ct) where T : class
        => ApiClientService.Instance.GetAsync<T>(endpoint, ct);

    protected override Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken ct) where T : class
        => ApiClientService.Instance.PostAsync<T>(endpoint, body, ct);

    protected override async Task<(bool Success, T? Data)> PostWithResponseAsync<T>(string endpoint, object? body, CancellationToken ct) where T : class
    {
        var response = await ApiClientService.Instance.PostWithResponseAsync<T>(endpoint, body, ct);
        return (response.Success, response.Data);
    }
}
