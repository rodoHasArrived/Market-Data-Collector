# Market Data Collector - Project Roadmap

**Version:** 1.6.2
**Last Updated:** 2026-02-15
**Status:** Production Ready (hardening and scale-up in progress)
**Repository Snapshot:** `src/` files: **712** | `tests/` files: **153** | HTTP routes mapped in `Ui.Shared/Endpoints`: **244** | Remaining stub routes: **0**

This roadmap is refreshed to match the current repository state and focuses on the remaining work required to move from "production-ready" to a more fully hardened v2.0 release posture.

---

## Current State Summary

### What is complete

- **Phases 0–6 are complete** (critical bug fixes, API route implementation, desktop workflow completion, operations baseline, and duplicate-code cleanup).
- **All previously declared stub HTTP routes have been implemented**; `StubEndpoints.MapStubEndpoints()` is intentionally empty and retained as a guardrail for future additions.
- **WPF is the active desktop path**; UWP is deprecated and maintained only for critical fixes.
- **Operational baseline is in place** (API auth/rate limiting, Prometheus export, deployment docs, alerting assets).
- **OpenTelemetry pipeline instrumentation** wired through `TracedEventMetrics` decorator with OTLP-compatible meters.
- **Provider unit tests** expanded for Polygon subscription/reconnect and StockSharp lifecycle scenarios.
- **OpenAPI typed annotations** added to core health and status endpoints.

### What remains

Remaining work is primarily quality and architecture hardening, as tracked in `docs/status/IMPROVEMENTS.md`:

- **37 tracked improvement items total** (4 new items added)
  - ✅ Completed: 19
  - 🔄 Partial: 4
  - 📝 Open: 14
- Biggest risk concentration remains in **Theme C (Architecture & Modularity)** and **Theme B (Testing & Quality)**.

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

### Sprint 5 (Next)

- **B2 (tranche 1)**: Increase endpoint integration coverage for health/status/config + negative-path behavior.
- **D7 (remainder)**: Extend typed OpenAPI annotations to backfill, config, and provider endpoint families.

### Sprint 6

- **C1/C2**: Provider registration and runtime composition unification under DI.
- **H1**: Rate limiting per-provider for backfill operations (new item).

### Sprint 7

- **H2**: Multi-instance coordination via distributed locking for symbol subscriptions (new item).
- **B3 (tranche 2)**: Provider tests for IB and Alpaca reconnect/credential-refresh behavior.

### Sprint 8

- **H3**: Event replay infrastructure for debugging and QA (new item).
- **G2 (remainder)**: End-to-end distributed tracing from provider through storage with trace context propagation.

---

## New Improvement Themes

### Theme H: Scalability & Reliability (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| H1 | Per-Provider Backfill Rate Limiting | 📝 Open | Enforce per-provider rate limits during backfill operations to prevent API bans. Currently rate limits are tracked but not enforced at the orchestration layer. |
| H2 | Multi-Instance Symbol Coordination | 📝 Open | Support running multiple collector instances without duplicate subscriptions. Requires distributed locking or leader election for symbol assignment. |
| H3 | Event Replay Infrastructure | 📝 Open | Build a replay service that can re-process stored JSONL/Parquet events through the pipeline for debugging, QA, and backfill verification. |
| H4 | Graceful Provider Degradation Scoring | 📝 Open | Implement a provider health scoring system that automatically deprioritizes degraded providers in the failover chain based on error rates, latency, and data quality metrics. |

### Theme I: Developer Experience (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| I1 | Integration Test Harness with Fixture Providers | 📝 Open | Create a test harness that runs the full pipeline with fixture data providers, enabling end-to-end integration testing without live API connections. |
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
| Improvement items completed | 19 / 37 | 28+ / 37 |
| Improvement items still open | 14 / 37 | <6 / 37 |
| Endpoint integration suite breadth | Baseline established | Critical endpoint families fully covered |
| Architecture debt (Theme C completed) | 1 / 7 | 5+ / 7 |
| Provider test coverage | Polygon + StockSharp | All 5 streaming providers |
| OpenTelemetry instrumentation | Pipeline metrics | Full trace propagation |

---

## Reference Documents

- `docs/status/IMPROVEMENTS.md` — canonical improvement tracking and sprint recommendations.
- `docs/status/production-status.md` — production readiness assessment narrative.
- `docs/status/CHANGELOG.md` — change log by release snapshot.
- `docs/status/TODO.md` — TODO/NOTE extraction for follow-up.
- `docs/development/uwp-to-wpf-migration.md` — desktop strategy and UWP deprecation context.

---

*Last Updated: 2026-02-15*
