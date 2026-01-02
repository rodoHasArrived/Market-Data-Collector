using Microsoft.UI.Xaml;
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

        // Navigate to the main page
        RootFrame.Navigate(typeof(MainPage));
    }
}
