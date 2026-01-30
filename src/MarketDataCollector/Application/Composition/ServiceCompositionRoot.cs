using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Config.Credentials;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Backfill.Scheduling;
using MarketDataCollector.Infrastructure.Providers.SymbolSearch;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Maintenance;
using MarketDataCollector.Storage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MarketDataCollector.Application.Composition;

/// <summary>
/// Centralizes all service registration for the application.
/// This is the single composition root that builds the service graph once,
/// with host-specific adapters (console, web, desktop) opting into endpoints.
/// </summary>
/// <remarks>
/// <para><b>Design Philosophy:</b></para>
/// <list type="bullet">
/// <item><description>Single source of truth for service registration</description></item>
/// <item><description>Host-agnostic core services</description></item>
/// <item><description>Feature flags for optional capabilities (HTTP server, backfill, etc.)</description></item>
/// <item><description>Lazy initialization for expensive services</description></item>
/// </list>
/// </remarks>
[ImplementsAdr("ADR-001", "Centralized composition root for service configuration")]
public static class ServiceCompositionRoot
{
    /// <summary>
    /// Registers all core services required by any host type.
    /// This is the minimum set of services needed for the application to function.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">Composition options controlling which services to register.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddMarketDataServices(
        this IServiceCollection services,
        CompositionOptions? options = null)
    {
        options ??= CompositionOptions.Default;

        // Core configuration services - always required
        services.AddCoreConfigurationServices(options);

        // Storage services - always required for data persistence
        services.AddStorageServices(options);

        // Optional feature sets based on composition options
        if (options.EnableSymbolManagement)
        {
            services.AddSymbolManagementServices();
        }

        if (options.EnableBackfillServices)
        {
            services.AddBackfillServices(options);
        }

        if (options.EnableMaintenanceServices)
        {
            services.AddMaintenanceServices(options);
        }

        if (options.EnableDiagnosticServices)
        {
            services.AddDiagnosticServices(options);
        }

        if (options.EnableCredentialServices)
        {
            services.AddCredentialServices(options);
        }

        return services;
    }

    #region Core Configuration Services

    /// <summary>
    /// Registers core configuration services that all hosts need.
    /// </summary>
    private static IServiceCollection AddCoreConfigurationServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // ConfigStore - unified configuration access
        if (!string.IsNullOrEmpty(options.ConfigPath))
        {
            services.AddSingleton(new ConfigStore(options.ConfigPath));
        }
        else
        {
            services.AddSingleton<ConfigStore>();
        }

        // ConfigurationService - consolidated configuration operations
        services.AddSingleton<ConfigurationService>();

        // Configuration utilities
        services.AddSingleton<ConfigTemplateGenerator>();
        services.AddSingleton<ConfigEnvironmentOverride>();
        services.AddSingleton<DryRunService>();

        return services;
    }

    #endregion

    #region Storage Services

    /// <summary>
    /// Registers storage and data persistence services.
    /// </summary>
    private static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // StorageOptions - configured from AppConfig or defaults
        services.AddSingleton<StorageOptions>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            var compressionEnabled = config.Compress ?? false;

            return config.Storage?.ToStorageOptions(config.DataRoot, compressionEnabled)
                ?? StorageProfilePresets.CreateFromProfile(null, config.DataRoot, compressionEnabled);
        });

        // Source registry for data source tracking
        services.AddSingleton<ISourceRegistry>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new SourceRegistry(config.Sources?.PersistencePath);
        });

        // Core storage services
        services.AddSingleton<IFileMaintenanceService, FileMaintenanceService>();
        services.AddSingleton<IDataQualityService, DataQualityService>();
        services.AddSingleton<IStorageSearchService, StorageSearchService>();
        services.AddSingleton<ITierMigrationService, TierMigrationService>();

        return services;
    }

    #endregion

    #region Symbol Management Services

    /// <summary>
    /// Registers symbol management and search services.
    /// </summary>
    private static IServiceCollection AddSymbolManagementServices(this IServiceCollection services)
    {
        // Symbol import/export
        services.AddSingleton<SymbolImportExportService>();
        services.AddSingleton<TemplateService>();
        services.AddSingleton<MetadataEnrichmentService>();
        services.AddSingleton<IndexSubscriptionService>();
        services.AddSingleton<WatchlistService>();
        services.AddSingleton<BatchOperationsService>();
        services.AddSingleton<PortfolioImportService>();

        // Symbol search providers
        services.AddSingleton<OpenFigiClient>();
        services.AddSingleton<SymbolSearchService>(sp =>
        {
            var metadataService = sp.GetRequiredService<MetadataEnrichmentService>();
            var figiClient = sp.GetRequiredService<OpenFigiClient>();
            return new SymbolSearchService(
                new ISymbolSearchProvider[]
                {
                    new AlpacaSymbolSearchProviderRefactored(),
                    new FinnhubSymbolSearchProviderRefactored(),
                    new PolygonSymbolSearchProvider()
                },
                figiClient,
                metadataService);
        });

        return services;
    }

    #endregion

    #region Backfill Services

    /// <summary>
    /// Registers backfill and scheduling services.
    /// </summary>
    private static IServiceCollection AddBackfillServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // BackfillCoordinator - the unified implementation from Application.UI
        services.AddSingleton<BackfillCoordinator>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            return new BackfillCoordinator(configStore);
        });

        // SchedulingService - symbol subscription scheduling
        services.AddSingleton<SchedulingService>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            return new SchedulingService(configStore);
        });

        // Backfill execution history and schedule manager
        services.AddSingleton<BackfillExecutionHistory>();
        services.AddSingleton<BackfillScheduleManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<BackfillScheduleManager>();
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            var history = sp.GetRequiredService<BackfillExecutionHistory>();
            return new BackfillScheduleManager(logger, config.DataRoot, history);
        });

        return services;
    }

    #endregion

    #region Maintenance Services

    /// <summary>
    /// Registers archive maintenance and cleanup services.
    /// </summary>
    private static IServiceCollection AddMaintenanceServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // Maintenance history and schedule manager
        services.AddSingleton<MaintenanceExecutionHistory>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new MaintenanceExecutionHistory(config.DataRoot);
        });

        services.AddSingleton<ArchiveMaintenanceScheduleManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ArchiveMaintenanceScheduleManager>();
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            var history = sp.GetRequiredService<MaintenanceExecutionHistory>();
            return new ArchiveMaintenanceScheduleManager(logger, config.DataRoot, history);
        });

        services.AddSingleton<ScheduledArchiveMaintenanceService>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ScheduledArchiveMaintenanceService>();
            var schedManager = sp.GetRequiredService<ArchiveMaintenanceScheduleManager>();
            var fileMaint = sp.GetRequiredService<IFileMaintenanceService>();
            var tierMigration = sp.GetRequiredService<ITierMigrationService>();
            var storageOpts = sp.GetRequiredService<StorageOptions>();
            return new ScheduledArchiveMaintenanceService(logger, schedManager, fileMaint, tierMigration, storageOpts);
        });

        return services;
    }

    #endregion

    #region Diagnostic Services

    /// <summary>
    /// Registers diagnostic and error tracking services.
    /// </summary>
    private static IServiceCollection AddDiagnosticServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // Historical data query
        services.AddSingleton<HistoricalDataQueryService>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new HistoricalDataQueryService(config.DataRoot);
        });

        // Diagnostic bundle generator
        services.AddSingleton<DiagnosticBundleService>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new DiagnosticBundleService(config.DataRoot, null, () => configStore.Load());
        });

        // Sample data generator
        services.AddSingleton<SampleDataGenerator>();

        // Error tracker
        services.AddSingleton<ErrorTracker>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new ErrorTracker(config.DataRoot);
        });

        // API documentation service
        services.AddSingleton<ApiDocumentationService>();

        return services;
    }

    #endregion

    #region Credential Services

    /// <summary>
    /// Registers credential management services.
    /// </summary>
    private static IServiceCollection AddCredentialServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        services.AddSingleton<CredentialTestingService>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new CredentialTestingService(config.DataRoot);
        });

        services.AddSingleton<OAuthTokenRefreshService>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            return new OAuthTokenRefreshService(config.DataRoot);
        });

        return services;
    }

    #endregion
}

/// <summary>
/// Options controlling which services are registered by the composition root.
/// </summary>
public sealed record CompositionOptions
{
    /// <summary>
    /// Default options enabling all commonly used services.
    /// </summary>
    public static CompositionOptions Default => new()
    {
        EnableSymbolManagement = true,
        EnableBackfillServices = true,
        EnableMaintenanceServices = true,
        EnableDiagnosticServices = true,
        EnableCredentialServices = true
    };

    /// <summary>
    /// Minimal options for console-only operation.
    /// </summary>
    public static CompositionOptions Minimal => new()
    {
        EnableSymbolManagement = false,
        EnableBackfillServices = false,
        EnableMaintenanceServices = false,
        EnableDiagnosticServices = false,
        EnableCredentialServices = false
    };

    /// <summary>
    /// Options optimized for web dashboard hosting.
    /// </summary>
    public static CompositionOptions WebDashboard => new()
    {
        EnableSymbolManagement = true,
        EnableBackfillServices = true,
        EnableMaintenanceServices = true,
        EnableDiagnosticServices = true,
        EnableCredentialServices = true
    };

    /// <summary>
    /// Path to the configuration file. If null, ConfigStore will use default resolution.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>
    /// Data root directory override. If null, uses value from configuration.
    /// </summary>
    public string? DataRoot { get; init; }

    /// <summary>
    /// Whether to enable symbol management services (import/export, search, watchlists).
    /// </summary>
    public bool EnableSymbolManagement { get; init; }

    /// <summary>
    /// Whether to enable backfill scheduling and coordination services.
    /// </summary>
    public bool EnableBackfillServices { get; init; }

    /// <summary>
    /// Whether to enable archive maintenance and cleanup services.
    /// </summary>
    public bool EnableMaintenanceServices { get; init; }

    /// <summary>
    /// Whether to enable diagnostic and error tracking services.
    /// </summary>
    public bool EnableDiagnosticServices { get; init; }

    /// <summary>
    /// Whether to enable credential testing and OAuth services.
    /// </summary>
    public bool EnableCredentialServices { get; init; }
}
