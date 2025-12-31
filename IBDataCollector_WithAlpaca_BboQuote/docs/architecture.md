# IB Data Collector – System Architecture

## Overview

The IB Data Collector is a modular, event-driven system for capturing, validating,
and persisting high-fidelity market microstructure data from Interactive Brokers (IB)
or Alpaca.

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
Infrastructure (IB / Alpaca)
```

### Infrastructure
* Owns all IB/Alpaca-specific code
* No domain logic
* Replaceable / mockable
* **Note:** `LightweightMarketDepthCollector.cs` is deprecated legacy code and should be removed

### Domain
* Pure business logic
* Trades, order books, integrity
* Deterministic and testable
* **Known Issue:** Price/Size validation not yet implemented (see TODOs in Trade.cs, OrderBookLevel.cs)

### Application
* Orchestration
* Hot reload
* Subscriptions
* Monitoring
* **Known Issue:** Error handling uses bare catch blocks in some areas (logging framework needed)

---

## Event Flow

1. IB/Alpaca sends raw callbacks
2. `IBCallbackRouter` or `AlpacaMarketDataClient` normalizes data
3. Domain collectors process updates
4. `MarketEvent` objects emitted
5. Events routed via `IMarketEventBus`
6. Stored as JSONL
7. Status written for UI

### Event Pipeline Details

* **Market event bus** – a per-symbol fan-out with back-pressure awareness to avoid blocking unrelated symbols when one stream misbehaves.
* **Storage policy** – `JsonlStoragePolicy` groups files by `<Symbol>.<MarketEventType>.jsonl` (for example, `SPY.LOBSnapshot.jsonl`) and rotates when file size thresholds are hit.
* **Metadata** – status writers expose lightweight health snapshots for the ASP.NET UI and the startup scripts to poll.

### Resilience and integrity

* **Depth gaps** – `MarketDepthCollector` freezes a symbol and emits a `DepthIntegrityEvent` if the sequence of updates is inconsistent; operators can call `ResetSymbolStream` via the UI or restart the subscription to resume.
* **Config hot reload** – `ConfigWatcher` listens for changes to `appsettings.json` and resubscribes symbols without tearing down the process.
* **Pluggable data source** – the `IIBMarketDataClient` abstraction allows switching between live IB API and Alpaca WebSocket clients without altering domain logic.

---

## Known Issues and TODOs

### Critical (Fixed)
- ~~Subscription bug in Program.cs where `SubscribeDepth` was checked twice instead of `SubscribeTrades`~~ (Fixed)

### High Priority
| Area | Issue | Location |
|------|-------|----------|
| Logging | No logging framework - errors silently swallowed | Codebase-wide |
| Security | API credentials in plaintext config files | `AlpacaOptions.cs` |
| Connection | No retry logic with exponential backoff | `EnhancedIBConnectionManager.cs`, `AlpacaMarketDataClient.cs` |
| Validation | No price/size validation in domain models | `Trade.cs`, `OrderBookLevel.cs` |

### Medium Priority
| Area | Issue | Location |
|------|-------|----------|
| Alpaca | Quote messages not wired to L2 collector | `AlpacaMarketDataClient.cs` |
| Types | Mixed use of `double` and `decimal` for prices | Domain models |
| Dead Code | `LightweightMarketDepthCollector.cs` is unused | Domain folder |

### Recommendations
1. Add Serilog or similar logging framework
2. Move credentials to environment variables or secure vault
3. Implement connection retry with exponential backoff
4. Add input validation for financial data (Price > 0, Size >= 0)
5. Delete deprecated `LightweightMarketDepthCollector.cs`
