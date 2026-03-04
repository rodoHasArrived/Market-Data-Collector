using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Domain.Events;
using Xunit;

namespace MarketDataCollector.Tests.Domain.Models;

/// <summary>
/// Tests for the <see cref="MarketEvent.EffectiveSymbol"/> property
/// that supports Phase 3 canonical read path.
/// </summary>
public sealed class EffectiveSymbolTests
{
    [Fact]
    public void EffectiveSymbol_ReturnsCanonicalSymbol_WhenSet()
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "AAPL.US",
            Type: MarketEventType.Trade,
            Payload: null,
            CanonicalSymbol: "AAPL");

        evt.EffectiveSymbol.Should().Be("AAPL");
    }

    [Fact]
    public void EffectiveSymbol_FallsBackToSymbol_WhenCanonicalNull()
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "AAPL.US",
            Type: MarketEventType.Trade,
            Payload: null,
            CanonicalSymbol: null);

        evt.EffectiveSymbol.Should().Be("AAPL.US");
    }

    [Fact]
    public void EffectiveSymbol_FallsBackToSymbol_WhenDefault()
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Type: MarketEventType.Trade,
            Payload: null);

        evt.EffectiveSymbol.Should().Be("SPY");
    }

    [Fact]
    public void EffectiveSymbol_WorksAfterWithExpression()
    {
        var raw = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "AAPL.US",
            Type: MarketEventType.Trade,
            Payload: null);

        var enriched = raw with { CanonicalSymbol = "AAPL", CanonicalizationVersion = 1 };

        enriched.EffectiveSymbol.Should().Be("AAPL");
        raw.EffectiveSymbol.Should().Be("AAPL.US"); // Original unchanged
    }

    [Fact]
    public void ContractsMarketEvent_EffectiveSymbol_WorksToo()
    {
        var evt = new Contracts.Domain.Events.MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "MSFT.US",
            Type: MarketEventType.Trade,
            Payload: null,
            CanonicalSymbol: "MSFT");

        evt.EffectiveSymbol.Should().Be("MSFT");
    }

    [Fact]
    public void ContractsMarketEvent_EffectiveSymbol_FallsBackToSymbol()
    {
        var evt = new Contracts.Domain.Events.MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "MSFT.US",
            Type: MarketEventType.Trade,
            Payload: null);

        evt.EffectiveSymbol.Should().Be("MSFT.US");
    }
}
