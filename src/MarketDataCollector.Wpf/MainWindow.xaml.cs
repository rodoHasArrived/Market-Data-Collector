using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MarketDataCollector.Wpf.Contracts;
using WpfServices = MarketDataCollector.Wpf.Services;
using MarketDataCollector.Wpf.Views;
using SysNavigation = System.Windows.Navigation;

namespace MarketDataCollector.Wpf;

/// <summary>
/// Main application window containing the navigation frame.
/// Handles global keyboard shortcuts and routes them to appropriate services.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IConnectionService _connectionService;
    private readonly WpfServices.NavigationService _navigationService;
    private readonly WpfServices.KeyboardShortcutService _keyboardShortcutService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly WpfServices.MessagingService _messagingService;
    private readonly WpfServices.ThemeService _themeService;

    public MainWindow(
        WpfServices.NavigationService navigationService,
        WpfServices.ConnectionService connectionService,
        WpfServices.KeyboardShortcutService keyboardShortcutService,
        WpfServices.NotificationService notificationService,
        WpfServices.MessagingService messagingService,
        WpfServices.ThemeService themeService)
    {
        InitializeComponent();

        _navigationService = navigationService;
        _connectionService = connectionService;
        _keyboardShortcutService = keyboardShortcutService;
        _notificationService = notificationService;
        _messagingService = messagingService;
        _themeService = themeService;

        // Subscribe to keyboard shortcuts
        _keyboardShortcutService.ShortcutInvoked += OnShortcutInvoked;

        // Subscribe to notifications for in-app display
        _notificationService.NotificationReceived += OnNotificationReceived;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize navigation service with the frame
        _navigationService.Initialize(RootFrame);

        // Initialize keyboard shortcuts
        _keyboardShortcutService.Initialize(this);

        // Navigate to the main page via DI
        RootFrame.Navigate(App.Services.GetRequiredService<MainPage>());
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Unsubscribe from all events to prevent memory leaks
        _keyboardShortcutService.ShortcutInvoked -= OnShortcutInvoked;
        _notificationService.NotificationReceived -= OnNotificationReceived;
    }

    private void OnRootFrameNavigated(object sender, SysNavigation.NavigationEventArgs e)
    {
        // In WPF, get content from the Frame (sender), not from event args
        if (sender is System.Windows.Controls.Frame frame && frame.Content is FrameworkElement element)
        {
            _keyboardShortcutService.Initialize(element);
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Route key events to keyboard shortcut service
        _keyboardShortcutService.HandleKeyDown(e);
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
                _messagingService.Send("PauseBackfill");
                break;
            case "CancelBackfill":
                // Send message to BackfillPage when active
                _messagingService.Send("CancelBackfill");
                break;

            // Symbol shortcuts
            case "AddSymbol":
                _navigationService.NavigateTo("Symbols");
                _messagingService.Send("AddSymbol");
                break;
            case "SearchSymbols":
                // Focus search box in current page
                _messagingService.Send("FocusSearch");
                break;
            case "DeleteSelected":
                // Send delete message to current page
                _messagingService.Send("DeleteSelected");
                break;
            case "SelectAll":
                // Send select all message to current page
                _messagingService.Send("SelectAll");
                break;

            // View shortcuts
            case "ToggleTheme":
                _themeService.ToggleTheme();
                break;
            case "ViewLogs":
                _navigationService.NavigateTo("ServiceManager");
                break;
            case "RefreshStatus":
                // Send refresh message to current page
                _messagingService.Send("RefreshStatus");
                break;
            case "ZoomIn":
                _messagingService.Send("ZoomIn");
                break;
            case "ZoomOut":
                _messagingService.Send("ZoomOut");
                break;

            // General shortcuts
            case "Save":
                // Send save message to current page
                _messagingService.Send("Save");
                break;
            case "Help":
                _navigationService.NavigateTo("Help");
                break;
            case "QuickCommand":
                // Focus the search box for quick command entry
                _messagingService.Send("QuickCommand");
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
                _notificationService.ShowNotification(
                    "Collector Started",
                    "Data collection has started successfully.",
                    NotificationType.Success,
                    5000);
            }
            else
            {
                _notificationService.ShowNotification(
                    "Start Failed",
                    "Failed to start the data collector. Check service connection.",
                    NotificationType.Error,
                    0);
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowNotification(
                "Start Error",
                $"Error starting collector: {ex.Message}",
                NotificationType.Error,
                0);
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
            _notificationService.ShowNotification(
                "Collector Stopped",
                "Data collection has been stopped.",
                NotificationType.Warning,
                5000);
        }
        catch (Exception ex)
        {
            _notificationService.ShowNotification(
                "Stop Error",
                $"Error stopping collector: {ex.Message}",
                NotificationType.Error,
                0);
        }
    }

    private void OnNotificationReceived(object? sender, NotificationEventArgs e)
    {
        // In-app notification handling can be added here
        // For now, notifications are handled by the NotificationService
    }
}
