using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.ProviderSdk;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for the plugin registration workflow: IProviderPlugin → IProviderRegistration.
/// Verifies that plugins correctly register their providers and services.
/// </summary>
public sealed class PluginRegistrationTests
{
    [Fact]
    public void FreeDataPlugin_RegistersThreeHistoricalProviders()
    {
        // Arrange
        var plugin = new Providers.FreeData.FreeDataPlugin();
        var registration = new TestRegistrationContext();

        // Act
        plugin.Register(registration);

        // Assert
        registration.HistoricalProviderTypes.Should().HaveCount(3);
        registration.HistoricalProviderTypes.Should().Contain(typeof(Providers.FreeData.Stooq.StooqProvider));
        registration.HistoricalProviderTypes.Should().Contain(typeof(Providers.FreeData.Tiingo.TiingoProvider));
        registration.HistoricalProviderTypes.Should().Contain(typeof(Providers.FreeData.YahooFinance.YahooFinanceProvider));
    }

    [Fact]
    public void FreeDataPlugin_RegistersNoStreamingOrSearchProviders()
    {
        // Arrange
        var plugin = new Providers.FreeData.FreeDataPlugin();
        var registration = new TestRegistrationContext();

        // Act
        plugin.Register(registration);

        // Assert
        registration.StreamingProviderTypes.Should().BeEmpty();
        registration.SymbolSearchProviderTypes.Should().BeEmpty();
    }

    [Fact]
    public void FreeDataPlugin_RegistersHttpClients()
    {
        // Arrange
        var plugin = new Providers.FreeData.FreeDataPlugin();
        var registration = new TestRegistrationContext();

        // Act
        plugin.Register(registration);

        // Assert
        registration.ServicesConfigured.Should().BeTrue();
    }

    [Fact]
    public void FreeDataPlugin_DeclaresCredentials()
    {
        // Arrange
        var plugin = new Providers.FreeData.FreeDataPlugin();
        var registration = new TestRegistrationContext();

        // Act
        plugin.Register(registration);

        // Assert
        registration.DeclaredCredentials.Should().ContainSingle();
        var credential = registration.DeclaredCredentials[0];
        credential.Name.Should().Be("TiingoToken");
        credential.EnvironmentVariable.Should().Be("TIINGO_API_TOKEN");
        credential.IsSensitive.Should().BeTrue();
        credential.Required.Should().BeFalse();
    }

    [Fact]
    public void FreeDataPlugin_InfoHasCorrectMetadata()
    {
        // Arrange
        var plugin = new Providers.FreeData.FreeDataPlugin();

        // Assert
        plugin.Info.PluginId.Should().Be("free-data");
        plugin.Info.DisplayName.Should().Be("Free Data Providers");
        plugin.Info.Version.Should().Be("1.0.0");
        plugin.Info.Description.Should().NotBeNullOrEmpty();
        plugin.Info.Author.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProviderPluginInfo_RecordEquality()
    {
        // Arrange
        var info1 = new ProviderPluginInfo("test-plugin", "Test", "1.0.0");
        var info2 = new ProviderPluginInfo("test-plugin", "Test", "1.0.0");
        var info3 = new ProviderPluginInfo("other-plugin", "Other", "2.0.0");

        // Assert
        info1.Should().Be(info2);
        info1.Should().NotBe(info3);
    }

    /// <summary>
    /// Test-only implementation of IProviderRegistration that captures registrations.
    /// </summary>
    private sealed class TestRegistrationContext : IProviderRegistration
    {
        public List<Type> StreamingProviderTypes { get; } = new();
        public List<Type> HistoricalProviderTypes { get; } = new();
        public List<Type> SymbolSearchProviderTypes { get; } = new();
        public List<ProviderCredentialField> DeclaredCredentials { get; } = new();
        public bool ServicesConfigured { get; private set; }

        public void AddStreamingProvider<T>() where T : class, IStreamingProvider
            => StreamingProviderTypes.Add(typeof(T));

        public void AddHistoricalProvider<T>() where T : class, IHistoricalProvider
            => HistoricalProviderTypes.Add(typeof(T));

        public void AddSymbolSearchProvider<T>() where T : class, ProviderSdk.Providers.ISymbolSearchProvider
            => SymbolSearchProviderTypes.Add(typeof(T));

        public void AddServices(Action<IServiceCollection> configure)
        {
            var services = new ServiceCollection();
            configure(services);
            ServicesConfigured = true;
        }

        public void DeclareCredentials(params ProviderCredentialField[] fields)
            => DeclaredCredentials.AddRange(fields);
    }
}
