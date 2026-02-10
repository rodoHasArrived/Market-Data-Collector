# Market Data Collector - Project Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-02-10
**Status:** Development / Pilot Ready
**Source Files:** 807 | **Test Files:** 105 | **API Endpoints:** ~269 declared, ~91 implemented, ~178 stubbed

This roadmap consolidates findings from the existing codebase analysis, production status assessment, improvement backlog, and structural review into a phased execution plan. Each phase builds on the previous one, progressing from critical fixes through testing and architecture improvements to production readiness.

---

## Table of Contents

- [Phase 0: Critical Fixes (Blockers)](#phase-0-critical-fixes-blockers)
- [Phase 1: Core Stability & Testing Foundation](#phase-1-core-stability--testing-foundation)
- [Phase 2: Architecture & Structural Improvements](#phase-2-architecture--structural-improvements)
- [Phase 3: API Completeness & Documentation](#phase-3-api-completeness--documentation)
- [Phase 4: Desktop App Maturity](#phase-4-desktop-app-maturity)
- [Phase 5: Operational Readiness](#phase-5-operational-readiness)
- [Phase 6: Extended Capabilities](#phase-6-extended-capabilities)
- [Provider Status Summary](#provider-status-summary)
- [Test Coverage Summary](#test-coverage-summary)
- [Stub Endpoint Inventory](#stub-endpoint-inventory)
- [Reference Documents](#reference-documents)

---

## Phase 0: Critical Fixes (Blockers)

Issues that silently lose data or prevent core features from working.

| # | Item | Location | Impact |
|---|------|----------|--------|
| 0.1 | **DataGapRepair.StoreBarsAsync is a no-op** — gap repair fetches data from providers but discards it (`await Task.CompletedTask`) due to a circular dependency between Infrastructure and Storage | `Infrastructure/Providers/Historical/GapAnalysis/DataGapRepair.cs:410` | Gap repair results are silently lost |
| 0.2 | **DataCompletenessService.RepairGapAsync always returns false** — UI-triggered gap repair never executes | `Ui.Services/Services/DataCompletenessService.cs:486` | Desktop gap repair is non-functional |
| 0.3 | **BackfillService.GetHistoricalBarsAsync returns empty list** — charting in desktop app shows no data | `Ui.Services/Services/BackfillService.cs:529` | Desktop charting is broken |
| 0.4 | **DataCalendarService and SmartRecommendationsService set `_completenessService = null!`** — will throw NullReferenceException when accessed | `Ui.Services/Services/DataCalendarService.cs:16`, `SmartRecommendationsService.cs:44` | Runtime crashes on calendar/recommendation pages |

**Resolution approach:**
- 0.1: Inject an `IStorageSink` abstraction into `DataGapRepair` to break the circular dependency
- 0.2–0.4: Refactor these UI services to use proper dependency injection instead of manual instantiation

---

## Phase 1: Core Stability & Testing Foundation

Build confidence in existing functionality before adding new features.

### 1A. HTTP Endpoint Integration Tests (P1)

Currently **zero** integration tests for the 91 implemented endpoints.

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 1A.1 | Set up `WebApplicationFactory<T>` test infrastructure | P1 | Base fixture exists at `EndpointIntegrationTestBase` but coverage is minimal |
| 1A.2 | Test core endpoints: `/api/status`, `/api/health`, `/healthz`, `/readyz`, `/livez` | P1 | Kubernetes health probes must be reliable |
| 1A.3 | Test configuration API: `/api/config/*` | P1 | Config changes affect runtime behavior |
| 1A.4 | Test backfill API: `/api/backfill/run`, `/api/backfill/status`, `/api/backfill/schedules` | P1 | Backfill is a primary use case |
| 1A.5 | Test provider API: `/api/providers/status`, `/api/providers/catalog` | P2 | |
| 1A.6 | Test quality API: `/api/quality/dashboard`, `/api/quality/metrics`, `/api/quality/gaps` | P2 | |
| 1A.7 | Test maintenance API: `/api/maintenance/*` | P2 | |
| 1A.8 | Test packaging API: `/api/packaging/*` | P2 | |

### 1B. Infrastructure Provider Unit Tests (P1)

55 provider source files but only 8 test files. Major providers lack dedicated tests.

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 1B.1 | Unit tests for `AlpacaMarketDataClient` (full client, not just quote routing) | P1 | Currently only `AlpacaQuoteRoutingTests` exists |
| 1B.2 | Unit tests for `PolygonMarketDataClient` connection lifecycle and subscription | P1 | Only message parsing is tested |
| 1B.3 | Unit tests for individual historical providers (Alpaca, Stooq, Tiingo, Finnhub, AlphaVantage, Polygon, NasdaqDataLink) | P1 | Zero dedicated unit tests for any historical provider |
| 1B.4 | Unit tests for `FailoverAwareMarketDataClient` failover scenarios | P2 | Some tests exist; need edge case coverage |
| 1B.5 | Unit tests for symbol search providers (Alpaca, Finnhub, Polygon) | P2 | Zero tests for 3 of 5 providers |
| 1B.6 | Unit tests for `WebSocketProviderBase` and `WebSocketConnectionManager` | P2 | Core infrastructure, only resilience policy tested |

### 1C. Application Service Tests (P2)

19 application services have zero test coverage.

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 1C.1 | `TradingCalendar` — market hours and holiday logic | P1 | Affects backfill scheduling and data gap detection |
| 1C.2 | `ConfigurationWizard` and `AutoConfigurationService` | P2 | First-run experience |
| 1C.3 | `ConnectivityTestService` and `CredentialValidationService` | P2 | Used by `--test-connectivity` and `--validate-credentials` |
| 1C.4 | Data quality services: `GapAnalyzer`, `AnomalyDetector`, `CompletenessScoreCalculator`, `SequenceErrorTracker` | P2 | Quality monitoring is a key feature |
| 1C.5 | `SubscriptionManager` and subscription services (10 files) | P2 | Symbol management subsystem |
| 1C.6 | `CronExpressionParser` and scheduling logic | P3 | Used by backfill and maintenance scheduling |

### 1D. Core & ProviderSdk Tests (P2)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 1D.1 | `AppConfig` and `ValidatedConfig` model tests | P2 | Central configuration models |
| 1D.2 | `DataSourceRegistry` and `CredentialValidator` tests | P2 | Provider SDK used by all providers |
| 1D.3 | Exception type tests (9 custom exceptions) | P3 | Ensure proper serialization and message formatting |

---

## Phase 2: Architecture & Structural Improvements

Address structural debt identified in `docs/STRUCTURAL_IMPROVEMENTS.md`. None of these 15 items have been started.

### 2A. Quick Wins (Phase 1 of structural work)

| # | Item | Priority | Source | Description |
|---|------|----------|--------|-------------|
| 2A.1 | Injectable metrics | P1 | STRUCTURAL A4 | Replace static `Metrics` globals in `EventPipeline` with injected interfaces for testability |
| 2A.2 | Consolidated config validation | P1 | STRUCTURAL C2 | Unify 3 overlapping validation classes into a single pipeline |
| 2A.3 | Composite storage sink | P1 | STRUCTURAL B4 | Pipeline accepts single `IStorageSink`; needs multi-sink fan-out support |
| 2A.4 | Quality metrics API endpoints | P1 | STRUCTURAL C3 | Expose computed drop statistics and quality metrics via HTTP (currently internal only) |

### 2B. Core Architecture (Phase 2 of structural work)

| # | Item | Priority | Source | Description |
|---|------|----------|--------|-------------|
| 2B.1 | Unified provider registry | P1 | STRUCTURAL A1 | Three separate provider creation paths; adding a provider requires changes in all three |
| 2B.2 | Single DI composition path | P1 | STRUCTURAL A2 | `Program.cs` bypasses DI for critical components, making DI registrations dead code |
| 2B.3 | Standardized error handling | P2 | STRUCTURAL A5 | Three concurrent strategies (exceptions, `Result<T>`, `Environment.Exit`) used inconsistently |
| 2B.4 | CLI argument parser utility | P2 | STRUCTURAL B1 | Argument parsing duplicated across 9 command files |

### 2C. Larger Refactors (Phase 3 of structural work)

| # | Item | Priority | Source | Description |
|---|------|----------|--------|-------------|
| 2C.1 | WebSocket base class adoption | P2 | STRUCTURAL A3 | Migrate Polygon and NYSE to use `WebSocketProviderBase` (eliminates ~800 lines of duplication) |
| 2C.2 | WPF/UWP service deduplication | P2 | STRUCTURAL C1 | 25–30 nearly identical services across WPF and UWP (~10,000 lines of duplication) |
| 2C.3 | Contextual CLI help | P2 | STRUCTURAL D2 | `--help` dumps 249 lines; needs topic-based help |

---

## Phase 3: API Completeness & Documentation

### 3A. Remaining Feature Improvements

From `docs/IMPROVEMENTS.md` — 6 items not started, 3 partially done.

| # | Item | Priority | Source | Status |
|---|------|----------|--------|--------|
| 3A.1 | `/api/quality/drops` endpoint for drop statistics | P1 | IMPROVEMENTS #16 | Not started |
| 3A.2 | `Retry-After` header parsing in backfill worker | P1 | IMPROVEMENTS #17 | Not started (currently parses exception message text) |
| 3A.3 | HTTP endpoint integration tests | P2 | IMPROVEMENTS #7 | Not started (covered in Phase 1A above) |
| 3A.4 | Polygon WebSocket zero-allocation message parsing | P2 | IMPROVEMENTS #18 | Not started (uses `JsonDocument.Parse()` per message) |
| 3A.5 | OpenAPI `[ProducesResponseType]` annotations | P3 | IMPROVEMENTS #19 | Not started |
| 3A.6 | UWP navigation consolidation | P3 | IMPROVEMENTS #15 | Not started (40+ flat items vs WPF's 5 workspaces) |
| 3A.7 | Dropped event audit trail HTTP endpoint | P1 | IMPROVEMENTS #8 | Partial — core service exists, needs `/api/quality/drops` endpoint |
| 3A.8 | Backfill rate limit `Retry-After` parsing | P1 | IMPROVEMENTS #2 | Partial — needs HTTP header parsing instead of exception text |
| 3A.9 | GC pressure reduction in Polygon hot path | P3 | IMPROVEMENTS #13 | Partial — needs `Utf8JsonReader`/`ObjectPool<T>` |

### 3B. Stub Endpoint Implementation

178 API endpoints return 501 Not Implemented. Prioritized by user-facing impact:

| # | Category | Stub Count | Priority | Notes |
|---|----------|-----------|----------|-------|
| 3B.1 | Symbol management (`/api/symbols/*`) | 15 | P1 | Core user workflow |
| 3B.2 | Storage operations (`/api/storage/*`) | 20 | P1 | Data management |
| 3B.3 | Storage quality (`/api/storage/quality/*`) | 9 | P1 | Quality monitoring |
| 3B.4 | Diagnostics (`/api/diagnostics/*`) | 16 | P2 | Operational debugging |
| 3B.5 | Admin/Maintenance (`/api/admin/*`) | 17 | P2 | System administration |
| 3B.6 | Maintenance schedules (`/api/maintenance/schedules/*`) | 8 | P2 | Automation |
| 3B.7 | Analytics (`/api/analytics/*`) | 10 | P2 | Insights and reporting |
| 3B.8 | System health (`/api/health/*`) | 8 | P2 | Monitoring |
| 3B.9 | Replay (`/api/replay/*`) | 10 | P2 | Data replay workflows |
| 3B.10 | Export (`/api/export/*`) | 6 | P2 | Data export |
| 3B.11 | Lean integration (`/api/lean/*`) | 12 | P3 | QuantConnect integration |
| 3B.12 | Messaging (`/api/messaging/*`) | 11 | P3 | Notifications |
| 3B.13 | Subscriptions (`/api/subscriptions/*`) | 3 | P3 | Subscription management |
| 3B.14 | Sampling (`/api/sampling/*`) | 4 | P3 | Data sampling |
| 3B.15 | Time series alignment (`/api/alignment/*`) | 2 | P3 | Time series tools |
| 3B.16 | Backfill advanced (`/api/backfill/*`) | 16 | P3 | Advanced backfill features |
| 3B.17 | Cron validation (`/api/schedules/cron/*`) | 2 | P3 | Schedule validation |
| 3B.18 | Index endpoints (`/api/indices/*`) | 1 | P3 | Index data |

### 3C. API Documentation

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 3C.1 | Complete OpenAPI/Swagger specification for implemented endpoints | P2 | OpenAPI integration exists (IMPROVEMENTS #11 completed) but annotations are sparse |
| 3C.2 | Add `[ProducesResponseType]` to all endpoint handlers | P3 | Ensures accurate API documentation |
| 3C.3 | Document HTTP endpoint response schemas in `docs/` | P2 | Currently undocumented |
| 3C.4 | Publish API reference to `docs/reference/api-reference.md` | P3 | Existing file may be incomplete |

---

## Phase 4: Desktop App Maturity

### 4A. WPF Desktop App (Recommended)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4A.1 | Implement `BackgroundTaskSchedulerService.ScheduleTask()` and `CancelTask()` | P1 | Currently empty stubs |
| 4A.2 | Implement `PendingOperationsQueueService.Enqueue()` | P1 | Currently empty stub |
| 4A.3 | Complete WPF feature parity with UWP | P2 | Tracked in `docs/development/uwp-to-wpf-migration.md` |
| 4A.4 | Fix desktop charting (depends on Phase 0.3 fix) | P1 | `BackfillService.GetHistoricalBarsAsync` returns empty |

### 4B. UWP Desktop App (Legacy)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4B.1 | Navigation consolidation (40+ flat items → organized workspaces) | P3 | STRUCTURAL D1 |
| 4B.2 | Decide on UWP maintenance strategy (active maintenance vs. deprecation) | P2 | WPF is recommended; UWP may be legacy-only |

### 4C. UI Service Deduplication

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4C.1 | Extract common logic from WPF and UWP services into `Ui.Services` | P2 | ~10,000 lines of near-identical code across 25–30 services |
| 4C.2 | Add unit tests for `Ui.Services` (60+ services, zero tests) | P2 | Most critical: `LiveDataService`, `BackfillService`, `SymbolManagementService`, `SystemHealthService` |

---

## Phase 5: Operational Readiness

The entire pre-production checklist from `docs/status/production-status.md` remains unchecked.

### 5A. Security & Secrets

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 5A.1 | Implement vault integration for credentials (currently environment variables only) | P2 | Stub contract exists in `DataSourceConfiguration.cs:543` |
| 5A.2 | Add authentication/authorization to HTTP endpoints | P2 | API key middleware exists (IMPROVEMENTS #14 completed) but broader auth is needed |
| 5A.3 | Audit and harden API key middleware | P2 | Rate limiting added but security review needed |

### 5B. Deployment & Monitoring

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 5B.1 | Improve Docker deployment documentation and validation | P2 | Dockerfile exists in `deploy/docker/` |
| 5B.2 | Validate systemd service configuration | P2 | Service file exists at `deploy/systemd/` |
| 5B.3 | Configure Prometheus/Grafana dashboards | P2 | Grafana provisioning exists in `deploy/monitoring/` |
| 5B.4 | Document alerting workflows and escalation paths | P2 | |
| 5B.5 | Performance tuning guide (pipeline capacity, depth levels, compression) | P3 | |
| 5B.6 | High availability documentation (failover planning, health monitoring) | P3 | |

### 5C. Provider Validation

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 5C.1 | Document IBAPI build steps and validate with live IB Gateway | P1 | Requires `#if IBAPI` compile flag + IB API reference DLL |
| 5C.2 | Validate NYSE integration with live NYSE Connect credentials | P2 | Requires NYSE API key |
| 5C.3 | Validate StockSharp integration with live setup | P2 | Requires `#if STOCKSHARP` flag + StockSharp packages |
| 5C.4 | End-to-end validation of Polygon WebSocket with paid credentials | P2 | Stub mode works; live streaming needs validation |

---

## Phase 6: Extended Capabilities

Longer-term goals for expanding the system.

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 6.1 | Cloud/object storage sinks (S3, Azure Blob, GCS) | P3 | Currently only local filesystem |
| 6.2 | Extended export formats (CSV, Arrow/Feather, HDF5) | P3 | JSONL and Parquet exist |
| 6.3 | Additional pipeline transforms | P3 | |
| 6.4 | Formal OpenAPI/Swagger specification | P3 | Basic integration exists |
| 6.5 | Event-driven architecture (message bus for downstream consumers) | P3 | |
| 6.6 | Multi-tenancy support | P3 | |
| 6.7 | Web dashboard enhancements (real-time charts, interactive controls) | P3 | SSE exists (IMPROVEMENTS #4) |

---

## Provider Status Summary

### Streaming Providers

| Provider | Implementation Status | Test Status | Credential Requirement |
|----------|----------------------|-------------|----------------------|
| Alpaca | Fully implemented | Partial (quote routing only) | `ALPACA__KEYID`, `ALPACA__SECRETKEY` |
| Polygon | Fully implemented | Partial (message parsing only) | `POLYGON__APIKEY` (stub mode without) |
| Interactive Brokers | Full with `IBAPI` flag; stub without | None | IB Gateway/TWS + IBAPI compile flag |
| NYSE | Fully implemented | Partial (message parsing only) | `NYSE__APIKEY` |
| StockSharp | Full with `STOCKSHARP` flag; stub without | None | StockSharp packages + compile flag |
| Failover | Fully implemented | Partial | N/A (wraps other providers) |
| IB Simulation | Fully implemented | None | None (synthetic data) |
| NoOp | Fully implemented | None | None (null-object pattern) |

### Historical Providers

| Provider | Implementation Status | Test Status | Notes |
|----------|----------------------|-------------|-------|
| Alpaca | Fully implemented | None | 200/min rate limit |
| Stooq | Fully implemented | None | Free, no API key |
| Tiingo | Fully implemented | None | 500/hour rate limit |
| Yahoo Finance | Fully implemented | Integration test only | Unofficial API |
| Finnhub | Fully implemented | None | 60/min rate limit |
| Alpha Vantage | Fully implemented | None | 5/min rate limit |
| Polygon | Fully implemented | None | 5/min free tier |
| Nasdaq Data Link | Fully implemented | None | Dynamic column mapping |
| Interactive Brokers | Full with `IBAPI` flag; stub without | None | IB pacing rules |
| StockSharp | Full with `STOCKSHARP` flag; stub without | None | Connector-based |
| Composite | Fully implemented | Tested | Orchestrates all above |

### Symbol Search Providers

| Provider | Implementation Status | Test Status |
|----------|----------------------|-------------|
| Alpaca | Fully implemented | None |
| Finnhub | Fully implemented | None |
| Polygon | Fully implemented | None |
| OpenFIGI | Fully implemented | Tested |
| StockSharp | Full with `STOCKSHARP` flag; stub without | None |

---

## Test Coverage Summary

| Project | Source Files | Test Files | Coverage Level | Priority |
|---------|-------------|------------|----------------|----------|
| `MarketDataCollector.Domain` | ~13 | ~15 | Good | — |
| `MarketDataCollector.FSharp` | 12 | 4 | Good | — |
| `MarketDataCollector.Storage` | ~47 | ~18 | Moderate | P2 |
| `MarketDataCollector.Application` | ~100 | ~33 | Partial (~33%) | P1 |
| `MarketDataCollector.Infrastructure` | ~85 | ~12 | Low (~14%) | P1 |
| `MarketDataCollector.Core` | ~40 | ~2 | Very Low | P2 |
| `MarketDataCollector.Contracts` | ~60 | ~11 (indirect) | Low | P3 |
| `MarketDataCollector.ProviderSdk` | 12 | 0 | None | P2 |
| `MarketDataCollector.Ui.Services` | ~60 | 0 | None | P2 |
| `MarketDataCollector.Ui.Shared` | ~17 | ~8 (integration) | Moderate | P2 |
| `MarketDataCollector.Wpf` | ~70 | 0 | None | P3 |
| `MarketDataCollector.Uwp` | ~100 | 0 | None | P3 |
| **Total** | **~616** | **~103** | **~17%** | |

---

## Stub Endpoint Inventory

178 API routes currently return HTTP 501 Not Implemented. Full list in `src/MarketDataCollector.Ui.Shared/Endpoints/StubEndpoints.cs`.

| Category | Route Prefix | Count | Suggested Phase |
|----------|-------------|-------|-----------------|
| Symbol management | `/api/symbols/*` | 15 | Phase 3B |
| Storage operations | `/api/storage/*` | 20 | Phase 3B |
| Storage quality | `/api/storage/quality/*` | 9 | Phase 3B |
| Backfill advanced | `/api/backfill/*` | 16 | Phase 3B |
| Diagnostics | `/api/diagnostics/*` | 16 | Phase 3B |
| Admin/Maintenance | `/api/admin/*` | 17 | Phase 3B |
| Maintenance schedules | `/api/maintenance/schedules/*` | 8 | Phase 3B |
| Analytics | `/api/analytics/*` | 10 | Phase 3B |
| System health | `/api/health/*` | 8 | Phase 3B |
| Messaging | `/api/messaging/*` | 11 | Phase 3B |
| Replay | `/api/replay/*` | 10 | Phase 3B |
| Export | `/api/export/*` | 6 | Phase 3B |
| Lean integration | `/api/lean/*` | 12 | Phase 3B |
| Subscriptions | `/api/subscriptions/*` | 3 | Phase 3B |
| Sampling | `/api/sampling/*` | 4 | Phase 3B |
| Cron validation | `/api/schedules/cron/*` | 2 | Phase 3B |
| Time series alignment | `/api/alignment/*` | 2 | Phase 3B |
| Index endpoints | `/api/indices/*` | 1 | Phase 3B |

---

## Reference Documents

| Document | Purpose |
|----------|---------|
| `docs/status/production-status.md` | Current production readiness assessment |
| `docs/status/CHANGELOG.md` | Snapshot-based change history |
| `docs/status/TODO.md` | Auto-scanned TODO/NOTE comments |
| `docs/IMPROVEMENTS.md` | Prioritized feature/reliability improvements (10 done, 3 partial, 6 open) |
| `docs/STRUCTURAL_IMPROVEMENTS.md` | 15 structural improvements (all open) |
| `docs/architecture/overview.md` | System architecture |
| `docs/development/uwp-to-wpf-migration.md` | WPF migration status |
| `docs/ai/ai-known-errors.md` | Recurring AI agent error patterns |

---

## Notes

- Phases are ordered by dependency and risk. Phase 0 should be addressed immediately as it involves silent data loss.
- Within each phase, items are ordered by priority (P1 > P2 > P3).
- Provider implementations are functionally complete — the conditional compilation pattern (`#if IBAPI`, `#if STOCKSHARP`) is intentional to avoid hard dependencies on commercial SDKs.
- The 178 stub endpoints are intentional placeholders that return 501 to prevent confusing 404 errors for declared routes.
- Test coverage (~17% by file count) is the largest gap in the project's production readiness.

---

*Last Updated: 2026-02-10*
