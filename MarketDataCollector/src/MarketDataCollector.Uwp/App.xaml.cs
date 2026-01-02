using Microsoft.UI.Xaml;
using MarketDataCollector.Uwp.Services;

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
        MainWindow = _window;

        // Initialize theme service
        ThemeService.Instance.Initialize(_window);

        // Start connection monitoring
        ConnectionService.Instance.StartMonitoring();

        _window.Activate();
    }

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Gets the notification service instance.
    /// </summary>
    public static NotificationService Notifications => NotificationService.Instance;

    /// <summary>
    /// Gets the connection service instance.
    /// </summary>
    public static ConnectionService Connection => ConnectionService.Instance;

    /// <summary>
    /// Gets the theme service instance.
    /// </summary>
    public static ThemeService Theme => ThemeService.Instance;
}
