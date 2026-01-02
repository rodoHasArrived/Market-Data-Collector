using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.ViewModels;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Main dashboard page with navigation to different sections.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        this.InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "Provider":
                    ContentFrame.Navigate(typeof(ProviderPage));
                    break;
                case "DataSources":
                    ContentFrame.Navigate(typeof(DataSourcesPage));
                    break;
                case "Storage":
                    ContentFrame.Navigate(typeof(StoragePage));
                    break;
                case "Symbols":
                    ContentFrame.Navigate(typeof(SymbolsPage));
                    break;
                case "Backfill":
                    ContentFrame.Navigate(typeof(BackfillPage));
                    break;
            }
        }
    }
}
