using MassTransit;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Messaging.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MarketDataCollector.Messaging.Configuration;

/// <summary>
/// Extension methods for configuring MassTransit in the service collection.
/// </summary>
public static class MassTransitServiceExtensions
{
    /// <summary>
    /// Adds MassTransit services to the service collection based on configuration.
    /// </summary>
    public static IServiceCollection AddMassTransitMessaging(
        this IServiceCollection services,
        MassTransitConfig config)
    {
        if (config is null || !config.Enabled)
        {
            Log.Information("MassTransit messaging is disabled");
            return services;
        }

        Log.Information("Configuring MassTransit with {Transport} transport", config.Transport);

        services.AddMassTransit(busConfig =>
        {
            // Register consumers
            busConfig.AddConsumer<TradeOccurredConsumer>();
            busConfig.AddConsumer<L2SnapshotReceivedConsumer>();
            busConfig.AddConsumer<BboQuoteUpdatedConsumer>();
            busConfig.AddConsumer<IntegrityEventConsumer>();

            // Configure transport
            ConfigureTransport(busConfig, config);
        });

        return services;
    }

    private static void ConfigureTransport(IBusRegistrationConfigurator busConfig, MassTransitConfig config)
    {
        var transport = ParseTransport(config.Transport);

        switch (transport)
        {
            case MassTransitTransport.RabbitMQ:
                ConfigureRabbitMq(busConfig, config);
                break;

            case MassTransitTransport.AzureServiceBus:
                ConfigureAzureServiceBus(busConfig, config);
                break;

            case MassTransitTransport.InMemory:
            default:
                ConfigureInMemory(busConfig, config);
                break;
        }
    }

    private static void ConfigureInMemory(IBusRegistrationConfigurator busConfig, MassTransitConfig config)
    {
        Log.Debug("Using InMemory transport for MassTransit");

        busConfig.UsingInMemory((context, cfg) =>
        {
            ApplyEndpointPrefix(cfg, config);
            cfg.ConfigureEndpoints(context);
        });
    }

    private static void ConfigureRabbitMq(IBusRegistrationConfigurator busConfig, MassTransitConfig config)
    {
        var rabbitConfig = config.RabbitMQ ?? new RabbitMqConfig();

        Log.Information("Configuring RabbitMQ transport: {Host}:{Port}/{VirtualHost}",
            rabbitConfig.Host, rabbitConfig.Port, rabbitConfig.VirtualHost);

        busConfig.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitConfig.Host, (ushort)rabbitConfig.Port, rabbitConfig.VirtualHost, h =>
            {
                h.Username(rabbitConfig.Username);
                h.Password(rabbitConfig.Password);

                if (rabbitConfig.UseSsl)
                {
                    h.UseSsl(s =>
                    {
                        s.Protocol = System.Security.Authentication.SslProtocols.Tls12;
                    });
                }

                if (rabbitConfig.PublisherConfirmation)
                {
                    h.PublisherConfirmation = true;
                }

                // Cluster configuration
                if (rabbitConfig.ClusterNodes?.Length > 0)
                {
                    h.UseCluster(c =>
                    {
                        foreach (var node in rabbitConfig.ClusterNodes)
                        {
                            c.Node(node);
                        }
                    });
                }
            });

            ConfigureRetry(cfg, config.Retry);
            ApplyEndpointPrefix(cfg, config);
            cfg.ConfigureEndpoints(context);
        });
    }

    private static void ConfigureAzureServiceBus(IBusRegistrationConfigurator busConfig, MassTransitConfig config)
    {
        var asbConfig = config.AzureServiceBus ?? throw new InvalidOperationException(
            "AzureServiceBus configuration is required when Transport is 'AzureServiceBus'");

        Log.Information("Configuring Azure Service Bus transport");

        busConfig.UsingAzureServiceBus((context, cfg) =>
        {
            if (asbConfig.UseManagedIdentity && !string.IsNullOrEmpty(asbConfig.Namespace))
            {
                // Use managed identity with namespace
                cfg.Host($"sb://{asbConfig.Namespace}.servicebus.windows.net");
            }
            else if (!string.IsNullOrEmpty(asbConfig.ConnectionString))
            {
                cfg.Host(asbConfig.ConnectionString);
            }
            else
            {
                throw new InvalidOperationException(
                    "Azure Service Bus requires either a ConnectionString or Namespace with UseManagedIdentity=true");
            }

            ConfigureRetry(cfg, config.Retry);
            ApplyEndpointPrefix(cfg, config);
            cfg.ConfigureEndpoints(context);
        });
    }

    private static void ConfigureRetry(IBusFactoryConfigurator cfg, RetryConfig? retryConfig)
    {
        retryConfig ??= new RetryConfig();

        cfg.UseMessageRetry(r =>
        {
            r.Exponential(
                retryConfig.MaxRetries,
                TimeSpan.FromMilliseconds(retryConfig.InitialIntervalMs),
                TimeSpan.FromMilliseconds(retryConfig.MaxIntervalMs),
                TimeSpan.FromMilliseconds(retryConfig.InitialIntervalMs));
        });
    }

    private static void ApplyEndpointPrefix(IBusFactoryConfigurator cfg, MassTransitConfig config)
    {
        // Apply endpoint prefix if configured
        if (!string.IsNullOrEmpty(config.EndpointPrefix))
        {
            cfg.MessageTopology.SetEntityNameFormatter(
                new PrefixEntityNameFormatter(cfg.MessageTopology.EntityNameFormatter, config.EndpointPrefix));
        }
    }

    private static MassTransitTransport ParseTransport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return MassTransitTransport.InMemory;

        return value.ToLowerInvariant() switch
        {
            "inmemory" => MassTransitTransport.InMemory,
            "rabbitmq" => MassTransitTransport.RabbitMQ,
            "azureservicebus" => MassTransitTransport.AzureServiceBus,
            _ => MassTransitTransport.InMemory
        };
    }
}

/// <summary>
/// Entity name formatter that adds a prefix to all entity names.
/// </summary>
internal sealed class PrefixEntityNameFormatter : IEntityNameFormatter
{
    private readonly IEntityNameFormatter _original;
    private readonly string _prefix;

    public PrefixEntityNameFormatter(IEntityNameFormatter original, string prefix)
    {
        _original = original;
        _prefix = prefix.TrimEnd('-', '_', '.') + "-";
    }

    public string FormatEntityName<T>()
    {
        return _prefix + _original.FormatEntityName<T>();
    }
}
