using System;
using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;
using SysNavigation = System.Windows.Navigation;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Main page with navigation sidebar and content frame.
/// Serves as the shell for all application content.
/// </summary>
public partial class MainPage : Page
{
    private readonly NavigationService _navigationService;
    private readonly IConnectionService _connectionService;

    public MainPage()
    {
        InitializeComponent();

        _navigationService = MarketDataCollector.Wpf.Services.NavigationService.Instance;
        _connectionService = MarketDataCollector.Wpf.Services.ConnectionService.Instance;

        // Subscribe to connection state changes
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        // Subscribe to messaging for page updates
        MessagingService.Instance.MessageReceived += OnMessageReceived;
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
            NavigationList.SelectedIndex = 0;
            // Default to Dashboard
            _navigationService.NavigateTo("Dashboard");
        }

        // Update connection status display
        UpdateConnectionStatus(_connectionService.State);

        // Update back button visibility
        UpdateBackButtonVisibility();
    }

    private void OnNavigationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            // Clear other list selections
            SecondaryNavigationList.SelectedItem = null;
            ToolsNavigationList.SelectedItem = null;

            NavigateToPage(pageTag);
        }
    }

    private void OnSecondaryNavigationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecondaryNavigationList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            // Clear other list selections
            NavigationList.SelectedItem = null;
            ToolsNavigationList.SelectedItem = null;

            NavigateToPage(pageTag);
        }
    }

    private void OnToolsNavigationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ToolsNavigationList.SelectedItem is ListBoxItem item && item.Tag is string pageTag)
        {
            // Clear other list selections
            NavigationList.SelectedItem = null;
            SecondaryNavigationList.SelectedItem = null;

            NavigateToPage(pageTag);
        }
    }

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
            "Symbols" => "Symbols",
            "Backfill" => "Historical Data Backfill",
            "Settings" => "Settings",
            "DataQuality" => "Data Quality",
            "ProviderHealth" => "Provider Health",
            "Storage" => "Storage",
            "DataExport" => "Data Export",
            "Charts" => "Charts",
            "Help" => "Help & Support",
            "SetupWizard" => "Setup Wizard",
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
        // Clear all selections
        NavigationList.SelectedItem = null;
        SecondaryNavigationList.SelectedItem = null;
        ToolsNavigationList.SelectedItem = null;

        _navigationService.NavigateTo("Help");
        UpdatePageTitle("Help");
    }

    private void OnRefreshButtonClick(object sender, RoutedEventArgs e)
    {
        MessagingService.Instance.Send("RefreshStatus");
    }

    private void OnNotificationsButtonClick(object sender, RoutedEventArgs e)
    {
        // Clear all selections
        NavigationList.SelectedItem = null;
        SecondaryNavigationList.SelectedItem = null;
        ToolsNavigationList.SelectedItem = null;

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
                NavigationList.SelectedIndex = 0;
                break;

            case "NavigateSymbols":
                NavigationList.SelectedIndex = 1;
                break;

            case "NavigateBackfill":
                NavigationList.SelectedIndex = 2;
                break;

            case "NavigateSettings":
                NavigationList.SelectedIndex = 3;
                break;
        }
    }
}
