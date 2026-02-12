using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;
using SysNavigation = System.Windows.Navigation;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Main page with workspace-based navigation sidebar (Monitor, Collect, Storage, Quality, Settings)
/// and command palette (Ctrl+K). Serves as the shell for all application content.
/// </summary>
public partial class MainPage : Page
{
    private readonly NavigationService _navigationService;
    private readonly ConnectionService _connectionService;
    private readonly SearchService _searchService;
    private readonly MessagingService _messagingService;
    private bool _commandPaletteOpen;

    /// <summary>
    /// All navigation ListBoxes, used to clear selection across workspaces.
    /// </summary>
    private ListBox[] AllNavLists => new[]
    {
        MonitorNavList, CollectNavList, StorageNavList, QualityNavList, SettingsNavList
    };

    public MainPage(
        NavigationService navigationService,
        ConnectionService connectionService,
        SearchService searchService,
        MessagingService messagingService)
    {
        InitializeComponent();

        _navigationService = navigationService;
        _connectionService = connectionService;
        _searchService = searchService;
        _messagingService = messagingService;

        // Subscribe to connection state changes
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        // Subscribe to messaging for page updates
        _messagingService.MessageReceived += OnMessageReceived;

        // Register Ctrl+K for command palette via PreviewKeyDown
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize navigation service with the content frame
        _navigationService.Initialize(ContentFrame);

        // Check for first-run wizard
        if (App.IsFirstRun)
        {
            _navigationService.NavigateTo("SetupWizard");
        }
        else
        {
            // Set selected index first (before navigation to avoid triggering SelectionChanged)
            MonitorNavList.SelectedIndex = 0;
            // Default to Dashboard
            _navigationService.NavigateTo("Dashboard");
        }

        // Update connection status display
        UpdateConnectionStatus(_connectionService.State);

        // Update back button visibility
        UpdateBackButtonVisibility();
    }

    #region Workspace Navigation Handlers

    private void OnMonitorNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MonitorNavList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            ClearOtherSelections(MonitorNavList);
            NavigateToPage(pageTag);
        }
    }

    private void OnCollectNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CollectNavList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            ClearOtherSelections(CollectNavList);
            NavigateToPage(pageTag);
        }
    }

    private void OnStorageNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StorageNavList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            ClearOtherSelections(StorageNavList);
            NavigateToPage(pageTag);
        }
    }

    private void OnQualityNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QualityNavList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            ClearOtherSelections(QualityNavList);
            NavigateToPage(pageTag);
        }
    }

    private void OnSettingsNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SettingsNavList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            ClearOtherSelections(SettingsNavList);
            NavigateToPage(pageTag);
        }
    }

    private void ClearOtherSelections(ListBox current)
    {
        foreach (var list in AllNavLists)
        {
            if (list != current)
            {
                list.SelectedItem = null;
            }
        }
    }

    #endregion

    #region Command Palette

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ToggleCommandPalette();
        }
        else if (e.Key == Key.Escape && _commandPaletteOpen)
        {
            e.Handled = true;
            CloseCommandPalette();
        }
    }

    private void OnCommandPaletteButtonClick(object sender, RoutedEventArgs e)
    {
        ToggleCommandPalette();
    }

    private void ToggleCommandPalette()
    {
        if (_commandPaletteOpen)
            CloseCommandPalette();
        else
            OpenCommandPalette();
    }

    private void OpenCommandPalette()
    {
        _commandPaletteOpen = true;
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        CommandPaletteTextBox.Text = string.Empty;
        CommandPaletteTextBox.Focus();
        UpdateCommandPaletteResults(string.Empty);
    }

    private void CloseCommandPalette()
    {
        _commandPaletteOpen = false;
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        CommandPaletteTextBox.Text = string.Empty;
        CommandPaletteResults.Items.Clear();
    }

    private void CommandPaletteOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Close palette when clicking the backdrop
        CloseCommandPalette();
    }

    private void CommandPaletteBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent close when clicking inside the palette border
        e.Handled = true;
    }

    private void CommandPaletteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCommandPaletteResults(CommandPaletteTextBox.Text);
    }

    private void CommandPaletteTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (CommandPaletteResults.SelectedItem is CommandPaletteItem selected)
            {
                ExecuteCommandPaletteItem(selected);
            }
            else if (CommandPaletteResults.Items.Count > 0)
            {
                ExecuteCommandPaletteItem((CommandPaletteItem)CommandPaletteResults.Items[0]!);
            }
        }
        else if (e.Key == Key.Down && CommandPaletteResults.Items.Count > 0)
        {
            e.Handled = true;
            CommandPaletteResults.SelectedIndex = Math.Min(
                CommandPaletteResults.SelectedIndex + 1,
                CommandPaletteResults.Items.Count - 1);
        }
        else if (e.Key == Key.Up && CommandPaletteResults.Items.Count > 0)
        {
            e.Handled = true;
            CommandPaletteResults.SelectedIndex = Math.Max(
                CommandPaletteResults.SelectedIndex - 1, 0);
        }
    }

    private void CommandPaletteResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Double-click or selection activates the item
        if (e.AddedItems.Count > 0 && Mouse.LeftButton == MouseButtonState.Pressed)
        {
            if (CommandPaletteResults.SelectedItem is CommandPaletteItem selected)
            {
                ExecuteCommandPaletteItem(selected);
            }
        }
    }

    private void UpdateCommandPaletteResults(string query)
    {
        CommandPaletteResults.Items.Clear();

        var allItems = GetCommandPaletteItems();

        IEnumerable<CommandPaletteItem> filtered;
        if (string.IsNullOrWhiteSpace(query))
        {
            filtered = allItems;
        }
        else
        {
            var normalizedQuery = query.Trim().ToUpperInvariant();
            filtered = allItems.Where(item =>
                item.DisplayText.ToUpperInvariant().Contains(normalizedQuery) ||
                item.Category.ToUpperInvariant().Contains(normalizedQuery) ||
                item.Keywords.Any(k => k.ToUpperInvariant().Contains(normalizedQuery)));
        }

        foreach (var item in filtered.Take(15))
        {
            CommandPaletteResults.Items.Add(item);
        }

        if (CommandPaletteResults.Items.Count > 0)
        {
            CommandPaletteResults.SelectedIndex = 0;
        }
    }

    private void ExecuteCommandPaletteItem(CommandPaletteItem item)
    {
        CloseCommandPalette();

        if (item.NavigationTarget.StartsWith("page:"))
        {
            var pageTag = item.NavigationTarget.Substring(5);
            _navigationService.NavigateTo(pageTag);
            UpdatePageTitle(pageTag);
            UpdateBackButtonVisibility();
        }
        else if (item.NavigationTarget.StartsWith("action:"))
        {
            var action = item.NavigationTarget.Substring(7);
            HandleAction(action);
        }
    }

    private static List<CommandPaletteItem> GetCommandPaletteItems()
    {
        return new List<CommandPaletteItem>
        {
            // Monitor workspace
            new("Dashboard", "Monitor", "page:Dashboard", new[] { "home", "overview", "status" }),
            new("Live Data", "Monitor", "page:LiveData", new[] { "realtime", "streaming", "trades" }),
            new("Charts", "Monitor", "page:Charts", new[] { "candlestick", "technical", "indicators" }),
            new("Order Book", "Monitor", "page:OrderBook", new[] { "depth", "l2", "heatmap" }),
            new("Watchlist", "Monitor", "page:Watchlist", new[] { "favorites", "tracked" }),
            new("Notifications", "Monitor", "page:NotificationCenter", new[] { "alerts", "incidents" }),

            // Collect workspace
            new("Provider", "Collect", "page:Provider", new[] { "source", "api", "connection" }),
            new("Multi-Source", "Collect", "page:DataSources", new[] { "failover", "multiple" }),
            new("Symbols", "Collect", "page:Symbols", new[] { "stocks", "tickers" }),
            new("Backfill", "Collect", "page:Backfill", new[] { "historical", "download" }),
            new("Schedules", "Collect", "page:Schedules", new[] { "schedule", "cron", "timer" }),
            new("Sessions", "Collect", "page:CollectionSessions", new[] { "history", "runs" }),

            // Storage workspace
            new("Data Browser", "Storage", "page:DataBrowser", new[] { "browse", "files" }),
            new("Storage", "Storage", "page:Storage", new[] { "disk", "usage", "tiers" }),
            new("Export", "Storage", "page:DataExport", new[] { "csv", "parquet", "json" }),
            new("Package Manager", "Storage", "page:PackageManager", new[] { "package", "portable" }),
            new("Data Calendar", "Storage", "page:DataCalendar", new[] { "coverage", "gaps", "heatmap" }),
            new("Event Replay", "Storage", "page:EventReplay", new[] { "replay", "playback" }),

            // Quality workspace
            new("Data Quality", "Quality", "page:DataQuality", new[] { "quality", "scores", "alerts" }),
            new("Analytics", "Quality", "page:AdvancedAnalytics", new[] { "gap", "analysis", "comparison" }),
            new("Archive Health", "Quality", "page:ArchiveHealth", new[] { "integrity", "verify" }),
            new("Provider Health", "Quality", "page:ProviderHealth", new[] { "latency", "uptime" }),
            new("System Health", "Quality", "page:SystemHealth", new[] { "connection", "diagnostics" }),
            new("Diagnostics", "Quality", "page:Diagnostics", new[] { "preflight", "dryrun" }),

            // Settings workspace
            new("Settings", "Settings", "page:Settings", new[] { "preferences", "config", "options" }),
            new("Admin", "Settings", "page:AdminMaintenance", new[] { "maintenance", "retention" }),
            new("Retention", "Settings", "page:RetentionAssurance", new[] { "guardrails", "holds" }),
            new("Optimization", "Settings", "page:StorageOptimization", new[] { "duplicates", "compression" }),
            new("Integrations", "Settings", "page:LeanIntegration", new[] { "quantconnect", "lean", "backtest" }),
            new("Setup Wizard", "Settings", "page:SetupWizard", new[] { "setup", "guided", "wizard" }),
            new("Help", "Settings", "page:Help", new[] { "docs", "faq", "documentation" }),

            // Quick actions
            new("Start Collector", "Action", "action:start", new[] { "begin", "run" }),
            new("Stop Collector", "Action", "action:stop", new[] { "halt", "end" }),
            new("Refresh Status", "Action", "action:refresh", new[] { "reload", "update" }),
        };
    }

    private void HandleAction(string action)
    {
        switch (action)
        {
            case "start":
            case "stop":
                // Collector control
                break;
            case "refresh":
                _messagingService.Send("RefreshStatus");
                break;
        }
    }

    #endregion

    private void NavigateToPage(string pageTag)
    {
        _navigationService.NavigateTo(pageTag);
        UpdatePageTitle(pageTag);
        UpdateBackButtonVisibility();
    }

    private void UpdatePageTitle(string pageTag)
    {
        // Convert page tag to display title
        var title = pageTag switch
        {
            "Dashboard" => "Dashboard",
            "LiveData" => "Live Data",
            "Charts" => "Charts",
            "OrderBook" => "Order Book",
            "Watchlist" => "Watchlist",
            "NotificationCenter" => "Notifications",
            "Provider" => "Data Provider",
            "DataSources" => "Multi-Source Config",
            "Symbols" => "Symbols",
            "Backfill" => "Historical Data Backfill",
            "Schedules" => "Schedules",
            "CollectionSessions" => "Collection Sessions",
            "DataBrowser" => "Data Browser",
            "Storage" => "Storage",
            "DataExport" => "Data Export",
            "PackageManager" => "Package Manager",
            "DataCalendar" => "Data Calendar",
            "EventReplay" => "Event Replay",
            "DataQuality" => "Data Quality",
            "AdvancedAnalytics" => "Analytics",
            "ArchiveHealth" => "Archive Health",
            "ProviderHealth" => "Provider Health",
            "SystemHealth" => "System Health",
            "Diagnostics" => "Diagnostics",
            "Settings" => "Settings",
            "AdminMaintenance" => "Admin & Maintenance",
            "RetentionAssurance" => "Retention Assurance",
            "StorageOptimization" => "Storage Optimization",
            "LeanIntegration" => "Lean Integration",
            "SetupWizard" => "Setup Wizard",
            "Help" => "Help & Support",
            _ => pageTag
        };

        PageTitleText.Text = title;
    }

    private void UpdateBackButtonVisibility()
    {
        BackButton.Visibility = _navigationService.CanGoBack
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        _navigationService.GoBack();
        UpdateBackButtonVisibility();
    }

    private void OnHelpButtonClick(object sender, RoutedEventArgs e)
    {
        foreach (var list in AllNavLists) list.SelectedItem = null;
        _navigationService.NavigateTo("Help");
        UpdatePageTitle("Help");
    }

    private void OnRefreshButtonClick(object sender, RoutedEventArgs e)
    {
        _messagingService.Send("RefreshStatus");
    }

    private void OnNotificationsButtonClick(object sender, RoutedEventArgs e)
    {
        foreach (var list in AllNavLists) list.SelectedItem = null;
        _navigationService.NavigateTo("NotificationCenter");
        UpdatePageTitle("Notifications");
    }

    private void OnContentFrameNavigated(object sender, SysNavigation.NavigationEventArgs e)
    {
        UpdateBackButtonVisibility();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateEventArgs e)
    {
        // Update UI on dispatcher thread
        Dispatcher.Invoke(() => UpdateConnectionStatus(e.State));
    }

    private void UpdateConnectionStatus(ConnectionState state)
    {
        switch (state)
        {
            case ConnectionState.Connected:
                ConnectionStatusDot.Fill = (System.Windows.Media.Brush)FindResource("SuccessColorBrush");
                ConnectionStatusText.Text = "Connected";
                ConnectionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessColorBrush");
                ConnectionStatusBadge.Style = (Style)FindResource("SuccessBadgeStyle");
                break;

            case ConnectionState.Connecting:
            case ConnectionState.Reconnecting:
                ConnectionStatusDot.Fill = (System.Windows.Media.Brush)FindResource("WarningColorBrush");
                ConnectionStatusText.Text = state == ConnectionState.Connecting ? "Connecting..." : "Reconnecting...";
                ConnectionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("WarningColorBrush");
                ConnectionStatusBadge.Style = (Style)FindResource("WarningBadgeStyle");
                break;

            case ConnectionState.Disconnected:
                ConnectionStatusDot.Fill = (System.Windows.Media.Brush)FindResource("ConsoleTextMutedBrush");
                ConnectionStatusText.Text = "Disconnected";
                ConnectionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ConsoleTextMutedBrush");
                ConnectionStatusBadge.Style = (Style)FindResource("NeutralBadgeStyle");
                break;

            case ConnectionState.Error:
                ConnectionStatusDot.Fill = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
                ConnectionStatusText.Text = "Error";
                ConnectionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorColorBrush");
                ConnectionStatusBadge.Style = (Style)FindResource("ErrorBadgeStyle");
                break;
        }
    }

    private void OnMessageReceived(object? sender, string message)
    {
        // Handle global messages
        switch (message)
        {
            case "RefreshStatus":
                // Propagate to current page
                break;

            case "NavigateDashboard":
                MonitorNavList.SelectedIndex = 0;
                break;

            case "NavigateSymbols":
                CollectNavList.SelectedIndex = 1;
                break;

            case "NavigateBackfill":
                CollectNavList.SelectedIndex = 2;
                break;

            case "NavigateSettings":
                SettingsNavList.SelectedIndex = 0;
                break;
        }
    }
}

/// <summary>
/// Item displayed in the command palette search results.
/// </summary>
public sealed class CommandPaletteItem
{
    public string DisplayText { get; }
    public string Category { get; }
    public string NavigationTarget { get; }
    public string[] Keywords { get; }

    public CommandPaletteItem(string displayText, string category, string navigationTarget, string[] keywords)
    {
        DisplayText = displayText;
        Category = category;
        NavigationTarget = navigationTarget;
        Keywords = keywords;
    }

    public override string ToString() => $"{DisplayText}  ({Category})";
}
