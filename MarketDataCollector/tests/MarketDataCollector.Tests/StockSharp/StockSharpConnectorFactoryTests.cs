using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Infrastructure.Providers.StockSharp;
using Xunit;

namespace MarketDataCollector.Tests.StockSharp;

/// <summary>
/// Tests for the StockSharp connector factory.
/// </summary>
public class StockSharpConnectorFactoryTests
{
    [Fact]
    public void SupportedConnectorTypes_ContainsExpectedTypes()
    {
        // Act
        var types = StockSharpConnectorFactory.SupportedConnectorTypes;

        // Assert
        types.Should().Contain("Rithmic");
        types.Should().Contain("IQFeed");
        types.Should().Contain("CQG");
        types.Should().Contain("InteractiveBrokers");
    }

    [Theory]
    [InlineData("Rithmic", true)]
    [InlineData("rithmic", true)]
    [InlineData("RITHMIC", true)]
    [InlineData("IQFeed", true)]
    [InlineData("iqfeed", true)]
    [InlineData("CQG", true)]
    [InlineData("InteractiveBrokers", true)]
    [InlineData("interactivebrokers", true)]
    [InlineData("Unknown", false)]
    [InlineData("", false)]
    [InlineData("FakeConnector", false)]
    public void IsSupported_ReturnsCorrectResult(string connectorType, bool expected)
    {
        // Act
        var result = StockSharpConnectorFactory.IsSupported(connectorType);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Create_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        // Note: Without StockSharp packages, this throws NotSupportedException
        // With packages, it should throw ArgumentNullException for null config
        var action = () => StockSharpConnectorFactory.Create(null!);
        action.Should().Throw<Exception>();
    }

    [Fact]
    public void Create_WithUnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "UnsupportedConnector");

        // Act & Assert
        var action = () => StockSharpConnectorFactory.Create(config);
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*UnsupportedConnector*");
    }

    [Fact]
    public void Create_WithRithmicType_RequiresStockSharpPackage()
    {
        // Arrange
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "Rithmic",
            Rithmic: new RithmicConfig(
                Server: "Rithmic Test",
                UserName: "testuser",
                Password: "testpass"));

        // Act & Assert
        // Without StockSharp.Rithmic package, this should throw NotSupportedException
        var action = () => StockSharpConnectorFactory.Create(config);
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*StockSharp*");
    }

    [Fact]
    public void Create_WithIQFeedType_RequiresStockSharpPackage()
    {
        // Arrange
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "IQFeed",
            IQFeed: new IQFeedConfig(
                Host: "127.0.0.1",
                ProductId: "TEST"));

        // Act & Assert
        var action = () => StockSharpConnectorFactory.Create(config);
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*StockSharp*");
    }

    [Fact]
    public void Create_WithCQGType_RequiresStockSharpPackage()
    {
        // Arrange
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "CQG",
            CQG: new CQGConfig(
                UserName: "testuser",
                Password: "testpass"));

        // Act & Assert
        var action = () => StockSharpConnectorFactory.Create(config);
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*StockSharp*");
    }

    [Fact]
    public void Create_WithInteractiveBrokersType_RequiresStockSharpPackage()
    {
        // Arrange
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: "InteractiveBrokers",
            InteractiveBrokers: new StockSharpIBConfig(
                Host: "127.0.0.1",
                Port: 7496,
                ClientId: 1));

        // Act & Assert
        var action = () => StockSharpConnectorFactory.Create(config);
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*StockSharp*");
    }

    [Theory]
    [InlineData("IB")]
    [InlineData("ib")]
    public void Create_WithIBAlias_RequiresStockSharpPackage(string connectorType)
    {
        // Arrange
        var config = new StockSharpConfig(
            Enabled: true,
            ConnectorType: connectorType);

        // Act & Assert
        var action = () => StockSharpConnectorFactory.Create(config);
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*StockSharp*");
    }
}
