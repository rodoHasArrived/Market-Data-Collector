using FluentAssertions;
using MarketDataCollector.Infrastructure.DataSources;
using Xunit;

namespace MarketDataCollector.Tests.DataSources;

public class SymbolMapperTests
{
    [Fact]
    public void MapToSource_WithExplicitMapping_ReturnsMapping()
    {
        // Arrange
        var config = new SymbolMappingConfig
        {
            Mappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["yahoo"] = new() { ["BRK.B"] = "BRK-B" }
            }
        };
        var mapper = new SymbolMapper(config);

        // Act
        var result = mapper.MapToSource("BRK.B", "yahoo");

        // Assert
        result.Should().Be("BRK-B");
    }

    [Fact]
    public void MapToSource_WithoutExplicitMapping_AppliesDefaultTransform()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act - Yahoo Finance uses dashes
        var yahooResult = mapper.MapToSource("BRK.B", "yahoo");

        // Assert
        yahooResult.Should().Be("BRK-B");
    }

    [Fact]
    public void MapToSource_ForIB_UsesSpaces()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var result = mapper.MapToSource("BRK.B", "ib");

        // Assert
        result.Should().Be("BRK B");
    }

    [Fact]
    public void MapToSource_ForAlpaca_ReturnsCanonical()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var result = mapper.MapToSource("AAPL", "alpaca");

        // Assert
        result.Should().Be("AAPL");
    }

    [Fact]
    public void MapFromSource_WithExplicitMapping_ReturnsCanonical()
    {
        // Arrange
        var config = new SymbolMappingConfig
        {
            Mappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["yahoo"] = new() { ["BRK.B"] = "BRK-B" }
            }
        };
        var mapper = new SymbolMapper(config);

        // Act
        var result = mapper.MapFromSource("BRK-B", "yahoo");

        // Assert
        result.Should().Be("BRK.B");
    }

    [Fact]
    public void MapFromSource_WithDefaultTransform_ReturnsCanonical()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var yahooResult = mapper.MapFromSource("BRK-B", "yahoo");
        var ibResult = mapper.MapFromSource("BRK B", "ib");

        // Assert
        yahooResult.Should().Be("BRK.B");
        ibResult.Should().Be("BRK.B");
    }

    [Fact]
    public void GetAllAliases_ReturnsAllFormats()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var result = mapper.GetAllAliases("BRK.B");

        // Assert
        result.Should().Contain("BRK.B");  // Canonical
        result.Should().Contain("BRK-B");  // Yahoo format
        result.Should().Contain("BRK B");  // IB format
    }

    [Fact]
    public void RegisterMapping_AddsNewMapping()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        mapper.RegisterMapping("GOOG", "yahoo", "GOOGL");
        var result = mapper.MapToSource("GOOG", "yahoo");

        // Assert
        result.Should().Be("GOOGL");
    }

    [Fact]
    public void RegisterMapping_CanBeReversed()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        mapper.RegisterMapping("GOOG", "yahoo", "GOOGL");
        var result = mapper.MapFromSource("GOOGL", "yahoo");

        // Assert
        result.Should().Be("GOOG");
    }

    [Fact]
    public void DefaultMarket_IsUS()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Assert
        mapper.DefaultMarket.Should().Be("US");
    }

    [Fact]
    public void DefaultMarket_CanBeConfigured()
    {
        // Arrange
        var config = new SymbolMappingConfig { DefaultMarket = "UK" };
        var mapper = new SymbolMapper(config);

        // Assert
        mapper.DefaultMarket.Should().Be("UK");
    }

    [Fact]
    public void CreateWithCommonMappings_HasPreConfiguredMappings()
    {
        // Arrange & Act
        var mapper = SymbolMapper.CreateWithCommonMappings();

        // Assert - Should have common class share mappings
        mapper.MapToSource("BRK.A", "yahoo").Should().Be("BRK-A");
        mapper.MapToSource("BRK.B", "yahoo").Should().Be("BRK-B");
        mapper.MapToSource("BRK.A", "ib").Should().Be("BRK A");
    }

    [Theory]
    [InlineData("AAPL", "yahoo", "AAPL")]
    [InlineData("MSFT", "yahoo", "MSFT")]
    [InlineData("SPY", "ib", "SPY")]
    [InlineData("QQQ", "alpaca", "QQQ")]
    public void MapToSource_SimpleSymbols_AreUnchanged(string symbol, string source, string expected)
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var result = mapper.MapToSource(symbol, source);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MapToSource_EmptySymbol_ReturnsEmpty()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var result = mapper.MapToSource("", "yahoo");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapToSource_NullSymbol_ReturnsNull()
    {
        // Arrange
        var mapper = new SymbolMapper();

        // Act
        var result = mapper.MapToSource(null!, "yahoo");

        // Assert
        result.Should().BeNull();
    }
}
