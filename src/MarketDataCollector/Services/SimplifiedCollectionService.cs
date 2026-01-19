using MarketDataCollector.Configuration;
using MarketDataCollector.Providers;
using MarketDataCollector.Storage;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MarketDataCollector.Services;

/// <summary>
/// Background service that collects market data from all enabled providers
/// and persists to storage.
/// </summary>
public sealed class SimplifiedCollectionService : BackgroundService
{
    private static readonly ILogger Logger = Log.ForContext<SimplifiedCollectionService>();

    private readonly SimplifiedAppConfiguration _config;
    private readonly IProviderFactory _providerFactory;
    private readonly ISimplifiedMarketDataStore _store;
    private readonly Dictionary<string, ISimplifiedMarketDataProvider> _providers = new();
    private readonly Dictionary<string, Task> _collectionTasks = new();

    public SimplifiedCollectionService(
        SimplifiedAppConfiguration config,
        IProviderFactory providerFactory,
        ISimplifiedMarketDataStore store)
    {
        _config = config;
        _providerFactory = providerFactory;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information("Starting market data collection service");

        // Initialize storage
        await _store.InitializeAsync(stoppingToken);

        // Create providers for all enabled configurations
        foreach (var providerConfig in _config.EnabledProviders)
        {
            try
            {
                var provider = _providerFactory.Create(providerConfig);
                _providers[providerConfig.Name] = provider;
                Logger.Information("Created provider: {Provider} ({Type})",
                    providerConfig.Name, providerConfig.Type);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create provider: {Provider}", providerConfig.Name);
            }
        }

        if (_providers.Count == 0)
        {
            Logger.Error("No providers were created. Exiting.");
            return;
        }

        // Connect all providers
        var connectTasks = _providers.Select(async kvp =>
        {
            var (name, provider) = kvp;
            try
            {
                await provider.ConnectAsync(stoppingToken);
                Logger.Information("Connected to provider: {Provider}", name);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to provider: {Provider}", name);
            }
        });

        await Task.WhenAll(connectTasks);

        // Start collection tasks for each provider
        foreach (var (name, provider) in _providers)
        {
            var providerConfig = _config.GetProvider(name);
            if (providerConfig == null) continue;

            var symbols = providerConfig.GetAllSymbols().ToList();

            var task = CollectFromProviderAsync(provider, symbols, stoppingToken);
            _collectionTasks[name] = task;
        }

        // Wait for all collection tasks
        try
        {
            await Task.WhenAll(_collectionTasks.Values);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Collection service cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Collection service error");
        }
    }

    private async Task CollectFromProviderAsync(
        ISimplifiedMarketDataProvider provider,
        List<string> symbols,
        CancellationToken ct)
    {
        var batchBuffer = new List<SimplifiedMarketData>();
        const int batchSize = 100;
        var lastFlush = DateTime.UtcNow;
        var flushInterval = TimeSpan.FromSeconds(5);

        Logger.Information("Starting collection from {Provider} for {Count} symbols",
            provider.Name, symbols.Count);

        try
        {
            await foreach (var data in provider.StreamAsync(symbols, ct))
            {
                batchBuffer.Add(data);

                // Flush batch if full or interval elapsed
                if (batchBuffer.Count >= batchSize ||
                    DateTime.UtcNow - lastFlush > flushInterval)
                {
                    await FlushBatchAsync(batchBuffer, ct);
                    batchBuffer.Clear();
                    lastFlush = DateTime.UtcNow;
                }
            }

            // Flush remaining
            if (batchBuffer.Count > 0)
            {
                await FlushBatchAsync(batchBuffer, ct);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Collection from {Provider} cancelled", provider.Name);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error collecting from {Provider}", provider.Name);
        }
    }

    private async Task FlushBatchAsync(List<SimplifiedMarketData> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        try
        {
            await _store.AppendBatchAsync(batch, ct);
            Logger.Debug("Stored {Count} records", batch.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to store batch of {Count} records", batch.Count);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.Information("Stopping market data collection service");

        // Disconnect all providers
        var disconnectTasks = _providers.Select(async kvp =>
        {
            var (name, provider) = kvp;
            try
            {
                await provider.DisconnectAsync(cancellationToken);
                Logger.Information("Disconnected from provider: {Provider}", name);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error disconnecting from provider: {Provider}", name);
            }
        });

        await Task.WhenAll(disconnectTasks);

        // Dispose providers
        foreach (var provider in _providers.Values)
        {
            try
            {
                await provider.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error disposing provider");
            }
        }

        _providers.Clear();

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Gets health status for all providers.
    /// </summary>
    public async Task<Dictionary<string, ProviderHealthStatus>> GetHealthAsync(CancellationToken ct = default)
    {
        var health = new Dictionary<string, ProviderHealthStatus>();

        foreach (var (name, provider) in _providers)
        {
            try
            {
                health[name] = await provider.GetHealthAsync(ct);
            }
            catch (Exception ex)
            {
                health[name] = ProviderHealthStatus.Unhealthy(ex.Message);
            }
        }

        return health;
    }
}

/// <summary>
/// Extension methods for registering the collection service.
/// </summary>
public static class CollectionServiceExtensions
{
    /// <summary>
    /// Adds the simplified market data collection service.
    /// </summary>
    public static IServiceCollection AddSimplifiedCollection(
        this IServiceCollection services,
        SimplifiedAppConfiguration config)
    {
        // Register configuration
        services.AddSingleton(config);

        // Register provider factory
        services.AddSingleton<IProviderFactory, SimplifiedProviderFactory>();

        // Register storage
        var storagePath = Path.Combine(config.DataPath, "market_data.db");
        services.AddSingleton<ISimplifiedMarketDataStore>(
            new SqliteSimplifiedStore(storagePath));

        // Register background service
        services.AddHostedService<SimplifiedCollectionService>();

        return services;
    }
}
