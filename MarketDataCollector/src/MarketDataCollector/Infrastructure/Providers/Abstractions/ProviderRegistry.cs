using System.Collections.Concurrent;
using System.Reflection;
using MarketDataCollector.Application.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Abstractions;

/// <summary>
/// Registry for discovering and managing provider types.
/// Supports automatic assembly scanning and manual registration.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>Get all registered provider types.</summary>
    IReadOnlyList<ProviderRegistration> GetRegistrations();

    /// <summary>Get registration by provider ID.</summary>
    ProviderRegistration? GetRegistration(string providerId);

    /// <summary>Find providers matching capability requirements.</summary>
    IReadOnlyList<ProviderRegistration> FindProviders(ProviderCapabilities required);

    /// <summary>Find providers matching any of the capabilities.</summary>
    IReadOnlyList<ProviderRegistration> FindProvidersWithAny(ProviderCapabilities any);

    /// <summary>Create a provider instance.</summary>
    IDataProvider CreateInstance(string providerId, IServiceProvider services);

    /// <summary>Register a provider type manually (for dynamic loading).</summary>
    void Register(ProviderRegistration registration);

    /// <summary>Scan assemblies for providers.</summary>
    int ScanAssemblies(params Assembly[] assemblies);

    /// <summary>Check if a provider is registered.</summary>
    bool IsRegistered(string providerId);
}

/// <summary>
/// Implementation of provider registry with assembly scanning.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, ProviderRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _log;

    public ProviderRegistry(ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<ProviderRegistry>();
    }

    /// <inheritdoc/>
    public int ScanAssemblies(params Assembly[] assemblies)
    {
        var registeredCount = 0;

        foreach (var assembly in assemblies)
        {
            try
            {
                var providerTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => t.GetCustomAttribute<DataProviderAttribute>() != null)
                    .Where(t => typeof(IDataProvider).IsAssignableFrom(t));

                foreach (var type in providerTypes)
                {
                    var attr = type.GetCustomAttribute<DataProviderAttribute>()!;
                    var registration = ProviderRegistration.FromAttribute(attr, type);

                    if (_registrations.TryAdd(attr.Id, registration))
                    {
                        _log.Information(
                            "Registered provider {ProviderId} ({DisplayName}) with capabilities: {Capabilities}",
                            attr.Id, attr.DisplayName, attr.Capabilities);
                        registeredCount++;
                    }
                    else
                    {
                        _log.Warning(
                            "Provider {ProviderId} already registered, skipping duplicate from {Type}",
                            attr.Id, type.FullName);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                _log.Warning(ex, "Failed to scan assembly {Assembly}", assembly.FullName);
            }
        }

        return registeredCount;
    }

    /// <inheritdoc/>
    public void Register(ProviderRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!_registrations.TryAdd(registration.ProviderId, registration))
        {
            throw new InvalidOperationException($"Provider '{registration.ProviderId}' is already registered");
        }

        _log.Information(
            "Manually registered provider {ProviderId} ({DisplayName})",
            registration.ProviderId, registration.DisplayName);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProviderRegistration> GetRegistrations() =>
        _registrations.Values.OrderBy(r => r.DefaultPriority).ToList();

    /// <inheritdoc/>
    public ProviderRegistration? GetRegistration(string providerId) =>
        _registrations.TryGetValue(providerId, out var registration) ? registration : null;

    /// <inheritdoc/>
    public bool IsRegistered(string providerId) =>
        _registrations.ContainsKey(providerId);

    /// <inheritdoc/>
    public IReadOnlyList<ProviderRegistration> FindProviders(ProviderCapabilities required) =>
        _registrations.Values
            .Where(r => r.Capabilities.HasAll(required))
            .OrderBy(r => r.DefaultPriority)
            .ToList();

    /// <inheritdoc/>
    public IReadOnlyList<ProviderRegistration> FindProvidersWithAny(ProviderCapabilities any) =>
        _registrations.Values
            .Where(r => r.Capabilities.HasAny(any))
            .OrderBy(r => r.DefaultPriority)
            .ToList();

    /// <inheritdoc/>
    public IDataProvider CreateInstance(string providerId, IServiceProvider services)
    {
        if (!_registrations.TryGetValue(providerId, out var registration))
            throw new ProviderNotFoundException(providerId);

        try
        {
            return (IDataProvider)ActivatorUtilities.CreateInstance(services, registration.ProviderType);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to create instance of provider {ProviderId}", providerId);
            throw new ProviderCreationException(providerId, ex);
        }
    }
}

/// <summary>
/// Exception thrown when a provider is not found in the registry.
/// </summary>
public sealed class ProviderNotFoundException : Exception
{
    public string ProviderId { get; }

    public ProviderNotFoundException(string providerId)
        : base($"Provider '{providerId}' is not registered")
    {
        ProviderId = providerId;
    }
}

/// <summary>
/// Exception thrown when provider instance creation fails.
/// </summary>
public sealed class ProviderCreationException : Exception
{
    public string ProviderId { get; }

    public ProviderCreationException(string providerId, Exception innerException)
        : base($"Failed to create instance of provider '{providerId}'", innerException)
    {
        ProviderId = providerId;
    }
}

/// <summary>
/// Extension methods for IServiceCollection to register the provider registry.
/// </summary>
public static class ProviderRegistryExtensions
{
    /// <summary>
    /// Add the provider registry and scan the calling assembly for providers.
    /// </summary>
    public static IServiceCollection AddProviderRegistry(this IServiceCollection services)
    {
        services.AddSingleton<IProviderRegistry>(sp =>
        {
            var registry = new ProviderRegistry();
            registry.ScanAssemblies(Assembly.GetCallingAssembly(), Assembly.GetExecutingAssembly());
            return registry;
        });

        return services;
    }

    /// <summary>
    /// Add the provider registry and scan specific assemblies for providers.
    /// </summary>
    public static IServiceCollection AddProviderRegistry(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddSingleton<IProviderRegistry>(sp =>
        {
            var registry = new ProviderRegistry();
            registry.ScanAssemblies(assemblies);
            return registry;
        });

        return services;
    }
}
