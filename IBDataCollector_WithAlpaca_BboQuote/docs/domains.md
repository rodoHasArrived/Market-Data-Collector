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

## TradeDataCollector
* Ingests tick-by-tick trades
* Maintains rolling statistics
* Emits:
  - Trade
  - OrderFlowStatistics
  - IntegrityEvent

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
