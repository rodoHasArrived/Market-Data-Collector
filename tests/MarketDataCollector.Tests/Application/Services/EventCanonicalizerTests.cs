using FluentAssertions;
using MarketDataCollector.Application.Canonicalization;
using MarketDataCollector.Contracts.Catalog;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using NSubstitute;
using Xunit;

namespace MarketDataCollector.Tests.Application.Services;

/// <summary>
/// Tests for the EventCanonicalizer, ConditionCodeMapper, and VenueMicMapper.
/// Covers Phase 1 acceptance criteria from docs/architecture/deterministic-canonicalization.md.
/// </summary>
public sealed class EventCanonicalizerTests : IDisposable
{
    private readonly ICanonicalSymbolRegistry _registry;
    private readonly ConditionCodeMapper _conditionMapper;
    private readonly VenueMicMapper _venueMapper;
    private readonly EventCanonicalizer _canonicalizer;

    public EventCanonicalizerTests()
    {
        _registry = Substitute.For<ICanonicalSymbolRegistry>();
        _conditionMapper = ConditionCodeMapper.CreateDefault();
        _venueMapper = VenueMicMapper.CreateDefault();
        _canonicalizer = new EventCanonicalizer(_registry, _conditionMapper, _venueMapper, version: 1);

        CanonicalizationMetrics.Reset();
    }

    public void Dispose()
    {
        CanonicalizationMetrics.Reset();
    }

    #region EventCanonicalizer Tests

    [Fact]
    public void Canonicalize_ResolvesSymbol_SetsCanonicalSymbol()
    {
        _registry.TryResolveWithProvider("AAPL", "ALPACA").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "ALPACA");
        var result = _canonicalizer.Canonicalize(raw);

        result.CanonicalSymbol.Should().Be("AAPL");
        result.CanonicalizationVersion.Should().Be(1);
        result.Tier.Should().Be(MarketEventTier.Enriched);
    }

    [Fact]
    public void Canonicalize_UnresolvedSymbol_FallsBackToRawSymbol()
    {
        _registry.TryResolveWithProvider("UNKNOWN.X", "ALPACA").Returns((string?)null);
        _registry.ResolveToCanonical("UNKNOWN.X").Returns((string?)null);

        var raw = CreateTradeEvent("UNKNOWN.X", "ALPACA");
        var result = _canonicalizer.Canonicalize(raw);

        result.CanonicalSymbol.Should().Be("UNKNOWN.X");
        result.CanonicalizationVersion.Should().Be(1);
    }

    [Fact]
    public void Canonicalize_ResolvesVenue_SetsCanonicalVenue()
    {
        _registry.TryResolveWithProvider("AAPL", "ALPACA").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "ALPACA", venue: "V");
        var result = _canonicalizer.Canonicalize(raw);

        result.CanonicalVenue.Should().Be("XNAS");
    }

    [Fact]
    public void Canonicalize_PolygonNumericVenue_MapsToMic()
    {
        _registry.TryResolveWithProvider("SPY", "POLYGON").Returns("SPY");

        var raw = CreateTradeEvent("SPY", "POLYGON", venue: "4");
        var result = _canonicalizer.Canonicalize(raw);

        result.CanonicalVenue.Should().Be("XNAS");
    }

    [Fact]
    public void Canonicalize_IBVenue_MapsToMic()
    {
        _registry.TryResolveWithProvider("AAPL", "IB").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "IB", venue: "ISLAND");
        var result = _canonicalizer.Canonicalize(raw);

        result.CanonicalVenue.Should().Be("XNAS");
    }

    [Fact]
    public void Canonicalize_PreservesRawSymbol()
    {
        _registry.TryResolveWithProvider("AAPL.US", "STOCKSHARP").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL.US", "STOCKSHARP");
        var result = _canonicalizer.Canonicalize(raw);

        result.Symbol.Should().Be("AAPL.US");
        result.CanonicalSymbol.Should().Be("AAPL");
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        _registry.TryResolveWithProvider("AAPL", Arg.Any<string>()).Returns("AAPL");
        _registry.ResolveToCanonical("AAPL").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "ALPACA", venue: "V");
        var first = _canonicalizer.Canonicalize(raw);
        var second = _canonicalizer.Canonicalize(first);

        second.CanonicalSymbol.Should().Be(first.CanonicalSymbol);
        second.CanonicalVenue.Should().Be(first.CanonicalVenue);
        second.CanonicalizationVersion.Should().Be(first.CanonicalizationVersion);
    }

    [Fact]
    public void Canonicalize_IsDeterministic()
    {
        _registry.TryResolveWithProvider("AAPL", "ALPACA").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "ALPACA", venue: "V");
        var result1 = _canonicalizer.Canonicalize(raw);
        var result2 = _canonicalizer.Canonicalize(raw);

        result1.CanonicalSymbol.Should().Be(result2.CanonicalSymbol);
        result1.CanonicalVenue.Should().Be(result2.CanonicalVenue);
        result1.CanonicalizationVersion.Should().Be(result2.CanonicalizationVersion);
    }

    [Fact]
    public void Canonicalize_TierOnlyIncreases()
    {
        _registry.TryResolveWithProvider("AAPL", "ALPACA").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "ALPACA");
        raw.Tier.Should().Be(MarketEventTier.Raw);

        var result = _canonicalizer.Canonicalize(raw);
        result.Tier.Should().BeGreaterThanOrEqualTo(raw.Tier);
    }

    [Fact]
    public void Canonicalize_EmptySymbol_DoesNotThrow()
    {
        // Hard fail: empty symbol means the event is returned as-is
        var raw = new MarketEvent(
            DateTimeOffset.UtcNow,
            "",
            MarketEventType.Trade,
            null,
            Source: "ALPACA");

        var result = _canonicalizer.Canonicalize(raw);

        // Should not throw, returns event with no enrichment
        result.CanonicalSymbol.Should().BeNull();
        result.CanonicalizationVersion.Should().Be(0);
    }

    [Fact]
    public void Canonicalize_NoVenueOnPayload_CanonicalVenueIsNull()
    {
        _registry.TryResolveWithProvider("AAPL", "ALPACA").Returns("AAPL");

        var raw = CreateTradeEvent("AAPL", "ALPACA", venue: null);
        var result = _canonicalizer.Canonicalize(raw);

        result.CanonicalVenue.Should().BeNull();
    }

    #endregion

    #region ConditionCodeMapper Tests

    [Fact]
    public void ConditionCodeMapper_AlpacaRegular_MapsCorrectly()
    {
        var (canonical, raw) = _conditionMapper.MapConditions("ALPACA", new[] { "@" });

        canonical.Should().HaveCount(1);
        canonical[0].Should().Be(CanonicalTradeCondition.Regular);
        raw.Should().Equal("@");
    }

    [Fact]
    public void ConditionCodeMapper_AlpacaFormT_MapsCorrectly()
    {
        var (canonical, _) = _conditionMapper.MapConditions("ALPACA", new[] { "T" });

        canonical.Should().HaveCount(1);
        canonical[0].Should().Be(CanonicalTradeCondition.FormT_ExtendedHours);
    }

    [Fact]
    public void ConditionCodeMapper_PolygonOddLot_MapsCorrectly()
    {
        var (canonical, _) = _conditionMapper.MapConditions("POLYGON", new[] { "37" });

        canonical.Should().HaveCount(1);
        canonical[0].Should().Be(CanonicalTradeCondition.OddLot);
    }

    [Fact]
    public void ConditionCodeMapper_PolygonSellerCodes_MapCorrectly()
    {
        var (canonical, _) = _conditionMapper.MapConditions("POLYGON", new[] { "29", "30" });

        canonical.Should().HaveCount(2);
        canonical[0].Should().Be(CanonicalTradeCondition.SellerInitiated);
        canonical[1].Should().Be(CanonicalTradeCondition.SellerDownExempt);
    }

    [Fact]
    public void ConditionCodeMapper_IBRegularTrade_MapsCorrectly()
    {
        var (canonical, _) = _conditionMapper.MapConditions("IB", new[] { "RegularTrade" });

        canonical.Should().HaveCount(1);
        canonical[0].Should().Be(CanonicalTradeCondition.Regular);
    }

    [Fact]
    public void ConditionCodeMapper_UnknownCode_ReturnsUnknown()
    {
        var (canonical, _) = _conditionMapper.MapConditions("ALPACA", new[] { "ZZZZZ" });

        canonical.Should().HaveCount(1);
        canonical[0].Should().Be(CanonicalTradeCondition.Unknown);
    }

    [Fact]
    public void ConditionCodeMapper_NullConditions_ReturnsEmpty()
    {
        var (canonical, raw) = _conditionMapper.MapConditions("ALPACA", null);

        canonical.Should().BeEmpty();
        raw.Should().BeEmpty();
    }

    [Fact]
    public void ConditionCodeMapper_EmptyConditions_ReturnsEmpty()
    {
        var (canonical, raw) = _conditionMapper.MapConditions("ALPACA", []);

        canonical.Should().BeEmpty();
        raw.Should().BeEmpty();
    }

    [Fact]
    public void ConditionCodeMapper_LoadFromJson_ParsesCorrectly()
    {
        var json = """
        {
          "version": 1,
          "mappings": [
            { "provider": "TEST", "rawCode": "X", "canonical": "CrossTrade" }
          ]
        }
        """;

        var mapper = ConditionCodeMapper.LoadFromJson(json);

        mapper.HasMapping("TEST", "X").Should().BeTrue();
        var (canonical, _) = mapper.MapConditions("TEST", new[] { "X" });
        canonical[0].Should().Be(CanonicalTradeCondition.CrossTrade);
    }

    [Fact]
    public void ConditionCodeMapper_CreateDefault_HasExpectedMappingCount()
    {
        var mapper = ConditionCodeMapper.CreateDefault();

        mapper.MappingCount.Should().BeGreaterThan(20);
    }

    #endregion

    #region VenueMicMapper Tests

    [Theory]
    [InlineData("ALPACA", "V", "XNAS")]
    [InlineData("ALPACA", "P", "ARCX")]
    [InlineData("ALPACA", "N", "XNYS")]
    [InlineData("POLYGON", "1", "XNYS")]
    [InlineData("POLYGON", "4", "XNAS")]
    [InlineData("POLYGON", "8", "BATS")]
    [InlineData("POLYGON", "9", "IEXG")]
    [InlineData("IB", "ISLAND", "XNAS")]
    [InlineData("IB", "ARCA", "ARCX")]
    [InlineData("IB", "NYSE", "XNYS")]
    public void VenueMicMapper_KnownVenues_MapCorrectly(string provider, string rawVenue, string expectedMic)
    {
        var result = _venueMapper.TryMapVenue(rawVenue, provider);
        result.Should().Be(expectedMic);
    }

    [Fact]
    public void VenueMicMapper_UnknownVenue_ReturnsNull()
    {
        var result = _venueMapper.TryMapVenue("UNKNOWN_VENUE", "ALPACA");
        result.Should().BeNull();
    }

    [Fact]
    public void VenueMicMapper_NullVenue_ReturnsNull()
    {
        var result = _venueMapper.TryMapVenue(null, "ALPACA");
        result.Should().BeNull();
    }

    [Fact]
    public void VenueMicMapper_AlreadyStandardMic_PassesThrough()
    {
        var result = _venueMapper.TryMapVenue("XNYS", "UNKNOWN_PROVIDER");
        result.Should().Be("XNYS");
    }

    [Fact]
    public void VenueMicMapper_LoadFromJson_ParsesCorrectly()
    {
        var json = """
        {
          "version": 1,
          "mappings": [
            { "provider": "TEST", "rawVenue": "EX1", "mic": "XTEST" }
          ]
        }
        """;

        var mapper = VenueMicMapper.LoadFromJson(json);
        var result = mapper.TryMapVenue("EX1", "TEST");
        result.Should().Be("XTEST");
    }

    [Fact]
    public void VenueMicMapper_CreateDefault_HasExpectedMappingCount()
    {
        var mapper = VenueMicMapper.CreateDefault();
        mapper.MappingCount.Should().BeGreaterThan(40);
    }

    #endregion

    #region MarketEvent New Fields Tests

    [Fact]
    public void MarketEvent_DefaultValues_BackwardCompatible()
    {
        var evt = MarketEvent.Trade(
            DateTimeOffset.UtcNow,
            "AAPL",
            new Trade(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: "AAPL",
                Price: 150m,
                Size: 100,
                Aggressor: AggressorSide.Buy,
                SequenceNumber: 1));

        evt.CanonicalSymbol.Should().BeNull();
        evt.CanonicalizationVersion.Should().Be(0);
        evt.CanonicalVenue.Should().BeNull();
    }

    [Fact]
    public void MarketEvent_WithExpression_SetsCanonicalFields()
    {
        var raw = MarketEvent.Trade(
            DateTimeOffset.UtcNow,
            "AAPL",
            new Trade(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: "AAPL",
                Price: 150m,
                Size: 100,
                Aggressor: AggressorSide.Buy,
                SequenceNumber: 1));

        var enriched = raw with
        {
            CanonicalSymbol = "AAPL",
            CanonicalizationVersion = 1,
            CanonicalVenue = "XNAS",
            Tier = MarketEventTier.Enriched
        };

        enriched.Symbol.Should().Be("AAPL");
        enriched.CanonicalSymbol.Should().Be("AAPL");
        enriched.CanonicalizationVersion.Should().Be(1);
        enriched.CanonicalVenue.Should().Be("XNAS");
        enriched.Tier.Should().Be(MarketEventTier.Enriched);
    }

    #endregion

    #region IntegrityEvent Factory Tests

    [Fact]
    public void IntegrityEvent_UnresolvedSymbol_SetsCorrectErrorCode()
    {
        var evt = IntegrityEvent.UnresolvedSymbol(
            DateTimeOffset.UtcNow, "AAPL.US", "STOCKSHARP", 42);

        evt.ErrorCode.Should().Be(1005);
        evt.Severity.Should().Be(IntegritySeverity.Warning);
        evt.Description.Should().Contain("AAPL.US");
        evt.Description.Should().Contain("STOCKSHARP");
    }

    [Fact]
    public void IntegrityEvent_CanonicalizationHardFail_SetsCorrectErrorCode()
    {
        var evt = IntegrityEvent.CanonicalizationHardFail(
            DateTimeOffset.UtcNow, "", "Symbol is empty", 0);

        evt.ErrorCode.Should().Be(1006);
        evt.Severity.Should().Be(IntegritySeverity.Error);
    }

    #endregion

    #region CanonicalizationMetrics Tests

    [Fact]
    public void CanonicalizationMetrics_RecordSuccess_IncrementsCounter()
    {
        CanonicalizationMetrics.RecordSuccess("ALPACA", "Trade");
        CanonicalizationMetrics.RecordSuccess("ALPACA", "Trade");

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.SuccessTotal.Should().Be(2);
    }

    [Fact]
    public void CanonicalizationMetrics_RecordUnresolved_TracksProviderAndField()
    {
        CanonicalizationMetrics.RecordUnresolved("POLYGON", "venue");
        CanonicalizationMetrics.RecordUnresolved("POLYGON", "venue");
        CanonicalizationMetrics.RecordUnresolved("ALPACA", "symbol");

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.UnresolvedCounts[("POLYGON", "venue")].Should().Be(2);
        snapshot.UnresolvedCounts[("ALPACA", "symbol")].Should().Be(1);
    }

    [Fact]
    public void CanonicalizationMetrics_Reset_ClearsAll()
    {
        CanonicalizationMetrics.RecordSuccess("ALPACA", "Trade");
        CanonicalizationMetrics.RecordHardFail("IB", "Trade");
        CanonicalizationMetrics.Reset();

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.SuccessTotal.Should().Be(0);
        snapshot.HardFailTotal.Should().Be(0);
    }

    #endregion

    #region Helpers

    private static MarketEvent CreateTradeEvent(string symbol, string source, string? venue = null)
    {
        return MarketEvent.Trade(
            DateTimeOffset.UtcNow,
            symbol,
            new Trade(
                Timestamp: DateTimeOffset.UtcNow,
                Symbol: symbol,
                Price: 150.25m,
                Size: 100,
                Aggressor: AggressorSide.Buy,
                SequenceNumber: 1,
                Venue: venue),
            source: source);
    }

    #endregion
}
