using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfServices = MarketDataCollector.Wpf.Services;
using NotificationType = MarketDataCollector.Wpf.Services.NotificationType;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Welcome page with project branding, quick-start steps, system overview, and recent features.
/// </summary>
public partial class WelcomePage : Page
{
    private readonly WpfServices.NavigationService _navigationService;
    private readonly WpfServices.NotificationService _notificationService;

    public WelcomePage(
        WpfServices.NavigationService navigationService,
        WpfServices.NotificationService notificationService)
    {
        InitializeComponent();
        _navigationService = navigationService;
        _notificationService = notificationService;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        UpdateSystemOverview();
    }

    private void UpdateSystemOverview()
    {
        // Connection status placeholder - updated when real status is available
        ConnectionStatusText.Text = "Disconnected";
        ConnectionStatusDot.Fill = (Brush)FindResource("ConsoleTextMutedBrush");
        ConnectionProviderText.Text = "No provider connected";

        // Symbols count placeholder
        SymbolsCountText.Text = "0";

        // Storage path placeholder
        StoragePathText.Text = "./data";
    }

    // -- Quick-start step card click handlers (Border.MouseLeftButtonUp) --

    private void StepProvider_CardClick(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo("Provider");
    }

    private void StepSymbols_CardClick(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo("Symbols");
    }

    private void StepStorage_CardClick(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo("Storage");
    }

    private void StepDashboard_CardClick(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo("Dashboard");
    }

    private void StepDataQuality_CardClick(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo("DataQuality");
    }

    // -- Button click handlers (Button.Click) --

    private void StepProvider_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Provider");
    }

    private void StepSymbols_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Symbols");
    }

    private void StepStorage_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Storage");
    }

    private void StepDashboard_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("Dashboard");
    }

    private void StepDataQuality_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("DataQuality");
    }

    private void OpenDocumentation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/example/market-data-collector",
                UseShellExecute = true
            });
        }
        catch
        {
            _notificationService.ShowNotification(
                "Error",
                "Could not open the documentation link. Please try again.",
                NotificationType.Error);
        }
    }
}
