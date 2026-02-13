using System;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// UWP implementation of admin maintenance service.
/// Inherits all API delegation from the shared base class.
/// </summary>
public sealed class AdminMaintenanceService : AdminMaintenanceServiceBase
{
    private static AdminMaintenanceService? _instance;
    private static readonly object _lock = new();

    public static AdminMaintenanceService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AdminMaintenanceService();
                }
            }
            return _instance;
        }
    }

    private AdminMaintenanceService() { }
}
