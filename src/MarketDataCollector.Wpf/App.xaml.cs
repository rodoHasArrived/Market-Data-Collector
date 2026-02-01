using System.Windows;
using MarketDataCollector.Wpf.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MarketDataCollector.Wpf;

/// <summary>
/// Market Data Collector WPF Application
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private static bool _isFirstRun;

    public App()
    {
        // Initialize HttpClientFactory early for proper HTTP client lifecycle management
        // This ensures all services use the factory-created clients with Polly policies
        InitializeHttpClientFactory();

        // Handle unhandled exceptions gracefully
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Handle app exit for clean shutdown
        Exit += OnAppExit;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build and start the host
        _host = CreateHostBuilder(e.Args).Build();
        await _host.StartAsync();

        // Perform async initialization
        await SafeOnStartupAsync();
    }

    /// <summary>
    /// Creates and configures the application host with dependency injection.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.sample.json", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Register services
                RegisterServices(services, context.Configuration);

                // Register MainWindow
                services.AddSingleton<MainWindow>();
            });

    /// <summary>
    /// Registers all application services with the DI container.
    /// </summary>
    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // HttpClient with Polly
        services.AddHttpClient();

        // Register singleton services
        services.AddSingleton<IConnectionService, ConnectionService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
        services.AddSingleton<IMessagingService, MessagingService>();
        services.AddSingleton<IOfflineTrackingPersistenceService, OfflineTrackingPersistenceService>();
        services.AddSingleton<IBackgroundTaskSchedulerService, BackgroundTaskSchedulerService>();
        services.AddSingleton<IPendingOperationsQueueService, PendingOperationsQueueService>();
    }

    /// <summary>
    /// Initializes HttpClientFactory for proper HTTP client lifecycle management.
    /// </summary>
    private static void InitializeHttpClientFactory()
    {
        // HttpClientFactory will be initialized through DI container
        // This is handled in RegisterServices
    }

    /// <summary>
    /// Performs async initialization with proper exception handling.
    /// </summary>
    private async Task SafeOnStartupAsync()
    {
        try
        {
            if (_host == null)
            {
                throw new InvalidOperationException("Host is not initialized");
            }

            // Run first-time setup before showing window
            await InitializeFirstRunAsync();

            // Initialize and validate configuration
            await InitializeConfigurationAsync();

            // Get and show MainWindow
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // Initialize theme service
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.Initialize();

            // Start connection monitoring
            var connectionService = _host.Services.GetRequiredService<IConnectionService>();
            await connectionService.StartMonitoringAsync();

            // Initialize offline tracking persistence
            await InitializeOfflineTrackingAsync();

            // Start background task scheduler
            await InitializeBackgroundServicesAsync();

            // Log successful startup
            var loggingService = _host.Services.GetRequiredService<ILoggingService>();
            loggingService.LogInfo("Application started successfully");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error during application startup: {ex.Message}\n\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Try to show a basic window even if initialization fails
            try
            {
                var mainWindow = _host?.Services.GetRequiredService<MainWindow>();
                if (mainWindow != null)
                {
                    mainWindow.Show();
                }
            }
            catch
            {
                // If we can't even create the window, shut down
                Shutdown(1);
            }
        }
    }

    /// <summary>
    /// Initializes offline tracking persistence and performs recovery if needed.
    /// </summary>
    private async Task InitializeOfflineTrackingAsync()
    {
        try
        {
            var offlineTracking = _host?.Services.GetRequiredService<IOfflineTrackingPersistenceService>();
            if (offlineTracking != null)
            {
                await offlineTracking.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("Offline tracking persistence initialized");
            }
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
    private async Task InitializeBackgroundServicesAsync()
    {
        try
        {
            var operationsQueue = _host?.Services.GetRequiredService<IPendingOperationsQueueService>();
            if (operationsQueue != null)
            {
                await operationsQueue.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("Pending operations queue initialized");
            }

            var scheduler = _host?.Services.GetRequiredService<IBackgroundTaskSchedulerService>();
            if (scheduler != null)
            {
                await scheduler.StartAsync();
                System.Diagnostics.Debug.WriteLine("Background task scheduler started");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize background services: {ex.Message}");
            // Continue - app should still work without background services
        }
    }

    /// <summary>
    /// Performs first-run initialization including config setup.
    /// </summary>
    private async Task InitializeFirstRunAsync()
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
    private async Task InitializeConfigurationAsync()
    {
        try
        {
            var configService = _host?.Services.GetRequiredService<IConfigService>();
            if (configService == null) return;

            await configService.InitializeAsync();

            var validationResult = await configService.ValidateConfigAsync();
            var loggingService = _host?.Services.GetRequiredService<ILoggingService>();

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    loggingService?.LogError($"Configuration error: {error}");
                }
            }

            foreach (var warning in validationResult.Warnings)
            {
                loggingService?.LogWarning($"Configuration warning: {warning}");
            }

            loggingService?.LogInfo("Configuration initialized",
                ("isValid", validationResult.IsValid.ToString()),
                ("errors", validationResult.Errors.Length.ToString()),
                ("warnings", validationResult.Warnings.Length.ToString()));
        }
        catch (Exception ex)
        {
            var loggingService = _host?.Services.GetRequiredService<ILoggingService>();
            loggingService?.LogError("Failed to initialize configuration", ex);
            // Continue - app should still work with defaults
        }
    }

    /// <summary>
    /// Handles app exit for clean shutdown of background services with timeout.
    /// </summary>
    private async void OnAppExit(object sender, ExitEventArgs e)
    {
        const int ShutdownTimeoutMs = 5000; // 5 second timeout for graceful shutdown

        try
        {
            System.Diagnostics.Debug.WriteLine("App exiting, shutting down services...");

            using var cts = new System.Threading.CancellationTokenSource(ShutdownTimeoutMs);

            if (_host != null)
            {
                // Shutdown services
                var scheduler = _host.Services.GetService<IBackgroundTaskSchedulerService>();
                var operationsQueue = _host.Services.GetService<IPendingOperationsQueueService>();
                var offlineTracking = _host.Services.GetService<IOfflineTrackingPersistenceService>();
                var connectionService = _host.Services.GetService<IConnectionService>();

                var shutdownTasks = new List<Task>();

                if (scheduler != null)
                    shutdownTasks.Add(scheduler.StopAsync());

                if (operationsQueue != null)
                    shutdownTasks.Add(operationsQueue.ShutdownAsync());

                if (offlineTracking != null)
                    shutdownTasks.Add(offlineTracking.ShutdownAsync());

                if (connectionService != null)
                    shutdownTasks.Add(connectionService.StopMonitoringAsync());

                await Task.WhenAll(shutdownTasks).WaitAsync(cts.Token);

                // Stop the host
                await _host.StopAsync(cts.Token);
                _host.Dispose();
            }

            System.Diagnostics.Debug.WriteLine("Services shut down cleanly");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("App shutdown timed out - forcing exit");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during app exit: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles unhandled dispatcher exceptions to prevent crashes.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled dispatcher exception: {e.Exception}");
        e.Handled = true;

        // Notify user of the error
        try
        {
            var notificationService = _host?.Services.GetService<INotificationService>();
            notificationService?.NotifyErrorAsync("Application Error", e.Exception.Message);
        }
        catch
        {
            // Ignore notification failures
            MessageBox.Show(e.Exception.Message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles unhandled domain exceptions.
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled domain exception: {ex}");
            MessageBox.Show(ex.Message, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Gets whether this is the first run of the application.
    /// </summary>
    public static bool IsFirstRun => _isFirstRun;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider? Services => ((App)Current)._host?.Services;
}
