using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf;

/// <summary>
/// Market Data Collector WPF Application
/// Provides maximum stability through WPF (.NET 9) for Windows-only deployment.
/// </summary>
public partial class App : Application
{
    private static bool _isFirstRun;
    private IHost? _host;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public static new MainWindow? MainWindow => Current.MainWindow as MainWindow;

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

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // Configure the host with dependency injection
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        Services = _host.Services;

        // TD-10: Initialize HttpClientFactory early for proper HTTP client lifecycle management
        HttpClientFactoryProvider.Initialize();

        // Handle unhandled exceptions gracefully
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Fire-and-forget async initialization with proper exception handling
        await SafeOnStartupAsync();
    }

    /// <summary>
    /// Configures services for dependency injection.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Register HttpClient factory
        services.AddHttpClient();

        // Register singleton services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ConnectionService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<LoggingService>();
        services.AddSingleton<KeyboardShortcutService>();
        services.AddSingleton<MessagingService>();
        services.AddSingleton<StatusService>();

        // Register background services
        services.AddSingleton<BackgroundTaskSchedulerService>();
        services.AddSingleton<OfflineTrackingPersistenceService>();
        services.AddSingleton<PendingOperationsQueueService>();

        // Register ViewModels
        services.AddTransient<ViewModels.MainViewModel>();
        services.AddTransient<ViewModels.DashboardViewModel>();
        services.AddTransient<ViewModels.BackfillViewModel>();
        services.AddTransient<ViewModels.DataExportViewModel>();
        services.AddTransient<ViewModels.DataQualityViewModel>();
    }

    /// <summary>
    /// Performs async initialization with proper exception handling.
    /// </summary>
    private async Task SafeOnStartupAsync()
    {
        try
        {
            // Run first-time setup before showing window
            await InitializeFirstRunAsync();

            // Initialize and validate configuration
            await InitializeConfigurationAsync();

            // Initialize theme service
            if (Current.MainWindow is MainWindow mainWindow)
            {
                ThemeService.Instance.Initialize(mainWindow);
            }

            // Start connection monitoring
            ConnectionService.Instance.StartMonitoring();

            // Initialize offline tracking persistence (handles recovery from crashes/restarts)
            await InitializeOfflineTrackingAsync();

            // Start background task scheduler
            await InitializeBackgroundServicesAsync();

            // Log successful startup
            LoggingService.Instance.LogInfo("Application started successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error during application startup: {ex.Message}");
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
    private async void OnExit(object sender, ExitEventArgs e)
    {
        await SafeOnExitAsync();
        _host?.Dispose();
    }

    /// <summary>
    /// Performs async shutdown with proper exception handling.
    /// </summary>
    private static async Task SafeOnExitAsync()
    {
        const int ShutdownTimeoutMs = 5000; // 5 second timeout for graceful shutdown

        try
        {
            System.Diagnostics.Debug.WriteLine("App exiting, shutting down services...");

            using var cts = new CancellationTokenSource(ShutdownTimeoutMs);

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
    private static async Task ShutdownServiceAsync(Func<Task> shutdownAction, string serviceName, CancellationToken ct)
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
    private static async Task ShutdownServiceAsync(Action shutdownAction, string serviceName, CancellationToken ct)
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
                    LoggingService.Instance.LogError($"Configuration error: {error}");
                }
            }

            foreach (var warning in validationResult.Warnings)
            {
                LoggingService.Instance.LogWarning($"Configuration warning: {warning}");
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
    /// Handles unhandled exceptions on the UI thread.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Log the exception but don't crash
        System.Diagnostics.Debug.WriteLine($"Dispatcher unhandled exception: {e.Exception}");
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
    /// Handles unhandled exceptions from non-UI threads.
    /// </summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Domain unhandled exception: {ex}");
        }
    }

    /// <summary>
    /// Handles unobserved task exceptions.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception}");
        e.SetObserved(); // Prevent the process from terminating
    }
}
