using System;
using Microsoft.UI.Xaml;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp;

/// <summary>
/// Market Data Collector UWP Application.
/// <para>
/// <b>DEPRECATED:</b> This UWP/WinUI 3 application is deprecated.
/// Use <see cref="MarketDataCollector.Wpf"/> (WPF desktop app) instead.
/// This project receives only critical security fixes.
/// See docs/development/uwp-to-wpf-migration.md for the migration guide.
/// </para>
/// </summary>
[Obsolete("UWP app is deprecated. Use MarketDataCollector.Wpf instead. See docs/development/uwp-to-wpf-migration.md.")]
public partial class App : Application
{
    private Window? _window;
    private static bool _isFirstRun;

    public App()
    {
        this.InitializeComponent();

        // TD-10: Initialize HttpClientFactory early for proper HTTP client lifecycle management
        // This ensures all services use the factory-created clients with Polly policies
        HttpClientFactoryProvider.Initialize();

        // Handle unhandled exceptions gracefully
        this.UnhandledException += OnUnhandledException;

        // Handle app exit for clean shutdown
        this.Exit += OnAppExit;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // C1: Initialize DI container before any service resolution
        ServiceLocator.Initialize();

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

            // Initialize and validate configuration
            await InitializeConfigurationAsync();

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

            // Log successful startup
            LoggingService.Instance.LogInfo("Application started successfully");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error during application launch", ex);
            // Try to show a basic window even if initialization fails
            try
            {
                _window ??= new MainWindow();
                MainWindow = _window;
                _window.Activate();
            }
            catch (Exception innerEx)
            {
                // Q2: Log window creation failures instead of silently swallowing
                System.Diagnostics.Debug.WriteLine($"[App] Failed to create fallback window: {innerEx.Message}");
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
            LoggingService.Instance.LogInfo("Offline tracking persistence initialized");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to initialize offline tracking", ex);
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
            LoggingService.Instance.LogInfo("Pending operations queue initialized");

            // Start background task scheduler
            await BackgroundTaskSchedulerService.Instance.StartAsync();
            LoggingService.Instance.LogInfo("Background task scheduler started");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to initialize background services", ex);
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
            LoggingService.Instance.LogInfo("App exiting, shutting down services...");

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

            LoggingService.Instance.LogInfo("Services shut down cleanly");
        }
        catch (OperationCanceledException)
        {
            LoggingService.Instance.LogWarning("App shutdown timed out - forcing exit");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error during app exit", ex);
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
            LoggingService.Instance.LogInfo("Service shut down successfully", ("service", serviceName));
        }
        catch (OperationCanceledException)
        {
            LoggingService.Instance.LogWarning("Service shutdown timed out", ("service", serviceName));
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Service shutdown failed", ("service", serviceName), ("error", ex.Message));
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
                LoggingService.Instance.LogInfo("Service shut down successfully", ("service", serviceName));
            }
            catch (OperationCanceledException)
            {
                LoggingService.Instance.LogWarning("Service shutdown timed out", ("service", serviceName));
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Service shutdown failed", ("service", serviceName), ("error", ex.Message));
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
            // C1: Resolve via DI instead of direct construction
            var firstRunService = ServiceLocator.IsInitialized
                ? ServiceLocator.GetService<IFirstRunService>()
                : new FirstRunService();
            _isFirstRun = await firstRunService.IsFirstRunAsync();

            if (_isFirstRun)
            {
                await firstRunService.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            // Continue even if first-run setup fails - app should still work
            LoggingService.Instance.LogWarning("First-run setup failed", ("error", ex.Message));
        }
    }

    /// <summary>
    /// Initializes and validates the application configuration.
    /// </summary>
    private static async Task InitializeConfigurationAsync()
    {
        try
        {
            // Initialize the config service
            await ConfigService.Instance.InitializeAsync();

            // Validate configuration
            var validationResult = await ConfigService.Instance.ValidateConfigAsync();

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    LoggingService.Instance.LogError("Configuration error", ("error", error));
                }
            }

            foreach (var warning in validationResult.Warnings)
            {
                LoggingService.Instance.LogWarning("Configuration warning", ("warning", warning));
            }

            LoggingService.Instance.LogInfo("Configuration initialized",
                ("isValid", validationResult.IsValid.ToString()),
                ("errors", validationResult.Errors.Length.ToString()),
                ("warnings", validationResult.Warnings.Length.ToString()));
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to initialize configuration", ex);
            // Continue - app should still work with defaults
        }
    }

    /// <summary>
    /// Handles unhandled exceptions to prevent crashes.
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Log the exception but don't crash
        LoggingService.Instance.LogError("Unhandled exception", e.Exception);
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
            LoggingService.Instance.LogWarning("Notification failure during startup");
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
