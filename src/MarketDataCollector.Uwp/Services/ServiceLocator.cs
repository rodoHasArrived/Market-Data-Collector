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
        // Register singleton services - uses existing Instance properties
        // to maintain backwards compatibility while enabling DI
        services.AddSingleton<IConfigService>(_ => ConfigService.Instance);
        services.AddSingleton(_ => StatusService.Instance);
        services.AddSingleton<IConnectionService>(_ => ConnectionService.Instance);
        services.AddSingleton<INavigationService>(_ => NavigationService.Instance);
        services.AddSingleton(_ => LoggingService.Instance);
        services.AddSingleton(_ => NotificationService.Instance);
        services.AddSingleton(_ => ThemeService.Instance);
        services.AddSingleton(_ => BackgroundTaskSchedulerService.Instance);
        services.AddSingleton(_ => MessagingService.Instance);
        services.AddSingleton(_ => WatchlistService.Instance);
        services.AddSingleton(_ => SchemaService.Instance);
        services.AddSingleton(_ => ArchiveHealthService.Instance);
        services.AddSingleton(_ => PendingOperationsQueueService.Instance);
        services.AddSingleton(_ => OfflineTrackingPersistenceService.Instance);
        services.AddSingleton(_ => AdminMaintenanceService.Instance);
        services.AddSingleton(_ => AdvancedAnalyticsService.Instance);
        services.AddSingleton(_ => StorageService.Instance);
        services.AddSingleton(_ => ExportPresetService.Instance);
        services.AddSingleton(_ => FormValidationService.Instance);
        services.AddSingleton(_ => KeyboardShortcutService.Instance);
        services.AddSingleton(_ => WorkspaceService.Instance);
        services.AddSingleton(_ => RetentionAssuranceService.Instance);
        services.AddSingleton(_ => InfoBarService.Instance);
        services.AddSingleton(_ => TooltipService.Instance);
        services.AddSingleton(_ => ContextMenuService.Instance);
        services.AddSingleton(_ => UwpDataQualityService.Instance);
        services.AddSingleton(_ => UwpAnalysisExportService.Instance);
        services.AddSingleton(_ => ActivityFeedService.Instance);
        services.AddSingleton(_ => ApiClientService.Instance);
        services.AddSingleton(_ => CollectionSessionService.Instance);
        services.AddSingleton(_ => SmartRecommendationsService.Instance);
        services.AddSingleton(_ => OAuthRefreshService.Instance);
        services.AddSingleton(_ => IntegrityEventsService.Instance);

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
