using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Application.Lending;

/// <summary>
/// Extension methods for registering the direct-lending domain services.
/// </summary>
public static class LendingServiceExtensions
{
    /// <summary>
    /// Adds the direct-lending domain services to the service collection.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="ILendingService"/> as a singleton backed by an in-memory
    /// event store. Call this in any .NET host (ASP.NET Core, Worker Service, etc.)
    /// to embed the lending aggregate without the full Meridian stack:
    /// <code>
    /// builder.Services.AddLendingServices();
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for chaining.</returns>
    public static IServiceCollection AddLendingServices(this IServiceCollection services)
    {
        services.AddSingleton<ILendingService, InMemoryLendingService>();
        return services;
    }
}
