using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;
using MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;
using Xunit;

namespace MarketDataCollector.Tests.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Unit tests for StockSharpAdapterRegistry.
/// </summary>
public sealed class StockSharpAdapterRegistryTests
{
    [Fact]
    public void CreateWithBuiltInAdapters_ShouldRegisterAllBuiltInAdapters()
    {
        // Act
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Assert
        var adapters = registry.GetAllAdapters();
        adapters.Should().NotBeEmpty();
        adapters.Should().Contain(a => a.AdapterId == "ib");
        adapters.Should().Contain(a => a.AdapterId == "alpaca");
        adapters.Should().Contain(a => a.AdapterId == "polygon");
        adapters.Should().Contain(a => a.AdapterId == "rithmic");
        adapters.Should().Contain(a => a.AdapterId == "iqfeed");
        adapters.Should().Contain(a => a.AdapterId == "binance");
    }

    [Fact]
    public void GetAdapterConfig_ShouldReturnCorrectAdapter()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act
        var ibAdapter = registry.GetAdapterConfig("ib");
        var alpacaAdapter = registry.GetAdapterConfig("alpaca");

        // Assert
        ibAdapter.Should().NotBeNull();
        ibAdapter!.DisplayName.Should().Be("Interactive Brokers");

        alpacaAdapter.Should().NotBeNull();
        alpacaAdapter!.DisplayName.Should().Be("Alpaca Markets");
    }

    [Fact]
    public void GetAdapterConfig_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act
        var adapter = registry.GetAdapterConfig("nonexistent");

        // Assert
        adapter.Should().BeNull();
    }

    [Fact]
    public void GetAdapterForProvider_ShouldMapProviderToAdapter()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act & Assert
        registry.GetAdapterForProvider("alpaca").Should().Be("alpaca");
        registry.GetAdapterForProvider("alpaca-markets").Should().Be("alpaca");
        registry.GetAdapterForProvider("polygon").Should().Be("polygon");
        registry.GetAdapterForProvider("polygon.io").Should().Be("polygon");
        registry.GetAdapterForProvider("ib").Should().Be("ib");
        registry.GetAdapterForProvider("interactivebrokers").Should().Be("ib");
        registry.GetAdapterForProvider("tws").Should().Be("ib");
        registry.GetAdapterForProvider("binance").Should().Be("binance");
    }

    [Fact]
    public void GetAdapterForExchange_ShouldMapExchangeToAdapter()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act & Assert
        // NYSE should be handled by first matching adapter (IB has highest coverage)
        var nyseAdapter = registry.GetAdapterForExchange("NYSE");
        nyseAdapter.Should().NotBeNull();

        // CME should be handled by futures-capable adapter
        var cmeAdapter = registry.GetAdapterForExchange("CME");
        cmeAdapter.Should().NotBeNull();

        // BINANCE should be handled by binance adapter
        registry.GetAdapterForExchange("BINANCE").Should().Be("binance");
    }

    [Fact]
    public void GetAdapterForAssetClass_ShouldMapAssetClassToAdapter()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act & Assert
        var equityAdapter = registry.GetAdapterForAssetClass("equity");
        equityAdapter.Should().NotBeNull();

        var futuresAdapter = registry.GetAdapterForAssetClass("futures");
        futuresAdapter.Should().NotBeNull();

        var cryptoAdapter = registry.GetAdapterForAssetClass("crypto");
        cryptoAdapter.Should().Be("binance");
    }

    [Fact]
    public void RegisterAdapter_ShouldAddNewAdapter()
    {
        // Arrange
        var registry = new StockSharpAdapterRegistry();
        var config = new InteractiveBrokersAdapterConfig();

        // Act
        registry.RegisterAdapter(config);

        // Assert
        registry.GetAdapterConfig("ib").Should().NotBeNull();
        registry.GetAllAdapters().Should().Contain(a => a.AdapterId == "ib");
    }

    [Fact]
    public void UnregisterAdapter_ShouldRemoveAdapter()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act
        var result = registry.UnregisterAdapter("ib");

        // Assert
        result.Should().BeTrue();
        registry.GetAdapterConfig("ib").Should().BeNull();
        registry.GetAdapterForProvider("interactivebrokers").Should().BeNull();
    }

    [Fact]
    public void UnregisterAdapter_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        var registry = new StockSharpAdapterRegistry();

        // Act
        var result = registry.UnregisterAdapter("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAdaptersForMarket_ShouldReturnCorrectAdapters()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act
        var usAdapters = registry.GetAdaptersForMarket("US");

        // Assert
        usAdapters.Should().NotBeEmpty();
        usAdapters.Should().Contain(a => a.AdapterId == "alpaca");
        usAdapters.Should().Contain(a => a.AdapterId == "polygon");
    }

    [Fact]
    public void GetDepthCapableAdapters_ShouldReturnOnlyDepthCapableAdapters()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act
        var depthAdapters = registry.GetDepthCapableAdapters();

        // Assert
        depthAdapters.Should().NotBeEmpty();
        depthAdapters.Should().OnlyContain(a => a.SupportsMarketDepth);
        depthAdapters.Should().Contain(a => a.AdapterId == "ib");
        depthAdapters.Should().Contain(a => a.AdapterId == "rithmic");
        depthAdapters.Should().Contain(a => a.AdapterId == "binance");
    }

    [Fact]
    public void GetBackfillCapableAdapters_ShouldReturnOnlyBackfillCapableAdapters()
    {
        // Arrange
        var registry = StockSharpAdapterRegistry.CreateWithBuiltInAdapters();

        // Act
        var backfillAdapters = registry.GetBackfillCapableAdapters();

        // Assert
        backfillAdapters.Should().NotBeEmpty();
        backfillAdapters.Should().OnlyContain(a => a.SupportsBackfill);
    }

    [Fact]
    public void GetAllAdapters_ShouldReturnSortedByPriority()
    {
        // Arrange
        var registry = new StockSharpAdapterRegistry();
        registry.RegisterAdapter(new InteractiveBrokersAdapterConfig { Priority = 30 });
        registry.RegisterAdapter(new AlpacaAdapterConfig { Priority = 10 });
        registry.RegisterAdapter(new PolygonAdapterConfig { Priority = 20 });

        // Act
        var adapters = registry.GetAllAdapters();

        // Assert
        adapters[0].AdapterId.Should().Be("alpaca"); // Priority 10
        adapters[1].AdapterId.Should().Be("polygon"); // Priority 20
        adapters[2].AdapterId.Should().Be("ib"); // Priority 30
    }

    [Fact]
    public void CreateFromOptions_ShouldCreateCorrectAdapters()
    {
        // Arrange
        var options = new ConnectorHubOptions
        {
            Enabled = true,
            Adapters =
            [
                new AdapterOptions
                {
                    Id = "test-ib",
                    Type = "ib",
                    Priority = 10,
                    Settings = new Dictionary<string, string>
                    {
                        ["Host"] = "192.168.1.100",
                        ["Port"] = "4001",
                        ["ClientId"] = "2"
                    }
                },
                new AdapterOptions
                {
                    Id = "test-alpaca",
                    Type = "alpaca",
                    Priority = 20,
                    Settings = new Dictionary<string, string>
                    {
                        ["KeyId"] = "test-key",
                        ["SecretKey"] = "test-secret"
                    }
                }
            ]
        };

        // Act
        var registry = StockSharpAdapterRegistry.CreateFromOptions(options);

        // Assert
        var adapters = registry.GetAllAdapters();
        adapters.Should().HaveCount(2);

        var ibAdapter = registry.GetAdapterConfig("test-ib") as InteractiveBrokersAdapterConfig;
        ibAdapter.Should().NotBeNull();
        ibAdapter!.Host.Should().Be("192.168.1.100");
        ibAdapter.Port.Should().Be(4001);
        ibAdapter.ClientId.Should().Be(2);
    }

    [Fact]
    public void DisabledAdapter_ShouldNotBeReturned()
    {
        // Arrange
        var registry = new StockSharpAdapterRegistry();
        registry.RegisterAdapter(new InteractiveBrokersAdapterConfig { Enabled = false });
        registry.RegisterAdapter(new AlpacaAdapterConfig { Enabled = true });

        // Act
        var adapters = registry.GetAllAdapters();
        var ibForProvider = registry.GetAdapterForProvider("ib");

        // Assert
        adapters.Should().HaveCount(1);
        adapters[0].AdapterId.Should().Be("alpaca");
        ibForProvider.Should().BeNull();
    }
}
