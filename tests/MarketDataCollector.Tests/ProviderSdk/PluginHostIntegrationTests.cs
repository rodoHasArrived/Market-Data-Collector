using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.PluginAdapters;
using MarketDataCollector.ProviderSdk;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Integration tests for <see cref="ProviderPluginHost"/> plugin discovery and registration.
/// Tests the full lifecycle: discover → load → register → resolve from DI.
/// </summary>
public sealed class PluginHostIntegrationTests
{
    [Fact]
    public void DiscoverAndLoadPlugins_FindsFreeDataPlugin()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var host = new ProviderPluginHost(services);

        // Act
        host.DiscoverAndLoadPlugins();

        // Assert
        host.LoadedPlugins.Should().ContainSingle(p => p.PluginId == "free-data");
    }

    [Fact]
    public void RegisterProviderServices_RegistersHistoricalProviderTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        // Add IHttpClientFactory since providers need it
        services.AddHttpClient();
        var host = new ProviderPluginHost(services);

        // Act
        host.DiscoverAndLoadPlugins();
        host.RegisterProviderServices();

        // Assert - host should be registered in DI
        var sp = services.BuildServiceProvider();
        var resolvedHost = sp.GetService<ProviderPluginHost>();
        resolvedHost.Should().NotBeNull();
        resolvedHost!.LoadedPlugins.Should().ContainSingle(p => p.PluginId == "free-data");
    }

    [Fact]
    public void RegisterProviderServices_ResolvesHistoricalProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        // Add named HTTP clients the FreeData plugin needs
        services.AddHttpClient("stooq-historical");
        services.AddHttpClient("tiingo-historical");
        services.AddHttpClient("yahoo-historical");
        var host = new ProviderPluginHost(services);

        // Act
        host.DiscoverAndLoadPlugins();
        host.RegisterProviderServices();
        var sp = services.BuildServiceProvider();

        // Assert
        var historicalProviders = sp.GetServices<IHistoricalProvider>().ToList();
        historicalProviders.Should().HaveCount(3);
        historicalProviders.Select(p => p.ProviderId).Should()
            .Contain("stooq-plugin")
            .And.Contain("tiingo-plugin")
            .And.Contain("yahoo-plugin");
    }

    [Fact]
    public void LoadPlugin_HandlesNullGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        var host = new ProviderPluginHost(services);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            host.LoadPlugin(null!));
    }

    [Fact]
    public void LoadPluginsFromDirectory_IgnoresNonExistentDirectory()
    {
        // Arrange
        var services = new ServiceCollection();
        var host = new ProviderPluginHost(services);

        // Act - should not throw
        host.LoadPluginsFromDirectory("/nonexistent/plugin/dir");

        // Assert
        host.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ThrowsOnNullServices()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ProviderPluginHost(null!));
    }
}
