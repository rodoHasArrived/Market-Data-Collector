using Microsoft.UI.Xaml;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp;

/// <summary>
/// Market Data Collector UWP Application
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private static bool _isFirstRun;

    public App()
    {
        this.InitializeComponent();

        // Handle unhandled exceptions gracefully
        this.UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Run first-time setup before showing window
        await InitializeFirstRunAsync();

        _window = new MainWindow();
        MainWindow = _window;

        // Initialize theme service
        ThemeService.Instance.Initialize(_window);

        // Start connection monitoring
        ConnectionService.Instance.StartMonitoring();

        _window.Activate();
    }

    /// <summary>
    /// Performs first-run initialization including config setup.
    /// </summary>
    private static async Task InitializeFirstRunAsync()
    {
        try
        {
            var firstRunService = new FirstRunService();
            _isFirstRun = await firstRunService.IsFirstRunAsync();

            if (_isFirstRun)
            {
                await firstRunService.InitializeAsync();
            }
        }
        catch
        {
            // Continue even if first-run setup fails - app should still work
        }
    }

    /// <summary>
    /// Handles unhandled exceptions to prevent crashes.
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Log the exception but don't crash
        System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Exception}");
        e.Handled = true;

        // Notify user of the error
        try
        {
            _ = NotificationService.Instance.NotifyErrorAsync(
                "Application Error",
                e.Exception.Message);
        }
        catch
        {
            // Ignore notification failures
        }
    }

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Gets whether this is the first run of the application.
    /// </summary>
    public static bool IsFirstRun => _isFirstRun;

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
