# Design Review Memo (Institutional Sign‑Off)

## Document Control
- **System:** IB Data Collector (Microstructure Recorder)
- **Scope:** Architecture, controls, operational posture, and readiness for controlled production use
- **Audience:** Engineering leadership, risk/compliance stakeholders, and institutional reviewers
- **Version:** 1.0

---

## 1. Executive Summary

The IB Data Collector is an event-driven, layered system designed to capture and store high-fidelity market microstructure data (tick-by-tick trades and L2 depth) from Interactive Brokers. The architecture emphasizes determinism, auditability, controlled change, and operational safety. It can run in both **live** (IB-connected) and **offline** (no IB dependency) modes with identical orchestration logic.

The system is suitable for controlled production deployment provided the operational controls in this memo are followed, and the remaining production hardening items are tracked and implemented.

---

## 2. Objectives and Non-Objectives

### Objectives
- Capture trades and L2 depth with integrity checks and stable schemas.
- Ensure clear separation between vendor integration (IB) and business logic (domain).
- Provide operational visibility (status + UI) and safe runtime configuration updates.
- Persist data in an audit-friendly format (JSONL with stable event types).

### Non-Objectives (current phase)
- Execution / order placement.
- Guaranteed zero loss under all circumstances (bounded channels may drop under pressure by design).
- Exchange-certified market data reconstruction (IB feed limitations acknowledged).

---

## 3. Architectural Summary

### Layering
- **Infrastructure:** IB connectivity, callback handling, contract creation. Contains all IB-specific code.
- **Domain:** Order book state, trade analytics, integrity events. Pure logic, testable without IB.
- **Application:** Orchestration, config hot-reload, subscription diffing, monitoring.
- **Pipeline/Storage:** Per-symbol routing and buffered persistence.

### Key Control: Unified Event Stream
All outputs normalize to `MarketEvent(Type, Symbol, Timestamp, Payload)` with typed payload records. This provides a stable contract for storage, monitoring, and future replay/backtesting.

---

## 4. Operational Safety and Controls

### 4.1 Integrity Controls
- **Depth:** `MarketDepthCollector` emits `DepthIntegrityEvent` on invalid operations/gaps and freezes the symbol stream until reset/resubscribe.
- **Trades:** Tick-by-tick subscriptions are managed by `SubscriptionManager`. Future enhancement: sequence validation for trades if IB provides stable sequencing.

### 4.2 Backpressure and Bounded Queues
The event bus uses bounded channels by design. Under pressure:
- events may be dropped to protect process stability
- drops are counted via `Metrics` and visible via status

This is an explicit tradeoff: stability and bounded memory over unbounded buffering.

### 4.3 Change Management (Hot Reload)
Configuration changes are applied via:
- atomic file replace from UI
- debounced, retried parsing by `ConfigWatcher`
- diff application by `SubscriptionManager`

This reduces restart risk and prevents partial-write corruption.

---

## 5. Data Governance

### Storage
- Append-only JSONL files partitioned by event type / symbol / date.
- Optional compression.
- Status snapshots written separately under `data/_status/status.json`.

### Schema Management
- `MarketEventType` is the canonical type registry.
- Payloads are typed records intended to be backward-compatible.

Recommended: version payload records if breaking changes become necessary.

---

## 6. Security & Access Considerations

- Local file output: ensure OS-level permissions restrict access to `data/`.
- UI dashboard: intended for internal/local use. If exposed:
  - add authentication
  - restrict network binding
  - add CSRF protections
- IB credentials/session: controlled externally (TWS/Gateway). No secrets stored in repo.

---

## 7. Deployment Guidance

Recommended deployment topology:
- Dedicated host / VM
- Local disk with sufficient throughput and monitoring
- Scheduled archival/rotation policy
- Paper trading first, then limited production scope

Operational prerequisites:
- IB market data entitlements (especially depth)
- Stable time sync (NTP)
- Log retention and disk monitoring

---

## 8. Risks and Mitigations

| Risk | Description | Mitigation |
|------|-------------|------------|
| Data gaps | IB feed interruptions or missed updates | Integrity events + resubscribe procedures |
| Event drops | Bounded queues may drop under load | Metrics + tuning + capacity planning |
| Preferred contract ambiguity | IB preferred shares can resolve incorrectly | Require `LocalSymbol`/`ConId` in config |
| UI exposure | UI has no auth by default | Keep local-only or add auth if deployed |
| Credential exposure | Alpaca API keys stored in plaintext config | Use env vars or secure vault (see TODOs) |
| Silent error swallowing | Bare catch blocks hide failures | Implement structured logging (Serilog) |
| Connection fragility | No retry logic; single-attempt connections | Implement exponential backoff retry |
| Data validation gaps | No price/size validation in domain models | Add validation guards to reject invalid data |
| Dead code confusion | Deprecated `LightweightMarketDepthCollector.cs` | Delete or clearly mark as obsolete |

---

## 8.1 Recently Fixed Issues

| Issue | Description | Status |
|-------|-------------|--------|
| Subscription bug | `SubscribeDepth` was checked twice instead of `SubscribeTrades` in Program.cs | **Fixed** |
| Performance allocation | `JsonSerializerOptions` created on every subscription message | **Fixed** (cached) |

---

## 9. Recommended Next Hardening Items

### Priority 1 (Critical)
1. **Add structured logging framework** (Serilog recommended) - bare catch blocks currently hide errors
2. **Move credentials to secure storage** - use environment variables or vault service for Alpaca keys
3. **Implement connection retry logic** - add exponential backoff for IB and Alpaca connections

### Priority 2 (High)
4. Add input validation for Price (> 0) and Size (>= 0) in domain models
5. Wire Alpaca quotes to L2 collector for full BBO support
6. Add QuoteStateStore for aggressor classification and better order-flow stats
7. Delete deprecated `LightweightMarketDepthCollector.cs`

### Priority 3 (Medium)
8. Add replay tool to validate stored events
9. Add unit/integration test suite automation in CI
10. Add authentication for UI if network-exposed
11. Add "auto-resubscribe on integrity" policy with rate limits
12. Standardize on `decimal` type for all financial data (currently mixed with `double`)

---

## 10. Sign-Off Recommendation

Given the current scope and implemented controls, the system is recommended for **controlled internal production use** (data capture only), subject to:
- adherence to runbook procedures,
- entitlement validation,
- disk and monitoring setup,
- completion of the next hardening items prior to broader deployment.

