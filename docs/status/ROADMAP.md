# Market Data Collector - Project Roadmap

**Version:** 1.6.2
**Last Updated:** 2026-02-26
**Status:** Development / Pilot Ready (hardening and scale-up in progress)
**Repository Snapshot:** `src/` files: **664** | `tests/` files: **219** | HTTP route constants: **283** | Remaining stub routes: **0** | Test methods: **~3,444**

This roadmap is refreshed to match the current repository state and focuses on the remaining work required to move from "production-ready" to a more fully hardened v2.0 release posture.

For a complete per-feature status breakdown see [`FEATURE_INVENTORY.md`](FEATURE_INVENTORY.md).

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

Remaining work is tracked in `docs/status/IMPROVEMENTS.md` and the new [`FEATURE_INVENTORY.md`](FEATURE_INVENTORY.md):

- **35 tracked improvement items total** (core themes A–G)
  - ✅ Completed: 33
  - 🔄 Partial: 1 (G2 — OpenTelemetry trace context propagation)
  - 📝 Open: 1 (C3 — WebSocket Provider Base Class Adoption)
- **8 new theme items** (themes H–I)
  - ✅ Completed: 6 (H1, H3, H4, I1, I2, I4)
  - 🔄 Partial: 1 (I3 — Configuration Schema Validation)
  - 📝 Open: 1 (H2 — Multi-Instance Coordination)
- **8 canonicalization items** (theme J)
  - ✅ Completed: 7 (J1–J7 — design, MarketEvent fields, canonicalizer, condition codes, venue normalization, provider wiring, metrics)
  - 🔄 Partial: 1 (J8 — Golden fixture test suite; curated fixtures + fixture-runner tests added, drift-canary CI still pending)
- Architecture debt largely resolved; C1/C2 unified provider registry and DI composition path are complete.
- **WPF UX parity**: Navigation complete; ~6 pages still show static placeholder data instead of live service data.
- **Provider completeness**: Polygon and StockSharp functional with credentials; IB and NYSE require external setup steps.

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
| Phase 11: WPF Full UX Parity | 📝 Planned | Wire live data to remaining static-data pages. |
| Phase 12: Provider Completeness | 📝 Planned | Validate Polygon/StockSharp feeds; simplify IB/NYSE setup. |
| Phase 13: Observability Completion | 📝 Planned | End-to-end OTel trace propagation; correlation IDs. |
| Phase 14: See detailed Phase 14 section below | 📝 Planned | See detailed roadmap section for Phase 14 scope. |
| Phase 15: See detailed Phase 15 section below | 📝 Planned | See detailed roadmap section for Phase 15 scope. |

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
| I4 | Provider SDK Documentation Generator | ✅ Complete | `generate-structure-docs.py` `extract_providers()` now reads from the correct `src/MarketDataCollector.Infrastructure/Providers` path, handles both positional and named `[DataSource]` attribute params, and emits a richer table with Class/Type/Category columns. Historical providers fall back to a curated static list. Run via `make gen-providers`. |

### Theme J: Data Canonicalization (New)

| ID | Title | Status | Description |
|----|-------|--------|-------------|
| J1 | Deterministic Canonicalization Design | ✅ Complete | Design document with provider field audit, condition code mapping, venue normalization, and 3-phase rollout plan. See [deterministic-canonicalization.md](../architecture/deterministic-canonicalization.md). |
| J2 | MarketEvent Canonical Fields | ✅ Complete | `CanonicalSymbol`, `CanonicalizationVersion`, `CanonicalVenue` fields added to `MarketEvent`. `EffectiveSymbol` property for downstream consumers. `MarketDataJsonContext` updated. |
| J3 | EventCanonicalizer Implementation | ✅ Complete | `IEventCanonicalizer` interface and `EventCanonicalizer` class. Resolves symbols via `CanonicalSymbolRegistry`, maps venues, extracts venue from typed payloads. |
| J4 | Condition Code Mapping Registry | ✅ Complete | `ConditionCodeMapper` with `config/condition-codes.json` — 17 Alpaca, 19 Polygon, 8 IB mappings to canonical enum. `FrozenDictionary` for hot-path performance. |
| J5 | Venue Normalization to ISO 10383 MIC | ✅ Complete | `VenueMicMapper` with `config/venue-mapping.json` — 29 Alpaca, 17 Polygon, 17 IB venue mappings to ISO 10383 MIC codes. |
| J6 | Provider Adapter Wiring | ✅ Complete | `CanonicalizingPublisher` decorator wraps `IMarketEventPublisher` with DI registration in `ServiceCompositionRoot`. Pilot symbol filtering, dual-write mode, lock-free metrics. |
| J7 | Canonicalization Metrics & Monitoring | ✅ Complete | `CanonicalizationMetrics` with per-provider parity stats. API endpoints for status, parity, and config. Thread-safe counters for success/fail/unresolved. |
| J8 | Golden Fixture Test Suite | 🔄 Partial | 8 curated fixture `.json` files added (Alpaca + Polygon: regular, extended-hours, odd-lot, cross-provider XNAS identity). `CanonicalizationGoldenFixtureTests` drives them via `[Theory][MemberData]` using production `condition-codes.json` and `venue-mapping.json`. Remaining: drift-canary CI job for detecting new unmapped codes. |

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

### Objective 6: Cross-Provider Data Canonicalization ✅ Substantially Achieved

- ✅ Design document complete with provider field audit and 3-phase rollout plan.
- ✅ J2–J7 fully implemented: canonical fields on `MarketEvent`, `EventCanonicalizer`, `ConditionCodeMapper`, `VenueMicMapper`, `CanonicalizingPublisher` decorator with DI wiring, `CanonicalizationMetrics` with API endpoints.
- 🔄 J8 partial: unit tests cover correctness; golden fixture files and drift canary CI pending.
- Target: >= 99.5% canonical identity match rate across providers for US liquid equities.

---

## Success Metrics (Updated Baseline)

| Metric | Current Baseline | 2026 Target |
|---|---:|---:|
| Stub endpoints remaining | 0 | 0 |
| Core improvement items completed | 33 / 35 | 35 / 35 |
| Core improvement items still open | 1 / 35 (C3) | 0 / 35 |
| New theme items (H/I) completed | 6 / 8 | 7+ / 8 |
| Source files | 664 | — |
| Test files | 219 | 250+ |
| Test methods | ~3,444 | 4,000+ |
| Route constants | 283 | 283 |
| Architecture debt (Theme C completed) | 6 / 7 | 7 / 7 |
| Provider test coverage | All 5 streaming providers + failover + backfill | Comprehensive |
| OpenTelemetry instrumentation | Pipeline metrics + activity spans | Full trace propagation |
| OpenAPI typed annotations | All endpoint families | Complete with error response types |
| Canonicalization design | Complete | Implementation complete |
| Canonicalization implementation (J2–J8) | 6 / 7 | 7 / 7 |
| Cross-provider canonical identity match | N/A | >= 99.5% |
| WPF pages with live data | ~42 / 49 | 49 / 49 |

---

## Phases 11–13: Full-Implementation Roadmap

These phases capture all remaining work identified in the [`FEATURE_INVENTORY.md`](FEATURE_INVENTORY.md) full-inventory audit. They bring every started feature to complete implementation.

### Phase 11: WPF Full UX Parity

**Goal:** Replace all static placeholder values with live data from backend services.

| Item | File(s) | Work |
|------|---------|------|
| P11-1 | `StoragePage.xaml` / `.cs` | Wire storage stats (total size, record count, symbol count, tier sizes) to `StorageServiceBase`; replace hardcoded strings with bound properties |
| P11-2 | `WelcomePage.xaml` / `.cs` | Wire connection status, symbol count, and storage path to `StatusService` / `ConnectionService` / `ConfigService`; remove placeholder comments |
| P11-3 | `TradingHoursPage.xaml` / `.cs` | Verify `TradingCalendar` integration is driving market hours display; add live next-open / next-close countdowns |
| P11-4 | All pages | Audit remaining pages for static text masquerading as live data; update or remove as appropriate |

**Exit criteria:** Every page in the WPF application shows live data or clearly-labeled static reference content. No hardcoded metric values remain.

---

### Phase 12: Provider Completeness

**Goal:** Bring all 5 streaming providers to verified, documentable production status.

| Item | Provider | Work |
|------|----------|------|
| P12-1 | **Polygon** | Capture a recorded Polygon v2 WebSocket session (free or paying account); add a replay-based integration test that validates message parsing for trades, quotes, aggregates, and status messages end-to-end |
| P12-2 | **StockSharp** | Document all supported `ConnectorType` values (QuikJson, Transaq, LMAX, etc.) in `docs/providers/`; add a validated `appsettings.sample.stocksharp.json`; fix `NotSupportedException` paths when connector type is unset |
| P12-3 | **Interactive Brokers** | Write `docs/providers/interactive-brokers-build.md` with step-by-step IBAPI download, reference, and build instructions (`dotnet build -p:DefineConstants=IBAPI`); add a CI matrix job that builds with a mocked IBApi stub |
| P12-4 | **NYSE** | Document NYSE Connect credential registration process; add connectivity smoke-test to `--test-connectivity` output |
| P12-5 | **All WebSocket (C3)** | Refactor Polygon, NYSE, StockSharp to extend `WebSocketProviderBase`; eliminate ~800 LOC of duplicated connection-lifecycle code; verify provider tests pass after refactor |

**Exit criteria:** All 5 streaming providers have documented setup paths. Polygon parsing validated against recorded feed. C3 refactor complete.

---

### Phase 13: Observability Completion

**Goal:** Complete end-to-end distributed tracing and improve log correlation.

| Item | Area | Work |
|------|------|------|
| P13-1 | **G2 trace propagation** | Wire `System.Diagnostics.Activity` context from each provider's receive loop through `EventPipeline.PublishAsync` to `IStorageSink.AppendAsync`; use `Activity.Current` baggage or W3C TraceContext headers for cross-component propagation |
| P13-2 | **Correlation IDs in logs** | Add `correlation_id` / `trace_id` structured log properties to all `ILogger` call sites in the pipeline hot path (`EventPipeline`, `JsonlStorageSink`, provider adapters) |
| P13-3 | **Backfill worker tracing** | Wire `StartBackfillActivity` spans per-symbol in `BackfillWorkerService`; propagate through `CompositeHistoricalDataProvider` |
| P13-4 | **OTLP export docs** | Add `docs/operations/opentelemetry-setup.md` with OTLP collector configuration for Jaeger and Zipkin; include Docker Compose sample |

**Exit criteria:** A single live request can be traced from provider receive → pipeline publish → storage write in Jaeger/Zipkin. Correlation IDs appear in all pipeline log entries.

---

### Phase 14: Configuration Schema & Test Completeness

**Goal:** Close the remaining I3 and J8 items.

| Item | Area | Work |
|------|------|------|
| P14-1 | **I3 Config JSON Schema** | Add a build step (or `dotnet run` tool) that generates `config/appsettings.schema.json` from `AppConfig` using `NJsonSchema` or `System.Text.Json.Schema`; add `$schema` pointer to `appsettings.sample.json`; enables IDE auto-complete and validation |
| P14-2 | **J8 Drift-canary CI** | Add a GitHub Actions workflow job that runs `CanonicalizationGoldenFixtureTests` on every push and posts a PR comment listing any new unmapped condition codes or venue identifiers found in the fixture files |

**Exit criteria:** `appsettings.schema.json` present and linked; IDE shows validation on `appsettings.json`. CI fails on unrecognized condition codes or venues.

---

### Phase 15: Scalability (Optional)

**Goal:** Support multiple collector instances without subscription conflicts.

| Item | Area | Work |
|------|------|------|
| P15-1 | **H2 Multi-instance coordination** | Design and implement distributed locking for symbol subscription assignment (Redis or file-based lock); leader election for scheduled backfill; documented topology for 2-node active/active deployment |

**Exit criteria:** Two collector instances can run simultaneously against the same symbol universe without duplicate subscriptions or conflicting backfill jobs.

---

## Reference Documents

- `docs/status/FEATURE_INVENTORY.md` — **new** comprehensive feature inventory with per-area status.
- `docs/status/IMPROVEMENTS.md` — canonical improvement tracking and sprint recommendations.
- `docs/status/EVALUATIONS_AND_AUDITS.md` — consolidated architecture evaluations, code audits, and assessments.
- `docs/status/production-status.md` — production readiness assessment narrative.
- `docs/status/CHANGELOG.md` — change log by release snapshot.
- `docs/status/TODO.md` — TODO/NOTE extraction for follow-up.
- `docs/evaluations/` — detailed evaluation source documents (summarized in EVALUATIONS_AND_AUDITS.md).
- `docs/audits/` — detailed audit source documents (summarized in EVALUATIONS_AND_AUDITS.md).
- `docs/architecture/deterministic-canonicalization.md` — cross-provider canonicalization design.

---

*Last Updated: 2026-02-26*
