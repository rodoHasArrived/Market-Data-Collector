using Microsoft.Extensions.DependencyInjection;
using MarketDataCollector.Infrastructure.DataSources;

namespace MarketDataCollector.Infrastructure.Providers;

/// <summary>
/// Defines a provider module that can register provider services and data sources.
/// </summary>
public interface IProviderModule
{
    /// <summary>
    /// Register provider services into the DI container.
    /// </summary>
    void Register(IServiceCollection services, DataSourceRegistry registry);
}
