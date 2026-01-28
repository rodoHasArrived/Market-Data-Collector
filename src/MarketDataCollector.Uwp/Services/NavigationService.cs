using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Contracts;
using MarketDataCollector.Uwp.Views;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing navigation throughout the application.
/// Provides centralized navigation with history tracking and breadcrumb support.
/// Implements <see cref="INavigationService"/> for testability.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private static NavigationService? _instance;
    private static readonly object _lock = new();

    private Frame? _frame;
    private readonly Stack<Contracts.NavigationEntry> _navigationHistory = new();
    private readonly Dictionary<string, Type> _pageRegistry = new();

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
        _frame = frame;
    }

    /// <summary>
    /// Gets whether navigation can go back.
    /// </summary>
    public bool CanGoBack => _frame?.CanGoBack == true || _navigationHistory.Count > 0;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<Contracts.NavigationEventArgs>? Navigated;

    /// <summary>
    /// Navigates to a page by tag name.
    /// </summary>
    public bool NavigateTo(string pageTag, object? parameter = null)
    {
        if (_frame == null) return false;

        if (_pageRegistry.TryGetValue(pageTag, out var pageType))
        {
            var entry = new Contracts.NavigationEntry
            {
                PageTag = pageTag,
                Parameter = parameter,
                Timestamp = DateTime.UtcNow
            };
            _navigationHistory.Push(entry);

            var result = _frame.Navigate(pageType, parameter);

            if (result)
            {
                Navigated?.Invoke(this, new Contracts.NavigationEventArgs
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
        return _frame.Navigate(pageType, parameter);
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
    public IReadOnlyList<Contracts.NavigationEntry> GetBreadcrumbs()
    {
        return _navigationHistory.ToArray();
    }

    /// <summary>
    /// Registers all navigable pages.
    /// </summary>
    private void RegisterPages()
    {
        // Primary navigation
        _pageRegistry["Dashboard"] = typeof(DashboardPage);
        _pageRegistry["Watchlist"] = typeof(WatchlistPage);

        // Data Sources
        _pageRegistry["Provider"] = typeof(ProviderPage);
        _pageRegistry["DataSources"] = typeof(DataSourcesPage);

        // Data Management
        _pageRegistry["LiveData"] = typeof(LiveDataViewerPage);
        _pageRegistry["Symbols"] = typeof(SymbolsPage);
        _pageRegistry["SymbolMapping"] = typeof(SymbolMappingPage);
        _pageRegistry["SymbolStorage"] = typeof(SymbolStoragePage);
        _pageRegistry["Storage"] = typeof(StoragePage);
        _pageRegistry["Backfill"] = typeof(BackfillPage);
        _pageRegistry["PortfolioImport"] = typeof(PortfolioImportPage);
        _pageRegistry["IndexSubscription"] = typeof(IndexSubscriptionPage);
        _pageRegistry["Schedules"] = typeof(ScheduleManagerPage);

        // Monitoring
        _pageRegistry["DataQuality"] = typeof(DataQualityPage);
        _pageRegistry["CollectionSessions"] = typeof(CollectionSessionPage);
        _pageRegistry["ArchiveHealth"] = typeof(ArchiveHealthPage);
        _pageRegistry["ServiceManager"] = typeof(ServiceManagerPage);
        _pageRegistry["SystemHealth"] = typeof(SystemHealthPage);
        _pageRegistry["Diagnostics"] = typeof(DiagnosticsPage);

        // Tools
        _pageRegistry["DataExport"] = typeof(DataExportPage);
        _pageRegistry["AnalysisExport"] = typeof(AnalysisExportPage);
        _pageRegistry["AnalysisExportWizard"] = typeof(AnalysisExportWizardPage);
        _pageRegistry["EventReplay"] = typeof(EventReplayPage);
        _pageRegistry["PackageManager"] = typeof(PackageManagerPage);
        _pageRegistry["TradingHours"] = typeof(TradingHoursPage);

        // Analytics & Visualization
        _pageRegistry["AdvancedAnalytics"] = typeof(AdvancedAnalyticsPage);
        _pageRegistry["Charts"] = typeof(ChartingPage);
        _pageRegistry["OrderBook"] = typeof(OrderBookPage);
        _pageRegistry["DataCalendar"] = typeof(DataCalendarPage);

        // Storage & Maintenance
        _pageRegistry["StorageOptimization"] = typeof(StorageOptimizationPage);
        _pageRegistry["RetentionAssurance"] = typeof(RetentionAssurancePage);
        _pageRegistry["AdminMaintenance"] = typeof(AdminMaintenancePage);

        // Integrations
        _pageRegistry["LeanIntegration"] = typeof(LeanIntegrationPage);
        _pageRegistry["MessagingHub"] = typeof(MessagingHubPage);

        // Workspaces & Notifications
        _pageRegistry["Workspaces"] = typeof(WorkspacePage);
        _pageRegistry["NotificationCenter"] = typeof(NotificationCenterPage);

        // Support & Setup
        _pageRegistry["Help"] = typeof(HelpPage);
        _pageRegistry["Welcome"] = typeof(WelcomePage);
        _pageRegistry["Settings"] = typeof(SettingsPage);
        _pageRegistry["KeyboardShortcuts"] = typeof(KeyboardShortcutsPage);
        _pageRegistry["SetupWizard"] = typeof(SetupWizardPage);
    }

    /// <summary>
    /// Gets all registered page tags.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredPages() => _pageRegistry.Keys;

    /// <summary>
    /// Checks if a page tag is registered.
    /// </summary>
    public bool IsPageRegistered(string pageTag) => _pageRegistry.ContainsKey(pageTag);
}
