using System;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// UWP implementation of advanced analytics service.
/// Inherits all API delegation from the shared base class.
/// </summary>
public sealed class AdvancedAnalyticsService : AdvancedAnalyticsServiceBase
{
    private static AdvancedAnalyticsService? _instance;
    private static readonly object _lock = new();

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

    private AdvancedAnalyticsService() { }
}
