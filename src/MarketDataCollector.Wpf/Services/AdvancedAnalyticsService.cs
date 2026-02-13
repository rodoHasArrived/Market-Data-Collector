using System;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF implementation of advanced analytics service.
/// Inherits all API delegation from the shared base class.
/// </summary>
public sealed class AdvancedAnalyticsService : AdvancedAnalyticsServiceBase
{
    private static readonly Lazy<AdvancedAnalyticsService> _instance = new(() => new AdvancedAnalyticsService());

    public static AdvancedAnalyticsService Instance => _instance.Value;

    private AdvancedAnalyticsService() { }
}
