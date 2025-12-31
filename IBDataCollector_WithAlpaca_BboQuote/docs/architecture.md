# IB Data Collector – System Architecture

## Overview

The IB Data Collector is a modular, event-driven system for capturing, validating,
and persisting high-fidelity market microstructure data from Interactive Brokers (IB)
and/or Alpaca WebSocket feeds.

The system is designed around strict separation of concerns and is safe to operate
with or without a live IB connection. It supports dual-source operation for reconciliation
and provider failover.

---

## Layered Architecture

```
UI (ASP.NET)
   ↓ JSON / FS
Application (Program, ConfigWatcher, StatusWriter)
   ↓ MarketEvents
Domain (TradeDataCollector, MarketDepthCollector, QuoteCollector)
   ↓ publish()
Event Pipeline (bounded Channel<MarketEvent>)
   ↓ append()
Storage (JsonlStorageSink + JsonlStoragePolicy)
   ↑
Infrastructure (IB / Alpaca)
```

### Infrastructure
* Owns all IB-specific and Alpaca-specific code
* `IBMarketDataClient` / `AlpacaMarketDataClient` implement `IIBMarketDataClient`
* `IBCallbackRouter` normalizes IB callbacks into domain updates
* `ContractFactory` resolves symbol configurations to IB contracts
* No domain logic – replaceable / mockable

### Domain
* Pure business logic – deterministic and testable
* `TradeDataCollector` – tick-by-tick trades with sequence validation and order-flow statistics
* `MarketDepthCollector` – L2 order book maintenance with integrity checking
* `QuoteCollector` – BBO state cache and `BboQuote` event emission
* Domain models: `Trade`, `LOBSnapshot`, `BboQuotePayload`, `OrderFlowStatistics`, integrity events

### Application
* `Program.cs` – composition root and startup
* `ConfigWatcher` – hot reload of `appsettings.json`
* `StatusWriter` – periodic health snapshot to `data/_status/status.json`
* `Metrics` – counters for published, dropped, and integrity events

### Storage
* `EventPipeline` – bounded `Channel<MarketEvent>` with configurable capacity and drop policy
* `JsonlStorageSink` – writes events to append-only JSONL files
* `JsonlStoragePolicy` – partitions by `<Symbol>.<EventType>.jsonl` with optional compression

---

## Event Flow

1. IB sends raw callbacks **or** Alpaca pushes WebSocket messages
2. `IBCallbackRouter` (IB) or `AlpacaMarketDataClient` (Alpaca) normalizes data into domain update structs
3. Domain collectors process updates:
   - `TradeDataCollector.OnTrade(MarketTradeUpdate)`
   - `MarketDepthCollector.OnDepth(MarketDepthUpdate)`
   - `QuoteCollector.OnQuote(MarketQuoteUpdate)`
4. Collectors emit strongly-typed `MarketEvent` objects via `IMarketEventPublisher`
5. `EventPipeline` routes events through a bounded channel to decouple producers from I/O
6. `JsonlStorageSink` appends events as JSONL
7. `StatusWriter` periodically dumps health snapshots for UI/monitoring

### Event Pipeline Details

* **Bounded channel** – `EventPipeline` uses `System.Threading.Channels` with configurable capacity (default 50,000) and `DropOldest` backpressure policy.
* **Storage policy** – `JsonlStoragePolicy` groups files by `<Symbol>.<MarketEventType>.jsonl` (e.g., `SPY.Trade.jsonl`, `AAPL.BboQuote.jsonl`) with optional gzip compression.
* **Metrics** – `Metrics.Published`, `Metrics.Dropped`, `Metrics.Integrity` track event throughput and data quality.

### Quote/BBO Path

* `QuoteCollector` maintains the latest BBO per symbol and emits `BboQuote` events with bid/ask price/size, spread, and mid-price when both sides are valid.
* Alpaca WebSocket feeds can supply quote updates even when IB is disabled; sequence numbers (`SequenceNumber`) and stream identifiers (`StreamId`) are preserved to support reconciliation.
* `TradeDataCollector` uses `IQuoteStateStore` (implemented by `QuoteCollector`) to infer aggressor side when the upstream feed provides `AggressorSide.Unknown`.

### Resilience and Integrity

* **Trade sequence validation** – `TradeDataCollector` emits `IntegrityEvent.OutOfOrder` or `IntegrityEvent.SequenceGap` when sequence numbers regress or skip; trades causing out-of-order are rejected.
* **Depth integrity** – `MarketDepthCollector` freezes a symbol and emits `DepthIntegrityEvent` if insert/update/delete operations target invalid positions; operators must call `ResetSymbolStream` to resume.
* **Config hot reload** – `ConfigWatcher` listens for changes to `appsettings.json` and resubscribes symbols without process restart.
* **Pluggable data source** – the `IIBMarketDataClient` abstraction allows switching between `IBMarketDataClient`, `AlpacaMarketDataClient`, or `NoOpIBClient` via the `DataSource` configuration option (`IB` or `Alpaca`).
