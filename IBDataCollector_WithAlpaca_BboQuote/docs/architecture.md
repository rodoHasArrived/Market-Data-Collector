# IB Data Collector – System Architecture

## Overview

The IB Data Collector is a modular, event-driven system for capturing, validating,
and persisting high-fidelity market microstructure data from Interactive Brokers (IB).

The system is designed around strict separation of concerns and is safe to operate
with or without a live IB connection.

---

## Layered Architecture

```
UI (ASP.NET)
   ↓ JSON / FS
Application
   ↓ MarketEvents
Domain
   ↓ publish()
Event Pipeline
   ↓ append()
Storage
   ↑
Infrastructure (IB)
```

### Infrastructure
* Owns all IB-specific code
* No domain logic
* Replaceable / mockable

### Domain
* Pure business logic
* Trades, order books, integrity
* Deterministic and testable

### Application
* Orchestration
* Hot reload
* Subscriptions
* Monitoring

---

## Event Flow

1. IB sends raw callbacks
2. `IBCallbackRouter` normalizes data
3. Domain collectors process updates
4. `MarketEvent` objects emitted
5. Events routed via `IMarketEventBus`
6. Stored as JSONL
7. Status written for UI

### Event Pipeline Details

* **Market event bus** – a per-symbol fan-out with back-pressure awareness to avoid blocking unrelated symbols when one stream misbehaves.
* **Storage policy** – `JsonlStoragePolicy` groups files by `<Symbol>.<MarketEventType>.jsonl` (for example, `SPY.LOBSnapshot.jsonl`) and rotates when file size thresholds are hit.
* **Metadata** – status writers expose lightweight health snapshots for the ASP.NET UI and the startup scripts to poll.

### Quote/BBO Path

* `QuoteStateStore` tracks the latest BBO per symbol and emits `BboQuote` events with bid/ask, spread, and mid-price when possible.
* Alpaca WebSocket feeds can supply BBO updates even when IB is disabled; sequence numbers and stream IDs are preserved to help reconcile overlapping feeds.
* `TradeDataCollector` uses the most recent BBO to infer aggressor side for trades and to compute buy/sell splits in order-flow statistics.

### Resilience and integrity

* **Depth gaps** – `MarketDepthCollector` freezes a symbol and emits a `DepthIntegrityEvent` if the sequence of updates is inconsistent; operators can call `ResetSymbolStream` via the UI or restart the subscription to resume.
* **Config hot reload** – `ConfigWatcher` listens for changes to `appsettings.json` and resubscribes symbols without tearing down the process.
* **Pluggable data source** – the `IIBMarketDataClient` abstraction allows switching between live IB API and Alpaca WebSocket clients without altering domain logic.
