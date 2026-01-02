using System.Reflection;
using MarketDataCollector.Application.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources;

/// <summary>
/// Extension methods for registering data sources with dependency injection.
/// </summary>
public static class DataSourceRegistration
{
    /// <summary>
    /// Adds data source services with automatic discovery.
    /// Discovers all types decorated with [DataSource] attribute and registers them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration for binding settings.</param>
    /// <param name="assemblies">Optional assemblies to scan. If empty, scans entry assembly.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataSources(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        var log = LoggingSetup.ForContext("DataSourceRegistration");

        // Get configuration
        var config = new UnifiedDataSourcesConfig();
        configuration.GetSection("DataSources").Bind(config);

        // Register configuration
        services.AddSingleton(config);
        services.AddSingleton(config.SymbolMapping);
        services.AddSingleton(config.Failover.ToOptions());

        // Register core services
        services.TryAddSingleton<ISymbolMapper, SymbolMapper>();
        services.TryAddSingleton<IDataSourceManager, DataSourceManager>();
        services.TryAddSingleton<IFallbackDataSourceOrchestrator, FallbackDataSourceOrchestrator>();

        // Discover and register data sources
        var assembliesToScan = assemblies.Length > 0
            ? assemblies
            : new[] { Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly() };

        var discoveredSources = DiscoverDataSources(assembliesToScan, log);

        foreach (var metadata in discoveredSources)
        {
            RegisterDataSource(services, metadata, config, log);
        }

        log.Information("Registered {Count} data sources", discoveredSources.Count);

        return services;
    }

    /// <summary>
    /// Adds a specific data source type.
    /// </summary>
    public static IServiceCollection AddDataSource<T>(
        this IServiceCollection services,
        IConfiguration configuration)
        where T : class, IDataSource
    {
        var type = typeof(T);
        var metadata = type.GetDataSourceMetadata();

        if (metadata == null)
        {
            throw new InvalidOperationException(
                $"Type {type.Name} must be decorated with [DataSource] attribute");
        }

        var config = new UnifiedDataSourcesConfig();
        configuration.GetSection("DataSources").Bind(config);

        var log = LoggingSetup.ForContext("DataSourceRegistration");
        RegisterDataSource(services, metadata, config, log);

        return services;
    }

    /// <summary>
    /// Adds a data source instance directly.
    /// </summary>
    public static IServiceCollection AddDataSourceInstance<T>(
        this IServiceCollection services,
        T instance)
        where T : class, IDataSource
    {
        services.AddSingleton<IDataSource>(instance);

        if (instance is IRealtimeDataSource realtime)
            services.AddSingleton<IRealtimeDataSource>(realtime);

        if (instance is IHistoricalDataSource historical)
            services.AddSingleton<IHistoricalDataSource>(historical);

        return services;
    }

    #region Discovery

    private static List<DataSourceMetadata> DiscoverDataSources(Assembly[] assemblies, ILogger log)
    {
        var discovered = new List<DataSourceMetadata>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsDataSource())
                    .ToList();

                foreach (var type in types)
                {
                    var metadata = type.GetDataSourceMetadata();
                    if (metadata != null)
                    {
                        discovered.Add(metadata);
                        log.Debug("Discovered data source: {Id} ({Type})", metadata.Id, metadata.ImplementationType.Name);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                log.Warning(ex, "Failed to load types from assembly {Assembly}", assembly.FullName);
            }
        }

        return discovered.OrderBy(m => m.Priority).ToList();
    }

    private static void RegisterDataSource(
        IServiceCollection services,
        DataSourceMetadata metadata,
        UnifiedDataSourcesConfig config,
        ILogger log)
    {
        var sourceConfig = config.Sources.GetValueOrDefault(metadata.Id);

        // Check if disabled in configuration
        if (sourceConfig != null && !sourceConfig.Enabled)
        {
            log.Debug("Skipping disabled data source: {Id}", metadata.Id);
            return;
        }

        // Check if enabled by default
        if (sourceConfig == null && !metadata.EnabledByDefault)
        {
            log.Debug("Skipping data source not enabled by default: {Id}", metadata.Id);
            return;
        }

        // Get options for this source
        var options = config.GetOptionsForSource(metadata.Id);

        // Register with appropriate factory
        services.AddSingleton(typeof(IDataSource), sp =>
        {
            return CreateDataSourceInstance(sp, metadata, options, log);
        });

        // Register specific interfaces
        if (metadata.IsRealtime)
        {
            services.AddSingleton(typeof(IRealtimeDataSource), sp =>
            {
                var sources = sp.GetServices<IDataSource>();
                return sources.OfType<IRealtimeDataSource>().First(s => s.Id == metadata.Id);
            });
        }

        if (metadata.IsHistorical)
        {
            services.AddSingleton(typeof(IHistoricalDataSource), sp =>
            {
                var sources = sp.GetServices<IDataSource>();
                return sources.OfType<IHistoricalDataSource>().First(s => s.Id == metadata.Id);
            });
        }

        log.Information("Registered data source: {Id} (Priority: {Priority}, Type: {Type})",
            metadata.Id, metadata.Priority, metadata.Type);
    }

    private static object CreateDataSourceInstance(
        IServiceProvider sp,
        DataSourceMetadata metadata,
        DataSourceOptions options,
        ILogger log)
    {
        try
        {
            // Try to resolve from DI container first
            var instance = sp.GetService(metadata.ImplementationType);
            if (instance != null)
                return instance;

            // Try to create with constructor injection
            var constructors = metadata.ImplementationType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToList();

            foreach (var ctor in constructors)
            {
                try
                {
                    var parameters = ctor.GetParameters();
                    var args = new object?[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];

                        // Handle known types
                        if (param.ParameterType == typeof(DataSourceOptions))
                            args[i] = options;
                        else if (param.ParameterType == typeof(ILogger))
                            args[i] = log.ForContext(metadata.ImplementationType);
                        else
                            args[i] = sp.GetService(param.ParameterType);
                    }

                    instance = ctor.Invoke(args);
                    if (instance != null)
                        return instance;
                }
                catch
                {
                    // Try next constructor
                }
            }

            // Fallback to parameterless constructor
            return Activator.CreateInstance(metadata.ImplementationType)
                ?? throw new InvalidOperationException($"Failed to create instance of {metadata.ImplementationType.Name}");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to create data source instance: {Type}", metadata.ImplementationType.Name);
            throw;
        }
    }

    #endregion
}

/// <summary>
/// Builder for configuring data sources with fluent API.
/// </summary>
public sealed class DataSourceBuilder
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly List<Assembly> _assemblies = new();
    private readonly List<Type> _explicitTypes = new();
    private readonly List<Func<IServiceProvider, IDataSource>> _factories = new();

    internal DataSourceBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    /// <summary>
    /// Scans the specified assembly for data sources.
    /// </summary>
    public DataSourceBuilder ScanAssembly(Assembly assembly)
    {
        _assemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Scans the assembly containing the specified type.
    /// </summary>
    public DataSourceBuilder ScanAssemblyContaining<T>()
    {
        return ScanAssembly(typeof(T).Assembly);
    }

    /// <summary>
    /// Adds a specific data source type.
    /// </summary>
    public DataSourceBuilder AddSource<T>() where T : class, IDataSource
    {
        _explicitTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds a data source using a factory function.
    /// </summary>
    public DataSourceBuilder AddSource(Func<IServiceProvider, IDataSource> factory)
    {
        _factories.Add(factory);
        return this;
    }

    /// <summary>
    /// Builds and registers all configured data sources.
    /// </summary>
    public IServiceCollection Build()
    {
        // Register from assemblies
        if (_assemblies.Count > 0)
        {
            _services.AddDataSources(_configuration, _assemblies.ToArray());
        }

        // Register explicit types
        foreach (var type in _explicitTypes)
        {
            var metadata = type.GetDataSourceMetadata();
            if (metadata != null)
            {
                _services.AddSingleton(typeof(IDataSource), type);
            }
        }

        // Register factories
        foreach (var factory in _factories)
        {
            _services.AddSingleton<IDataSource>(factory);
        }

        return _services;
    }
}

/// <summary>
/// Extension methods for fluent data source configuration.
/// </summary>
public static class DataSourceBuilderExtensions
{
    /// <summary>
    /// Configures data sources with a builder.
    /// </summary>
    public static IServiceCollection ConfigureDataSources(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DataSourceBuilder> configure)
    {
        var builder = new DataSourceBuilder(services, configuration);
        configure(builder);
        return builder.Build();
    }
}
