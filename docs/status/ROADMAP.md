# Market Data Collector - Project Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-02-20
**Status:** Development / Pilot Ready (hardening and scale-up in progress)
**Repository Snapshot:** `src/` files: **647** | `tests/` files: **164** | HTTP routes mapped in `Ui.Shared/Endpoints`: **244** | Remaining stub routes: **0**

This roadmap is refreshed to match the current repository state and focuses on the remaining work required to move from "production-ready" to a more fully hardened v2.0 release posture.

---

## Current State Summary

### What is complete

- **Phases 0–6 are complete** (critical bug fixes, API route implementation, desktop workflow completion, operations baseline, and duplicate-code cleanup).
- **All previously declared stub HTTP routes have been implemented**; `StubEndpoints.MapStubEndpoints()` is intentionally empty and retained as a guardrail for future additions.
- **WPF is the sole desktop client**; UWP has been fully removed.
- **Operational baseline is in place** (API auth/rate limiting, Prometheus export, deployment docs, alerting assets).
- **OpenTelemetry pipeline instrumentation** wired through `TracedEventMetrics` decorator with OTLP-compatible meters.
- **Provider unit tests** expanded for Polygon subscription/reconnect and StockSharp lifecycle scenarios.
- **OpenAPI typed annotations** added to all endpoint families (status, health, backfill, config, providers).
- **Negative-path and schema validation integration tests** added for health/status/config/backfill/provider endpoints.

### What remains

Remaining work is primarily quality and architecture hardening, as tracked in `docs/status/IMPROVEMENTS.md`:

- **35 tracked improvement items total** (core themes A–G)
  - ✅ Completed: 27
  - 🔄 Partial: 4
  - 📝 Open: 4
- Biggest risk concentration remains in **Theme C (Architecture & Modularity)** (4/7 completed).

---

## Phase Status (Updated)

| Phase | Status | Notes |
|---|---|---|
| Phase 0: Critical Fixes | ✅ Completed | Historical blockers closed. |
| Phase 1: Core Stability & Testing Foundation | ✅ Completed (baseline) | Foundation shipped; deeper coverage remains in active backlog (Theme B). |
| Phase 2: Architecture & Structural Improvements | ✅ Completed (baseline) | Follow-on architectural debt tracked in Theme C open items. |
| Phase 3: API Completeness & Documentation | ✅ Completed | Route implementation gap closed; continuing API polish and schema depth in D4/D7. |
| Phase 4: Desktop App Maturity | ✅ Completed | WPF workflow parity achieved; UWP now legacy/deprecated. |
| Phase 5: Operational Readiness | ✅ Completed | Monitoring/auth/deployment foundations in place. |
| Phase 6: Duplicate & Unused Code Cleanup | ✅ Completed | Cleanup phase closed; residual cleanup now folded into normal maintenance. |
| Phase 7: Extended Capabilities | ⏸️ Optional / rolling | Scheduled as capacity permits. |
| Phase 8: Repository Organization & Optimization | 🔄 In progress (rolling) | Continued doc and code organization improvements. |
| Phase 9: Final Production Release | 🔄 Active target | Focus shifted to hardening, coverage, architecture, and performance confidence. |
| Phase 10: Scalability & Multi-Instance | 📝 Planned | New phase for horizontal scaling and multi-instance coordination. |

---

## Priority Roadmap (Next 6 Sprints)

This section supersedes the prior effort model and aligns with the current active backlog.

### Sprint 1 ✅

- **C4**: ✅ Remove static metrics dependency from `EventPipeline` via DI-friendly metrics abstraction.
- **C5**: ✅ Consolidate configuration validation path into one canonical pipeline.

### Sprint 2 ✅

- **D4**: ✅ Implement quality metrics API surface (`/api/quality/drops`, symbol-specific variants).
- **B1 (remainder)**: ✅ Expand endpoint integration checks around newly implemented quality endpoints.

### Sprint 3 ✅

- **C6**: ✅ Complete multi-sink fan-out hardening for storage writes (CompositeSink with per-sink fault isolation).
- **A7**: ✅ Standardize startup/runtime error handling conventions and diagnostics (ErrorCode-based exit codes).

### Sprint 4 ✅

- **B3 (tranche 1)**: ✅ Provider-focused tests for Polygon subscription/reconnect and StockSharp lifecycle.
- **G2 (partial)**: ✅ OpenTelemetry pipeline instrumentation via `TracedEventMetrics` decorator and OTLP meter registration.
- **D7 (partial)**: ✅ Typed OpenAPI response annotations on core health/status endpoints.

### Sprint 5 ✅

- **B2 (tranche 1)**: ✅ Negative-path endpoint tests (40+ tests) and response schema validation tests (15+ tests) for health/status/config/backfill/provider families.
- **D7 (remainder)**: ✅ Typed `Produces<T>()` and `.WithDescription()` OpenAPI annotations extended to all endpoint families (58+ endpoints across 7 files).

### Sprint 6 (partial)

- **C1/C2**: Provider registration and runtime composition unification under DI. *(pending)*
- **H1**: ✅ Rate limiting per-provider for backfill operations — already implemented via `ProviderRateLimitTracker` in orchestration layer.
- **H4**: ✅ Provider degradation scoring — `ProviderDegradationScorer` with composite health scores and 20+ unit tests.
- **I1**: ✅ Integration test harness — `FixtureMarketDataClient` + `InMemoryStorageSink` + 9 pipeline integration tests.

### Sprint 7 (partial)

- **H2**: Multi-instance coordination via distributed locking for symbol subscriptions. *(pending)*
- **B3 (tranche 2)**: ✅ Provider tests for IB simulation client (15 tests) and Alpaca credential/reconnect behavior (10 tests).

### Sprint 8

- **H3**: Event replay infrastructure for debugging and QA (new item).
- **G2 (remainder)**: End-to-end distributed tracing from provider through storage with trace context propagation.

---

## New Improvement Themes

### Theme H: Scalability & Reliability (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| H1 | Per-Provider Backfill Rate Limiting | ✅ Complete | Rate limits are tracked and enforced via `ProviderRateLimitTracker` in the `CompositeHistoricalDataProvider` and `BackfillWorkerService`. |
| H2 | Multi-Instance Symbol Coordination | 📝 Open | Support running multiple collector instances without duplicate subscriptions. Requires distributed locking or leader election for symbol assignment. |
| H3 | Event Replay Infrastructure | 📝 Open | Build a replay service that can re-process stored JSONL/Parquet events through the pipeline for debugging, QA, and backfill verification. |
| H4 | Graceful Provider Degradation Scoring | ✅ Complete | `ProviderDegradationScorer` computes composite health scores from latency, error rate, connection health, and reconnect frequency. Automatically deprioritizes degraded providers. |

### Theme I: Developer Experience (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| I1 | Integration Test Harness with Fixture Providers | ✅ Complete | `FixtureMarketDataClient` and `InMemoryStorageSink` enable full pipeline integration testing without live API connections. See `tests/.../Integration/FixtureProviderTests.cs`. |
| I2 | CLI Progress Reporting | 📝 Open | Add structured progress reporting to long-running CLI operations (backfill, packaging, maintenance) with ETA and throughput metrics. |
| I3 | Configuration Schema Validation at Startup | 📝 Open | Generate JSON Schema from AppConfig record types and validate appsettings.json against it during startup, providing actionable error messages for misconfigurations. |
| I4 | Provider SDK Documentation Generator | 📝 Open | Auto-generate provider capability documentation from `[DataSource]` attributes and `HistoricalDataCapabilities`, keeping docs in sync with code. |

---

## 2026 Delivery Objectives

### Objective 1: Test Confidence

- Expand integration and provider tests for critical APIs and reconnect/backfill paths.
- Prioritize risk-based coverage over broad shallow coverage.
- Build integration test harness for full-pipeline testing without live connections.

### Objective 2: Architectural Sustainability

- Close the Theme C items that currently block easier testing and provider evolution.
- Reduce reliance on static singletons and duplicated configuration logic.
- Unify provider registration under a single DI-driven composition path.

### Objective 3: API Productization

- Complete quality metrics API exposure and API documentation parity.
- Improve response schema completeness and operational diagnostics.
- Typed OpenAPI annotations across all endpoint families.

### Objective 4: Operational Hardening

- Tighten observability-to-action workflows (alerts, runbooks, quality dashboards).
- Continue deployment profile validation under realistic production-like workloads.
- End-to-end distributed tracing from provider ingestion through storage.

### Objective 5: Scalability (New)

- Support multi-instance deployment with symbol coordination.
- Per-provider rate limit enforcement during backfill operations.
- Provider degradation scoring for intelligent failover.

---

## Success Metrics (Updated Baseline)

| Metric | Current Baseline | 2026 Target |
|---|---:|---:|
| Stub endpoints remaining | 0 | 0 |
| Improvement items completed | 27 / 35 | 30+ / 35 |
| Improvement items still open | 4 / 35 | <3 / 35 |
| Endpoint integration suite breadth | Negative-path + schema validation coverage | Critical endpoint families fully covered |
| Architecture debt (Theme C completed) | 4 / 7 | 5+ / 7 |
| Provider test coverage | Polygon + StockSharp + IB Sim + Alpaca | All 5 streaming providers |
| OpenTelemetry instrumentation | Pipeline metrics | Full trace propagation |
| OpenAPI typed annotations | All endpoint families | Complete with error response types |

---

## Reference Documents

- `docs/status/IMPROVEMENTS.md` — canonical improvement tracking and sprint recommendations.
- `docs/status/production-status.md` — production readiness assessment narrative.
- `docs/status/CHANGELOG.md` — change log by release snapshot.
- `docs/status/TODO.md` — TODO/NOTE extraction for follow-up.
- `docs/archived/uwp-to-wpf-migration.md` — desktop strategy and UWP deprecation context (archived).

---

*Last Updated: 2026-02-20*
