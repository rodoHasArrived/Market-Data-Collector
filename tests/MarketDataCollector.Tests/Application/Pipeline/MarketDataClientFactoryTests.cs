using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure;
using MarketDataCollector.Infrastructure.Providers;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using MarketDataCollector.Tests.TestHelpers;
using Xunit;

namespace MarketDataCollector.Tests.Application.Pipeline;

public sealed class MarketDataClientFactoryTests
{
    [Fact]
    public void SupportedSources_ContainsExpectedProviders()
    {
        // Arrange
        var factory = new MarketDataClientFactory();

        // Assert
        factory.SupportedSources.Should().Contain(DataSourceKind.IB);
        factory.SupportedSources.Should().Contain(DataSourceKind.Alpaca);
        factory.SupportedSources.Should().Contain(DataSourceKind.Polygon);
        factory.SupportedSources.Should().Contain(DataSourceKind.StockSharp);
    }

    [Fact]
    public void Create_IB_ReturnsIBClient()
    {
        // Arrange
        var factory = new MarketDataClientFactory();
        var (config, publisher, trade, depth, quote) = CreateDependencies();

        // Act
        var client = factory.Create(DataSourceKind.IB, config, publisher, trade, depth, quote);

        // Assert
        client.Should().BeOfType<IBMarketDataClient>();
    }

    [Fact]
    public void Create_Polygon_ReturnsPolygonClient()
    {
        // Arrange
        var factory = new MarketDataClientFactory();
        var (config, publisher, trade, depth, quote) = CreateDependencies();

        // Act
        var client = factory.Create(DataSourceKind.Polygon, config, publisher, trade, depth, quote);

        // Assert
        client.Should().BeOfType<PolygonMarketDataClient>();
    }

    [Fact]
    public void Create_Alpaca_ReturnsAlpacaClient()
    {
        // Arrange
        var factory = new MarketDataClientFactory(
            alpacaCredentialResolver: (_, _) => ("test-key", "test-secret"));
        var (_, publisher, trade, depth, quote) = CreateDependencies();
        var config = new AppConfig
        {
            Alpaca = new AlpacaOptions { KeyId = "k", SecretKey = "s" }
        };

        // Act
        var client = factory.Create(DataSourceKind.Alpaca, config, publisher, trade, depth, quote);

        // Assert
        client.Should().BeOfType<AlpacaMarketDataClient>();
    }

    [Fact]
    public void Create_ThrowsOnNullConfig()
    {
        // Arrange
        var factory = new MarketDataClientFactory();
        var (_, publisher, trade, depth, quote) = CreateDependencies();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.Create(DataSourceKind.IB, null!, publisher, trade, depth, quote));
    }

    [Fact]
    public void Create_ThrowsOnNullPublisher()
    {
        // Arrange
        var factory = new MarketDataClientFactory();
        var config = new AppConfig();
        var publisher = new TestMarketEventPublisher();
        var trade = new TradeDataCollector(publisher);
        var depth = new MarketDepthCollector(publisher);
        var quote = new QuoteCollector(publisher);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.Create(DataSourceKind.IB, config, null!, trade, depth, quote));
    }

    [Fact]
    public void Create_UnknownDataSource_FallsBackToIB()
    {
        // Arrange
        var factory = new MarketDataClientFactory();
        var (config, publisher, trade, depth, quote) = CreateDependencies();

        // Act - use default case (which maps to IB)
        var client = factory.Create((DataSourceKind)999, config, publisher, trade, depth, quote);

        // Assert
        client.Should().BeOfType<IBMarketDataClient>();
    }

    [Fact]
    public void Create_Alpaca_UsesCustomCredentialResolver()
    {
        // Arrange
        var resolverCalled = false;
        var factory = new MarketDataClientFactory(
            alpacaCredentialResolver: (_, _) =>
            {
                resolverCalled = true;
                return ("custom-key", "custom-secret");
            });
        var (_, publisher, trade, depth, quote) = CreateDependencies();
        var config = new AppConfig
        {
            Alpaca = new AlpacaOptions()
        };

        // Act
        factory.Create(DataSourceKind.Alpaca, config, publisher, trade, depth, quote);

        // Assert
        resolverCalled.Should().BeTrue();
    }

    private static (AppConfig config, IMarketEventPublisher publisher, TradeDataCollector trade, MarketDepthCollector depth, QuoteCollector quote) CreateDependencies()
    {
        var config = new AppConfig();
        IMarketEventPublisher publisher = new TestMarketEventPublisher();
        var trade = new TradeDataCollector(publisher);
        var depth = new MarketDepthCollector(publisher);
        var quote = new QuoteCollector(publisher);
        return (config, publisher, trade, depth, quote);
    }
}
