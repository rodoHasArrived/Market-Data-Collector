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
