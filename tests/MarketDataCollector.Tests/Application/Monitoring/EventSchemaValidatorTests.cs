using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Events;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Events;
using Xunit;

namespace MarketDataCollector.Tests.Application.Monitoring;

public sealed class EventSchemaValidatorTests
{
    // ── Valid events ────────────────────────────────────────────────

    [Fact]
    public void Validate_WithValidTradeEvent_DoesNotThrow()
    {
        var evt = CreateValidEvent(MarketEventType.Trade);
        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithValidHeartbeatAndNullPayload_DoesNotThrow()
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SYSTEM",
            Type: MarketEventType.Heartbeat,
            Payload: null,
            Sequence: 1,
            Source: "IB",
            SchemaVersion: EventSchemaValidator.CurrentSchemaVersion
        );

        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().NotThrow();
    }

    // ── Timestamp validation ────────────────────────────────────────

    [Fact]
    public void Validate_WithDefaultTimestamp_ThrowsInvalidOperation()
    {
        var evt = new MarketEvent(
            Timestamp: default,
            Symbol: "SPY",
            Type: MarketEventType.Trade,
            Payload: CreatePayload(),
            Sequence: 1,
            Source: "IB",
            SchemaVersion: EventSchemaValidator.CurrentSchemaVersion
        );

        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*timestamp*");
    }

    // ── Symbol validation ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidSymbol_ThrowsInvalidOperation(string? symbol)
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: symbol!,
            Type: MarketEventType.Trade,
            Payload: CreatePayload(),
            Sequence: 1,
            Source: "IB",
            SchemaVersion: EventSchemaValidator.CurrentSchemaVersion
        );

        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*symbol*");
    }

    // ── Event type validation ───────────────────────────────────────

    [Fact]
    public void Validate_WithUnknownEventType_ThrowsInvalidOperation()
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Type: MarketEventType.Unknown,
            Payload: CreatePayload(),
            Sequence: 1,
            Source: "IB",
            SchemaVersion: EventSchemaValidator.CurrentSchemaVersion
        );

        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*type*");
    }

    // ── Schema version validation ───────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void Validate_WithWrongSchemaVersion_ThrowsInvalidOperation(int schemaVersion)
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Type: MarketEventType.Trade,
            Payload: CreatePayload(),
            Sequence: 1,
            Source: "IB",
            SchemaVersion: schemaVersion
        );

        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*schema version*");
    }

    [Fact]
    public void CurrentSchemaVersion_IsOne()
    {
        EventSchemaValidator.CurrentSchemaVersion.Should().Be(1);
    }

    // ── Payload validation ──────────────────────────────────────────

    [Theory]
    [InlineData(MarketEventType.Trade)]
    [InlineData(MarketEventType.BboQuote)]
    [InlineData(MarketEventType.L2Snapshot)]
    public void Validate_WithNullPayloadOnNonHeartbeat_ThrowsInvalidOperation(MarketEventType eventType)
    {
        var evt = new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Type: eventType,
            Payload: null,
            Sequence: 1,
            Source: "IB",
            SchemaVersion: EventSchemaValidator.CurrentSchemaVersion
        );

        var act = () => EventSchemaValidator.Validate(evt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*payload*");
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static MarketEvent CreateValidEvent(MarketEventType type)
    {
        return new MarketEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Type: type,
            Payload: CreatePayload(),
            Sequence: 1,
            Source: "IB",
            SchemaVersion: EventSchemaValidator.CurrentSchemaVersion
        );
    }

    private static MarketEventPayload CreatePayload()
    {
        return new Trade(
            Timestamp: DateTimeOffset.UtcNow,
            Symbol: "SPY",
            Price: 450.50m,
            Size: 100,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: 1
        );
    }
}
