using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MarketDataCollector.Wpf.Contracts;
using MarketDataCollector.Wpf.Services;
using WpfServices = MarketDataCollector.Wpf.Services;
using MarketDataCollector.Wpf.Views;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Services.Services;
using SysNavigation = System.Windows.Navigation;

namespace MarketDataCollector.Wpf;

/// <summary>
/// Main application window containing the navigation frame.
/// Handles global keyboard shortcuts, command palette, and window state persistence.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IConnectionService _connectionService;
    private readonly WpfServices.NavigationService _navigationService;
    private readonly WpfServices.KeyboardShortcutService _keyboardShortcutService;
    private readonly WpfServices.NotificationService _notificationService;
    private readonly WpfServices.MessagingService _messagingService;
    private readonly WpfServices.ThemeService _themeService;
    private readonly OnboardingTourService _tourService;
    private readonly AlertService _alertService;
    private readonly FixtureModeDetector _fixtureModeDetector;

    private static readonly string WindowStateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarketDataCollector",
        "window-state.json");

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
        _tourService = OnboardingTourService.Instance;
        _alertService = AlertService.Instance;
        _fixtureModeDetector = FixtureModeDetector.Instance;

        // Subscribe to fixture/offline mode changes
        _fixtureModeDetector.ModeChanged += OnFixtureModeChanged;
        UpdateFixtureModeBanner();

        // Subscribe to keyboard shortcuts
        _keyboardShortcutService.ShortcutInvoked += OnShortcutInvoked;

        // Subscribe to notifications for in-app display
        _notificationService.NotificationReceived += OnNotificationReceived;

        // Subscribe to onboarding tour events
        _tourService.StepChanged += OnTourStepChanged;
        _tourService.TourCompleted += OnTourCompleted;

        // Subscribe to alert events for guided remediation
        _alertService.AlertRaised += OnAlertRaised;

        // Restore window state from previous session
        RestoreWindowState();
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
        // Save window state before closing
        SaveWindowState();

        // Unsubscribe from all events to prevent memory leaks
        _keyboardShortcutService.ShortcutInvoked -= OnShortcutInvoked;
        _notificationService.NotificationReceived -= OnNotificationReceived;
        _tourService.StepChanged -= OnTourStepChanged;
        _tourService.TourCompleted -= OnTourCompleted;
        _alertService.AlertRaised -= OnAlertRaised;
        _fixtureModeDetector.ModeChanged -= OnFixtureModeChanged;
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
                ShowCommandPalette();
                break;
        }
    }

    /// <summary>
    /// Opens the command palette dialog (Ctrl+K).
    /// </summary>
    private void ShowCommandPalette()
    {
        var paletteService = CommandPaletteService.Instance;
        var palette = new CommandPaletteWindow(paletteService)
        {
            Owner = this
        };

        // Subscribe to command execution
        paletteService.CommandExecuted += OnPaletteCommandExecuted;

        try
        {
            palette.ShowDialog();
        }
        finally
        {
            paletteService.CommandExecuted -= OnPaletteCommandExecuted;
        }
    }

    private void OnPaletteCommandExecuted(object? sender, PaletteCommandEventArgs e)
    {
        switch (e.Category)
        {
            case PaletteCommandCategory.Navigation:
                _navigationService.NavigateTo(e.ActionId);
                break;

            case PaletteCommandCategory.Action:
                HandlePaletteAction(e.ActionId);
                break;
        }
    }

    private void HandlePaletteAction(string actionId)
    {
        switch (actionId)
        {
            case "StartCollector":
                _ = StartCollectorAsync();
                break;
            case "StopCollector":
                _ = StopCollectorAsync();
                break;
            case "RunBackfill":
                _navigationService.NavigateTo("Backfill");
                break;
            case "RefreshStatus":
                _messagingService.Send("RefreshStatus");
                break;
            case "AddSymbol":
                _navigationService.NavigateTo("Symbols");
                _messagingService.Send("AddSymbol");
                break;
            case "ToggleTheme":
                _themeService.ToggleTheme();
                break;
            case "Save":
                _messagingService.Send("Save");
                break;
            case "SearchSymbols":
                _messagingService.Send("FocusSearch");
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

    #region Fixture/Offline Mode Banner

    private void OnFixtureModeChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnFixtureModeChanged(sender, e));
            return;
        }

        UpdateFixtureModeBanner();
    }

    private void UpdateFixtureModeBanner()
    {
        if (_fixtureModeDetector.IsNonLiveMode)
        {
            FixtureModeBanner.Visibility = Visibility.Visible;
            FixtureModeText.Text = _fixtureModeDetector.ModeLabel;

            var color = _fixtureModeDetector.IsFixtureMode
                ? System.Windows.Media.Color.FromRgb(0xFF, 0xB3, 0x00) // Amber for fixture
                : System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36); // Red for offline

            FixtureModeBanner.Background = new System.Windows.Media.SolidColorBrush(color);
        }
        else
        {
            FixtureModeBanner.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Onboarding Tour Overlay

    private void OnTourStepChanged(object? sender, TourStepEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnTourStepChanged(sender, e));
            return;
        }

        ShowTourStepNotification(e);
    }

    private void OnTourCompleted(object? sender, TourCompletedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnTourCompleted(sender, e));
            return;
        }

        _notificationService.ShowNotification(
            "Tour Complete",
            $"You've completed the '{e.TourId}' tour! ({e.StepsCompleted} steps in {e.Duration.TotalSeconds:F0}s)",
            NotificationType.Success,
            5000);
    }

    private void ShowTourStepNotification(TourStepEventArgs e)
    {
        var stepInfo = $"Step {e.StepIndex + 1}/{e.TotalSteps}";
        var message = $"{stepInfo}: {e.Step.Content}";

        if (e.IsLast)
        {
            message += "\n(Last step - press Next to finish)";
        }

        _notificationService.ShowNotification(
            $"Tour: {e.Step.Title}",
            message,
            NotificationType.Info,
            0); // Persistent until user advances

        // Auto-advance via messaging: pages can listen for "TourNext" / "TourDismiss"
    }

    #endregion

    #region Alert Remediation

    private void OnAlertRaised(object? sender, AlertEventArgs e)
    {
        if (e.IsUpdate) return; // Only show notification for new alerts

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnAlertRaised(sender, e));
            return;
        }

        var alert = e.Alert;
        if (alert.IsSuppressed || alert.IsSnoozed) return;

        // Show notification with remediation guidance if playbook is available
        if (alert.Playbook != null)
        {
            var firstStep = alert.Playbook.RemediationSteps.Length > 0
                ? $"\nTry: {alert.Playbook.RemediationSteps[0].Description}"
                : "";

            var notificationType = alert.Severity switch
            {
                AlertSeverity.Critical or AlertSeverity.Emergency => NotificationType.Error,
                AlertSeverity.Error => NotificationType.Error,
                AlertSeverity.Warning => NotificationType.Warning,
                _ => NotificationType.Info
            };

            _notificationService.ShowNotification(
                alert.Title,
                $"{alert.Description}{firstStep}",
                notificationType,
                alert.Severity >= AlertSeverity.Error ? 0 : 8000);
        }
        else
        {
            var notificationType = alert.Severity >= AlertSeverity.Error
                ? NotificationType.Error
                : NotificationType.Warning;

            _notificationService.ShowNotification(
                alert.Title,
                alert.Description,
                notificationType,
                8000);
        }
    }

    #endregion

    #region Window State Persistence

    /// <summary>
    /// Saves the current window position, size, and state to disk.
    /// </summary>
    private void SaveWindowState()
    {
        try
        {
            var state = new PersistedWindowState
            {
                Left = RestoreBounds.Left,
                Top = RestoreBounds.Top,
                Width = RestoreBounds.Width,
                Height = RestoreBounds.Height,
                IsMaximized = WindowState == WindowState.Maximized,
                SavedAt = DateTime.UtcNow
            };

            var dir = Path.GetDirectoryName(WindowStateFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(WindowStateFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to save window state: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores window position, size, and state from disk.
    /// Validates that the restored position is visible on a connected monitor.
    /// </summary>
    private void RestoreWindowState()
    {
        try
        {
            if (!File.Exists(WindowStateFilePath)) return;

            var json = File.ReadAllText(WindowStateFilePath);
            var state = JsonSerializer.Deserialize<PersistedWindowState>(json);
            if (state == null) return;

            // Validate dimensions are reasonable
            if (state.Width < MinWidth || state.Height < MinHeight) return;
            if (state.Width > 10000 || state.Height > 10000) return;

            // Validate position is on a visible monitor
            if (!IsPositionOnScreen(state.Left, state.Top, state.Width, state.Height))
            {
                // Position is off-screen (monitor may have been disconnected)
                // Keep default CenterScreen position but restore size
                Width = state.Width;
                Height = state.Height;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = state.Left;
                Top = state.Top;
                Width = state.Width;
                Height = state.Height;
            }

            if (state.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to restore window state: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a position is visible on the virtual screen area.
    /// Uses WPF SystemParameters to avoid a WinForms dependency.
    /// </summary>
    private static bool IsPositionOnScreen(double left, double top, double width, double height)
    {
        // Use the virtual screen bounds (spans all monitors)
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        var virtualRect = new Rect(virtualLeft, virtualTop, virtualWidth, virtualHeight);
        var windowRect = new Rect(left, top, width, height);

        var intersection = Rect.Intersect(windowRect, virtualRect);

        // At least 100x100 pixels must be visible
        const double minVisibleSize = 100;
        return !intersection.IsEmpty &&
               intersection.Width >= minVisibleSize &&
               intersection.Height >= minVisibleSize;
    }

    /// <summary>
    /// Persisted window state for save/restore across sessions.
    /// </summary>
    private sealed class PersistedWindowState
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
        public DateTime SavedAt { get; set; }
    }

    #endregion
}
