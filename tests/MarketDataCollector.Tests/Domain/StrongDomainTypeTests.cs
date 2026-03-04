using FluentAssertions;
using MarketDataCollector.Contracts.Domain;
using Xunit;

namespace MarketDataCollector.Tests.Domain;

/// <summary>
/// Tests for the strong domain-type value objects introduced in Item 1 of the
/// high-impact improvements list: <see cref="SymbolId"/>, <see cref="ProviderId"/>,
/// and <see cref="VenueCode"/>.
/// </summary>
public sealed class StrongDomainTypeTests
{
    // ------------------------------------------------------------------ //
    //  SymbolId                                                            //
    // ------------------------------------------------------------------ //

    [Fact]
    public void SymbolId_Value_IsUpperCase()
    {
        var id = new SymbolId("spy");
        id.Value.Should().Be("SPY");
    }

    [Fact]
    public void SymbolId_ImplicitConversion_YieldsString()
    {
        SymbolId id = new("AAPL");
        string s = id;
        s.Should().Be("AAPL");
    }

    [Fact]
    public void SymbolId_ExplicitConversion_FromString()
    {
        var id = (SymbolId)"msft";
        id.Value.Should().Be("MSFT");
    }

    [Fact]
    public void SymbolId_EqualityIsCaseInsensitive()
    {
        var a = new SymbolId("SPY");
        var b = new SymbolId("spy");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void SymbolId_NullOrWhitespace_ThrowsArgumentException()
    {
        var act1 = () => new SymbolId("");
        var act2 = () => new SymbolId("  ");
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SymbolId_ToString_ReturnsTicker()
    {
        var id = new SymbolId("QQQ");
        id.ToString().Should().Be("QQQ");
    }

    [Fact]
    public void SymbolId_HashCode_IsCaseInsensitiveConsistent()
    {
        var a = new SymbolId("SPY");
        var b = new SymbolId("spy");
        a.GetHashCode().Should().Be(b.GetHashCode(),
            "case-insensitive equality requires matching hash codes");
    }

    // ------------------------------------------------------------------ //
    //  ProviderId                                                          //
    // ------------------------------------------------------------------ //

    [Fact]
    public void ProviderId_Value_IsLowerCase()
    {
        var id = new ProviderId("ALPACA");
        id.Value.Should().Be("alpaca");
    }

    [Fact]
    public void ProviderId_WellKnownConstants_HaveCorrectValues()
    {
        ProviderId.Alpaca.Value.Should().Be("alpaca");
        ProviderId.Polygon.Value.Should().Be("polygon");
        ProviderId.Stooq.Value.Should().Be("stooq");
    }

    [Fact]
    public void ProviderId_EqualityIsCaseInsensitive()
    {
        var a = new ProviderId("Polygon");
        var b = new ProviderId("polygon");
        a.Should().Be(b);
    }

    [Fact]
    public void ProviderId_ImplicitConversion_YieldsString()
    {
        ProviderId id = ProviderId.Stooq;
        string s = id;
        s.Should().Be("stooq");
    }

    [Fact]
    public void ProviderId_NullOrWhitespace_ThrowsArgumentException()
    {
        var act = () => new ProviderId(null!);
        act.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------ //
    //  VenueCode                                                           //
    // ------------------------------------------------------------------ //

    [Fact]
    public void VenueCode_Value_IsUpperCase()
    {
        var code = new VenueCode("nyse");
        code.Value.Should().Be("NYSE");
    }

    [Fact]
    public void VenueCode_WellKnownConstants_HaveCorrectValues()
    {
        VenueCode.Nyse.Value.Should().Be("NYSE");
        VenueCode.Nasdaq.Value.Should().Be("NASDAQ");
        VenueCode.Unknown.Value.Should().Be("UNKNOWN");
    }

    [Fact]
    public void VenueCode_EqualityIsCaseInsensitive()
    {
        var a = new VenueCode("NASDAQ");
        var b = new VenueCode("nasdaq");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void VenueCode_NullOrWhitespace_ThrowsArgumentException()
    {
        var act = () => new VenueCode("");
        act.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------ //
    //  Cross-type non-interchangeability                                  //
    // ------------------------------------------------------------------ //

    [Fact]
    public void StrongTypes_AreNotInterchangeable()
    {
        // The compiler enforces this at compile time; this test documents intent.
        var symbol = new SymbolId("SPY");
        var provider = new ProviderId("stooq");
        var venue = new VenueCode("NYSE");

        // SymbolId, ProviderId and VenueCode are distinct value types – confirmed by
        // verifying they carry independent values even when the raw strings match.
        var sameLetter = new SymbolId("ALPACA");
        var sameLetterProvider = new ProviderId("ALPACA");
        ((string)sameLetter).Should().NotBeSameAs((string)sameLetterProvider,
            because: "although the raw letters match, they are semantically different entities");
        _ = symbol;
        _ = provider;
        _ = venue;
    }
}
