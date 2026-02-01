using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;
using MarketDataCollector.Wpf.Views;

namespace MarketDataCollector.Wpf;

/// <summary>
/// Main application window containing the navigation frame.
/// Handles global keyboard shortcuts and routes them to appropriate services.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IConnectionService _connectionService;
    private readonly NavigationService _navigationService;

    public MainWindow()
    {
        InitializeComponent();

        // Get service instances
        _connectionService = ConnectionService.Instance;
        _navigationService = NavigationService.Instance;

        // Subscribe to keyboard shortcuts
        KeyboardShortcutService.Instance.ShortcutInvoked += OnShortcutInvoked;

        // Subscribe to notifications for in-app display
        NotificationService.Instance.NotificationReceived += OnNotificationReceived;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize navigation service with the frame
        _navigationService.Initialize(RootFrame);

        // Initialize keyboard shortcuts
        KeyboardShortcutService.Instance.Initialize(this);

        // Navigate to the main page
        RootFrame.Navigate(new MainPage());
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Unsubscribe from all events to prevent memory leaks
        KeyboardShortcutService.Instance.ShortcutInvoked -= OnShortcutInvoked;
        NotificationService.Instance.NotificationReceived -= OnNotificationReceived;
    }

    private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is FrameworkElement element)
        {
            KeyboardShortcutService.Instance.Initialize(element);
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Route key events to keyboard shortcut service
        KeyboardShortcutService.Instance.HandleKeyDown(e);
    }

    private void OnShortcutInvoked(object? sender, ShortcutInvokedEventArgs e)
    {
        // Handle global shortcuts using NavigationService for consistent routing
        switch (e.ActionId)
        {
            // Navigation shortcuts
            case "NavigateDashboard":
                _navigationService.NavigateTo("Dashboard");
                break;
            case "NavigateSymbols":
                _navigationService.NavigateTo("Symbols");
                break;
            case "NavigateBackfill":
                _navigationService.NavigateTo("Backfill");
                break;
            case "NavigateSettings":
                _navigationService.NavigateTo("Settings");
                break;

            // Collector control shortcuts
            case "StartCollector":
                _ = StartCollectorAsync();
                break;
            case "StopCollector":
                _ = StopCollectorAsync();
                break;

            // Backfill shortcuts
            case "RunBackfill":
                _navigationService.NavigateTo("Backfill");
                break;
            case "PauseBackfill":
                // Send message to BackfillPage when active
                MessagingService.Instance.Send("PauseBackfill");
                break;
            case "CancelBackfill":
                // Send message to BackfillPage when active
                MessagingService.Instance.Send("CancelBackfill");
                break;

            // Symbol shortcuts
            case "AddSymbol":
                _navigationService.NavigateTo("Symbols");
                MessagingService.Instance.Send("AddSymbol");
                break;
            case "SearchSymbols":
                // Focus search box in current page
                MessagingService.Instance.Send("FocusSearch");
                break;
            case "DeleteSelected":
                // Send delete message to current page
                MessagingService.Instance.Send("DeleteSelected");
                break;
            case "SelectAll":
                // Send select all message to current page
                MessagingService.Instance.Send("SelectAll");
                break;

            // View shortcuts
            case "ToggleTheme":
                ThemeService.Instance.ToggleTheme();
                break;
            case "ViewLogs":
                _navigationService.NavigateTo("ServiceManager");
                break;
            case "RefreshStatus":
                // Send refresh message to current page
                MessagingService.Instance.Send("RefreshStatus");
                break;
            case "ZoomIn":
                MessagingService.Instance.Send("ZoomIn");
                break;
            case "ZoomOut":
                MessagingService.Instance.Send("ZoomOut");
                break;

            // General shortcuts
            case "Save":
                // Send save message to current page
                MessagingService.Instance.Send("Save");
                break;
            case "Help":
                _navigationService.NavigateTo("Help");
                break;
            case "QuickCommand":
                // Focus the search box for quick command entry
                MessagingService.Instance.Send("QuickCommand");
                break;
        }
    }

    /// <summary>
    /// Starts the data collector service via keyboard shortcut.
    /// </summary>
    private async Task StartCollectorAsync()
    {
        try
        {
            var provider = _connectionService.CurrentProvider;
            if (string.IsNullOrEmpty(provider))
            {
                provider = "default";
            }

            var success = await _connectionService.ConnectAsync(provider);
            if (success)
            {
                NotificationService.Instance.ShowNotification(
                    "Collector Started",
                    "Data collection has started successfully.",
                    NotificationType.Success,
                    "collector");
            }
            else
            {
                NotificationService.Instance.ShowNotification(
                    "Start Failed",
                    "Failed to start the data collector. Check service connection.",
                    NotificationType.Error,
                    "collector");
            }
        }
        catch (Exception ex)
        {
            NotificationService.Instance.ShowNotification(
                "Start Error",
                $"Error starting collector: {ex.Message}",
                NotificationType.Error,
                "collector");
        }
    }

    /// <summary>
    /// Stops the data collector service via keyboard shortcut.
    /// </summary>
    private async Task StopCollectorAsync()
    {
        try
        {
            await _connectionService.DisconnectAsync();
            NotificationService.Instance.ShowNotification(
                "Collector Stopped",
                "Data collection has been stopped.",
                NotificationType.Warning,
                "collector");
        }
        catch (Exception ex)
        {
            NotificationService.Instance.ShowNotification(
                "Stop Error",
                $"Error stopping collector: {ex.Message}",
                NotificationType.Error,
                "collector");
        }
    }

    private void OnNotificationReceived(object? sender, NotificationEventArgs e)
    {
        // In-app notification handling can be added here
        // For now, notifications are handled by the NotificationService
    }
}
