using FluentAssertions;
using MarketDataCollector.Application.Canonicalization;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Catalog;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using NSubstitute;
using Xunit;

namespace MarketDataCollector.Tests.Application.Services;

/// <summary>
/// Tests for <see cref="CanonicalizingPublisher"/>.
/// Covers Phase 2 dual-write validation, pilot symbol scoping, and decorator behavior.
/// </summary>
public sealed class CanonicalizingPublisherTests : IDisposable
{
    private readonly RecordingPublisher _inner;
    private readonly ICanonicalSymbolRegistry _registry;
    private readonly EventCanonicalizer _canonicalizer;

    public CanonicalizingPublisherTests()
    {
        _inner = new RecordingPublisher();
        _registry = Substitute.For<ICanonicalSymbolRegistry>();
        _registry.TryResolveWithProvider(Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<string>(0));

        _canonicalizer = new EventCanonicalizer(
            _registry,
            ConditionCodeMapper.CreateDefault(),
            VenueMicMapper.CreateDefault(),
            version: 1);

        CanonicalizationMetrics.Reset();
    }

    public void Dispose()
    {
        CanonicalizationMetrics.Reset();
    }

    [Fact]
    public void TryPublish_CanonicalizationEnabled_EnrichesEvent()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        var evt = CreateTradeEvent("AAPL", "ALPACA");
        publisher.TryPublish(evt);

        _inner.Published.Should().HaveCount(1);
        _inner.Published[0].CanonicalSymbol.Should().Be("AAPL");
        _inner.Published[0].CanonicalizationVersion.Should().Be(1);
        _inner.Published[0].Tier.Should().Be(MarketEventTier.Enriched);
    }

    [Fact]
    public void TryPublish_DualWrite_PublishesBothRawAndEnriched()
    {
        var config = new CanonicalizationConfig(Enabled: true, EnableDualWrite: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        var evt = CreateTradeEvent("AAPL", "ALPACA");
        publisher.TryPublish(evt);

        _inner.Published.Should().HaveCount(2);

        // First event is raw (unchanged)
        _inner.Published[0].CanonicalSymbol.Should().BeNull();
        _inner.Published[0].CanonicalizationVersion.Should().Be(0);
        _inner.Published[0].Tier.Should().Be(MarketEventTier.Raw);

        // Second event is enriched
        _inner.Published[1].CanonicalSymbol.Should().Be("AAPL");
        _inner.Published[1].CanonicalizationVersion.Should().Be(1);
        _inner.Published[1].Tier.Should().Be(MarketEventTier.Enriched);
    }

    [Fact]
    public void TryPublish_DualWrite_RecordsDualWriteMetric()
    {
        var config = new CanonicalizationConfig(Enabled: true, EnableDualWrite: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));
        publisher.TryPublish(CreateTradeEvent("SPY", "ALPACA"));

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.DualWriteTotal.Should().Be(2);
    }

    [Fact]
    public void TryPublish_NoDualWrite_PublishesOnlyEnriched()
    {
        var config = new CanonicalizationConfig(Enabled: true, EnableDualWrite: false);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));

        _inner.Published.Should().HaveCount(1);
        _inner.Published[0].CanonicalizationVersion.Should().Be(1);
    }

    [Fact]
    public void TryPublish_PilotSymbols_OnlyCanonicalizesMatchingSymbols()
    {
        var config = new CanonicalizationConfig(
            Enabled: true,
            PilotSymbols: new[] { "AAPL", "SPY" });
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));
        publisher.TryPublish(CreateTradeEvent("TSLA", "ALPACA"));
        publisher.TryPublish(CreateTradeEvent("SPY", "ALPACA"));

        _inner.Published.Should().HaveCount(3);

        // AAPL should be enriched
        _inner.Published[0].CanonicalizationVersion.Should().Be(1);

        // TSLA should be raw (not in pilot list)
        _inner.Published[1].CanonicalizationVersion.Should().Be(0);

        // SPY should be enriched
        _inner.Published[2].CanonicalizationVersion.Should().Be(1);
    }

    [Fact]
    public void TryPublish_PilotSymbols_CaseInsensitive()
    {
        var config = new CanonicalizationConfig(
            Enabled: true,
            PilotSymbols: new[] { "AAPL" });
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("aapl", "ALPACA"));

        _inner.Published.Should().HaveCount(1);
        _inner.Published[0].CanonicalizationVersion.Should().Be(1);
    }

    [Fact]
    public void TryPublish_EmptyPilotSymbols_CanonicalizesAll()
    {
        var config = new CanonicalizationConfig(
            Enabled: true,
            PilotSymbols: Array.Empty<string>());
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));
        publisher.TryPublish(CreateTradeEvent("TSLA", "ALPACA"));

        _inner.Published.Should().AllSatisfy(e =>
            e.CanonicalizationVersion.Should().Be(1));
    }

    [Fact]
    public void TryPublish_NullPilotSymbols_CanonicalizesAll()
    {
        var config = new CanonicalizationConfig(Enabled: true, PilotSymbols: null);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));

        _inner.Published[0].CanonicalizationVersion.Should().Be(1);
    }

    [Fact]
    public void TryPublish_PreservesRawSymbol()
    {
        _registry.TryResolveWithProvider("AAPL.US", "STOCKSHARP").Returns("AAPL");

        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL.US", "STOCKSHARP"));

        _inner.Published[0].Symbol.Should().Be("AAPL.US");
        _inner.Published[0].CanonicalSymbol.Should().Be("AAPL");
    }

    [Fact]
    public void TryPublish_ReturnsInnerPublisherResult()
    {
        _inner.ShouldSucceed = false;
        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        var result = publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));

        result.Should().BeFalse();
    }

    [Fact]
    public void TryPublish_VenueIsCanonicalized()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA", venue: "V"));

        _inner.Published[0].CanonicalVenue.Should().Be("XNAS");
    }

    [Fact]
    public void Constructor_ThrowsOnNullInner()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var act = () => new CanonicalizingPublisher(null!, _canonicalizer, config);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullCanonicalizer()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var act = () => new CanonicalizingPublisher(_inner, null!, config);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullConfig()
    {
        var act = () => new CanonicalizingPublisher(_inner, _canonicalizer, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #region Parity Metrics Tests

    [Fact]
    public void ParityMetrics_TracksPerProvider()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));
        publisher.TryPublish(CreateTradeEvent("SPY", "POLYGON"));
        publisher.TryPublish(CreateTradeEvent("MSFT", "ALPACA"));

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.ProviderParity.Should().ContainKey("ALPACA");
        snapshot.ProviderParity.Should().ContainKey("POLYGON");
        snapshot.ProviderParity["ALPACA"].Total.Should().Be(2);
        snapshot.ProviderParity["POLYGON"].Total.Should().Be(1);
    }

    [Fact]
    public void ParityMetrics_MatchRateCalculation()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        // All should succeed since registry returns the same symbol
        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA"));
        publisher.TryPublish(CreateTradeEvent("SPY", "ALPACA"));

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.ProviderParity["ALPACA"].MatchRatePercent.Should().Be(100.0);
    }

    [Fact]
    public void ParityMetrics_UnresolvedVenueTracked()
    {
        var config = new CanonicalizationConfig(Enabled: true);
        var publisher = new CanonicalizingPublisher(_inner, _canonicalizer, config);

        // Unknown venue should be tracked as unresolved
        publisher.TryPublish(CreateTradeEvent("AAPL", "ALPACA", venue: "UNKNOWN_EX"));

        var snapshot = CanonicalizationMetrics.GetSnapshot();
        snapshot.ProviderParity["ALPACA"].UnresolvedVenue.Should().Be(1);
    }

    #endregion

    #region CanonicalizationConfig Tests

    [Fact]
    public void CanonicalizationConfig_Defaults_AreCorrect()
    {
        var config = new CanonicalizationConfig();

        config.Enabled.Should().BeFalse();
        config.Version.Should().Be(1);
        config.PilotSymbols.Should().BeNull();
        config.EnableDualWrite.Should().BeFalse();
        config.UnresolvedAlertThresholdPercent.Should().Be(0.1);
        config.ConditionCodesPath.Should().BeNull();
        config.VenueMappingPath.Should().BeNull();
    }

    [Fact]
    public void CanonicalizationConfig_PilotOverride()
    {
        var config = new CanonicalizationConfig(
            Enabled: true,
            PilotSymbols: new[] { "AAPL", "SPY" },
            EnableDualWrite: true,
            Version: 2);

        config.Enabled.Should().BeTrue();
        config.PilotSymbols.Should().HaveCount(2);
        config.EnableDualWrite.Should().BeTrue();
        config.Version.Should().Be(2);
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

    /// <summary>
    /// Test double that records all published events for assertion.
    /// </summary>
    private sealed class RecordingPublisher : IMarketEventPublisher
    {
        public List<MarketEvent> Published { get; } = new();
        public bool ShouldSucceed { get; set; } = true;

        public bool TryPublish(in MarketEvent evt)
        {
            Published.Add(evt);
            return ShouldSucceed;
        }
    }

    #endregion
}
