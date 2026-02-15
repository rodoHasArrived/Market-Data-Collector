using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

/// <summary>
/// Unit tests for ContractFactory — specifically the InferPreferredLocalSymbol logic
/// that works without the IBAPI conditional compilation constant.
/// Covers B3 tranche 2 from the project roadmap — IB provider behavior validation.
/// </summary>
public sealed class IBContractFactoryTests
{
    #region Create Without IBAPI

    [Fact]
    public void Create_WithoutIBAPI_ThrowsNotSupportedException()
    {
        // When built without IBAPI constant, Create should throw
        var cfg = new SymbolConfig("SPY");

        var act = () => ContractFactory.Create(cfg);

        act.Should().Throw<NotSupportedException>()
            .And.Message.Should().Contain("IBAPI");
    }

    #endregion

    #region InferPreferredLocalSymbol via Reflection

    // The InferPreferredLocalSymbol method is private static. We test it indirectly
    // by using reflection since it contains important business logic for IB preferred
    // stock symbol handling. This ensures the pattern-matching works correctly.

    [Theory]
    [InlineData("SPY", "SPY", null)]
    [InlineData("AAPL", "AAPL", null)]
    [InlineData("MSFT", "MSFT", null)]
    public void InferPreferredLocalSymbol_RegularSymbol_ReturnsUnchanged(
        string input, string expectedSymbol, string? expectedLocalSymbol)
    {
        var (ibSymbol, localSymFallback) = InvokeInferPreferred(input);

        ibSymbol.Should().Be(expectedSymbol);
        localSymFallback.Should().Be(expectedLocalSymbol);
    }

    [Theory]
    [InlineData("PCG-PA", "PCG", "PCG PRA")]
    [InlineData("BAC-PB", "BAC", "BAC PRB")]
    [InlineData("WFC-PC", "WFC", "WFC PRC")]
    [InlineData("JPM-PD", "JPM", "JPM PRD")]
    public void InferPreferredLocalSymbol_PreferredStock_InfersLocalSymbol(
        string input, string expectedSymbol, string expectedLocalSymbol)
    {
        var (ibSymbol, localSymFallback) = InvokeInferPreferred(input);

        ibSymbol.Should().Be(expectedSymbol);
        localSymFallback.Should().Be(expectedLocalSymbol);
    }

    [Theory]
    [InlineData("pcg-pa", "pcg", "pcg PRA")]
    [InlineData("bac-pb", "bac", "bac PRB")]
    public void InferPreferredLocalSymbol_LowercaseInput_InfersSeriesInUppercase(
        string input, string expectedSymbol, string expectedLocalSymbol)
    {
        var (ibSymbol, localSymFallback) = InvokeInferPreferred(input);

        ibSymbol.Should().Be(expectedSymbol);
        localSymFallback.Should().Be(expectedLocalSymbol);
    }

    [Theory]
    [InlineData("P-PA")]
    [InlineData("X-PB")]
    public void InferPreferredLocalSymbol_SingleCharUnderlying_StillWorks(string input)
    {
        var (ibSymbol, localSymFallback) = InvokeInferPreferred(input);

        // Single-char underlying should still parse correctly
        localSymFallback.Should().NotBeNull();
        localSymFallback.Should().EndWith("PR" + input[^1]);
    }

    [Theory]
    [InlineData("-PA")]       // No underlying
    [InlineData("PCG-PABC")] // Series too long (>2 chars)
    [InlineData("PCG")]      // No preferred suffix
    [InlineData("PCG-")]     // Dash with nothing after
    [InlineData("PCG-X")]    // Not a P-series pattern
    public void InferPreferredLocalSymbol_InvalidPatterns_ReturnsOriginal(string input)
    {
        var (ibSymbol, localSymFallback) = InvokeInferPreferred(input);

        ibSymbol.Should().Be(input);
        localSymFallback.Should().BeNull();
    }

    /// <summary>
    /// Invokes the private static InferPreferredLocalSymbol method via reflection.
    /// </summary>
    private static (string ibSymbol, string? localSymbolFallback) InvokeInferPreferred(string symbol)
    {
        var method = typeof(ContractFactory).GetMethod(
            "InferPreferredLocalSymbol",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("InferPreferredLocalSymbol should exist as a private static method");

        var result = method!.Invoke(null, new object[] { symbol });
        var tuple = ((string, string?))result!;
        return tuple;
    }
}
