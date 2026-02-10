using Microsoft.Extensions.DependencyInjection;
using MarketDataCollector.Uwp.Contracts;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// C1: Centralized service locator and DI container for the UWP application.
/// Provides a transition path from manual singleton management to proper DI.
///
/// Usage:
///   // In App.xaml.cs during startup:
///   ServiceLocator.Initialize();
///
///   // To resolve a service:
///   var configService = ServiceLocator.GetService&lt;IConfigService&gt;();
///
///   // Existing singleton patterns still work for backwards compatibility:
///   var configService = ConfigService.Instance;
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;
    private static ServiceCollection? _services;

    /// <summary>
    /// Gets the service provider. Throws if not initialized.
    /// </summary>
    public static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException(
            "ServiceLocator has not been initialized. Call ServiceLocator.Initialize() during app startup.");

    /// <summary>
    /// Gets whether the service locator has been initialized.
    /// </summary>
    public static bool IsInitialized => _serviceProvider != null;

    /// <summary>
    /// Initializes the DI container with all application services.
    /// Call this during application startup (App.xaml.cs OnLaunched).
    /// </summary>
    public static void Initialize()
    {
        _services = new ServiceCollection();
        ConfigureServices(_services);
        _serviceProvider = _services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets a required service of the specified type.
    /// </summary>
    public static T GetService<T>() where T : notnull =>
        Services.GetRequiredService<T>();

    /// <summary>
    /// Gets an optional service of the specified type.
    /// Returns null if not registered.
    /// </summary>
    public static T? GetOptionalService<T>() where T : class =>
        Services.GetService<T>();

    private static void ConfigureServices(IServiceCollection services)
    {
        // C1: Register services by interface for DI-first resolution.
        // Uses existing singleton instances for backwards compatibility.

        // Shared UI service interfaces (from MarketDataCollector.Ui.Services.Contracts)
        services.AddSingleton<IConfigService>(_ => ConfigService.Instance);
        services.AddSingleton<IConnectionService>(_ => ConnectionService.Instance);
        services.AddSingleton<INavigationService>(_ => NavigationService.Instance);
        services.AddSingleton<ILoggingService>(_ => LoggingService.Instance);
        services.AddSingleton<IStatusService>(_ => StatusService.Instance);
        services.AddSingleton<INotificationService>(_ => NotificationService.Instance);
        services.AddSingleton<IThemeService>(_ => ThemeService.Instance);
        services.AddSingleton<IBackgroundTaskSchedulerService>(_ => BackgroundTaskSchedulerService.Instance);
        services.AddSingleton<IMessagingService>(_ => MessagingService.Instance);
        services.AddSingleton<IWatchlistService>(_ => WatchlistService.Instance);
        services.AddSingleton<ISchemaService>(_ => SchemaService.Instance);
        services.AddSingleton<IArchiveHealthService>(_ => ArchiveHealthService.Instance);
        services.AddSingleton<IPendingOperationsQueueService>(_ => PendingOperationsQueueService.Instance);
        services.AddSingleton<IOfflineTrackingPersistenceService>(_ => OfflineTrackingPersistenceService.Instance);

        // UWP-specific service interfaces (from MarketDataCollector.Uwp.Contracts)
        services.AddSingleton<IAdminMaintenanceService>(_ => AdminMaintenanceService.Instance);
        services.AddSingleton<IAdvancedAnalyticsService>(_ => AdvancedAnalyticsService.Instance);
        services.AddSingleton<IStorageService>(_ => StorageService.Instance);
        services.AddSingleton<IExportPresetService>(_ => ExportPresetService.Instance);
        services.AddSingleton<IFormValidationService>(_ => FormValidationService.Instance);
        services.AddSingleton<IKeyboardShortcutService>(_ => KeyboardShortcutService.Instance);
        services.AddSingleton<IWorkspaceService>(_ => WorkspaceService.Instance);
        services.AddSingleton<IRetentionAssuranceService>(_ => RetentionAssuranceService.Instance);
        services.AddSingleton<IInfoBarService>(_ => InfoBarService.Instance);
        services.AddSingleton<ITooltipService>(_ => TooltipService.Instance);
        services.AddSingleton<IContextMenuService>(_ => ContextMenuService.Instance);
        services.AddSingleton<IUwpDataQualityService>(_ => UwpDataQualityService.Instance);
        services.AddSingleton<IUwpAnalysisExportService>(_ => UwpAnalysisExportService.Instance);
        services.AddSingleton<IFirstRunService>(_ => new FirstRunService());

        // Services without interfaces (concrete-type registration)
        services.AddSingleton(_ => ActivityFeedService.Instance);
        services.AddSingleton(_ => ApiClientService.Instance);
        services.AddSingleton(_ => CollectionSessionService.Instance);
        services.AddSingleton(_ => SmartRecommendationsService.Instance);
        services.AddSingleton(_ => OAuthRefreshService.Instance);
        services.AddSingleton(_ => IntegrityEventsService.Instance);

        // Also register concrete types for backwards compatibility during migration
        services.AddSingleton(_ => ConfigService.Instance);
        services.AddSingleton(_ => ConnectionService.Instance);
        services.AddSingleton(_ => NavigationService.Instance);
        services.AddSingleton(_ => LoggingService.Instance);
        services.AddSingleton(_ => StatusService.Instance);
        services.AddSingleton(_ => NotificationService.Instance);

        // Register transient services (created per-request, no singleton)
        services.AddTransient<CredentialService>();

        // Register ViewModels
        services.AddTransient<ViewModels.DashboardViewModel>();
        services.AddTransient<ViewModels.BackfillViewModel>();
        services.AddTransient<ViewModels.DataExportViewModel>();
        services.AddTransient<ViewModels.DataQualityViewModel>();
        services.AddTransient<ViewModels.MainViewModel>();
    }
}
