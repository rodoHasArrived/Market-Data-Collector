using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;
using MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;
using Xunit;

namespace MarketDataCollector.Tests.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Unit tests for adapter configuration classes.
/// </summary>
public sealed class AdapterConfigTests
{
    #region InteractiveBrokersAdapterConfig Tests

    [Fact]
    public void InteractiveBrokersAdapterConfig_ShouldHaveCorrectDefaults()
    {
        // Act
        var config = new InteractiveBrokersAdapterConfig();

        // Assert
        config.AdapterId.Should().Be("ib");
        config.DisplayName.Should().Be("Interactive Brokers");
        config.Host.Should().Be("127.0.0.1");
        config.Port.Should().Be(7496);
        config.ClientId.Should().Be(1);
        config.SupportsStreaming.Should().BeTrue();
        config.SupportsBackfill.Should().BeTrue();
        config.SupportsMarketDepth.Should().BeTrue();
        config.SupportedAssetClasses.Should().Contain("equity", "futures", "options", "forex");
    }

    [Fact]
    public void InteractiveBrokersAdapterConfig_Validate_ShouldSucceedWithValidConfig()
    {
        // Arrange
        var config = new InteractiveBrokersAdapterConfig
        {
            Host = "192.168.1.100",
            Port = 4001,
            ClientId = 2
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void InteractiveBrokersAdapterConfig_Validate_ShouldFailWithInvalidPort()
    {
        // Arrange
        var config = new InteractiveBrokersAdapterConfig
        {
            Port = -1
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("port"));
    }

    [Fact]
    public void InteractiveBrokersAdapterConfig_Validate_ShouldFailWithInvalidHost()
    {
        // Arrange
        var config = new InteractiveBrokersAdapterConfig
        {
            Host = "invalid-host"
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("host"));
    }

    #endregion

    #region AlpacaAdapterConfig Tests

    [Fact]
    public void AlpacaAdapterConfig_ShouldHaveCorrectDefaults()
    {
        // Act
        var config = new AlpacaAdapterConfig();

        // Assert
        config.AdapterId.Should().Be("alpaca");
        config.DisplayName.Should().Be("Alpaca Markets");
        config.UsePaper.Should().BeTrue();
        config.Feed.Should().Be("iex");
        config.SupportsStreaming.Should().BeTrue();
        config.SupportsBackfill.Should().BeTrue();
        config.SupportsMarketDepth.Should().BeFalse();
        config.MappedProviderIds.Should().Contain("alpaca", "alpaca-markets");
    }

    [Fact]
    public void AlpacaAdapterConfig_Validate_ShouldFailWithoutCredentials()
    {
        // Arrange
        var config = new AlpacaAdapterConfig();

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Key ID"));
        result.Errors.Should().Contain(e => e.Contains("Secret Key"));
    }

    [Fact]
    public void AlpacaAdapterConfig_Validate_ShouldWarnAboutIexFeed()
    {
        // Arrange
        var config = new AlpacaAdapterConfig
        {
            KeyId = "test-key",
            SecretKey = "test-secret",
            Feed = "iex"
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("IEX"));
    }

    [Fact]
    public void AlpacaAdapterConfig_Validate_ShouldWarnAboutPaperTrading()
    {
        // Arrange
        var config = new AlpacaAdapterConfig
        {
            KeyId = "test-key",
            SecretKey = "test-secret",
            UsePaper = true
        };

        // Act
        var result = config.Validate();

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("paper"));
    }

    #endregion

    #region PolygonAdapterConfig Tests

    [Fact]
    public void PolygonAdapterConfig_ShouldHaveCorrectDefaults()
    {
        // Act
        var config = new PolygonAdapterConfig();

        // Assert
        config.AdapterId.Should().Be("polygon");
        config.DisplayName.Should().Be("Polygon.io");
        config.UseDelayed.Should().BeFalse();
        config.SupportsStreaming.Should().BeTrue();
        config.SupportsBackfill.Should().BeTrue();
        config.MappedProviderIds.Should().Contain("polygon", "polygon.io");
    }

    [Fact]
    public void PolygonAdapterConfig_Validate_ShouldFailWithoutApiKey()
    {
        // Arrange
        var config = new PolygonAdapterConfig();

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("API key"));
    }

    #endregion

    #region RithmicAdapterConfig Tests

    [Fact]
    public void RithmicAdapterConfig_ShouldHaveCorrectDefaults()
    {
        // Act
        var config = new RithmicAdapterConfig();

        // Assert
        config.AdapterId.Should().Be("rithmic");
        config.DisplayName.Should().Be("Rithmic");
        config.Server.Should().Be("Rithmic Test");
        config.SupportsStreaming.Should().BeTrue();
        config.SupportsMarketDepth.Should().BeTrue();
        config.SupportedAssetClasses.Should().Contain("futures");
        config.SupportedExchanges.Should().Contain("CME", "CBOT", "NYMEX", "COMEX");
    }

    [Fact]
    public void RithmicAdapterConfig_Validate_ShouldFailWithoutCredentials()
    {
        // Arrange
        var config = new RithmicAdapterConfig();

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("username"));
        result.Errors.Should().Contain(e => e.Contains("password"));
    }

    #endregion

    #region BinanceAdapterConfig Tests

    [Fact]
    public void BinanceAdapterConfig_ShouldHaveCorrectDefaults()
    {
        // Act
        var config = new BinanceAdapterConfig();

        // Assert
        config.AdapterId.Should().Be("binance");
        config.DisplayName.Should().Be("Binance");
        config.UseTestnet.Should().BeFalse();
        config.UseFutures.Should().BeFalse();
        config.SupportsStreaming.Should().BeTrue();
        config.SupportsBackfill.Should().BeTrue();
        config.SupportsMarketDepth.Should().BeTrue();
        config.MaxDepthLevels.Should().Be(20);
        config.SupportedAssetClasses.Should().Contain("crypto");
    }

    [Fact]
    public void BinanceAdapterConfig_Validate_ShouldSucceedWithoutCredentials()
    {
        // Arrange - Binance allows market data without auth
        var config = new BinanceAdapterConfig();

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("API key"));
    }

    #endregion

    #region CustomAdapterConfig Tests

    [Fact]
    public void CustomAdapterConfig_ShouldRequireAdapterTypeName()
    {
        // Act & Assert
        var act = () => new CustomAdapterConfig("test", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CustomAdapterConfig_Validate_ShouldFailWithEmptyTypeName()
    {
        // Arrange
        var config = new CustomAdapterConfig("test", "");

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("type name"));
    }

    [Fact]
    public void CustomAdapterConfig_ForStockSharpAdapter_ShouldCreateCorrectConfig()
    {
        // Act
        var config = CustomAdapterConfig.ForStockSharpAdapter(
            "lmax",
            "Lmax",
            "LMAX",
            "LMAX Exchange",
            "FX trading via LMAX");

        // Assert
        config.AdapterId.Should().Be("lmax");
        config.DisplayName.Should().Be("LMAX Exchange");
        config.Description.Should().Be("FX trading via LMAX");
        config.AdapterTypeName.Should().Be("StockSharp.LMAX.LmaxMessageAdapter");
        config.AdapterAssembly.Should().Be("StockSharp.LMAX");
    }

    [Fact]
    public void CustomAdapterConfigs_Factory_ShouldCreateValidConfigs()
    {
        // Act
        var lmax = CustomAdapterConfigs.LMAX("key", "secret");
        var coinbase = CustomAdapterConfigs.Coinbase("key", "secret");
        var kraken = CustomAdapterConfigs.Kraken("key", "secret");

        // Assert
        lmax.AdapterId.Should().Be("lmax");
        coinbase.AdapterId.Should().Be("coinbase");
        kraken.AdapterId.Should().Be("kraken");

        lmax.Properties.Should().ContainKey("Login");
        coinbase.Properties.Should().ContainKey("Key");
        kraken.Properties.Should().ContainKey("Secret");
    }

    #endregion

    #region IQFeedAdapterConfig Tests

    [Fact]
    public void IQFeedAdapterConfig_ShouldHaveCorrectDefaults()
    {
        // Act
        var config = new IQFeedAdapterConfig();

        // Assert
        config.AdapterId.Should().Be("iqfeed");
        config.Host.Should().Be("127.0.0.1");
        config.Level1Port.Should().Be(9100);
        config.Level2Port.Should().Be(9200);
        config.LookupPort.Should().Be(9300);
        config.SupportsStreaming.Should().BeTrue();
        config.SupportsBackfill.Should().BeTrue();
        config.SupportsMarketDepth.Should().BeTrue();
    }

    [Fact]
    public void IQFeedAdapterConfig_Validate_ShouldFailWithInvalidPorts()
    {
        // Arrange
        var config = new IQFeedAdapterConfig
        {
            Level1Port = -1,
            Level2Port = 70000
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }

    #endregion
}
