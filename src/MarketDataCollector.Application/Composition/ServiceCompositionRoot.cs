using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Config.Credentials;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Monitoring.DataQuality;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Scheduling;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.DataSources;
using MarketDataCollector.Infrastructure.Http;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Infrastructure.Providers.SymbolSearch;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Maintenance;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Services;
using MarketDataCollector.Storage.Sinks;
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
    /// <remarks>
    /// <para><b>Service Registration Order:</b></para>
    /// <list type="number">
    /// <item><description>Core configuration services (always required)</description></item>
    /// <item><description>Storage services (always required)</description></item>
    /// <item><description>Credential services (before providers for credential resolution)</description></item>
    /// <item><description>Provider services (ProviderRegistry, ProviderFactory - before dependent services)</description></item>
    /// <item><description>Symbol management services (depends on ProviderFactory/Registry)</description></item>
    /// <item><description>Backfill services (depends on ProviderRegistry/Factory)</description></item>
    /// <item><description>Other services (maintenance, diagnostic, pipeline, collector)</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddMarketDataServices(
        this IServiceCollection services,
        CompositionOptions? options = null)
    {
        options ??= CompositionOptions.Default;

        // Core configuration services - always required
        services.AddCoreConfigurationServices(options);

        // Storage services - always required for data persistence
        services.AddStorageServices(options);

        // Credential services - must come before provider services for credential resolution
        if (options.EnableCredentialServices)
        {
            services.AddCredentialServices(options);
        }

        // Provider services (ProviderRegistry, ProviderFactory) - must come before
        // dependent services (SymbolManagement, Backfill) to ensure providers are available
        if (options.EnableProviderServices)
        {
            services.AddProviderServices(options);
        }

        // Symbol management - depends on ProviderFactory/ProviderRegistry
        if (options.EnableSymbolManagement)
        {
            services.AddSymbolManagementServices();
        }

        // Backfill services - depends on ProviderRegistry/ProviderFactory
        if (options.EnableBackfillServices)
        {
            services.AddBackfillServices(options);
        }

        // Remaining optional services
        if (options.EnableMaintenanceServices)
        {
            services.AddMaintenanceServices(options);
        }

        if (options.EnableDiagnosticServices)
        {
            services.AddDiagnosticServices(options);
        }

        if (options.EnablePipelineServices)
        {
            services.AddPipelineServices(options);
        }

        if (options.EnableCollectorServices)
        {
            services.AddCollectorServices(options);
        }

        if (options.EnableHttpClientFactory)
        {
            services.AddHttpClientFactoryServices();
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
    /// <remarks>
    /// Symbol search providers are resolved from <see cref="ProviderRegistry"/> which is populated
    /// by <see cref="RegisterSymbolSearchProviders"/> during startup.
    /// All symbol search operations go through <see cref="SymbolSearchService"/>.
    /// </remarks>
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

        // Symbol search providers - consolidated through SymbolSearchService
        services.AddSingleton<OpenFigiClient>();
        services.AddSingleton<SymbolSearchService>(sp =>
        {
            var metadataService = sp.GetRequiredService<MetadataEnrichmentService>();
            var figiClient = sp.GetRequiredService<OpenFigiClient>();
            var log = LoggingSetup.ForContext<SymbolSearchService>();

            // Priority-based provider discovery
            var providers = GetSymbolSearchProviders(sp, log);

            return new SymbolSearchService(providers, figiClient, metadataService);
        });

        return services;
    }

    /// <summary>
    /// Gets symbol search providers from the unified ProviderRegistry.
    /// Providers are populated by <see cref="RegisterSymbolSearchProviders"/> during startup.
    /// </summary>
    private static IEnumerable<ISymbolSearchProvider> GetSymbolSearchProviders(
        IServiceProvider sp,
        Serilog.ILogger log)
    {
        var registry = sp.GetService<ProviderRegistry>();
        if (registry != null)
        {
            var providers = registry.GetSymbolSearchProviders();
            if (providers.Count > 0)
            {
                log.Information("Using {Count} symbol search providers from ProviderRegistry", providers.Count);
                return providers;
            }
        }

        log.Warning("No symbol search providers available from ProviderRegistry");
        return Array.Empty<ISymbolSearchProvider>();
    }

    #endregion

    #region Backfill Services

    /// <summary>
    /// Registers backfill and scheduling services.
    /// Uses <see cref="ProviderRegistry"/> for unified provider discovery.
    /// </summary>
    /// <remarks>
    /// <para>Requires <see cref="AddProviderServices"/> to be called first to ensure
    /// <see cref="ProviderRegistry"/> and <see cref="ProviderFactory"/> are available.</para>
    /// </remarks>
    private static IServiceCollection AddBackfillServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // BackfillCoordinator - uses ProviderRegistry for unified provider discovery
        services.AddSingleton<BackfillCoordinator>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var registry = sp.GetService<ProviderRegistry>();
            var factory = sp.GetService<ProviderFactory>();
            return new BackfillCoordinator(configStore, registry, factory);
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

    #region Provider Services

    /// <summary>
    /// Registers the unified <see cref="ProviderRegistry"/> and populates it with
    /// streaming factory functions (keyed by <see cref="DataSourceKind"/>), backfill providers,
    /// and symbol search providers. All providers are resolved through DI.
    /// </summary>
    /// <remarks>
    /// Streaming factories are registered as <c>Dictionary&lt;DataSourceKind, Func&lt;IMarketDataClient&gt;&gt;</c>
    /// entries inside <see cref="ProviderRegistry.RegisterStreamingFactory"/>. The old
    /// <c>MarketDataClientFactory</c> switch statement and <c>ProviderFactory</c> streaming
    /// creation are replaced by this dictionary-based approach.
    /// </remarks>
    private static IServiceCollection AddProviderServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // DataSourceRegistry - discovers providers decorated with [DataSource] (ADR-005).
        // This allows new providers to be registered by adding the attribute and implementing
        // the interface, without modifying factory methods or switch statements.
        services.AddSingleton<DataSourceRegistry>(sp =>
        {
            var registry = new DataSourceRegistry();
            registry.DiscoverFromAssemblies(typeof(MarketDataCollector.Infrastructure.NoOpMarketDataClient).Assembly);
            return registry;
        });

        // Register credential resolver - wraps ConfigurationService for provider credential resolution
        services.AddSingleton<ICredentialResolver>(sp =>
        {
            var configService = sp.GetRequiredService<ConfigurationService>();
            return new ConfigurationServiceCredentialAdapter(configService);
        });

        // Register the unified ProviderRegistry as singleton - this is the single source of truth
        // for all provider types (streaming, backfill, symbol search).
        services.AddSingleton<ProviderRegistry>(sp =>
        {
            var registry = new ProviderRegistry(alertDispatcher: null, LoggingSetup.ForContext<ProviderRegistry>());

            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            var credentialResolver = sp.GetRequiredService<ICredentialResolver>();
            var log = LoggingSetup.ForContext("ProviderRegistration");

            // --- Streaming factories (dictionary-based, replaces switch statements) ---
            RegisterStreamingFactories(registry, config, credentialResolver, sp, log);

            // --- Backfill providers ---
            RegisterBackfillProviders(registry, config, credentialResolver, log);

            // --- Symbol search providers ---
            RegisterSymbolSearchProviders(registry, config, credentialResolver, log);

            return registry;
        });

        // Keep ProviderFactory registered for backward compatibility with consumers
        // that still depend on it (BackfillCoordinators, HostStartup).
        services.AddSingleton<ProviderFactory>(sp =>
        {
            var configStore = sp.GetRequiredService<ConfigStore>();
            var config = configStore.Load();
            var credentialResolver = sp.GetRequiredService<ICredentialResolver>();
            var logger = LoggingSetup.ForContext<ProviderFactory>();
            return new ProviderFactory(config, credentialResolver, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers streaming client factory functions with the provider registry.
    /// Each <see cref="DataSourceKind"/> maps to a factory that creates the appropriate
    /// <see cref="IMarketDataClient"/> implementation, replacing the old switch-based approach.
    /// </summary>
    private static void RegisterStreamingFactories(
        ProviderRegistry registry,
        AppConfig config,
        ICredentialResolver credentialResolver,
        IServiceProvider sp,
        Serilog.ILogger log)
    {
        // IB (default)
        registry.RegisterStreamingFactory(DataSourceKind.IB, () =>
        {
            var publisher = sp.GetRequiredService<IMarketEventPublisher>();
            var tradeCollector = sp.GetRequiredService<TradeDataCollector>();
            var depthCollector = sp.GetRequiredService<MarketDepthCollector>();
            return new Infrastructure.Providers.InteractiveBrokers.IBMarketDataClient(
                publisher, tradeCollector, depthCollector);
        });

        // Alpaca
        registry.RegisterStreamingFactory(DataSourceKind.Alpaca, () =>
        {
            var tradeCollector = sp.GetRequiredService<TradeDataCollector>();
            var quoteCollector = sp.GetRequiredService<QuoteCollector>();
            var (keyId, secretKey) = credentialResolver.ResolveAlpacaCredentials(
                config.Alpaca?.KeyId, config.Alpaca?.SecretKey);
            return new Infrastructure.Providers.Alpaca.AlpacaMarketDataClient(
                tradeCollector, quoteCollector,
                config.Alpaca! with { KeyId = keyId ?? "", SecretKey = secretKey ?? "" });
        });

        // Polygon
        registry.RegisterStreamingFactory(DataSourceKind.Polygon, () =>
        {
            var publisher = sp.GetRequiredService<IMarketEventPublisher>();
            var tradeCollector = sp.GetRequiredService<TradeDataCollector>();
            var quoteCollector = sp.GetRequiredService<QuoteCollector>();
            return new Infrastructure.Providers.Polygon.PolygonMarketDataClient(
                publisher, tradeCollector, quoteCollector);
        });

        // StockSharp
        registry.RegisterStreamingFactory(DataSourceKind.StockSharp, () =>
        {
            var tradeCollector = sp.GetRequiredService<TradeDataCollector>();
            var depthCollector = sp.GetRequiredService<MarketDepthCollector>();
            var quoteCollector = sp.GetRequiredService<QuoteCollector>();
            return new Infrastructure.Providers.StockSharp.StockSharpMarketDataClient(
                tradeCollector, depthCollector, quoteCollector,
                config.StockSharp ?? new StockSharpConfig());
        });

        // NYSE (uses IB as underlying implementation per existing behavior)
        registry.RegisterStreamingFactory(DataSourceKind.NYSE, () =>
        {
            var publisher = sp.GetRequiredService<IMarketEventPublisher>();
            var tradeCollector = sp.GetRequiredService<TradeDataCollector>();
            var depthCollector = sp.GetRequiredService<MarketDepthCollector>();
            return new Infrastructure.Providers.InteractiveBrokers.IBMarketDataClient(
                publisher, tradeCollector, depthCollector);
        });

        log.Information("Registered streaming factories for {Count} data sources",
            registry.SupportedStreamingSources.Count);
    }

    /// <summary>
    /// Creates and registers backfill providers with the registry using credential resolution.
    /// </summary>
    private static void RegisterBackfillProviders(
        ProviderRegistry registry,
        AppConfig config,
        ICredentialResolver credentialResolver,
        Serilog.ILogger log)
    {
        var factory = new ProviderFactory(config, credentialResolver, log);
        var providers = factory.CreateBackfillProviders();
        foreach (var provider in providers)
        {
            registry.Register(provider);
        }
    }

    /// <summary>
    /// Creates and registers symbol search providers with the registry using credential resolution.
    /// </summary>
    private static void RegisterSymbolSearchProviders(
        ProviderRegistry registry,
        AppConfig config,
        ICredentialResolver credentialResolver,
        Serilog.ILogger log)
    {
        var factory = new ProviderFactory(config, credentialResolver, log);
        var providers = factory.CreateSymbolSearchProviders();
        foreach (var provider in providers)
        {
            registry.Register(provider);
        }
    }

    #endregion

    #region Pipeline Services

    /// <summary>
    /// Registers event pipeline and storage sink services.
    /// </summary>
    private static IServiceCollection AddPipelineServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // IEventMetrics - injectable metrics for pipeline and publisher.
        // When OpenTelemetry tracing is enabled, wraps DefaultEventMetrics with
        // TracedEventMetrics to export pipeline counters via System.Diagnostics.Metrics.
        if (options.EnableOpenTelemetry)
        {
            services.AddSingleton<IEventMetrics>(sp =>
                new Tracing.TracedEventMetrics(new DefaultEventMetrics()));
        }
        else
        {
            services.AddSingleton<IEventMetrics, DefaultEventMetrics>();
        }

        // DataQualityMonitoringService - orchestrates all quality monitoring components
        services.AddSingleton<DataQualityMonitoringService>(sp =>
        {
            var eventMetrics = sp.GetRequiredService<IEventMetrics>();
            return new DataQualityMonitoringService(eventMetrics: eventMetrics);
        });

        // DataFreshnessSlaMonitor - monitors data freshness SLA compliance
        services.AddSingleton<DataFreshnessSlaMonitor>();

        // JsonlStoragePolicy - controls file path generation
        services.AddSingleton<JsonlStoragePolicy>(sp =>
        {
            var storageOptions = sp.GetRequiredService<StorageOptions>();
            return new JsonlStoragePolicy(storageOptions);
        });

        // JsonlStorageSink - writes events to JSONL files (always registered)
        services.AddSingleton<JsonlStorageSink>(sp =>
        {
            var storageOptions = sp.GetRequiredService<StorageOptions>();
            var policy = sp.GetRequiredService<JsonlStoragePolicy>();
            return new JsonlStorageSink(storageOptions, policy);
        });

        // ParquetStorageSink - writes events to Parquet files (optional)
        services.AddSingleton<ParquetStorageSink>(sp =>
        {
            var storageOptions = sp.GetRequiredService<StorageOptions>();
            return new ParquetStorageSink(storageOptions);
        });

        // IStorageSink - resolved as CompositeSink when Parquet is enabled,
        // otherwise falls back to JsonlStorageSink alone.
        services.AddSingleton<IStorageSink>(sp =>
        {
            var storageOptions = sp.GetRequiredService<StorageOptions>();
            var jsonlSink = sp.GetRequiredService<JsonlStorageSink>();

            if (storageOptions.EnableParquetSink)
            {
                var parquetSink = sp.GetRequiredService<ParquetStorageSink>();
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<CompositeSink>>();
                return new CompositeSink(new IStorageSink[] { jsonlSink, parquetSink }, logger);
            }

            return jsonlSink;
        });

        // WriteAheadLog - crash-safe durability for the event pipeline
        services.AddSingleton<Storage.Archival.WriteAheadLog>(sp =>
        {
            var storageOptions = sp.GetRequiredService<StorageOptions>();
            var walDir = Path.Combine(storageOptions.RootPath, "_wal");
            return new Storage.Archival.WriteAheadLog(walDir, new Storage.Archival.WalOptions
            {
                SyncMode = Storage.Archival.WalSyncMode.BatchedSync,
                SyncBatchSize = 1000,
                MaxFlushDelay = TimeSpan.FromSeconds(1)
            });
        });

        // DroppedEventAuditTrail - records events dropped due to backpressure
        services.AddSingleton<Pipeline.DroppedEventAuditTrail>(sp =>
        {
            var storageOptions = sp.GetRequiredService<StorageOptions>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<Pipeline.DroppedEventAuditTrail>>();
            return new Pipeline.DroppedEventAuditTrail(storageOptions.RootPath, logger);
        });

        // EventPipeline - bounded channel event routing with WAL for durability
        // Uses IStorageSink which may be a CompositeSink wrapping JSONL + Parquet
        services.AddSingleton<EventPipeline>(sp =>
        {
            var sink = sp.GetRequiredService<IStorageSink>();
            var metrics = sp.GetRequiredService<IEventMetrics>();
            var wal = sp.GetService<Storage.Archival.WriteAheadLog>();
            var auditTrail = sp.GetService<Pipeline.DroppedEventAuditTrail>();
            return new EventPipeline(sink, EventPipelinePolicy.HighThroughput, metrics: metrics, wal: wal, auditTrail: auditTrail);
        });

        // IMarketEventPublisher - facade for publishing events
        services.AddSingleton<IMarketEventPublisher>(sp =>
        {
            var pipeline = sp.GetRequiredService<EventPipeline>();
            var metrics = sp.GetRequiredService<IEventMetrics>();
            return new PipelinePublisher(pipeline, metrics);
        });

        return services;
    }

    #endregion

    #region Collector Services

    /// <summary>
    /// Registers market data collector services.
    /// </summary>
    private static IServiceCollection AddCollectorServices(
        this IServiceCollection services,
        CompositionOptions options)
    {
        // QuoteCollector - BBO state tracking
        services.AddSingleton<QuoteCollector>(sp =>
        {
            var publisher = sp.GetRequiredService<IMarketEventPublisher>();
            return new QuoteCollector(publisher);
        });

        // TradeDataCollector - tick-by-tick trade processing
        services.AddSingleton<TradeDataCollector>(sp =>
        {
            var publisher = sp.GetRequiredService<IMarketEventPublisher>();
            var quoteCollector = sp.GetRequiredService<QuoteCollector>();
            return new TradeDataCollector(publisher, quoteCollector);
        });

        // MarketDepthCollector - L2 order book maintenance
        services.AddSingleton<MarketDepthCollector>(sp =>
        {
            var publisher = sp.GetRequiredService<IMarketEventPublisher>();
            return new MarketDepthCollector(publisher, requireExplicitSubscription: true);
        });

        return services;
    }

    #endregion

    #region HttpClient Factory Services

    /// <summary>
    /// Registers HttpClientFactory for proper HTTP client lifecycle management.
    /// Implements ADR-010: HttpClient Factory pattern.
    /// </summary>
    private static IServiceCollection AddHttpClientFactoryServices(this IServiceCollection services)
    {
        // Register all named HttpClient configurations with Polly policies
        services.AddMarketDataHttpClients();

        return services;
    }

    #endregion
}

/// <summary>
/// Simple publisher that wraps EventPipeline for IMarketEventPublisher interface.
/// Registered as singleton in the composition root, but also usable directly.
/// </summary>
public sealed class PipelinePublisher : IMarketEventPublisher
{
    private readonly EventPipeline _pipeline;
    private readonly IEventMetrics _metrics;

    public PipelinePublisher(EventPipeline pipeline, IEventMetrics? metrics = null)
    {
        _pipeline = pipeline;
        _metrics = metrics ?? new DefaultEventMetrics();
    }

    public bool TryPublish(in MarketEvent evt)
    {
        var ok = _pipeline.TryPublish(evt);

        // Integrity tracking lives here because EventPipeline is type-agnostic.
        // Published/Dropped are tracked inside EventPipeline.TryPublish() already.
        if (evt.Type == MarketEventType.Integrity) _metrics.IncIntegrity();
        return ok;
    }
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
        EnableCredentialServices = true,
        EnableProviderServices = true,
        EnablePipelineServices = true,
        EnableCollectorServices = true,
        EnableHttpClientFactory = true
    };

    /// <summary>
    /// Minimal options for console-only operation (utility commands, validation, etc.).
    /// </summary>
    public static CompositionOptions Minimal => new()
    {
        EnableSymbolManagement = false,
        EnableBackfillServices = false,
        EnableMaintenanceServices = false,
        EnableDiagnosticServices = false,
        EnableCredentialServices = false,
        EnableProviderServices = false,
        EnablePipelineServices = false,
        EnableCollectorServices = false,
        EnableHttpClientFactory = false
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
        EnableCredentialServices = true,
        EnableProviderServices = true,
        EnablePipelineServices = true,
        EnableCollectorServices = true,
        EnableHttpClientFactory = true
    };

    /// <summary>
    /// Options for streaming data collection (CLI headless mode).
    /// </summary>
    public static CompositionOptions Streaming => new()
    {
        EnableSymbolManagement = true,
        EnableBackfillServices = true,
        EnableMaintenanceServices = false,
        EnableDiagnosticServices = true,
        EnableCredentialServices = true,
        EnableProviderServices = true,
        EnablePipelineServices = true,
        EnableCollectorServices = true,
        EnableHttpClientFactory = true
    };

    /// <summary>
    /// Options for backfill-only operation.
    /// </summary>
    public static CompositionOptions BackfillOnly => new()
    {
        EnableSymbolManagement = false,
        EnableBackfillServices = true,
        EnableMaintenanceServices = false,
        EnableDiagnosticServices = false,
        EnableCredentialServices = true,
        EnableProviderServices = true,
        EnablePipelineServices = true,
        EnableCollectorServices = false,
        EnableHttpClientFactory = true
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

    /// <summary>
    /// Whether to enable provider factory and registry services.
    /// </summary>
    public bool EnableProviderServices { get; init; }

    /// <summary>
    /// Whether to enable event pipeline and storage sink services.
    /// </summary>
    public bool EnablePipelineServices { get; init; }

    /// <summary>
    /// Whether to enable market data collector services (Trade, Quote, Depth).
    /// </summary>
    public bool EnableCollectorServices { get; init; }

    /// <summary>
    /// Whether to enable HttpClientFactory for HTTP client lifecycle management.
    /// </summary>
    public bool EnableHttpClientFactory { get; init; }

    /// <summary>
    /// Whether to enable OpenTelemetry tracing and metrics instrumentation.
    /// When enabled, wraps IEventMetrics with TracedEventMetrics for OTLP-compatible
    /// pipeline counter export. Controlled via MDC_OTEL_ENABLED environment variable
    /// or explicit configuration.
    /// </summary>
    public bool EnableOpenTelemetry { get; init; }
}
