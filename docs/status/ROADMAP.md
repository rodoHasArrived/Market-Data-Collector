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
- **OpenAPI typed annotations** added to all endpoint families (status, health, backfill, config, providers).
- **Negative-path and schema validation integration tests** added for health/status/config/backfill/provider endpoints.

### What remains

Remaining work is primarily quality and architecture hardening, as tracked in `docs/status/IMPROVEMENTS.md`:

- **39 tracked improvement items total** (core themes A–H)
  - ✅ Completed: 32
  - 🔄 Partial: 4
  - 📝 Open: 3
- Biggest remaining refactors: **C3** (WebSocket base class adoption — deferred, high effort/low ROI) and **C7** (WPF/UWP service dedup).

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
| Phase 10: Scalability & Multi-Instance | ✅ Completed | H1-H4 complete. Per-provider rate limiting, multi-instance coordination, event replay, and degradation scoring all shipped. |

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

### Sprint 6 ✅

- **C1/C2**: ✅ Provider registration and runtime composition unified under single `ProviderFactory.CreateAndRegisterAllAsync()` call in `ServiceCompositionRoot`.
- **H1**: ✅ Per-provider backfill rate limiting enforcement via `ProviderRateLimitTracker.WaitForSlotAsync()`. 28 new tests in `ProviderRateLimitTrackerTests.cs`.

### Sprint 7 ✅

- **H2**: ✅ Multi-instance symbol coordination via `IInstanceCoordinator` interface and `FileBasedInstanceCoordinator` implementation. JSON claim files with heartbeat-based staleness detection. 19 tests in `FileBasedInstanceCoordinatorTests.cs`.
- **B3 (tranche 2)**: ✅ Provider tests for IB and Alpaca: `IBSimulationClientTests` (24 tests), `IBApiLimitsTests` (40+ tests across 8 classes), `IBContractFactoryTests` (12 tests), `AlpacaProviderTests` (18 tests), `SubscriptionManagerTests` (22 tests).

### Sprint 8 ✅

- **H3**: ✅ Event replay infrastructure via `EventReplayPipeline` — reads stored JSONL events with symbol/type/time filtering, speed control, pause/resume, optional sink publishing. `ReplayPipelineOptions` and `ReplaySessionStatistics` for configuration and metrics. 23 tests in `EventReplayPipelineTests.cs`.
- **G2 (remainder)**: ✅ End-to-end distributed tracing via `TracedStorageSink` decorator — wraps `IStorageSink` with `ActivitySource`-based tracing for append and flush operations. Per-event tags (symbol, type, source, sequence), error recording, operational counters. 16 tests in `TracedStorageSinkTests.cs`.

### Sprint 9 ✅

- **H4**: ✅ Graceful provider degradation scoring via `ProviderDegradationScorer` — multi-factor health scoring engine with configurable weights (latency 25%, stability 30%, completeness 25%, consistency 20%). Continuous 0-100 scores with `ProviderHealthRecommendation` enum (Healthy/Caution/Degraded/FailoverRecommended/Unavailable). Provider ranking, best-provider selection with exclusion, degradation detection, and failover decision support. 35+ tests in `ProviderDegradationScorerTests.cs`.
- **C3**: Deferred — analysis showed HIGH effort (~300 LOC savings) with significant risk refactoring 3 production WebSocket providers. ROI insufficient for current sprint capacity.

### Sprint 10

- **B3 (tranche 3)**: NYSE provider test coverage — hybrid streaming + historical scenarios.
- **I1**: Integration test harness with fixture providers — full pipeline end-to-end testing without live API connections.
- **Theme I exploration**: Evaluate I2 (CLI progress reporting) and I3 (configuration schema validation) for quick wins.

---

## New Improvement Themes

### Theme H: Scalability & Reliability (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| H1 | Per-Provider Backfill Rate Limiting | ✅ Done | Proactive per-provider rate limit enforcement via `WaitForSlotAsync()` in `ProviderRateLimitTracker`, wired into `CompositeHistoricalDataProvider`. 28 tests. |
| H2 | Multi-Instance Symbol Coordination | ✅ Done | `IInstanceCoordinator` interface with `FileBasedInstanceCoordinator` implementation. JSON claim files with heartbeat timeout, stale reclamation, and graceful shutdown. 19 tests. |
| H3 | Event Replay Infrastructure | ✅ Done | `EventReplayPipeline` service with symbol/type/time filtering, speed control, pause/resume, optional sink re-publishing, and `ReplaySessionStatistics`. 23 tests. |
| H4 | Graceful Provider Degradation Scoring | ✅ Done | `ProviderDegradationScorer` with multi-factor scoring (latency/stability/completeness/consistency), configurable weights, continuous 0-100 scores, provider ranking, and failover recommendations. 35+ tests. |

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
| Improvement items completed | 32 / 39 | 36+ / 39 |
| Improvement items still open | 3 / 39 | <3 / 39 |
| Endpoint integration suite breadth | Negative-path + schema validation coverage | Critical endpoint families fully covered |
| Architecture debt (Theme C completed) | 5 / 7 | 7 / 7 |
| Provider test coverage | Polygon + StockSharp + IB + Alpaca | All 5 streaming providers |
| OpenTelemetry instrumentation | Pipeline metrics + storage tracing | Full trace propagation |
| OpenAPI typed annotations | All endpoint families | Complete with error response types |

---

## Reference Documents

- `docs/status/IMPROVEMENTS.md` — canonical improvement tracking and sprint recommendations.
- `docs/status/production-status.md` — production readiness assessment narrative.
- `docs/status/CHANGELOG.md` — change log by release snapshot.
- `docs/status/TODO.md` — TODO/NOTE extraction for follow-up.
- `docs/development/uwp-to-wpf-migration.md` — desktop strategy and UWP deprecation context.

---

*Last Updated: 2026-02-15*
