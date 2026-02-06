using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Utilities;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure;

/// <summary>
/// Tests for cross-provider data normalization of symbols, timestamps, and aggressor side.
/// Verifies that data from Alpaca, Polygon, IB, StockSharp, and NYSE is unified
/// into a consistent format before entering the domain collectors.
/// </summary>
public sealed class ProviderDataNormalizerTests
{
    private readonly ProviderDataNormalizer _normalizer = new();

    #region Symbol Normalization

    [Theory]
    [InlineData("aapl", "AAPL")]
    [InlineData("Aapl", "AAPL")]
    [InlineData("AAPL", "AAPL")]
    [InlineData("  aapl  ", "AAPL")]
    [InlineData("  SPY ", "SPY")]
    [InlineData("brk.a", "BRK.A")]
    [InlineData("es-2024", "ES-2024")]
    [InlineData("btc_usd", "BTC_USD")]
    public void NormalizeSymbol_VariousCasings_ReturnsUppercaseTrimmed(string input, string expected)
    {
        var result = ProviderDataNormalizer.NormalizeSymbol(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeSymbol_AlreadyNormalized_ReturnsSameReference()
    {
        const string symbol = "AAPL";
        var result = ProviderDataNormalizer.NormalizeSymbol(symbol);
        result.Should().BeSameAs(symbol);
    }

    [Fact]
    public void NormalizeSymbol_NullInput_ReturnsEmpty()
    {
        var result = ProviderDataNormalizer.NormalizeSymbol(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeSymbol_EmptyString_ReturnsEmpty()
    {
        var result = ProviderDataNormalizer.NormalizeSymbol("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeSymbol_WhitespaceOnly_ReturnsEmpty()
    {
        var result = ProviderDataNormalizer.NormalizeSymbol("   ");
        result.Should().BeEmpty();
    }

    #endregion

    #region Timestamp Normalization

    [Fact]
    public void NormalizeTimestamp_AlreadyUtc_ReturnsSameValue()
    {
        var utc = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.Zero);
        var result = ProviderDataNormalizer.NormalizeTimestamp(utc);
        result.Should().Be(utc);
        result.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void NormalizeTimestamp_WithEasternOffset_ConvertsToUtc()
    {
        // Eastern Time: 2024-06-15T09:30:00-05:00 = UTC 2024-06-15T14:30:00Z
        var eastern = new DateTimeOffset(2024, 6, 15, 9, 30, 0, TimeSpan.FromHours(-5));
        var result = ProviderDataNormalizer.NormalizeTimestamp(eastern);
        result.Offset.Should().Be(TimeSpan.Zero);
        result.UtcDateTime.Should().Be(new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NormalizeTimestamp_WithPositiveOffset_ConvertsToUtc()
    {
        // Tokyo Time: 2024-06-15T23:30:00+09:00 = UTC 2024-06-15T14:30:00Z
        var tokyo = new DateTimeOffset(2024, 6, 15, 23, 30, 0, TimeSpan.FromHours(9));
        var result = ProviderDataNormalizer.NormalizeTimestamp(tokyo);
        result.Offset.Should().Be(TimeSpan.Zero);
        result.UtcDateTime.Should().Be(new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NormalizeTimestamp_PreservesInstant()
    {
        // The actual instant in time must not change
        var offset = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.FromHours(-5));
        var result = ProviderDataNormalizer.NormalizeTimestamp(offset);

        // Same instant in time
        result.UtcTicks.Should().Be(offset.UtcTicks);
    }

    [Fact]
    public void NormalizeTimestamp_MinValue_ReturnsMinValue()
    {
        var result = ProviderDataNormalizer.NormalizeTimestamp(DateTimeOffset.MinValue);
        result.Offset.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region Aggressor Side Normalization

    [Theory]
    [InlineData(AggressorSide.Unknown, AggressorSide.Unknown)]
    [InlineData(AggressorSide.Buy, AggressorSide.Buy)]
    [InlineData(AggressorSide.Sell, AggressorSide.Sell)]
    public void NormalizeAggressor_ValidValues_ReturnsSameValue(AggressorSide input, AggressorSide expected)
    {
        var result = ProviderDataNormalizer.NormalizeAggressor(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeAggressor_UndefinedEnumValue_ReturnsUnknown()
    {
        var result = ProviderDataNormalizer.NormalizeAggressor((AggressorSide)99);
        result.Should().Be(AggressorSide.Unknown);
    }

    [Fact]
    public void NormalizeAggressor_NegativeEnumValue_ReturnsUnknown()
    {
        var result = ProviderDataNormalizer.NormalizeAggressor((AggressorSide)(-1));
        result.Should().Be(AggressorSide.Unknown);
    }

    #endregion

    #region Trade Normalization (End-to-End)

    [Fact]
    public void NormalizeTrade_LowercaseSymbolNonUtcTimestamp_NormalizesAll()
    {
        var eastern = new DateTimeOffset(2024, 6, 15, 9, 30, 0, TimeSpan.FromHours(-5));
        var trade = new MarketTradeUpdate(
            Timestamp: eastern,
            Symbol: "aapl",
            Price: 195.50m,
            Size: 100,
            Aggressor: AggressorSide.Buy,
            SequenceNumber: 42,
            StreamId: "ALPACA",
            Venue: "NYSE"
        );

        var result = _normalizer.NormalizeTrade(trade);

        result.Symbol.Should().Be("AAPL");
        result.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        result.Timestamp.UtcDateTime.Should().Be(new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc));
        result.Price.Should().Be(195.50m);
        result.Size.Should().Be(100);
        result.Aggressor.Should().Be(AggressorSide.Buy);
        result.SequenceNumber.Should().Be(42);
        result.StreamId.Should().Be("ALPACA");
        result.Venue.Should().Be("NYSE");
    }

    [Fact]
    public void NormalizeTrade_AlreadyNormalized_ReturnsSameInstance()
    {
        var utc = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.Zero);
        var trade = new MarketTradeUpdate(
            Timestamp: utc,
            Symbol: "SPY",
            Price: 450.00m,
            Size: 200,
            Aggressor: AggressorSide.Sell,
            SequenceNumber: 1
        );

        var result = _normalizer.NormalizeTrade(trade);
        result.Should().BeSameAs(trade);
    }

    [Fact]
    public void NormalizeTrade_SimulateAlpacaData_NormalizesCorrectly()
    {
        // Alpaca: symbol from JSON, ISO 8601 timestamp, always Unknown aggressor
        var alpacaTrade = new MarketTradeUpdate(
            Timestamp: DateTimeOffset.Parse("2024-06-15T09:30:00.123-04:00"),
            Symbol: "TSLA",
            Price: 177.50m,
            Size: 50,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: 12345,
            StreamId: "ALPACA",
            Venue: "ALPACA"
        );

        var result = _normalizer.NormalizeTrade(alpacaTrade);
        result.Symbol.Should().Be("TSLA");
        result.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        result.Aggressor.Should().Be(AggressorSide.Unknown);
    }

    [Fact]
    public void NormalizeTrade_SimulatePolygonData_NormalizesCorrectly()
    {
        // Polygon: Unix millis (already UTC offset=0), mixed case possible, partial aggressor
        var polygonTrade = new MarketTradeUpdate(
            Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(1718451000000),
            Symbol: "AAPL",
            Price: 195.00m,
            Size: 100,
            Aggressor: AggressorSide.Sell,
            SequenceNumber: 1,
            StreamId: "POLYGON_1",
            Venue: "XNAS"
        );

        var result = _normalizer.NormalizeTrade(polygonTrade);
        result.Symbol.Should().Be("AAPL");
        result.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        result.Aggressor.Should().Be(AggressorSide.Sell);
    }

    [Fact]
    public void NormalizeTrade_SimulateIBData_NormalizesCorrectly()
    {
        // IB: Unix seconds (already UTC), symbol from ticker map, Unknown aggressor
        var ibTrade = new MarketTradeUpdate(
            Timestamp: DateTimeOffset.FromUnixTimeSeconds(1718451000),
            Symbol: "SPY",
            Price: 550.25m,
            Size: 300,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: 0,
            StreamId: "IB-TBT",
            Venue: "ARCA"
        );

        var result = _normalizer.NormalizeTrade(ibTrade);
        result.Symbol.Should().Be("SPY");
        result.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void NormalizeTrade_InvalidAggressorEnumValue_MapsToUnknown()
    {
        var trade = new MarketTradeUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Price: 100m,
            Size: 50,
            Aggressor: (AggressorSide)42,
            SequenceNumber: 1
        );

        var result = _normalizer.NormalizeTrade(trade);
        result.Aggressor.Should().Be(AggressorSide.Unknown);
    }

    [Fact]
    public void NormalizeTrade_NullInput_ThrowsArgumentNullException()
    {
        var act = () => _normalizer.NormalizeTrade(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Quote Normalization (End-to-End)

    [Fact]
    public void NormalizeQuote_LowercaseSymbolNonUtcTimestamp_NormalizesAll()
    {
        var quote = new MarketQuoteUpdate(
            Timestamp: new DateTimeOffset(2024, 6, 15, 9, 30, 0, TimeSpan.FromHours(-5)),
            Symbol: "msft",
            BidPrice: 420.50m,
            BidSize: 100,
            AskPrice: 420.55m,
            AskSize: 200,
            StreamId: "ALPACA",
            Venue: "ALPACA"
        );

        var result = _normalizer.NormalizeQuote(quote);

        result.Symbol.Should().Be("MSFT");
        result.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        result.BidPrice.Should().Be(420.50m);
        result.BidSize.Should().Be(100);
        result.AskPrice.Should().Be(420.55m);
        result.AskSize.Should().Be(200);
    }

    [Fact]
    public void NormalizeQuote_AlreadyNormalized_ReturnsSameInstance()
    {
        var quote = new MarketQuoteUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            BidPrice: 450.00m,
            BidSize: 100,
            AskPrice: 450.05m,
            AskSize: 150
        );

        var result = _normalizer.NormalizeQuote(quote);
        result.Should().BeSameAs(quote);
    }

    [Fact]
    public void NormalizeQuote_NullInput_ThrowsArgumentNullException()
    {
        var act = () => _normalizer.NormalizeQuote(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Depth Normalization (End-to-End)

    [Fact]
    public void NormalizeDepth_LowercaseSymbolNonUtcTimestamp_NormalizesAll()
    {
        var depth = new MarketDepthUpdate(
            Timestamp: new DateTimeOffset(2024, 6, 15, 9, 30, 0, TimeSpan.FromHours(-5)),
            Symbol: "spy",
            Position: 0,
            Operation: DepthOperation.Insert,
            Side: OrderBookSide.Bid,
            Price: 450.00m,
            Size: 1000,
            StreamId: "IB",
            Venue: null
        );

        var result = _normalizer.NormalizeDepth(depth);

        result.Symbol.Should().Be("SPY");
        result.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        result.Position.Should().Be(0);
        result.Operation.Should().Be(DepthOperation.Insert);
        result.Side.Should().Be(OrderBookSide.Bid);
        result.Price.Should().Be(450.00m);
        result.Size.Should().Be(1000);
    }

    [Fact]
    public void NormalizeDepth_AlreadyNormalized_ReturnsSameInstance()
    {
        var depth = new MarketDepthUpdate(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Position: 0,
            Operation: DepthOperation.Update,
            Side: OrderBookSide.Ask,
            Price: 450.05m,
            Size: 500
        );

        var result = _normalizer.NormalizeDepth(depth);
        result.Should().BeSameAs(depth);
    }

    [Fact]
    public void NormalizeDepth_NullInput_ThrowsArgumentNullException()
    {
        var act = () => _normalizer.NormalizeDepth(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Cross-Provider Consistency Tests

    [Fact]
    public void NormalizeTrade_SameSymbolDifferentCasings_ProducesIdenticalSymbols()
    {
        // Simulates same stock arriving from different providers with different casing
        var alpaca = new MarketTradeUpdate(DateTimeOffset.UtcNow, "AAPL", 195m, 100, AggressorSide.Unknown, 1, "ALPACA");
        var polygon = new MarketTradeUpdate(DateTimeOffset.UtcNow, "aapl", 195m, 100, AggressorSide.Sell, 2, "POLYGON");
        var ib = new MarketTradeUpdate(DateTimeOffset.UtcNow, "Aapl", 195m, 100, AggressorSide.Unknown, 3, "IB");

        var normalizedAlpaca = _normalizer.NormalizeTrade(alpaca);
        var normalizedPolygon = _normalizer.NormalizeTrade(polygon);
        var normalizedIb = _normalizer.NormalizeTrade(ib);

        normalizedAlpaca.Symbol.Should().Be("AAPL");
        normalizedPolygon.Symbol.Should().Be("AAPL");
        normalizedIb.Symbol.Should().Be("AAPL");
    }

    [Fact]
    public void NormalizeTrade_SameInstantDifferentTimezones_ProducesIdenticalTimestamps()
    {
        // Same instant in time, different provider timestamp formats
        var utcTime = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.Zero);
        var easternTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(-4));
        var unixMillis = DateTimeOffset.FromUnixTimeMilliseconds(utcTime.ToUnixTimeMilliseconds());

        var fromAlpaca = new MarketTradeUpdate(easternTime, "SPY", 100m, 1, AggressorSide.Unknown, 1);
        var fromPolygon = new MarketTradeUpdate(unixMillis, "SPY", 100m, 1, AggressorSide.Unknown, 2);
        var fromIB = new MarketTradeUpdate(utcTime, "SPY", 100m, 1, AggressorSide.Unknown, 3);

        var nAlpaca = _normalizer.NormalizeTrade(fromAlpaca);
        var nPolygon = _normalizer.NormalizeTrade(fromPolygon);
        var nIB = _normalizer.NormalizeTrade(fromIB);

        nAlpaca.Timestamp.UtcTicks.Should().Be(nPolygon.Timestamp.UtcTicks);
        nPolygon.Timestamp.UtcTicks.Should().Be(nIB.Timestamp.UtcTicks);
        nAlpaca.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        nPolygon.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        nIB.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    #endregion
}
