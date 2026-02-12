using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MarketDataCollector.Ui.Services.Contracts;
using MarketDataCollector.Ui.Services.Services;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Views;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF-specific navigation service that extends <see cref="NavigationServiceBase"/> with
/// WPF Frame navigation and DI-aware page creation.
/// Implements <see cref="INavigationService"/> with singleton pattern.
/// Phase 6C.2: Shared base class extracts page registry, history tracking, and breadcrumb logic.
/// </summary>
public sealed class NavigationService : NavigationServiceBase, INavigationService
{
    private static NavigationService? _instance;
    private static readonly object _singletonLock = new();

    private Frame? _frame;
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the singleton instance of the NavigationService.
    /// </summary>
    public static NavigationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_singletonLock)
                {
                    _instance ??= new NavigationService();
                }
            }
            return _instance;
        }
    }

    /// <inheritdoc />
    public override bool CanGoBack => _frame?.CanGoBack ?? false;

    private NavigationService()
    {
    }

    /// <summary>
    /// Sets the DI service provider used to resolve page instances.
    /// Called once during application startup after the DI container is built.
    /// </summary>
    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Initializes the navigation service with the main frame.
    /// </summary>
    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    /// <inheritdoc />
    protected override void RegisterAllPages()
    {
        // Primary navigation (2 pages)
        RegisterPage("Dashboard", typeof(DashboardPage));
        RegisterPage("Watchlist", typeof(WatchlistPage));

        // Data Sources (3 pages)
        RegisterPage("Provider", typeof(ProviderPage));
        RegisterPage("ProviderHealth", typeof(ProviderHealthPage));
        RegisterPage("DataSources", typeof(DataSourcesPage));

        // Data Management (9 pages)
        RegisterPage("LiveData", typeof(LiveDataViewerPage));
        RegisterPage("DataBrowser", typeof(DataBrowserPage));
        RegisterPage("Symbols", typeof(SymbolsPage));
        RegisterPage("SymbolMapping", typeof(SymbolMappingPage));
        RegisterPage("SymbolStorage", typeof(SymbolStoragePage));
        RegisterPage("Storage", typeof(StoragePage));
        RegisterPage("Backfill", typeof(BackfillPage));
        RegisterPage("PortfolioImport", typeof(PortfolioImportPage));
        RegisterPage("IndexSubscription", typeof(IndexSubscriptionPage));
        RegisterPage("Schedules", typeof(ScheduleManagerPage));

        // Monitoring (6 pages)
        RegisterPage("DataQuality", typeof(DataQualityPage));
        RegisterPage("CollectionSessions", typeof(CollectionSessionPage));
        RegisterPage("ArchiveHealth", typeof(ArchiveHealthPage));
        RegisterPage("ServiceManager", typeof(ServiceManagerPage));
        RegisterPage("SystemHealth", typeof(SystemHealthPage));
        RegisterPage("Diagnostics", typeof(DiagnosticsPage));

        // Tools (10 pages)
        RegisterPage("DataExport", typeof(DataExportPage));
        RegisterPage("DataSampling", typeof(DataSamplingPage));
        RegisterPage("TimeSeriesAlignment", typeof(TimeSeriesAlignmentPage));
        RegisterPage("ExportPresets", typeof(ExportPresetsPage));
        RegisterPage("AnalysisExport", typeof(AnalysisExportPage));
        RegisterPage("AnalysisExportWizard", typeof(AnalysisExportWizardPage));
        RegisterPage("EventReplay", typeof(EventReplayPage));
        RegisterPage("PackageManager", typeof(PackageManagerPage));
        RegisterPage("TradingHours", typeof(TradingHoursPage));

        // Analytics & Visualization (4 pages)
        RegisterPage("AdvancedAnalytics", typeof(AdvancedAnalyticsPage));
        RegisterPage("Charts", typeof(ChartingPage));
        RegisterPage("OrderBook", typeof(OrderBookPage));
        RegisterPage("DataCalendar", typeof(DataCalendarPage));

        // Storage & Maintenance (3 pages)
        RegisterPage("StorageOptimization", typeof(StorageOptimizationPage));
        RegisterPage("RetentionAssurance", typeof(RetentionAssurancePage));
        RegisterPage("AdminMaintenance", typeof(AdminMaintenancePage));

        // Integrations (2 pages)
        RegisterPage("LeanIntegration", typeof(LeanIntegrationPage));
        RegisterPage("MessagingHub", typeof(MessagingHubPage));

        // Workspaces & Notifications (2 pages)
        RegisterPage("Workspaces", typeof(WorkspacePage));
        RegisterPage("NotificationCenter", typeof(NotificationCenterPage));

        // Support & Setup (6 pages)
        RegisterPage("Help", typeof(HelpPage));
        RegisterPage("Welcome", typeof(WelcomePage));
        RegisterPage("Settings", typeof(SettingsPage));
        RegisterPage("KeyboardShortcuts", typeof(KeyboardShortcutsPage));
        RegisterPage("SetupWizard", typeof(SetupWizardPage));

        // Activity Log (1 page)
        RegisterPage("ActivityLog", typeof(ActivityLogPage));
    }

    /// <inheritdoc />
    protected override bool NavigateToPageCore(Type pageType, object? parameter)
    {
        if (_frame == null) return false;

        var page = CreatePage(pageType);

        if (parameter != null && page is Page wpfPage && wpfPage.DataContext != null)
        {
            var parameterProperty = wpfPage.DataContext.GetType().GetProperty("Parameter");
            parameterProperty?.SetValue(wpfPage.DataContext, parameter);
        }

        return _frame.Navigate(page);
    }

    /// <inheritdoc />
    protected override void GoBackCore()
    {
        if (_frame?.CanGoBack ?? false)
        {
            _frame.GoBack();
        }
    }

    /// <inheritdoc />
    protected override void ClearHistoryCore()
    {
        while (_frame?.CanGoBack ?? false)
        {
            _frame.RemoveBackEntry();
        }
    }

    /// <summary>
    /// Creates a page instance using the DI container if available, falling back to Activator.
    /// </summary>
    private object? CreatePage(Type pageType)
    {
        if (_serviceProvider != null)
        {
            return _serviceProvider.GetService(pageType) ?? ActivatorUtilities.CreateInstance(_serviceProvider, pageType);
        }

        return Activator.CreateInstance(pageType);
    }
}
