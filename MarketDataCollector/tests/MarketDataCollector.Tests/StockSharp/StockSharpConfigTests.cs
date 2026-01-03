using FluentAssertions;
using MarketDataCollector.Application.Config;
using Xunit;

namespace MarketDataCollector.Tests.StockSharp;

/// <summary>
/// Tests for StockSharp configuration models.
/// </summary>
public class StockSharpConfigTests
{
    [Fact]
    public void StockSharpConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new StockSharpConfig();

        // Assert
        config.Enabled.Should().BeFalse();
        config.ConnectorType.Should().Be("Rithmic");
        config.UseBinaryStorage.Should().BeFalse();
        config.StoragePath.Should().Be("data/stocksharp");
        config.EnableRealTime.Should().BeTrue();
        config.EnableHistorical.Should().BeTrue();
        config.ConnectionParams.Should().BeNull();
        config.Rithmic.Should().BeNull();
        config.IQFeed.Should().BeNull();
        config.CQG.Should().BeNull();
        config.InteractiveBrokers.Should().BeNull();
    }

    [Fact]
    public void StockSharpConfig_WithCustomValues_SetsProperties()
    {
        // Arrange & Act
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "IQFeed",
            UseBinaryStorage: true,
            StoragePath: "custom/path",
            EnableRealTime: false,
            EnableHistorical: true);

        // Assert
        config.Enabled.Should().BeTrue();
        config.ConnectorType.Should().Be("IQFeed");
        config.UseBinaryStorage.Should().BeTrue();
        config.StoragePath.Should().Be("custom/path");
        config.EnableRealTime.Should().BeFalse();
        config.EnableHistorical.Should().BeTrue();
    }

    [Fact]
    public void RithmicConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new RithmicConfig();

        // Assert
        config.Server.Should().Be("Rithmic Test");
        config.UserName.Should().BeEmpty();
        config.Password.Should().BeEmpty();
        config.CertFile.Should().BeEmpty();
        config.UsePaperTrading.Should().BeTrue();
    }

    [Fact]
    public void RithmicConfig_WithCredentials_SetsProperties()
    {
        // Arrange & Act
        var config = new RithmicConfig(
            Server: "Rithmic 01",
            UserName: "testuser",
            Password: "testpass",
            CertFile: "/path/to/cert.pem",
            UsePaperTrading: false);

        // Assert
        config.Server.Should().Be("Rithmic 01");
        config.UserName.Should().Be("testuser");
        config.Password.Should().Be("testpass");
        config.CertFile.Should().Be("/path/to/cert.pem");
        config.UsePaperTrading.Should().BeFalse();
    }

    [Fact]
    public void IQFeedConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new IQFeedConfig();

        // Assert
        config.Host.Should().Be("127.0.0.1");
        config.Level1Port.Should().Be(9100);
        config.Level2Port.Should().Be(9200);
        config.LookupPort.Should().Be(9300);
        config.ProductId.Should().BeEmpty();
        config.ProductVersion.Should().Be("1.0");
    }

    [Fact]
    public void CQGConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new CQGConfig();

        // Assert
        config.UserName.Should().BeEmpty();
        config.Password.Should().BeEmpty();
        config.UseDemoServer.Should().BeTrue();
    }

    [Fact]
    public void StockSharpIBConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new StockSharpIBConfig();

        // Assert
        config.Host.Should().Be("127.0.0.1");
        config.Port.Should().Be(7496);
        config.ClientId.Should().Be(1);
    }

    [Fact]
    public void StockSharpConfig_WithNestedConfigs_SetsAllProperties()
    {
        // Arrange
        var rithmicConfig = new RithmicConfig(
            Server: "Rithmic Paper Trading",
            UserName: "user1",
            Password: "pass1");

        var iqFeedConfig = new IQFeedConfig(
            Host: "192.168.1.100",
            ProductId: "PROD123");

        // Act
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "Rithmic",
            Rithmic: rithmicConfig,
            IQFeed: iqFeedConfig);

        // Assert
        config.Rithmic.Should().NotBeNull();
        config.Rithmic!.Server.Should().Be("Rithmic Paper Trading");
        config.Rithmic.UserName.Should().Be("user1");

        config.IQFeed.Should().NotBeNull();
        config.IQFeed!.Host.Should().Be("192.168.1.100");
        config.IQFeed.ProductId.Should().Be("PROD123");
    }
}
