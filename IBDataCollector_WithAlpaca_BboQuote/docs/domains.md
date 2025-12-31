# Domain Model

## MarketEvent

All domain activity is normalized into a single event type:

```csharp
record MarketEvent(
    DateTimeOffset Timestamp,
    string Symbol,
    MarketEventType Type,
    object Payload
);
```

Each payload is a strongly-typed record; the `Type` field disambiguates serialization.

## TradeDataCollector
* Ingests tick-by-tick trades
* Maintains rolling statistics
* Emits:
  - Trade
  - OrderFlowStatistics
  - IntegrityEvent

### Trade payload

* **Timestamp** – exchange timestamp if available; otherwise when the update was observed.
* **Price / Size** – raw print values from the feed.
* **Aggressor** – derived when quote context is available (e.g., Alpaca quotes for BBO).
* **Exchange / Conditions** – passed through for downstream filtering and TCA.

### OrderFlowStatistics payload

Rolling statistics emitted periodically per symbol to support lightweight monitoring without replaying the full tape:

* Trade count, volume, and notional over the rolling window
* Volume-weighted average price (VWAP)
* Buy vs. sell aggressor totals when quote context is present

## MarketDepthCollector
* Maintains L2 order books
* Applies incremental updates
* Emits:
  - LOBSnapshot
  - DepthIntegrityEvent

### Integrity Guarantees
If an invalid update is detected:
* Symbol is frozen
* IntegrityEvent is emitted
* Manual or automatic resubscription required

### LOBSnapshot payload

* Sorted bid/ask ladders including price, size, and level index
* Per-book sequence numbers to make it easy to detect dropped files during replay
* Optional metadata about the update source (e.g., IB vs. Alpaca) to aid reconciliation

### DepthIntegrityEvent payload

Provides operators with enough context to respond quickly:

* The offending operation and level (insert/update/delete)
* Expected vs. observed sequence
* Suggested action (usually resubscribe the symbol or clear the book)
