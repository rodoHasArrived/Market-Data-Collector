using System;
using System.Collections.Generic;
using System.Windows.Controls;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Views;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing navigation throughout the application.
/// Provides centralized navigation with history tracking and breadcrumb support.
/// Uses WPF's System.Windows.Controls.Frame for navigation.
/// Implements <see cref="INavigationService"/> for testability.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private static NavigationService? _instance;
    private static readonly object _lock = new();

    private Frame? _frame;
    private readonly Stack<NavigationEntry> _navigationHistory = new();
    private readonly Dictionary<string, Type> _pageRegistry = new();

    /// <summary>
    /// Gets the singleton instance of the NavigationService.
    /// </summary>
    public static NavigationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new NavigationService();
                }
            }
            return _instance;
        }
    }

    private NavigationService()
    {
        RegisterPages();
    }

    /// <summary>
    /// Initializes the navigation service with the main frame.
    /// </summary>
    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    /// <summary>
    /// Gets whether navigation can go back.
    /// </summary>
    public bool CanGoBack => _frame?.CanGoBack == true;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<NavigationEventArgs>? Navigated;

    /// <summary>
    /// Navigates to a page by tag name.
    /// </summary>
    public bool NavigateTo(string pageTag, object? parameter = null)
    {
        if (_frame == null) return false;

        if (_pageRegistry.TryGetValue(pageTag, out var pageType))
        {
            bool result;
            if (parameter != null)
            {
                // Create page instance and pass parameter
                var page = Activator.CreateInstance(pageType);
                if (page is Page wpfPage && wpfPage.DataContext != null)
                {
                    // If the page has a ViewModel with a Parameter property, set it
                    var parameterProperty = wpfPage.DataContext.GetType().GetProperty("Parameter");
                    parameterProperty?.SetValue(wpfPage.DataContext, parameter);
                }
                result = _frame.Navigate(page);
            }
            else
            {
                result = _frame.Navigate(Activator.CreateInstance(pageType));
            }

            if (result)
            {
                // Only push to history after successful navigation
                var entry = new NavigationEntry
                {
                    PageTag = pageTag,
                    Parameter = parameter,
                    Timestamp = DateTime.UtcNow
                };
                _navigationHistory.Push(entry);

                Navigated?.Invoke(this, new NavigationEventArgs
                {
                    PageTag = pageTag,
                    Parameter = parameter
                });
            }

            return result;
        }

        System.Diagnostics.Debug.WriteLine($"[NavigationService] Unknown page tag: {pageTag}");
        return false;
    }

    /// <summary>
    /// Navigates to a page type directly.
    /// </summary>
    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        if (_frame == null) return false;

        var page = Activator.CreateInstance(pageType);
        return _frame.Navigate(page);
    }

    /// <summary>
    /// Navigates back.
    /// </summary>
    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
        {
            _frame.GoBack();
            if (_navigationHistory.Count > 0)
            {
                _navigationHistory.Pop();
            }
        }
    }

    /// <summary>
    /// Gets the page type for a given tag.
    /// </summary>
    public Type? GetPageType(string pageTag)
    {
        return _pageRegistry.TryGetValue(pageTag, out var pageType) ? pageType : null;
    }

    /// <summary>
    /// Gets navigation breadcrumbs.
    /// </summary>
    public IReadOnlyList<NavigationEntry> GetBreadcrumbs()
    {
        return _navigationHistory.ToArray();
    }

    /// <summary>
    /// Registers all navigable pages (47 pages).
    /// </summary>
    private void RegisterPages()
    {
        // Primary navigation (2 pages)
        _pageRegistry["Dashboard"] = typeof(DashboardPage);
        _pageRegistry["Watchlist"] = typeof(WatchlistPage);

        // Data Sources (3 pages)
        _pageRegistry["Provider"] = typeof(ProviderPage);
        _pageRegistry["ProviderHealth"] = typeof(ProviderHealthPage);
        _pageRegistry["DataSources"] = typeof(DataSourcesPage);

        // Data Management (9 pages)
        _pageRegistry["LiveData"] = typeof(LiveDataViewerPage);
        _pageRegistry["DataBrowser"] = typeof(DataBrowserPage);
        _pageRegistry["Symbols"] = typeof(SymbolsPage);
        _pageRegistry["SymbolMapping"] = typeof(SymbolMappingPage);
        _pageRegistry["SymbolStorage"] = typeof(SymbolStoragePage);
        _pageRegistry["Storage"] = typeof(StoragePage);
        _pageRegistry["Backfill"] = typeof(BackfillPage);
        _pageRegistry["PortfolioImport"] = typeof(PortfolioImportPage);
        _pageRegistry["IndexSubscription"] = typeof(IndexSubscriptionPage);
        _pageRegistry["Schedules"] = typeof(ScheduleManagerPage);

        // Monitoring (6 pages)
        _pageRegistry["DataQuality"] = typeof(DataQualityPage);
        _pageRegistry["CollectionSessions"] = typeof(CollectionSessionPage);
        _pageRegistry["ArchiveHealth"] = typeof(ArchiveHealthPage);
        _pageRegistry["ServiceManager"] = typeof(ServiceManagerPage);
        _pageRegistry["SystemHealth"] = typeof(SystemHealthPage);
        _pageRegistry["Diagnostics"] = typeof(DiagnosticsPage);

        // Tools (10 pages)
        _pageRegistry["DataExport"] = typeof(DataExportPage);
        _pageRegistry["DataSampling"] = typeof(DataSamplingPage);
        _pageRegistry["TimeSeriesAlignment"] = typeof(TimeSeriesAlignmentPage);
        _pageRegistry["ExportPresets"] = typeof(ExportPresetsPage);
        _pageRegistry["AnalysisExport"] = typeof(AnalysisExportPage);
        _pageRegistry["AnalysisExportWizard"] = typeof(AnalysisExportWizardPage);
        _pageRegistry["EventReplay"] = typeof(EventReplayPage);
        _pageRegistry["PackageManager"] = typeof(PackageManagerPage);
        _pageRegistry["TradingHours"] = typeof(TradingHoursPage);

        // Analytics & Visualization (4 pages)
        _pageRegistry["AdvancedAnalytics"] = typeof(AdvancedAnalyticsPage);
        _pageRegistry["Charts"] = typeof(ChartingPage);
        _pageRegistry["OrderBook"] = typeof(OrderBookPage);
        _pageRegistry["DataCalendar"] = typeof(DataCalendarPage);

        // Storage & Maintenance (3 pages)
        _pageRegistry["StorageOptimization"] = typeof(StorageOptimizationPage);
        _pageRegistry["RetentionAssurance"] = typeof(RetentionAssurancePage);
        _pageRegistry["AdminMaintenance"] = typeof(AdminMaintenancePage);

        // Integrations (2 pages)
        _pageRegistry["LeanIntegration"] = typeof(LeanIntegrationPage);
        _pageRegistry["MessagingHub"] = typeof(MessagingHubPage);

        // Workspaces & Notifications (2 pages)
        _pageRegistry["Workspaces"] = typeof(WorkspacePage);
        _pageRegistry["NotificationCenter"] = typeof(NotificationCenterPage);

        // Support & Setup (6 pages)
        _pageRegistry["Help"] = typeof(HelpPage);
        _pageRegistry["Welcome"] = typeof(WelcomePage);
        _pageRegistry["Settings"] = typeof(SettingsPage);
        _pageRegistry["KeyboardShortcuts"] = typeof(KeyboardShortcutsPage);
        _pageRegistry["SetupWizard"] = typeof(SetupWizardPage);

        // Activity Log (1 page)
        _pageRegistry["ActivityLog"] = typeof(ActivityLogPage);
    }

    /// <summary>
    /// Gets all registered page tags.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredPages() => _pageRegistry.Keys;

    /// <summary>
    /// Checks if a page tag is registered.
    /// </summary>
    public bool IsPageRegistered(string pageTag) => _pageRegistry.ContainsKey(pageTag);

    /// <summary>
    /// Clears navigation history.
    /// </summary>
    public void ClearHistory()
    {
        _navigationHistory.Clear();
        while (_frame?.CanGoBack == true)
        {
            _frame.RemoveBackEntry();
        }
    }

    /// <summary>
    /// Gets the current page tag.
    /// </summary>
    public string? GetCurrentPageTag()
    {
        if (_navigationHistory.Count > 0)
        {
            return _navigationHistory.Peek().PageTag;
        }
        return null;
    }
}
