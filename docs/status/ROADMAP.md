# Market Data Collector - Project Roadmap

**Version:** 1.6.1  
**Last Updated:** 2026-02-14  
**Status:** Production Ready (hardening and scale-up in progress)  
**Repository Snapshot:** `src/` files: **710** | `tests/` files: **149** | HTTP routes mapped in `Ui.Shared/Endpoints`: **244** | Remaining stub routes: **0**

This roadmap is refreshed to match the current repository state and focuses on the remaining work required to move from "production-ready" to a more fully hardened v2.0 release posture.

---

## Current State Summary

### What is complete

- **Phases 0–6 are complete** (critical bug fixes, API route implementation, desktop workflow completion, operations baseline, and duplicate-code cleanup).
- **All previously declared stub HTTP routes have been implemented**; `StubEndpoints.MapStubEndpoints()` is intentionally empty and retained as a guardrail for future additions.
- **WPF is the active desktop path**; UWP is deprecated and maintained only for critical fixes.
- **Operational baseline is in place** (API auth/rate limiting, Prometheus export, deployment docs, alerting assets).

### What remains

Remaining work is primarily quality and architecture hardening, as tracked in `docs/status/IMPROVEMENTS.md`:

- **33 tracked improvement items total**
  - ✅ Completed: 14
  - 🔄 Partial: 4
  - 📝 Open: 15
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

---

## Priority Roadmap (Next 6 Sprints)

This section supersedes the prior effort model and aligns with the current active backlog.

### Sprint 1 ✅ COMPLETED

- ✅ **C4**: Remove static metrics dependency from `EventPipeline` via DI-friendly metrics abstraction.
  - `IEventMetrics` interface with injectable implementation
  - Tests: `EventPipelineMetricsTests.cs` (5 tests)
- ✅ **C5**: Consolidate configuration validation path into one canonical pipeline.
  - `ConfigValidationPipeline` with composable stages
  - Tests: `ConfigValidationPipelineTests.cs` (13 tests)

### Sprint 2 ✅ COMPLETED

- ✅ **D4**: Implement quality metrics API surface (`/api/quality/drops`, symbol-specific variants).
  - Endpoints: `/api/quality/drops` and `/api/quality/drops/{symbol}`
  - Full implementation in `QualityDropsEndpoints.cs`
- ✅ **B1 (remainder)**: Expand endpoint integration checks around newly implemented quality endpoints.
  - Tests: `QualityDropsEndpointTests.cs` (10 tests)

### Sprint 3 📝 ACTIVE

- 📝 **C6**: Complete multi-sink fan-out hardening for storage writes.
- 📝 **A7**: Standardize startup/runtime error handling conventions and diagnostics.

### Sprint 4 📋 PLANNED

- 📋 **B2 (tranche 1)**: Increase endpoint integration coverage for health/status/config + negative-path behavior.

### Sprint 5 📋 PLANNED

- 📋 **C1/C2**: Provider registration and runtime composition unification under DI.

### Sprint 6 📋 PLANNED

- 📋 **B3 (tranche 1)**: Provider-focused tests for parsing/subscription/reconnect behavior (starting with highest-risk providers).

---

## 2026 Delivery Objectives

### Objective 1: Test Confidence

- Expand integration and provider tests for critical APIs and reconnect/backfill paths.
- Prioritize risk-based coverage over broad shallow coverage.

### Objective 2: Architectural Sustainability

- Close the Theme C items that currently block easier testing and provider evolution.
- Reduce reliance on static singletons and duplicated configuration logic.

### Objective 3: API Productization

- Complete quality metrics API exposure and API documentation parity.
- Improve response schema completeness and operational diagnostics.

### Objective 4: Operational Hardening

- Tighten observability-to-action workflows (alerts, runbooks, quality dashboards).
- Continue deployment profile validation under realistic production-like workloads.

---

## Success Metrics (Updated Baseline)

| Metric | Current Baseline | 2026 Target | Sprint 1-2 Progress |
|---|---:|---:|---:|
| Stub endpoints remaining | 0 | 0 | ✅ Maintained |
| Improvement items completed | 18 / 33 | 24+ / 33 | ✅ +4 items (C4, C5, D4, B1) |
| Improvement items still open | 12 / 33 | <6 / 33 | ✅ -4 items |
| Endpoint integration suite breadth | Baseline established | Critical endpoint families fully covered | ✅ Quality drops endpoints covered (10 tests) |
| Architecture debt (Theme C completed) | 2 / 7 | 5+ / 7 | ✅ +2 (C4, C5) |
| Sprint completion rate | 2 / 6 sprints | 6 / 6 sprints | ✅ 33% complete |

---

## Reference Documents

- `docs/status/IMPROVEMENTS.md` — canonical improvement tracking and sprint recommendations.
- `docs/status/production-status.md` — production readiness assessment narrative.
- `docs/status/CHANGELOG.md` — change log by release snapshot.
- `docs/status/TODO.md` — TODO/NOTE extraction for follow-up.
- `docs/development/uwp-to-wpf-migration.md` — desktop strategy and UWP deprecation context.

---

*Last Updated: 2026-02-14*
