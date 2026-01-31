using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MarketDataCollector.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Wpf;

/// <summary>
/// Main application window containing the navigation frame.
/// Handles global keyboard shortcuts and routes them to appropriate services.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IConnectionService _connectionService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IKeyboardShortcutService _keyboardShortcutService;
    private readonly IMessagingService _messagingService;

    public MainWindow(
        IConnectionService connectionService,
        INavigationService navigationService,
        INotificationService notificationService,
        IKeyboardShortcutService keyboardShortcutService,
        IMessagingService messagingService)
    {
        InitializeComponent();

        _connectionService = connectionService;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _keyboardShortcutService = keyboardShortcutService;
        _messagingService = messagingService;

        // Initialize navigation service with the frame
        _navigationService.Initialize(MainFrame);

        // Subscribe to keyboard shortcuts
        _keyboardShortcutService.ShortcutInvoked += OnShortcutInvoked;

        // Subscribe to notifications for in-app display
        _notificationService.NotificationReceived += OnNotificationReceived;

        // Subscribe to connection status changes
        _connectionService.ConnectionStatusChanged += OnConnectionStatusChanged;

        // Set up keyboard shortcuts
        SetupKeyboardShortcuts();

        // Navigate to the dashboard page
        NavigateToDashboard(this, null!);

        // Clean up event subscriptions when window closes
        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Sets up keyboard shortcuts for the application.
    /// </summary>
    private void SetupKeyboardShortcuts()
    {
        // Navigation shortcuts
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => _navigationService.NavigateTo("Dashboard")),
            Key.D, ModifierKeys.Control));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => _navigationService.NavigateTo("Symbols")),
            Key.S, ModifierKeys.Control));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => _navigationService.NavigateTo("Backfill")),
            Key.B, ModifierKeys.Control));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => _navigationService.NavigateTo("Settings")),
            Key.OemComma, ModifierKeys.Control));

        // Collector control shortcuts
        InputBindings.Add(new KeyBinding(
            new RelayCommand(async _ => await StartCollectorAsync()),
            Key.F5, ModifierKeys.None));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(async _ => await StopCollectorAsync()),
            Key.F6, ModifierKeys.None));

        // Theme toggle
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => ToggleTheme()),
            Key.T, ModifierKeys.Control | ModifierKeys.Shift));
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Unsubscribe from all events to prevent memory leaks
        _keyboardShortcutService.ShortcutInvoked -= OnShortcutInvoked;
        _notificationService.NotificationReceived -= OnNotificationReceived;
        _connectionService.ConnectionStatusChanged -= OnConnectionStatusChanged;
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

            // View shortcuts
            case "ToggleTheme":
                ToggleTheme();
                break;
            case "RefreshStatus":
                _messagingService.Send("RefreshStatus");
                break;

            // General shortcuts
            case "Help":
                _navigationService.NavigateTo("Help");
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
                await _notificationService.NotifySuccessAsync(
                    "Collector Started",
                    "Data collection has started successfully.");
            }
            else
            {
                await _notificationService.NotifyErrorAsync(
                    "Start Failed",
                    "Failed to start the data collector. Check service connection.");
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync(
                "Start Error",
                $"Error starting collector: {ex.Message}");
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
            await _notificationService.NotifyWarningAsync(
                "Collector Stopped",
                "Data collection has been stopped.");
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync(
                "Stop Error",
                $"Error stopping collector: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    private void ToggleTheme()
    {
        var themeService = App.Services?.GetService<IThemeService>();
        themeService?.ToggleTheme();
    }

    private void OnNotificationReceived(object? sender, NotificationEventArgs e)
    {
        // In-app notification handling can be added here
        // For now, showing a simple message
        Dispatcher.Invoke(() =>
        {
            // Could display a toast notification or update a notification panel
            System.Diagnostics.Debug.WriteLine($"Notification: {e.Title} - {e.Message}");
        });
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        // Update the status indicator on the UI thread
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = e.Status;

            // Update indicator color based on connection status
            StatusIndicator.Background = e.Status.ToLowerInvariant() switch
            {
                "connected" => new SolidColorBrush(Colors.LimeGreen),
                "connecting" => new SolidColorBrush(Colors.Orange),
                "disconnected" => new SolidColorBrush(Colors.Gray),
                "error" => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        });
    }

    // Navigation button handlers
    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle navigation panel visibility
        NavigationPanel.Visibility = NavigationPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void NavigateToDashboard(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Dashboard");
    }

    private void NavigateToSymbols(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Symbols");
    }

    private void NavigateToBackfill(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Backfill");
    }

    private void NavigateToSettings(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Settings");
    }
}

/// <summary>
/// Simple relay command implementation for WPF commands.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}
