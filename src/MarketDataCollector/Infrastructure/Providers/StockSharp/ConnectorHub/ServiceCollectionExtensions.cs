using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MarketDataCollector.Infrastructure.Contracts;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Extension methods for registering StockSharp Connector Hub services.
/// </summary>
[ImplementsAdr("ADR-001", "DI registration for StockSharp connector hub")]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add StockSharp Connector Hub services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration section for ConnectorHub.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStockSharpConnectorHub(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Register options
        if (configuration != null)
        {
            services.Configure<ConnectorHubOptions>(configuration.GetSection("StockSharp:ConnectorHub"));
        }

        // Register adapter registry
        services.AddSingleton<IStockSharpAdapterRegistry>(sp =>
        {
            var options = GetOptions(sp, configuration);
            return options.Adapters.Count > 0
                ? StockSharpAdapterRegistry.CreateFromOptions(options)
                : StockSharpAdapterRegistry.CreateWithBuiltInAdapters();
        });

        // Register connector hub
        services.AddSingleton<StockSharpConnectorHub>();

        // Register as IMarketDataClient if enabled
        services.AddSingleton<IMarketDataClient>(sp =>
        {
            var options = GetOptions(sp, configuration);
            if (options.Enabled)
            {
                return sp.GetRequiredService<StockSharpConnectorHub>();
            }

            // Return null or throw based on configuration
            throw new InvalidOperationException(
                "StockSharp Connector Hub is not enabled. Set StockSharp:ConnectorHub:Enabled to true.");
        });

        return services;
    }

    /// <summary>
    /// Add StockSharp Connector Hub with explicit options.
    /// </summary>
    public static IServiceCollection AddStockSharpConnectorHub(
        this IServiceCollection services,
        ConnectorHubOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        services.AddSingleton(options);

        services.AddSingleton<IStockSharpAdapterRegistry>(sp =>
            options.Adapters.Count > 0
                ? StockSharpAdapterRegistry.CreateFromOptions(options)
                : StockSharpAdapterRegistry.CreateWithBuiltInAdapters());

        services.AddSingleton<StockSharpConnectorHub>();

        return services;
    }

    /// <summary>
    /// Add StockSharp Connector Hub with configuration action.
    /// </summary>
    public static IServiceCollection AddStockSharpConnectorHub(
        this IServiceCollection services,
        Action<ConnectorHubOptions> configure)
    {
        var options = new ConnectorHubOptions();
        configure(options);
        return services.AddStockSharpConnectorHub(options);
    }

    /// <summary>
    /// Add StockSharp Connector Hub configured for Interactive Brokers.
    /// </summary>
    public static IServiceCollection AddStockSharpConnectorHubForIB(
        this IServiceCollection services,
        string host = "127.0.0.1",
        int port = 7496,
        int clientId = 1)
    {
        return services.AddStockSharpConnectorHub(
            ConnectorHubOptionsFactory.ForInteractiveBrokers(host, port, clientId));
    }

    /// <summary>
    /// Add StockSharp Connector Hub configured for Alpaca Markets.
    /// </summary>
    public static IServiceCollection AddStockSharpConnectorHubForAlpaca(
        this IServiceCollection services,
        string keyId,
        string secretKey,
        bool usePaper = true,
        string feed = "iex")
    {
        return services.AddStockSharpConnectorHub(
            ConnectorHubOptionsFactory.ForAlpaca(keyId, secretKey, usePaper, feed));
    }

    private static ConnectorHubOptions GetOptions(
        IServiceProvider sp,
        IConfiguration? configuration)
    {
        // Try to get from DI first
        var options = sp.GetService<ConnectorHubOptions>();
        if (options != null)
            return options;

        // Try to bind from configuration
        if (configuration != null)
        {
            var configSection = configuration.GetSection("StockSharp:ConnectorHub");
            if (configSection.Exists())
            {
                options = new ConnectorHubOptions();
                configSection.Bind(options);
                return options;
            }
        }

        // Return default options
        return new ConnectorHubOptions();
    }
}
