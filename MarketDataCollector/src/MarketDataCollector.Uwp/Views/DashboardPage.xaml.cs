using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.ViewModels;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Dashboard page showing system status and metrics.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public MainViewModel ViewModel { get; }

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }
}
