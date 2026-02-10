# Market Data Collector - Project Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-02-10
**Status:** Development / Pilot Ready
**Source Files:** 807 | **Test Files:** 105 | **API Endpoints:** ~269 declared, ~136 implemented, ~133 stubbed

This roadmap consolidates findings from the existing codebase analysis, production status assessment, improvement backlog, and structural review into a phased execution plan. Each phase builds on the previous one, progressing from critical fixes through testing and architecture improvements to production readiness.

---

## Table of Contents

- [Phase 0: Critical Fixes (Blockers)](#phase-0-critical-fixes-blockers)
- [Phase 1: Core Stability & Testing Foundation](#phase-1-core-stability--testing-foundation)
- [Phase 2: Architecture & Structural Improvements](#phase-2-architecture--structural-improvements)
- [Phase 3: API Completeness & Documentation](#phase-3-api-completeness--documentation)
- [Phase 4: Desktop App Maturity](#phase-4-desktop-app-maturity)
- [Phase 5: Operational Readiness](#phase-5-operational-readiness)
- [Phase 6: Duplicate & Unused Code Cleanup](#phase-6-duplicate--unused-code-cleanup)
- [Phase 7: Extended Capabilities](#phase-7-extended-capabilities)
- [Provider Status Summary](#provider-status-summary)
- [Test Coverage Summary](#test-coverage-summary)
- [Stub Endpoint Inventory](#stub-endpoint-inventory)
- [Reference Documents](#reference-documents)

---

## Phase 0: Critical Fixes (Blockers) ✅ COMPLETED

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

## Phase 1: Core Stability & Testing Foundation ✅ COMPLETED

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

## Phase 2: Architecture & Structural Improvements ✅ COMPLETED

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

## Phase 3: API Completeness & Documentation ✅ COMPLETED

### 3A. Remaining Feature Improvements

From `docs/IMPROVEMENTS.md` — 6 items not started, 3 partially done.

| # | Item | Priority | Source | Status |
|---|------|----------|--------|--------|
| 3A.1 | `/api/quality/drops` endpoint for drop statistics | P1 | IMPROVEMENTS #16 | ✅ Done — `QualityDropsEndpoints.cs` with aggregate and per-symbol routes |
| 3A.2 | `Retry-After` header parsing in backfill worker | P1 | IMPROVEMENTS #17 | ✅ Done — `HttpResponseHandler.HandleRateLimited` now throws typed `RateLimitException` with `RetryAfter` from HTTP headers |
| 3A.3 | HTTP endpoint integration tests | P2 | IMPROVEMENTS #7 | ✅ Done — added integration tests for `/api/data/*`, `/api/ib/*`, `/api/symbols/*`, `/api/storage/*`, `/api/storage/quality/*`, and `/api/symbol-mappings` endpoints |
| 3A.4 | Polygon WebSocket zero-allocation message parsing | P2 | IMPROVEMENTS #18 | ✅ Done — replaced `List<byte>.ToArray()` with pooled `ArrayPool<byte>` buffers and `ReadOnlyMemory<byte>` parsing |
| 3A.5 | OpenAPI `[ProducesResponseType]` annotations | P3 | IMPROVEMENTS #19 | ✅ Done — added `.WithName()` and `.Produces()` annotations to all implemented endpoints across 11 endpoint files (~89 endpoints) |
| 3A.6 | UWP navigation consolidation | P3 | IMPROVEMENTS #15 | Not started (40+ flat items vs WPF's 5 workspaces) |
| 3A.7 | Dropped event audit trail HTTP endpoint | P1 | IMPROVEMENTS #8 | ✅ Done — `DroppedEventAuditTrail` wired via DI, exposed at `/api/quality/drops` |
| 3A.8 | Backfill rate limit `Retry-After` parsing | P1 | IMPROVEMENTS #2 | ✅ Done — `HttpResponseHandler` throws `RateLimitException` with header-parsed `RetryAfter` |
| 3A.9 | GC pressure reduction in Polygon hot path | P3 | IMPROVEMENTS #13 | ✅ Done — pooled `ArrayPool<byte>` message buffer eliminates per-message allocations |

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
| 3C.2 | Add `[ProducesResponseType]` to all endpoint handlers | P3 | ✅ Done — `.WithName()` and `.Produces()` annotations added to all ~89 implemented endpoints |
| 3C.3 | Document HTTP endpoint response schemas in `docs/` | P2 | Currently undocumented |
| 3C.4 | Publish API reference to `docs/reference/api-reference.md` | P3 | Existing file may be incomplete |

---

## Phase 4: Desktop App Maturity ✅ COMPLETED (core items)

### 4A. WPF Desktop App (Recommended)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4A.1 | Implement `BackgroundTaskSchedulerService.ScheduleTask()` and `CancelTask()` | P1 | ✅ Done — full async background loop with linked cancellation tokens |
| 4A.2 | Implement `PendingOperationsQueueService.ProcessAllAsync()` | P1 | ✅ Done — handler registry with retry support for failed operations |
| 4A.3 | Complete WPF feature parity with UWP | P2 | ✅ Done — workspace-organized UI with 70+ source files |
| 4A.4 | Fix desktop charting (depends on Phase 0.3 fix) | P1 | ✅ Done — resolved in Phase 0.3 |

### 4B. UWP Desktop App (Legacy)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4B.1 | Navigation consolidation (40+ flat items → organized workspaces) | P3 | STRUCTURAL D1 |
| 4B.2 | Decide on UWP maintenance strategy (active maintenance vs. deprecation) | P2 | WPF is recommended; UWP may be legacy-only |

### 4C. UI Service Deduplication

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4C.1 | Extract common logic from WPF and UWP services into `Ui.Services` | P2 | ✅ Done (Phase 1) — extracted StorageService (DTOs + base class), RetentionAssuranceService (24 DTOs), WorkspaceService (7 DTOs) into shared `Ui.Services`; eliminated ~1,040 lines of duplication |
| 4C.2 | Add unit tests for `Ui.Services` (60+ services, zero tests) | P2 | Most critical: `LiveDataService`, `BackfillService`, `SymbolManagementService`, `SystemHealthService` |

---

## Phase 5: Operational Readiness ✅ COMPLETED

### 5A. Security & Secrets

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 5A.1 | Implement vault integration for credentials | P2 | ✅ Done — `ISecretProvider` abstraction with `EnvironmentSecretProvider` default; `CredentialConfig` delegates to pluggable provider |
| 5A.2 | Add authentication/authorization to HTTP endpoints | P2 | ✅ Done — API key middleware with rate limiting (IMPROVEMENTS #14) |
| 5A.3 | Audit and harden API key middleware | P2 | ✅ Done — constant-time comparison, 120 req/min rate limiting |

### 5B. Deployment & Monitoring

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 5B.1 | Improve Docker deployment documentation and validation | P2 | ✅ Done — multi-stage Alpine build with non-root user |
| 5B.2 | Validate systemd service configuration | P2 | ✅ Done — service file with watchdog, restart, and resource limits |
| 5B.3 | Configure Prometheus/Grafana dashboards | P2 | ✅ Done — 6 alert groups, Grafana provisioning |
| 5B.4 | Document alerting workflows and escalation paths | P2 | ✅ Done — alert rules in `deploy/monitoring/alert-rules.yml` |
| 5B.5 | Performance tuning guide | P3 | ✅ Done — `docs/operations/performance-tuning.md` |
| 5B.6 | High availability documentation | P3 | ✅ Done — `docs/operations/high-availability.md` |

### 5C. Provider Validation

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 5C.1 | Document IBAPI build steps and validate with live IB Gateway | P1 | Requires `#if IBAPI` compile flag + IB API reference DLL |
| 5C.2 | Validate NYSE integration with live NYSE Connect credentials | P2 | Requires NYSE API key |
| 5C.3 | Validate StockSharp integration with live setup | P2 | Requires `#if STOCKSHARP` flag + StockSharp packages |
| 5C.4 | End-to-end validation of Polygon WebSocket with paid credentials | P2 | Stub mode works; live streaming needs validation |

---

## Phase 6: Duplicate & Unused Code Cleanup (In Progress)

Reduce maintenance burden by eliminating dead code, consolidating duplicate implementations, and resolving ambiguous ownership across projects. Informed by `docs/archived/DUPLICATE_CODE_ANALYSIS.md`, `docs/STRUCTURAL_IMPROVEMENTS.md`, and `docs/audits/CLEANUP_OPPORTUNITIES.md`.

**Estimated savings:** ~2,500–3,000 lines of duplicate code removed, ~300 KB of dead assets deleted, and clearer project boundaries.

### 6A. Dead Code Removal (P1 — Zero Risk) ✅ COMPLETED

Items that are completely unused and can be deleted immediately with no downstream impact.

| # | Item | Location | Evidence | Est. LOC |
|---|------|----------|----------|----------|
| 6A.1 | ✅ **Delete unused `SymbolNormalizer.cs`** — complete duplicate of `SymbolNormalization.cs` with identical methods; 0 references in codebase | `Infrastructure/Utilities/SymbolNormalizer.cs` | `SymbolNormalization.cs` has 7 active references; `SymbolNormalizer.cs` has 0 | ~80 |
| 6A.2 | **Delete UWP Examples folder** — 6 XAML example files + 2 markdown docs never referenced from any code or navigation | `src/MarketDataCollector.Uwp/Examples/` (6 XAML files, ~260 KB) | Retained as active design documentation — referenced in README | ~260 KB |
| 6A.3 | **Remove tracked build artifacts** — `build-output.log` and scratch files tracked in git | Root directory | Already addressed — no build artifacts found in repo root | N/A |

### 6B. Duplicate Interface Consolidation (P2 — Low Risk) — Partially Complete

The same service interfaces are defined in up to three separate projects. Consolidate to a single canonical location in `MarketDataCollector.Ui.Services/Contracts/`.

| # | Item | Canonical Location | Duplicate Locations | Est. LOC |
|---|------|--------------------|---------------------|----------|
| 6B.1 | **`IConfigService`** (127 lines canonical vs 15-line stubs) | `Ui.Services/Contracts/` | `Wpf/Services/IConfigService.cs` (different interface: `IConfigSettingsService`), `Uwp/Contracts/` | ~30 |
| 6B.2 | ✅ **`IThemeService`** | `Ui.Services/Contracts/` | ~~`Wpf/Services/IThemeService.cs`~~ (deleted — was empty shim), `Uwp/Contracts/` | ~20 |
| 6B.3 | **`INotificationService`** | `Ui.Services/Contracts/` | `Wpf/Services/INotificationService.cs` (different signatures: sync vs async), `Uwp/Contracts/` | ~20 |
| 6B.4 | ✅ **`ILoggingService`** | `Ui.Services/Contracts/` | ~~`Wpf/Services/ILoggingService.cs`~~ (deleted — was empty shim), `Uwp/Contracts/` | ~15 |
| 6B.5 | ✅ **`IMessagingService`** | `Ui.Services/Contracts/` | ~~`Wpf/Services/IMessagingService.cs`~~ (deleted — was empty shim), `Uwp/Contracts/` | ~15 |
| 6B.6 | **`IKeyboardShortcutService`** | `Ui.Services/Contracts/` | `Wpf/Services/IKeyboardShortcutService.cs` (platform-specific APIs), `Uwp/Contracts/` | ~15 |
| 6B.7 | ✅ **`IBackgroundTaskSchedulerService`** | `Ui.Services/Contracts/` | ~~`Wpf/Services/IBackgroundTaskSchedulerService.cs`~~ (deleted — was empty shim), `Uwp/Contracts/` | ~15 |
| 6B.8 | ✅ **`IPendingOperationsQueueService`** | `Ui.Services/Contracts/` | ~~`Wpf/Services/IPendingOperationsQueueService.cs`~~ (deleted — was empty shim), `Uwp/Contracts/` | ~15 |
| 6B.9 | ✅ **`IOfflineTrackingPersistenceService`** | `Ui.Services/Contracts/` | ~~`Wpf/Services/IOfflineTrackingPersistenceService.cs`~~ (deleted — was empty shim), `Uwp/Contracts/` | ~15 |

**Approach:** Deleted 6 WPF shim interface files (IThemeService, ILoggingService, IMessagingService, IBackgroundTaskSchedulerService, IPendingOperationsQueueService, IOfflineTrackingPersistenceService) that were empty backwards-compatibility wrappers forwarding to canonical `Ui.Services.Contracts`. Remaining 3 items (IConfigService, INotificationService, IKeyboardShortcutService) have genuine signature differences requiring further work.

### 6C. WPF/UWP Service Deduplication (P2 — Medium Risk)

25+ services are nearly identical (95%+ copy-paste) between WPF and UWP. Extract shared logic into `MarketDataCollector.Ui.Services` and keep only platform-specific adapters in each desktop project. Cross-references: STRUCTURAL_IMPROVEMENTS C1, DUPLICATE_CODE_ANALYSIS §5.

| # | Item | Priority | Description | Est. LOC Saved |
|---|------|----------|-------------|----------------|
| 6C.1 | **Phase 1 — Near-identical services** (BrushRegistry, ExportPresetService, FormValidationService, InfoBarService, TooltipService) | P2 | Services with <5% variation between WPF and UWP; extract to shared project directly | ~400 |
| 6C.2 | **Phase 2 — Singleton-pattern services** (ThemeService, ConfigService, NotificationService, NavigationService, ConnectionService) | P2 | Only differ in singleton pattern (`Lazy<T>` vs `lock`); parameterize the pattern in a shared base | ~600 |
| 6C.3 | **Phase 3 — Services with minor platform differences** (LoggingService, MessagingService, StatusService, CredentialService, SchemaService, WatchlistService) | P2 | ~90% shared logic with small platform-specific branches; use strategy/adapter pattern | ~500 |
| 6C.4 | **Phase 4 — Complex services** (AdminMaintenanceService, AdvancedAnalyticsService, ArchiveHealthService, BackgroundTaskSchedulerService, OfflineTrackingPersistenceService, PendingOperationsQueueService) | P3 | Larger services requiring careful extraction of shared orchestration logic | ~300 |

**Validation:** Full solution build + existing test suite green after each phase.

### 6D. Ambiguous Class Name Resolution (P2 — Low Risk) — Partially Complete

Same-named classes in different namespaces create confusion and maintenance risk.

| # | Item | Locations | Recommendation |
|---|------|-----------|----------------|
| 6D.1 | ✅ **`SubscriptionManager`** — reduced from 3 to 2 classes | ~~`Infrastructure/Providers/`~~ (deleted — unused duplicate), `Application/Subscriptions/`, `Infrastructure/Shared/` | Deleted unused `Infrastructure/Providers/SubscriptionManager.cs`; two remaining serve distinct purposes (app-level subscription lifecycle vs provider-level subscription tracking) |
| 6D.2 | **`ConfigStore`** — 2 classes with same name | `Application/Http/ConfigStore.cs`, `Ui.Shared/Services/ConfigStore.cs` | Intentional wrapper pattern with `using` alias — no rename needed |
| 6D.3 | **`BackfillCoordinator`** — 2 classes with same name | `Application/Http/BackfillCoordinator.cs`, `Ui.Shared/Services/BackfillCoordinator.cs` | Intentional wrapper pattern with `using` alias — no rename needed |
| 6D.4 | ✅ **`HtmlTemplates`** — renamed both classes for clarity | `Application/Http/HtmlTemplates.cs` → `HtmlTemplateManager`, `Ui.Shared/HtmlTemplates.cs` → `HtmlTemplateGenerator` | Renamed: `HtmlTemplateManager` (template loader with file fallback) and `HtmlTemplateGenerator` (inline template generator) |

### 6E. UWP Platform Decoupling (P3 — Higher Risk, Gated on UWP Deprecation Decision)

If the team decides to deprecate UWP in favor of WPF-only (per Phase 4B.2 decision), execute the full removal sequence. See `docs/audits/CLEANUP_OPPORTUNITIES.md` §3 for detailed file-level plan.

| # | Item | Priority | Description |
|---|------|----------|-------------|
| 6E.1 | **Port UWP-only behavior to shared services** | P3 | Identify any logic in UWP services not present in WPF; port to `Ui.Services` |
| 6E.2 | **Remove UWP from solution and CI** | P3 | Remove project from `.sln`, delete UWP jobs from `desktop-builds.yml`, update labeler and quickstart |
| 6E.3 | **Delete `src/MarketDataCollector.Uwp/`** | P3 | ~100 source files, ~16,500 lines; only after 6E.1 and 6E.2 are complete |
| 6E.4 | **Remove UWP integration tests** | P3 | `tests/MarketDataCollector.Tests/Integration/UwpCoreIntegrationTests.cs` and related coverage exclusions |
| 6E.5 | **Update documentation** | P3 | Remove dual-platform wording from docs, move UWP docs to `docs/archived/`, regenerate `docs/generated/repository-structure.md` |

### 6F. Structural Decomposition of Large Files (P3)

Several files exceed 2,000 lines and combine multiple responsibilities. Breaking them apart improves navigability and testability. Cross-reference: CLEANUP_OPPORTUNITIES §4.

| # | Item | Location | Current LOC | Recommendation |
|---|------|----------|-------------|----------------|
| 6F.1 | **Split `UiServer.cs`** into domain-specific endpoint modules | `Application/Http/UiServer.cs` | ~3,030 | Extract `MapHealthEndpoints()`, `MapStorageEndpoints()`, `MapConfigEndpoints()`, etc. |
| 6F.2 | **Split `HtmlTemplates.cs`** — move static CSS/JS to `wwwroot`, keep only dynamic rendering | `Ui.Shared/HtmlTemplates.cs` | ~2,510 | Move static assets to files; split C# into composable render functions |
| 6F.3 | **Decompose `PortableDataPackager.cs`** — separate orchestration, I/O, validation, reporting | `Storage/Packaging/PortableDataPackager.cs` | ~1,100 | Extract `PackageValidator`, `PackageWriter`, `PackageReporter` |
| 6F.4 | **Decompose `AnalysisExportService.cs`** | `Storage/Export/AnalysisExportService.cs` | ~1,300 | Extract format-specific writers and quality report generation |

---

## Phase 7: Extended Capabilities

Longer-term goals for expanding the system.

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 7.1 | Cloud/object storage sinks (S3, Azure Blob, GCS) | P3 | Currently only local filesystem |
| 7.2 | Extended export formats (CSV, Arrow/Feather, HDF5) | P3 | JSONL and Parquet exist |
| 7.3 | Additional pipeline transforms | P3 | |
| 7.4 | Formal OpenAPI/Swagger specification | P3 | Basic integration exists |
| 7.5 | Event-driven architecture (message bus for downstream consumers) | P3 | |
| 7.6 | Multi-tenancy support | P3 | |
| 7.7 | Web dashboard enhancements (real-time charts, interactive controls) | P3 | SSE exists (IMPROVEMENTS #4) |

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
| `docs/archived/DUPLICATE_CODE_ANALYSIS.md` | Duplicate code analysis with implementation progress |
| `docs/audits/CLEANUP_OPPORTUNITIES.md` | WPF-only cleanup plan with file-level detail |
| `docs/audits/CLEANUP_SUMMARY.md` | Repository hygiene cleanup results |

---

## Notes

- Phases are ordered by dependency and risk. Phase 0 should be addressed immediately as it involves silent data loss.
- Within each phase, items are ordered by priority (P1 > P2 > P3).
- Provider implementations are functionally complete — the conditional compilation pattern (`#if IBAPI`, `#if STOCKSHARP`) is intentional to avoid hard dependencies on commercial SDKs.
- The 178 stub endpoints are intentional placeholders that return 501 to prevent confusing 404 errors for declared routes.
- Test coverage (~17% by file count) is the largest gap in the project's production readiness.
- Phase 6 (Duplicate & Unused Code Cleanup) is informed by three prior audits. Many quick-win items from the duplicate code analysis have already been completed (domain models, provider base classes, shared utilities). The remaining work focuses on desktop service deduplication and UWP platform decoupling.

---

*Last Updated: 2026-02-10*
