# Domain Model

This document describes the **runtime domain contracts** used by the collectors and event pipeline.
Primary source of truth lives in:

- `src/MarketDataCollector.Contracts/Domain/Events/`
- `src/MarketDataCollector.Contracts/Domain/Models/`
- `src/MarketDataCollector.Domain/Collectors/`

## MarketEvent Envelope

All emitted domain activity is wrapped in `MarketEvent`:

```csharp
public sealed record MarketEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    MarketEventType Type,
    MarketEventPayload? Payload,
    long Sequence = 0,
    string Source = "IB",
    int SchemaVersion = 1,
    MarketEventTier Tier = MarketEventTier.Raw
);
```

### Envelope fields

- `Timestamp` – canonical event timestamp used by the pipeline.
- `Symbol` – instrument identifier (or `SYSTEM` for platform events like heartbeat).
- `Type` – discriminator for payload parsing and downstream routing.
- `Payload` – strongly typed `MarketEventPayload` instance (nullable for heartbeat/system events).
- `Sequence` – monotonic sequence when available from source or collector.
- `Source` – provider/source identifier (examples: `IB`, `ALPACA`, `stooq`).
- `SchemaVersion` – payload schema compatibility marker.
- `Tier` – event tier classification (raw/derived/normalized workflows).

### Current `MarketEventType` values

- `Unknown`
- `L2Snapshot`
- `BboQuote`
- `Trade`
- `OrderFlow`
- `Heartbeat`
- `ConnectionStatus`
- `Integrity`
- `HistoricalBar`
- `HistoricalQuote`
- `HistoricalTrade`
- `HistoricalAuction`
- `AggregateBar`
- `Quote`
- `Depth`
- `OptionQuote`
- `OptionTrade`
- `OptionGreeks`
- `OptionChain`
- `OpenInterest`

> Note: not every enum member is currently emitted by the three core collectors (`TradeDataCollector`, `MarketDepthCollector`, `QuoteCollector`); several are used by adapters/backfill paths.

---

## Collector Responsibilities

## TradeDataCollector

Processes `MarketTradeUpdate` and emits:

- `Trade` events
- `OrderFlow` events (payload: `OrderFlowStatistics`)
- `Integrity` events (payload: `IntegrityEvent`) for sequence anomalies

Behavioral notes:

- Validates sequence continuity per symbol/stream.
- Infers aggressor side from quote context (`IQuoteStateStore`) when feed-side aggressor is unknown.
- Maintains rolling order-flow stats (VWAP + buy/sell/unknown volume splits).

### `Trade` payload

```csharp
public sealed record Trade : MarketEventPayload
{
    DateTimeOffset Timestamp;
    string Symbol;
    decimal Price;
    long Size;
    AggressorSide Aggressor;
    long SequenceNumber;
    string? StreamId;
    string? Venue;
}
```

Validation highlights:

- `Price > 0`
- `Size >= 0`
- non-empty `Symbol`

### `OrderFlowStatistics` payload

```csharp
public sealed record OrderFlowStatistics(
    DateTimeOffset Timestamp,
    string Symbol,
    long BuyVolume,
    long SellVolume,
    long UnknownVolume,
    decimal VWAP,
    decimal Imbalance,
    int TradeCount,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
```

Derived values include:

- VWAP over the collector window/state
- imbalance ratio from directional volume
- cumulative directional volume and trade count

### `IntegrityEvent` payload

Represents data continuity/quality issues (for example sequence gaps and out-of-order updates), including severity, description, error code, and sequence context.

---

## MarketDepthCollector

Processes `MarketDepthUpdate` and emits:

- `L2Snapshot` events (payload: `LOBSnapshot`)
- `Integrity` events (payload: `DepthIntegrityEvent`) for depth stream violations

Behavioral notes:

- Tracks per-symbol order book buffers.
- Applies insert/update/delete operations by side and position.
- Freezes symbol streams on integrity failures until reset.

### `LOBSnapshot` payload

```csharp
public sealed record LOBSnapshot(
    DateTimeOffset Timestamp,
    string Symbol,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks,
    decimal? MidPrice = null,
    decimal? MicroPrice = null,
    decimal? Imbalance = null,
    MarketState MarketState = MarketState.Normal,
    long SequenceNumber = 0,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
```

Common derived metrics:

- `MidPrice = (BestBid + BestAsk) / 2` when both sides are present
- top-of-book imbalance
- optional micro-price when available

### `DepthIntegrityEvent` payload

```csharp
public sealed record DepthIntegrityEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    DepthIntegrityKind Kind,
    string Description,
    int Position,
    DepthOperation Operation,
    OrderBookSide Side,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
```

Provides actionable context for operators and automated recovery logic.

---

## QuoteCollector (BBO)

Processes `MarketQuoteUpdate` and emits:

- `BboQuote` events (payload: `BboQuotePayload`)

Behavioral notes:

- Maintains latest per-symbol BBO snapshot cache.
- Maintains collector-local monotonically increasing quote sequence per symbol.
- Implements `IQuoteStateStore` for downstream consumers.

### `BboQuotePayload`

```csharp
public sealed record BboQuotePayload(
    DateTimeOffset Timestamp,
    string Symbol,
    decimal BidPrice,
    long BidSize,
    decimal AskPrice,
    long AskSize,
    decimal? MidPrice,
    decimal? Spread,
    long SequenceNumber,
    string? StreamId = null,
    string? Venue = null
) : MarketEventPayload;
```

`TradeDataCollector` uses quote state to improve aggressor classification when prints do not explicitly include side.

### `IQuoteStateStore`

```csharp
public interface IQuoteStateStore
{
    bool TryGet(string symbol, out BboQuotePayload? quote);
}
```

---

## Historical Domain Payloads

## `HistoricalBar`

Current backfill bar model is daily-session focused:

```csharp
public sealed record HistoricalBar : MarketEventPayload
{
    string Symbol;
    DateOnly SessionDate;
    decimal Open;
    decimal High;
    decimal Low;
    decimal Close;
    long Volume;
    string Source;
    long SequenceNumber;
}
```

Key points:

- Includes strict OHLC validation (`Low <= Open/Close <= High`, all positive).
- Stores `SessionDate` rather than arbitrary timestamp.
- `ToTimestampUtc()` maps session date to midnight UTC when timestamp normalization is required.

---

## Practical Mapping Summary

- **Trade tape + order-flow** → `TradeDataCollector`
- **Order book (L2)** → `MarketDepthCollector`
- **Best bid/offer cache + events** → `QuoteCollector`
- **Backfill bars** → `HistoricalBar` payloads on `MarketEventType.HistoricalBar`

This split keeps domain logic deterministic, testable, and independent from provider adapter implementation details.
