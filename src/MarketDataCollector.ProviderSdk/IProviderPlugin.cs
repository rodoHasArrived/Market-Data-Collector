using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.ProviderSdk;

/// <summary>
/// Entry point for provider plugin assemblies. Each plugin assembly must contain
/// exactly one public class implementing this interface. The host application
/// discovers and invokes plugins at startup to register their providers.
/// </summary>
/// <remarks>
/// Plugin assemblies reference only MarketDataCollector.ProviderSdk and
/// MarketDataCollector.Contracts. They do not reference the core application,
/// ensuring a clean dependency boundary.
///
/// Example usage:
/// <code>
/// public sealed class MyPlugin : IProviderPlugin
/// {
///     public ProviderPluginInfo Info => new("my-plugin", "My Data Plugin", "1.0.0");
///
///     public void Register(IProviderRegistration registration)
///     {
///         registration.AddHistoricalProvider&lt;MyHistoricalProvider&gt;();
///         registration.AddServices(services =>
///         {
///             services.AddHttpClient("my-provider");
///         });
///     }
/// }
/// </code>
/// </remarks>
public interface IProviderPlugin
{
    /// <summary>
    /// Metadata about this plugin (id, name, version).
    /// </summary>
    ProviderPluginInfo Info { get; }

    /// <summary>
    /// Register all providers and services contributed by this plugin.
    /// Called once during application startup.
    /// </summary>
    /// <param name="registration">Registration context for adding providers and services.</param>
    void Register(IProviderRegistration registration);
}

/// <summary>
/// Metadata describing a provider plugin.
/// </summary>
/// <param name="PluginId">Unique identifier for the plugin (e.g., "free-data", "alpaca").</param>
/// <param name="DisplayName">Human-readable plugin name.</param>
/// <param name="Version">Plugin version string.</param>
/// <param name="Description">Optional description of what the plugin provides.</param>
/// <param name="Author">Optional plugin author.</param>
public sealed record ProviderPluginInfo(
    string PluginId,
    string DisplayName,
    string Version,
    string? Description = null,
    string? Author = null);

/// <summary>
/// Registration context passed to <see cref="IProviderPlugin.Register"/>.
/// Allows plugins to contribute providers and services to the host application.
/// </summary>
public interface IProviderRegistration
{
    /// <summary>
    /// Register a streaming data provider type.
    /// The host will instantiate it via DI.
    /// </summary>
    void AddStreamingProvider<T>() where T : class, IStreamingProvider;

    /// <summary>
    /// Register a historical/backfill data provider type.
    /// The host will instantiate it via DI.
    /// </summary>
    void AddHistoricalProvider<T>() where T : class, IHistoricalProvider;

    /// <summary>
    /// Register a symbol search provider type.
    /// The host will instantiate it via DI.
    /// </summary>
    void AddSymbolSearchProvider<T>() where T : class, ISymbolSearchProvider;

    /// <summary>
    /// Register additional services needed by this plugin's providers
    /// (e.g., HttpClient registrations, configuration bindings).
    /// </summary>
    void AddServices(Action<IServiceCollection> configure);

    /// <summary>
    /// Declare credential fields required by this plugin's providers.
    /// Used for UI generation and configuration validation.
    /// </summary>
    void DeclareCredentials(params ProviderCredentialField[] fields);
}
