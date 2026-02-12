using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Views;

/// <summary>
/// Page for viewing and managing market data providers for real-time streaming and historical data.
/// </summary>
public partial class ProviderPage : Page
{
    private readonly NavigationService _navigationService;
    private readonly NotificationService _notificationService;

    public ProviderPage(
        NavigationService navigationService,
        NotificationService notificationService)
    {
        InitializeComponent();

        _navigationService = navigationService;
        _notificationService = notificationService;
    }

    private void TestAllConnections_Click(object sender, RoutedEventArgs e)
    {
        _notificationService.NotifyInfo(
            "Connection Test",
            "Testing connectivity to all configured providers...");
    }

    private void ConfigureProvider_Click(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo("DataSources");
    }
}
