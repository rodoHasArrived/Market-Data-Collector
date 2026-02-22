# Market Data Collector - Project Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-02-22
**Status:** Development / Pilot Ready (hardening and scale-up in progress)
**Repository Snapshot:** `src/` files: **664** | `tests/` files: **219** | HTTP route constants: **283** | Remaining stub routes: **0** | Test methods: **~3,444**

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

Remaining work is minimal, tracked in `docs/status/IMPROVEMENTS.md`:

- **35 tracked improvement items total** (core themes A–G)
  - ✅ Completed: 33
  - 🔄 Partial: 1 (G2 — OpenTelemetry trace context propagation)
  - 📝 Open: 1 (C3 — WebSocket Provider Base Class Adoption)
- **8 new theme items** (themes H–I)
  - ✅ Completed: 5 (H1, H3, H4, I1, I2)
  - 🔄 Partial: 1 (I3 — Configuration Schema Validation)
  - 📝 Open: 2 (H2 — Multi-Instance Coordination, I4 — Provider SDK Doc Generator)
- Architecture debt largely resolved; C1/C2 unified provider registry and DI composition path are complete.

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
| Phase 9: Final Production Release | 🔄 Active target | 94.3% of core improvements complete; remaining: C3 WebSocket refactor, G2 trace propagation. |
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

### Sprint 6 ✅

- **C1/C2**: ✅ Provider registration and runtime composition unified under DI — `ProviderRegistry` is the single entry point; all services resolved via `ServiceCompositionRoot`.
- **H1**: ✅ Rate limiting per-provider for backfill operations — already implemented via `ProviderRateLimitTracker` in orchestration layer.
- **H4**: ✅ Provider degradation scoring — `ProviderDegradationScorer` with composite health scores and 20+ unit tests.
- **I1**: ✅ Integration test harness — `FixtureMarketDataClient` + `InMemoryStorageSink` + 9 pipeline integration tests.

### Sprint 7 (partial)

- **H2**: Multi-instance coordination via distributed locking for symbol subscriptions. *(pending — not needed for single-instance deployments)*
- **B3 (tranche 2)**: ✅ Provider tests for IB simulation client (15 tests) and Alpaca credential/reconnect behavior (10 tests).

### Sprint 8 (partial)

- **H3**: ✅ Event replay infrastructure — `JsonlReplayer`, `MemoryMappedJsonlReader`, `EventReplayService` with pause/resume/seek, CLI `--replay` flag, desktop `EventReplayPage`.
- **I2**: ✅ CLI progress reporting — `ProgressDisplayService` with progress bars (ETA/throughput), spinners, checklists, and tables.
- **G2 (remainder)**: End-to-end distributed tracing from provider through storage with trace context propagation. *(pending)*

---

## New Improvement Themes

### Theme H: Scalability & Reliability (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| H1 | Per-Provider Backfill Rate Limiting | ✅ Complete | Rate limits are tracked and enforced via `ProviderRateLimitTracker` in the `CompositeHistoricalDataProvider` and `BackfillWorkerService`. |
| H2 | Multi-Instance Symbol Coordination | 📝 Open | Support running multiple collector instances without duplicate subscriptions. Requires distributed locking or leader election for symbol assignment. |
| H3 | Event Replay Infrastructure | ✅ Complete | `JsonlReplayer` and `MemoryMappedJsonlReader` for high-performance replay. `EventReplayService` provides pause/resume/seek controls. CLI `--replay` flag and desktop `EventReplayPage` for UI-based replay. |
| H4 | Graceful Provider Degradation Scoring | ✅ Complete | `ProviderDegradationScorer` computes composite health scores from latency, error rate, connection health, and reconnect frequency. Automatically deprioritizes degraded providers. |

### Theme I: Developer Experience (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| I1 | Integration Test Harness with Fixture Providers | ✅ Complete | `FixtureMarketDataClient` and `InMemoryStorageSink` enable full pipeline integration testing without live API connections. See `tests/.../Integration/FixtureProviderTests.cs`. |
| I2 | CLI Progress Reporting | ✅ Complete | `ProgressDisplayService` provides progress bars with ETA/throughput, Unicode spinners, multi-step checklists, and formatted tables. Supports interactive and CI/CD (non-interactive) modes. |
| I3 | Configuration Schema Validation at Startup | 🔄 Partial | `SchemaValidationService` validates stored data formats against schema versions at startup (`--validate-schemas`, `--strict-schemas`). Missing: JSON Schema generation from C# models for config file validation. |
| I4 | Provider SDK Documentation Generator | 📝 Open | Auto-generate provider capability documentation from `[DataSource]` attributes and `HistoricalDataCapabilities`, keeping docs in sync with code. |

---

## 2026 Delivery Objectives

### Objective 1: Test Confidence ✅ Achieved

- ✅ Expanded integration and provider tests — 12 provider test files, 219 test files total, ~3,444 test methods.
- ✅ Risk-based coverage with negative-path and schema validation tests.
- ✅ Integration test harness with `FixtureMarketDataClient` and `InMemoryStorageSink`.

### Objective 2: Architectural Sustainability ✅ Substantially Achieved

- ✅ C1/C2 complete — unified `ProviderRegistry` and single DI composition path.
- ✅ Static singletons replaced with injectable `IEventMetrics`.
- ✅ Consolidated configuration validation pipeline.
- 🔄 C3 (WebSocket base class) remains open — functional but duplicates ~200-300 LOC.

### Objective 3: API Productization ✅ Achieved

- ✅ Quality metrics API fully exposed (`/api/quality/drops`, per-symbol drill-down).
- ✅ Typed OpenAPI annotations across all endpoint families (58+ endpoints).
- ✅ 283 route constants with 0 stubs remaining.

### Objective 4: Operational Hardening 🔄 Mostly Achieved

- ✅ Prometheus metrics, API auth/rate limiting, category-accurate exit codes.
- ✅ OpenTelemetry pipeline instrumentation with activity spans.
- 🔄 End-to-end trace context propagation pending (G2 remainder).

### Objective 5: Scalability 🔄 Partially Achieved

- ✅ Per-provider rate limit enforcement via `ProviderRateLimitTracker`.
- ✅ Provider degradation scoring via `ProviderDegradationScorer`.
- 📝 H2 multi-instance coordination pending (not needed for single-instance).

---

## Success Metrics (Updated Baseline)

| Metric | Current Baseline | 2026 Target |
|---|---:|---:|
| Stub endpoints remaining | 0 | 0 |
| Core improvement items completed | 33 / 35 | 35 / 35 |
| Core improvement items still open | 1 / 35 (C3) | 0 / 35 |
| New theme items (H/I) completed | 5 / 8 | 7+ / 8 |
| Source files | 664 | — |
| Test files | 219 | 250+ |
| Test methods | ~3,444 | 4,000+ |
| Route constants | 283 | 283 |
| Architecture debt (Theme C completed) | 6 / 7 | 7 / 7 |
| Provider test coverage | All 5 streaming providers + failover + backfill | Comprehensive |
| OpenTelemetry instrumentation | Pipeline metrics + activity spans | Full trace propagation |
| OpenAPI typed annotations | All endpoint families | Complete with error response types |

---

## Reference Documents

- `docs/status/IMPROVEMENTS.md` — canonical improvement tracking and sprint recommendations.
- `docs/status/EVALUATIONS_AND_AUDITS.md` — consolidated architecture evaluations, code audits, and assessments.
- `docs/status/production-status.md` — production readiness assessment narrative.
- `docs/status/CHANGELOG.md` — change log by release snapshot.
- `docs/status/TODO.md` — TODO/NOTE extraction for follow-up.
- `docs/evaluations/` — detailed evaluation source documents (summarized in EVALUATIONS_AND_AUDITS.md).
- `docs/audits/` — detailed audit source documents (summarized in EVALUATIONS_AND_AUDITS.md).

---

*Last Updated: 2026-02-22*
