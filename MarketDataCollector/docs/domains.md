# Domain Model

## MarketEvent

All domain activity is normalized into a single event type:

```csharp
record MarketEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    MarketEventType Type,
    MarketEventPayload Payload
);
```

Each payload is a strongly-typed record derived from `MarketEventPayload`; the `Type` field disambiguates serialization. Supported event types include:
- `Trade` – tick-by-tick trade prints
- `L2Snapshot` – full order book state after each depth update
- `BboQuote` – best bid/offer snapshots
- `OrderFlow` – rolling order-flow statistics
- `Integrity` – trade sequence anomalies
- `DepthIntegrity` – order book integrity failures

## TradeDataCollector
* Ingests tick-by-tick trades via `OnTrade(MarketTradeUpdate)`
* Validates sequence continuity per symbol/stream
* Infers aggressor side using `IQuoteStateStore` (BBO context) when upstream provides `Unknown`
* Maintains rolling order-flow statistics (VWAP, buy/sell volume splits, imbalance)
* Emits:
  - `Trade`
  - `OrderFlowStatistics`
  - `IntegrityEvent` (on sequence gaps or out-of-order)

### Trade payload

```csharp
record Trade(
    DateTimeOffset Timestamp,
    string Symbol,
    decimal Price,
    long Size,
    AggressorSide Aggressor,
    long? SequenceNumber,
    string? StreamId,
    string? Venue
);
```

* **Timestamp** – exchange timestamp if available; otherwise when the update was observed.
* **Price / Size** – raw print values from the feed.
* **Aggressor** – derived when quote context is available (`Buy` if price >= ask, `Sell` if price <= bid, otherwise `Unknown`).
* **SequenceNumber / StreamId / Venue** – passed through for downstream filtering, TCA, and reconciliation.

### OrderFlowStatistics payload

```csharp
record OrderFlowStatistics(
    DateTimeOffset Timestamp,
    string Symbol,
    long BuyVolume,
    long SellVolume,
    long UnknownVolume,
    decimal VWAP,
    decimal Imbalance,
    int TradeCount,
    long SequenceNumber,
    string? StreamId,
    string? Venue
);
```

Rolling statistics emitted after each trade to support lightweight monitoring without replaying the full tape:

* Trade count and cumulative volume (buy / sell / unknown)
* Volume-weighted average price (VWAP)
* Imbalance ratio: `(BuyVolume - SellVolume) / TotalVolume`

### IntegrityEvent

Emitted when trade sequence validation fails:

* **OutOfOrder** – received sequence <= last sequence (trade is rejected)
* **SequenceGap** – received sequence > expected next (trade is accepted but stats marked stale)

## MarketDepthCollector
* Maintains L2 order books per symbol via `OnDepth(MarketDepthUpdate)`
* Applies incremental updates (insert/update/delete)
* Freezes symbol stream on integrity violations
* Emits:
  - `LOBSnapshot`
  - `DepthIntegrityEvent`

### Integrity Guarantees
If an invalid update is detected:
* Symbol is frozen (`IsSymbolStreamStale` returns true)
* `DepthIntegrityEvent` is emitted with detailed context
* Operator must call `ResetSymbolStream(symbol)` to resume

### LOBSnapshot payload

```csharp
record LOBSnapshot(
    DateTimeOffset Timestamp,
    string Symbol,
    OrderBookLevel[] Bids,
    OrderBookLevel[] Asks,
    double? MidPrice,
    double? MicroPrice,
    double? Imbalance,
    MarketState MarketState,
    long SequenceNumber,
    string? StreamId,
    string? Venue
);
```

* Sorted bid/ask ladders with `OrderBookLevel(Side, Level, Price, Size, MarketMaker)`
* Derived mid-price: `(BestBid + BestAsk) / 2`
* Top-of-book imbalance: `(BidSize - AskSize) / (BidSize + AskSize)`
* Sequence numbers for replay continuity detection
* Optional `StreamId` / `Venue` for multi-source reconciliation

### DepthIntegrityEvent payload

```csharp
record DepthIntegrityEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    DepthIntegrityKind Kind,
    string Description,
    int Position,
    DepthOperation Operation,
    OrderBookSide Side,
    long SequenceNumber,
    string? StreamId,
    string? Venue
);
```

Provides operators with enough context to respond quickly:

* **Kind** – `Gap`, `OutOfOrder`, `InvalidPosition`, `Stale`, or `Unknown`
* **Position / Operation / Side** – the offending update details
* **Description** – human-readable error message
* Suggested action: resubscribe the symbol or call `ResetSymbolStream`

## QuoteCollector (BBO)

* Tracks the latest best-bid/offer snapshot per symbol via `OnQuote(MarketQuoteUpdate)`
* Implements `IQuoteStateStore` interface for lookup by other collectors
* Maintains monotonically increasing per-symbol sequence numbers
* Emits:
  - `BboQuote`

### BboQuote payload

```csharp
record BboQuotePayload(
    DateTimeOffset Timestamp,
    string Symbol,
    decimal BidPrice,
    long BidSize,
    decimal AskPrice,
    long AskSize,
    decimal? MidPrice,
    decimal? Spread,
    long SequenceNumber,
    string? StreamId,
    string? Venue
);
```

* Bid/ask price and size with sequence numbers and stream identifiers.
* Derived mid-price and spread fields are populated only when both sides are positive and `AskPrice >= BidPrice`.
* Optional venue and stream IDs help downstream consumers reconcile overlapping IB/Alpaca feeds.

### IQuoteStateStore Interface

```csharp
interface IQuoteStateStore
{
    bool TryGet(string symbol, out BboQuotePayload quote);
}
```

* `TradeDataCollector` uses this to classify trade aggressor side.
* `QuoteCollector.Snapshot()` returns all current BBO states for UI/API access.

### Consumers

* `TradeDataCollector` uses the current BBO when classifying trade aggressor side.
* `OrderFlowStatistics` includes buy/sell splits when BBO context is available.
* Operators can snapshot current quotes through the API/UI without replaying stored events.
