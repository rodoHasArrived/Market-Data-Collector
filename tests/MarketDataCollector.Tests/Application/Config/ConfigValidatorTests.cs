using FluentAssertions;
using MarketDataCollector.Application.Config;
using Xunit;

namespace MarketDataCollector.Tests;

public class ConfigValidatorTests
{
    private readonly AppConfigValidator _validator;

    public ConfigValidatorTests()
    {
        _validator = new AppConfigValidator();
    }

    [Fact]
    public void Validate_DefaultConfig_IsValid()
    {
        // Arrange
        var config = new AppConfig();

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyDataRoot_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(DataRoot: "");

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DataRoot");
    }

    [Fact]
    public void Validate_AlpacaWithoutApiKeys_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(
            DataSource: DataSourceKind.Alpaca,
            Alpaca: new AlpacaOptions
            {
                KeyId = "",
                SecretKey = ""
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("KeyId"));
    }

    [Fact]
    public void Validate_AlpacaWithPlaceholderKeys_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(
            DataSource: DataSourceKind.Alpaca,
            Alpaca: new AlpacaOptions
            {
                KeyId = "__SET_ME__",
                SecretKey = "__SET_ME__"
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("KeyId"));
    }

    [Fact]
    public void Validate_AlpacaWithValidKeys_IsValid()
    {
        // Arrange
        var config = new AppConfig(
            DataSource: DataSourceKind.Alpaca,
            Alpaca: new AlpacaOptions
            {
                KeyId = "AKXXXXXXXXXXXXXXXX",
                SecretKey = "secretxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                Feed = "iex"
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AlpacaWithInvalidFeed_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(
            DataSource: DataSourceKind.Alpaca,
            Alpaca: new AlpacaOptions
            {
                KeyId = "AKXXXXXXXXXXXXXXXX",
                SecretKey = "secretxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                Feed = "invalid"
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Feed"));
    }

    [Fact]
    public void Validate_IB_DoesNotRequireAlpacaConfig()
    {
        // Arrange
        var config = new AppConfig(
            DataSource: DataSourceKind.IB,
            Alpaca: null
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidSymbolConfig_IsValid()
    {
        // Arrange
        var config = new AppConfig(
            Symbols: new[]
            {
                new SymbolConfig(
                    Symbol: "SPY",
                    SubscribeTrades: true,
                    SubscribeDepth: true,
                    DepthLevels: 10,
                    SecurityType: "STK",
                    Exchange: "SMART",
                    Currency: "USD"
                )
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySymbol_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(
            Symbols: new[]
            {
                new SymbolConfig(Symbol: "")
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Symbol"));
    }

    [Fact]
    public void Validate_InvalidDepthLevels_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(
            Symbols: new[]
            {
                new SymbolConfig(
                    Symbol: "SPY",
                    DepthLevels: 100 // Too high
                )
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("DepthLevels"));
    }

    [Fact]
    public void Validate_InvalidSecurityType_IsInvalid()
    {
        // Arrange
        var config = new AppConfig(
            Symbols: new[]
            {
                new SymbolConfig(
                    Symbol: "SPY",
                    SecurityType: "INVALID"
                )
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("SecurityType"));
    }

    [Theory]
    [InlineData("STK")]
    [InlineData("OPT")]
    [InlineData("FUT")]
    [InlineData("CASH")]
    public void Validate_ValidSecurityTypes_AreValid(string securityType)
    {
        // Arrange
        var config = new AppConfig(
            Symbols: new[]
            {
                new SymbolConfig(
                    Symbol: "TEST",
                    SecurityType: securityType
                )
            }
        );

        // Act
        var result = _validator.Validate(config);

        // Assert
        result.Errors.Should().NotContain(e => e.PropertyName.Contains("SecurityType"));
    }
}
