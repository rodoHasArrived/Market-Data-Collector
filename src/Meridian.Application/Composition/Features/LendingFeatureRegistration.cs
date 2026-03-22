using Meridian.Application.Lending;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Application.Composition.Features;

/// <summary>
/// Registers the direct-lending domain services into the DI container.
/// </summary>
/// <remarks>
/// Adds <see cref="ILendingService"/> as a singleton backed by the
/// in-memory event store. Replace the registration with a durable
/// implementation when persistence is required.
/// </remarks>
internal sealed class LendingFeatureRegistration : IServiceFeatureRegistration
{
    public IServiceCollection Register(IServiceCollection services, CompositionOptions options)
    {
        services.AddSingleton<ILendingService, InMemoryLendingService>();
        return services;
    }
}
