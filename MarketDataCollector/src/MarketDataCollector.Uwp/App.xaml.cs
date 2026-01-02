using Microsoft.UI.Xaml;

namespace MarketDataCollector.Uwp;

/// <summary>
/// Market Data Collector UWP Application
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    public static Window? MainWindow { get; private set; }
}
