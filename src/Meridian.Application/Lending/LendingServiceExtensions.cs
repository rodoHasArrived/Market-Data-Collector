using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using InMemoryStore = Meridian.Lending.EventStore.InMemoryLoanEventStore;
using PostgresStore = Meridian.Lending.EventStore.PostgresLoanEventStore;
using LendingStore = Meridian.Lending.EventStore.ILoanEventStore;

namespace Meridian.Application.Lending;

/// <summary>
/// Extension methods for registering the direct-lending domain services.
/// </summary>
public static class LendingServiceExtensions
{
    /// <summary>
    /// Adds the direct-lending domain services backed by an <b>in-memory</b> event store.
    /// </summary>
    /// <remarks>
    /// Suitable for development, unit tests, and single-process deployments that do not
    /// require durability across restarts.
    /// <code>
    /// builder.Services.AddLendingServices();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLendingServices(this IServiceCollection services)
    {
        services.AddSingleton<LendingStore.ILoanEventStore>(_ => new InMemoryStore.InMemoryLoanEventStore());
        services.AddSingleton<ILendingService, InMemoryLendingService>();
        return services;
    }

    /// <summary>
    /// Adds the direct-lending domain services backed by a <b>PostgreSQL</b> event store.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">Npgsql connection string for the PostgreSQL database.</param>
    /// <remarks>
    /// Requires the schema created by <c>deploy/sql/lending/V1__loan_contract_events.sql</c>.
    /// <code>
    /// builder.Services.AddPostgresLendingServices(
    ///     "Host=localhost;Database=meridian;Username=app;Password=secret");
    /// </code>
    /// </remarks>
    public static IServiceCollection AddPostgresLendingServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<LendingStore.ILoanEventStore>(
            _ => new PostgresStore.PostgresLoanEventStore(connectionString));
        services.AddSingleton<ILendingService, PostgresLendingService>();
        return services;
    }

    /// <summary>
    /// Adds the direct-lending domain services, choosing the backend based on
    /// <see cref="LendingStorageOptions"/> bound from configuration.
    /// </summary>
    /// <remarks>
    /// Bind via <c>appsettings.json</c>:
    /// <code>
    /// "Lending": {
    ///   "Storage": {
    ///     "UsePostgres": true,
    ///     "ConnectionString": "Host=localhost;Database=meridian;..."
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLendingServicesFromConfig(
        this IServiceCollection services)
    {
        services.AddSingleton<LendingStore.ILoanEventStore>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LendingStorageOptions>>().Value;
            if (opts.UsePostgres)
            {
                if (string.IsNullOrWhiteSpace(opts.ConnectionString))
                    throw new InvalidOperationException(
                        $"{LendingStorageOptions.Section}:{nameof(LendingStorageOptions.ConnectionString)} " +
                        "is required when UsePostgres is true.");

                return new PostgresStore.PostgresLoanEventStore(opts.ConnectionString);
            }

            return new InMemoryStore.InMemoryLoanEventStore();
        });

        services.AddSingleton<ILendingService>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LendingStorageOptions>>().Value;
            if (opts.UsePostgres)
                return ActivatorUtilities.CreateInstance<PostgresLendingService>(sp);

            return ActivatorUtilities.CreateInstance<InMemoryLendingService>(sp);
        });

        return services;
    }
}

