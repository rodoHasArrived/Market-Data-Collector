using System;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF implementation of admin maintenance service.
/// Inherits all API delegation from the shared base class.
/// </summary>
public sealed class AdminMaintenanceService : AdminMaintenanceServiceBase
{
    private static readonly Lazy<AdminMaintenanceService> _instance = new(() => new AdminMaintenanceService());

    public static AdminMaintenanceService Instance => _instance.Value;

    private AdminMaintenanceService() { }
}
