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

        // Handle app exit for clean shutdown
        this.Exit += OnAppExit;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Fire-and-forget async initialization with proper exception handling
        _ = SafeOnLaunchedAsync();
    }

    /// <summary>
    /// Performs async initialization with proper exception handling.
    /// Called from OnLaunched via fire-and-forget pattern.
    /// </summary>
    private async Task SafeOnLaunchedAsync()
    {
        try
        {
            // Run first-time setup before showing window
            await InitializeFirstRunAsync();

            _window = new MainWindow();
            MainWindow = _window;

            // Initialize theme service
            ThemeService.Instance.Initialize(_window);

            // Start connection monitoring
            ConnectionService.Instance.StartMonitoring();

            // Initialize offline tracking persistence (handles recovery from crashes/restarts)
            await InitializeOfflineTrackingAsync();

            // Start background task scheduler
            await InitializeBackgroundServicesAsync();

            _window.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error during application launch: {ex.Message}");
            // Try to show a basic window even if initialization fails
            try
            {
                _window ??= new MainWindow();
                MainWindow = _window;
                _window.Activate();
            }
            catch
            {
                // If we can't even create the window, there's nothing we can do
            }
        }
    }

    /// <summary>
    /// Initializes offline tracking persistence and performs recovery if needed.
    /// </summary>
    private static async Task InitializeOfflineTrackingAsync()
    {
        try
        {
            await OfflineTrackingPersistenceService.Instance.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("Offline tracking persistence initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize offline tracking: {ex.Message}");
            // Continue - app should still work without persistence
        }
    }

    /// <summary>
    /// Initializes background services for scheduled tasks and offline queue processing.
    /// </summary>
    private static async Task InitializeBackgroundServicesAsync()
    {
        try
        {
            // Initialize pending operations queue
            await PendingOperationsQueueService.Instance.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("Pending operations queue initialized");

            // Start background task scheduler
            await BackgroundTaskSchedulerService.Instance.StartAsync();
            System.Diagnostics.Debug.WriteLine("Background task scheduler started");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize background services: {ex.Message}");
            // Continue - app should still work without background services
        }
    }

    /// <summary>
    /// Handles app exit for clean shutdown of background services with timeout.
    /// </summary>
    private void OnAppExit(object sender, EventArgs e)
    {
        // Fire-and-forget async shutdown with proper exception handling
        _ = SafeOnAppExitAsync();
    }

    /// <summary>
    /// Performs async shutdown with proper exception handling.
    /// Called from OnAppExit via fire-and-forget pattern.
    /// </summary>
    private async Task SafeOnAppExitAsync()
    {
        const int ShutdownTimeoutMs = 5000; // 5 second timeout for graceful shutdown

        try
        {
            System.Diagnostics.Debug.WriteLine("App exiting, shutting down services...");

            using var cts = new System.Threading.CancellationTokenSource(ShutdownTimeoutMs);

            // Shutdown services in parallel with timeout for better performance
            var shutdownTasks = new[]
            {
                ShutdownServiceAsync(() => BackgroundTaskSchedulerService.Instance.StopAsync(), "BackgroundTaskScheduler", cts.Token),
                ShutdownServiceAsync(() => PendingOperationsQueueService.Instance.ShutdownAsync(), "PendingOperationsQueue", cts.Token),
                ShutdownServiceAsync(() => OfflineTrackingPersistenceService.Instance.ShutdownAsync(), "OfflineTrackingPersistence", cts.Token),
                ShutdownServiceAsync(() => ConnectionService.Instance.StopMonitoring(), "ConnectionService", cts.Token)
            };

            await Task.WhenAll(shutdownTasks);

            System.Diagnostics.Debug.WriteLine("Services shut down cleanly");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("App shutdown timed out - forcing exit");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error during app exit: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to shutdown a service with proper error handling.
    /// </summary>
    private static async Task ShutdownServiceAsync(Func<Task> shutdownAction, string serviceName, System.Threading.CancellationToken ct)
    {
        try
        {
            await shutdownAction().WaitAsync(ct);
            System.Diagnostics.Debug.WriteLine($"{serviceName} shut down successfully");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"{serviceName} shutdown timed out");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{serviceName} shutdown failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to shutdown a synchronous service.
    /// </summary>
    private static async Task ShutdownServiceAsync(Action shutdownAction, string serviceName, System.Threading.CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                shutdownAction();
                System.Diagnostics.Debug.WriteLine($"{serviceName} shut down successfully");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"{serviceName} shutdown timed out");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{serviceName} shutdown failed: {ex.Message}");
            }
        }, ct);
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

    /// <summary>
    /// Gets the offline tracking persistence service instance.
    /// </summary>
    public static OfflineTrackingPersistenceService OfflineTracking => OfflineTrackingPersistenceService.Instance;

    /// <summary>
    /// Gets the background task scheduler service instance.
    /// </summary>
    public static BackgroundTaskSchedulerService Scheduler => BackgroundTaskSchedulerService.Instance;

    /// <summary>
    /// Gets the pending operations queue service instance.
    /// </summary>
    public static PendingOperationsQueueService OperationsQueue => PendingOperationsQueueService.Instance;
}
