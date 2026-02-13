using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Application.Subscriptions;
using MarketDataCollector.Application.UI;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Http;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Infrastructure.Providers.Core;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MarketDataCollector.Application.Composition;

/// <summary>
/// Unified startup class that provides a single entry point for all host types.
/// All hosts flow through this class, which delegates to ServiceCompositionRoot.
/// </summary>
/// <remarks>
/// <para><b>Design Philosophy:</b></para>
/// <list type="bullet">
/// <item><description>Single setup path for CLI, Web, and Desktop modes</description></item>
/// <item><description>Uses ServiceCompositionRoot for all DI registration</description></item>
/// <item><description>Host-specific adapters customize endpoint exposure</description></item>
/// <item><description>Eliminates duplicated service wiring across hosts</description></item>
/// </list>
/// </remarks>
[ImplementsAdr("ADR-001", "Unified host startup for all deployment modes")]
public sealed class HostStartup : IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CompositionOptions _options;
    private readonly Serilog.ILogger _log;
    private bool _disposed;

    private HostStartup(IServiceProvider serviceProvider, CompositionOptions options, Serilog.ILogger log)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _log = log;
    }

    /// <summary>
    /// Creates a host startup for streaming data collection (CLI headless mode).
    /// </summary>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured HostStartup instance.</returns>
    public static HostStartup CreateForStreaming(string configPath)
    {
        var log = LoggingSetup.ForContext<HostStartup>();
        var options = CompositionOptions.Streaming with { ConfigPath = configPath };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog());
        services.AddMarketDataServices(options);

        var serviceProvider = services.BuildServiceProvider();
        InitializeHttpClientFactory(serviceProvider, log);

        return new HostStartup(serviceProvider, options, log);
    }

    /// <summary>
    /// Creates a host startup for backfill-only operation.
    /// </summary>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured HostStartup instance.</returns>
    public static HostStartup CreateForBackfill(string configPath)
    {
        var log = LoggingSetup.ForContext<HostStartup>();
        var options = CompositionOptions.BackfillOnly with { ConfigPath = configPath };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog());
        services.AddMarketDataServices(options);

        var serviceProvider = services.BuildServiceProvider();
        InitializeHttpClientFactory(serviceProvider, log);

        return new HostStartup(serviceProvider, options, log);
    }

    /// <summary>
    /// Creates a host startup for minimal utility commands (validation, config checks, etc.).
    /// </summary>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured HostStartup instance.</returns>
    public static HostStartup CreateForUtility(string configPath)
    {
        var log = LoggingSetup.ForContext<HostStartup>();
        var options = CompositionOptions.Minimal with { ConfigPath = configPath };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog());
        services.AddMarketDataServices(options);

        var serviceProvider = services.BuildServiceProvider();
        return new HostStartup(serviceProvider, options, log);
    }

    /// <summary>
    /// Gets a required service from the DI container.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull
        => _serviceProvider.GetRequiredService<T>();

    /// <summary>
    /// Gets a service from the DI container, or null if not registered.
    /// </summary>
    public T? GetService<T>() where T : class
        => _serviceProvider.GetService<T>();

    /// <summary>
    /// Gets the service provider for advanced scenarios.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the ConfigStore from the DI container.
    /// </summary>
    public ConfigStore ConfigStore => GetRequiredService<ConfigStore>();

    /// <summary>
    /// Gets the ConfigurationService from the DI container.
    /// </summary>
    public ConfigurationService ConfigurationService => GetRequiredService<ConfigurationService>();

    /// <summary>
    /// Gets the ProviderFactory from the DI container.
    /// </summary>
    public ProviderFactory ProviderFactory => GetRequiredService<ProviderFactory>();

    /// <summary>
    /// Gets the EventPipeline from the DI container.
    /// </summary>
    public EventPipeline Pipeline => GetRequiredService<EventPipeline>();

    /// <summary>
    /// Gets the StorageOptions from the DI container.
    /// </summary>
    public StorageOptions StorageOptions => GetRequiredService<StorageOptions>();

    /// <summary>
    /// Creates the streaming market data client based on configuration.
    /// Delegates to <see cref="ProviderRegistry.CreateStreamingClient"/> which uses
    /// dictionary-based factory lookup instead of switch statements.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <returns>Configured market data client.</returns>
    public IMarketDataClient CreateStreamingClient(AppConfig config)
    {
        var registry = GetRequiredService<ProviderRegistry>();
        return registry.CreateStreamingClient(config.DataSource);
    }

    /// <summary>
    /// Creates a subscription manager for managing symbol subscriptions.
    /// </summary>
    /// <param name="dataClient">The market data client.</param>
    /// <returns>Configured subscription manager.</returns>
    public SubscriptionOrchestrator CreateSubscriptionOrchestrator(IMarketDataClient dataClient)
    {
        var depthCollector = GetRequiredService<MarketDepthCollector>();
        var tradeCollector = GetRequiredService<TradeDataCollector>();
        var log = LoggingSetup.ForContext<SubscriptionOrchestrator>();

        return new SubscriptionOrchestrator(
            depthCollector,
            tradeCollector,
            dataClient,
            log);
    }

    /// <summary>
    /// Creates the backfill providers using the ProviderFactory.
    /// Uses unified credential resolution through ConfigurationService.
    /// </summary>
    /// <returns>List of configured backfill providers.</returns>
    public IReadOnlyList<IHistoricalDataProvider> CreateBackfillProviders()
    {
        var factory = GetRequiredService<ProviderFactory>();
        return factory.CreateBackfillProviders();
    }

    /// <summary>
    /// Creates a composite backfill provider with automatic failover.
    /// </summary>
    /// <param name="providers">Individual backfill providers.</param>
    /// <returns>Composite provider with failover support.</returns>
    public CompositeHistoricalDataProvider CreateCompositeBackfillProvider(
        IReadOnlyList<IHistoricalDataProvider> providers)
    {
        var factory = GetRequiredService<ProviderFactory>();
        return factory.CreateCompositeBackfillProvider(providers);
    }

    /// <summary>
    /// Creates a StatusWriter for persisting status information.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured StatusWriter.</returns>
    public StatusWriter CreateStatusWriter(AppConfig config, string configPath)
    {
        var configService = GetRequiredService<ConfigurationService>();
        var statusPath = Path.Combine(config.DataRoot, "_status", "status.json");
        return new StatusWriter(statusPath, () => configService.LoadAndPrepareConfig(configPath));
    }

    /// <summary>
    /// Starts hot-reload configuration watching.
    /// </summary>
    /// <param name="configPath">Path to configuration file.</param>
    /// <param name="onReload">Callback when configuration changes.</param>
    /// <param name="onError">Callback for errors.</param>
    /// <returns>ConfigWatcher instance.</returns>
    public ConfigWatcher? StartHotReload(
        string configPath,
        Action<AppConfig> onReload,
        Action<Exception> onError)
    {
        var configService = GetRequiredService<ConfigurationService>();
        return configService.StartHotReload(configPath, onReload, onError);
    }

    /// <summary>
    /// Initializes HttpClientFactory for proper HTTP client lifecycle management.
    /// </summary>
    private static void InitializeHttpClientFactory(IServiceProvider serviceProvider, Serilog.ILogger log)
    {
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        if (httpClientFactory != null)
        {
            HttpClientFactoryProvider.Initialize(serviceProvider);
            log.Debug("HttpClientFactory initialized with named clients for all data providers");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose pipeline if it exists
        if (_options.EnablePipelineServices)
        {
            var pipeline = GetService<EventPipeline>();
            if (pipeline != null)
            {
                await pipeline.FlushAsync();
                await pipeline.DisposeAsync();
            }

            var sink = GetService<JsonlStorageSink>();
            if (sink != null)
            {
                await sink.DisposeAsync();
            }
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Static entry point for unified host startup operations.
/// Provides factory methods for different deployment modes.
/// </summary>
public static class HostStartupFactory
{
    /// <summary>
    /// Creates the appropriate HostStartup based on deployment context.
    /// </summary>
    /// <param name="deployment">Deployment context from command line arguments.</param>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured HostStartup instance.</returns>
    public static HostStartup Create(DeploymentContext deployment, string configPath)
    {
        return deployment.Mode switch
        {
            DeploymentMode.Web => HostStartup.CreateForStreaming(configPath), // Web mode still needs streaming services
            DeploymentMode.Desktop => HostStartup.CreateForStreaming(configPath),
            _ => HostStartup.CreateForStreaming(configPath)
        };
    }

    /// <summary>
    /// Creates a HostStartup for backfill operations.
    /// </summary>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured HostStartup for backfill.</returns>
    public static HostStartup CreateForBackfill(string configPath)
        => HostStartup.CreateForBackfill(configPath);

    /// <summary>
    /// Creates a HostStartup for utility commands (validation, config checks, etc.).
    /// </summary>
    /// <param name="configPath">Path to configuration file.</param>
    /// <returns>Configured HostStartup for utilities.</returns>
    public static HostStartup CreateForUtility(string configPath)
        => HostStartup.CreateForUtility(configPath);
}
