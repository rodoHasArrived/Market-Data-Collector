# Design Review Memo (Institutional Sign-Off)

## Document Control
- **System:** IB Data Collector (Microstructure Recorder)
- **Scope:** Architecture, controls, operational posture, and readiness for controlled production use
- **Audience:** Engineering leadership, risk/compliance stakeholders, and institutional reviewers
- **Version:** 1.1

---

## 1. Executive Summary

The IB Data Collector is an event-driven, layered system designed to capture and store high-fidelity market microstructure data (tick-by-tick trades, BBO quotes, and L2 depth) from Interactive Brokers or Alpaca. The architecture emphasizes determinism, auditability, controlled change, and operational safety. It can run in both **live** (IB-connected) and **offline** (no IB dependency) modes with identical orchestration logic and optional Alpaca-only quote/trade ingestion.

The system is suitable for controlled production deployment provided the operational controls in this memo are followed, and the remaining production hardening items are tracked and implemented.

---

## 2. Objectives and Non-Objectives

### Objectives
- Capture trades and L2 depth with integrity checks and stable schemas.
- Ensure clear separation between vendor integration (IB/Alpaca) and business logic (domain).
- Provide operational visibility (status + UI) and safe runtime configuration updates.
- Persist data in an audit-friendly format (JSONL with stable event types).

### Non-Objectives (current phase)
- Execution / order placement.
- Guaranteed zero loss under all circumstances (bounded channels may drop under pressure by design).
- Exchange-certified market data reconstruction (IB feed limitations acknowledged).

---

## 3. Architectural Summary

### Layering
- **Infrastructure:** IB/Alpaca connectivity, callback handling, contract creation. Contains all provider-specific code via `IIBMarketDataClient` implementations (`IBMarketDataClient`, `AlpacaMarketDataClient`, `NoOpIBClient`).
- **Domain:** Order book state (`MarketDepthCollector`), trade analytics (`TradeDataCollector`), BBO caching (`QuoteCollector`), integrity events. Pure logic, testable without IB.
- **Application:** Orchestration (`Program.cs`), config hot-reload (`ConfigWatcher`), status monitoring (`StatusWriter`, `Metrics`).
- **Pipeline/Storage:** Bounded channel routing (`EventPipeline`) and buffered persistence (`JsonlStorageSink`, `JsonlStoragePolicy`).

### Key Control: Unified Event Stream
All outputs normalize to `MarketEvent(Type, Symbol, Timestamp, Payload)` with typed payload records derived from `MarketEventPayload`. This provides a stable contract for storage, monitoring, and future replay/backtesting, and ensures BBO quote updates and trade events carry comparable sequencing metadata across IB and Alpaca feeds.

---

## 4. Operational Safety and Controls

### 4.1 Integrity Controls
- **Trades:** `TradeDataCollector` validates sequence continuity per symbol/stream. Emits `IntegrityEvent.OutOfOrder` (trade rejected) or `IntegrityEvent.SequenceGap` (trade accepted, stats marked stale) when anomalies occur.
- **Depth:** `MarketDepthCollector` emits `DepthIntegrityEvent` on invalid operations/gaps and freezes the symbol stream until `ResetSymbolStream()` is called.
- **Quotes/BBO:** `QuoteCollector` maintains per-symbol BBO state with monotonically increasing sequence numbers. The `IQuoteStateStore` interface allows `TradeDataCollector` to infer aggressor side.

### 4.2 Backpressure and Bounded Queues
The `EventPipeline` uses `System.Threading.Channels` with bounded capacity (default 50,000) and `DropOldest` policy. Under pressure:
- events may be dropped to protect process stability
- drops are counted via `Metrics.Dropped` and visible via status

This is an explicit tradeoff: stability and bounded memory over unbounded buffering.

### 4.3 Change Management (Hot Reload)
Configuration changes are applied via:
- atomic file replace from UI
- debounced, retried parsing by `ConfigWatcher`
- diff application via `SubscriptionManager`

This reduces restart risk and prevents partial-write corruption.

---

## 5. Data Governance

### Storage
- Append-only JSONL files partitioned by `<Symbol>.<EventType>.jsonl` (e.g., `AAPL.Trade.jsonl`, `SPY.BboQuote.jsonl`).
- Optional gzip compression via `Compress` config option.
- Status snapshots written separately under `data/_status/status.json` by `StatusWriter`.

### Schema Management
- `MarketEventType` is the canonical type registry: `Trade`, `L2Snapshot`, `BboQuote`, `OrderFlow`, `Integrity`, `DepthIntegrity`.
- Payloads are typed records intended to be backward-compatible.
- Quote and trade events include `SequenceNumber`, `StreamId`, and `Venue` fields to support reconciliation and replay.

Recommended: version payload records if breaking changes become necessary.

---

## 6. Security & Access Considerations

- Local file output: ensure OS-level permissions restrict access to `data/`.
- UI dashboard: intended for internal/local use. If exposed:
  - add authentication
  - restrict network binding
  - add CSRF protections
- IB credentials/session: controlled externally (TWS/Gateway). No secrets stored in repo.
- Alpaca credentials: stored in `appsettings.json` under `Alpaca.KeyId` and `Alpaca.SecretKey`. Protect this file appropriately or migrate to environment variables / secret vault.

---

## 7. Deployment Guidance

Recommended deployment topology:
- Dedicated host / VM
- Local disk with sufficient throughput and monitoring
- Scheduled archival/rotation policy
- Paper trading first, then limited production scope

Operational prerequisites:
- IB market data entitlements (especially depth) or Alpaca API credentials
- Stable time sync (NTP)
- Log retention and disk monitoring
- Preflight checks (disk space, directory permissions, IB/Alpaca reachability)

---

## 8. Risks and Mitigations

| Risk | Description | Mitigation |
|------|-------------|------------|
| Data gaps | IB/Alpaca feed interruptions or missed updates | Integrity events + resubscribe procedures |
| Event drops | Bounded queues may drop under load | `Metrics.Dropped` + tuning + capacity planning |
| Trade sequence anomalies | Gaps or out-of-order sequences | `IntegrityEvent` emission; out-of-order trades rejected |
| Preferred contract ambiguity | IB preferred shares can resolve incorrectly | Require `LocalSymbol`/`ConId` in config |
| Feed divergence | IB and Alpaca quotes may disagree | Preserve `StreamId`/`Venue`; pick a primary source |
| UI exposure | UI has no auth by default | Keep local-only or add auth if deployed |

---

## 9. Recommended Next Hardening Items

1. Add structured logging sinks and log rotation.
2. Add replay tool to validate stored events.
3. Add unit/integration test suite automation in CI.
4. Add authentication for UI if network-exposed.
5. Add "auto-resubscribe on integrity" policy with rate limits.
6. Add feed-divergence alarms when IB and Alpaca BBO deviate beyond configured tolerances.
7. Wire Alpaca quote messages to `QuoteCollector` for full BBO support.
8. Migrate Alpaca credentials from `appsettings.json` to environment variables or secret vault.

---

## 10. Sign-Off Recommendation

Given the current scope and implemented controls, the system is recommended for **controlled internal production use** (data capture only), subject to:
- adherence to runbook procedures,
- entitlement validation,
- disk and monitoring setup,
- completion of the next hardening items prior to broader deployment.
