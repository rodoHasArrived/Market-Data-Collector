using Microsoft.UI.Xaml;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.Views;

namespace MarketDataCollector.Uwp;

/// <summary>
/// Main application window containing the navigation frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Set window properties
        Title = "Market Data Collector";

        // Initialize keyboard shortcuts
        if (RootFrame.Content is FrameworkElement rootElement)
        {
            KeyboardShortcutService.Instance.Initialize(rootElement);
        }

        // Subscribe to keyboard shortcuts
        KeyboardShortcutService.Instance.ShortcutInvoked += OnShortcutInvoked;

        // Subscribe to notifications for in-app display
        NotificationService.Instance.NotificationReceived += OnNotificationReceived;

        // Navigate to the main page
        RootFrame.Navigate(typeof(MainPage));

        // Initialize keyboard shortcuts after navigation
        RootFrame.Navigated += (s, e) =>
        {
            if (e.Content is FrameworkElement element)
            {
                KeyboardShortcutService.Instance.Initialize(element);
            }
        };
    }

    private void OnShortcutInvoked(object? sender, ShortcutInvokedEventArgs e)
    {
        // Handle global shortcuts
        switch (e.ActionId)
        {
            case "NavigateDashboard":
                NavigateToPage(typeof(DashboardPage));
                break;
            case "NavigateSymbols":
                NavigateToPage(typeof(SymbolsPage));
                break;
            case "NavigateBackfill":
                NavigateToPage(typeof(BackfillPage));
                break;
            case "NavigateSettings":
                NavigateToPage(typeof(SettingsPage));
                break;
            case "ToggleTheme":
                ThemeService.Instance.ToggleTheme();
                break;
            case "ViewLogs":
                NavigateToPage(typeof(ServiceManagerPage));
                break;
            case "RefreshStatus":
                // Trigger refresh in current page if applicable
                break;
            case "Help":
                NavigateToPage(typeof(HelpPage));
                break;
        }
    }

    private void NavigateToPage(Type pageType)
    {
        if (RootFrame.Content is MainPage mainPage)
        {
            // MainPage handles navigation internally via NavigationView
            // We need to find the MainPage's ContentFrame
        }
        else
        {
            RootFrame.Navigate(pageType);
        }
    }

    private void OnNotificationReceived(object? sender, NotificationEventArgs e)
    {
        // In-app notification handling can be added here
        // For now, Windows notifications are handled by the NotificationService
    }
}
