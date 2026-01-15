using Microsoft.Extensions.DependencyInjection;

namespace DataIngestion.Contracts.Services.DeadLetter;

/// <summary>
/// Extension methods for registering dead letter queues with DI containers.
/// </summary>
public static class DeadLetterQueueExtensions
{
    /// <summary>
    /// Adds a dead letter queue for the specified message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of messages the queue will handle.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">Name of the service using this queue.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDeadLetterQueue<TMessage>(
        this IServiceCollection services,
        string serviceName,
        Action<DeadLetterQueueConfig>? configureOptions = null)
    {
        var config = new DeadLetterQueueConfig { QueueName = typeof(TMessage).Name };
        configureOptions?.Invoke(config);

        services.AddSingleton<IDeadLetterQueue<TMessage>>(sp =>
            new DeadLetterQueue<TMessage>(config, serviceName));

        return services;
    }

    /// <summary>
    /// Adds a dead letter queue with the specified configuration.
    /// </summary>
    /// <typeparam name="TMessage">The type of messages the queue will handle.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">Name of the service using this queue.</param>
    /// <param name="config">The queue configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDeadLetterQueue<TMessage>(
        this IServiceCollection services,
        string serviceName,
        DeadLetterQueueConfig config)
    {
        services.AddSingleton<IDeadLetterQueue<TMessage>>(sp =>
            new DeadLetterQueue<TMessage>(config, serviceName));

        return services;
    }
}
