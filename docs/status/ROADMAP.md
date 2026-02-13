# Market Data Collector - Project Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-02-13
**Status:** Development / Pilot Ready
**Source Files:** 807 | **Test Files:** 105 | **API Endpoints:** ~269 declared, ~136 implemented, ~133 stubbed

This roadmap consolidates findings from the existing codebase analysis, production status assessment, improvement backlog, and structural review into a phased execution plan. Each phase builds on the previous one, progressing from critical fixes through testing and architecture improvements to production readiness and final optimization.

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
- [Phase 8: Repository Organization & Optimization](#phase-8-repository-organization--optimization)
- [Phase 9: Final Production Release](#phase-9-final-production-release)
- [Execution Timeline & Dependencies](#execution-timeline--dependencies)
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

Address structural debt identified in consolidated [IMPROVEMENTS.md](IMPROVEMENTS.md) themes C (Architecture & Modularity) and E (Performance). None of these 15 items were started at the beginning of Phase 2.

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

From consolidated [IMPROVEMENTS.md](IMPROVEMENTS.md) — items from themes A (Reliability), D (API), and F (UX).

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

## Phase 4: Desktop App Maturity ✅ COMPLETED (all placeholder pages replaced, UI service tests added)

### 4A. WPF Desktop App (Recommended)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4A.1 | Implement `BackgroundTaskSchedulerService.ScheduleTask()` and `CancelTask()` | P1 | ✅ Done — full async background loop with linked cancellation tokens |
| 4A.2 | Implement `PendingOperationsQueueService.ProcessAllAsync()` | P1 | ✅ Done — handler registry with retry support for failed operations |
| 4A.3 | Complete WPF feature parity with UWP | P2 | ✅ Done — all 22 formerly-placeholder pages now have functional implementations with full service integration |
| 4A.4 | Fix desktop charting (depends on Phase 0.3 fix) | P1 | ✅ Done — resolved in Phase 0.3 |
| 4A.5 | Replace placeholder WPF pages with functional screens for end-user workflows | P1 | ✅ Done — 22 of 22 placeholder pages replaced with functional implementations. Final batch: ChartingPage (candlestick charts, 7 technical indicators, volume profile), LeanIntegrationPage (Lean config, data sync, backtest runner with progress), PortfolioImportPage (file import, index constituent import, manual entry), TimeSeriesAlignmentPage (multi-symbol alignment with gap handling presets) |
| 4A.6 | Publish a user-visible WPF implementation matrix (functional vs placeholder pages) | P2 | Keep roadmap messaging aligned with actual click-through UX reality for each release |

**Reality check (current repo state):** `src/MarketDataCollector.Wpf/Views` contains 48 page XAML files. All 22 formerly-placeholder pages now have functional implementations with full XAML UI and code-behind wired to `Ui.Services`. Zero "Coming Soon" placeholder pages remain.

### 4B. UWP Desktop App (Legacy)

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4B.1 | Navigation consolidation (40+ flat items → organized workspaces) | P3 | STRUCTURAL D1 |
| 4B.2 | Decide on UWP maintenance strategy (active maintenance vs. deprecation) | P2 | WPF is recommended; UWP may be legacy-only |

### 4C. UI Service Deduplication

| # | Item | Priority | Notes |
|---|------|----------|-------|
| 4C.1 | Extract common logic from WPF and UWP services into `Ui.Services` | P2 | ✅ Done (Phase 1) — extracted StorageService (DTOs + base class), RetentionAssuranceService (24 DTOs), WorkspaceService (7 DTOs) into shared `Ui.Services`; eliminated ~1,040 lines of duplication |
| 4C.2 | Add unit tests for `Ui.Services` (60+ services) | P2 | ✅ Started — added tests for ChartingService (14 tests: SMA, EMA, RSI, MACD, Bollinger Bands, ATR, VWAP, volume profile), TimeSeriesAlignmentService (11 tests: validation, intervals, gap strategies, presets), LeanIntegrationService (11 tests: DTOs, singleton, enums); plus existing tests for ApiClientService, BackfillService, FixtureDataService, FormValidationService, SystemHealthService, WatchlistService, PortfolioImportService, SchemaService, OrderBookVisualizationService. Remaining: LiveDataService, SymbolManagementService, and other high-traffic services |

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

## Phase 6: Duplicate & Unused Code Cleanup

Reduce maintenance burden by eliminating dead code, consolidating duplicate implementations, and resolving ambiguous ownership across projects. Informed by `docs/archived/DUPLICATE_CODE_ANALYSIS.md`, consolidated [IMPROVEMENTS.md](IMPROVEMENTS.md) Theme C items, and `docs/audits/CLEANUP_OPPORTUNITIES.md`.

**Estimated savings:** ~2,500–3,000 lines of duplicate code removed, ~300 KB of dead assets deleted, and clearer project boundaries.

### 6A. Dead Code Removal (P1 — Zero Risk) — PARTIALLY COMPLETED

Items that are completely unused and can be deleted immediately with no downstream impact.

| # | Item | Location | Evidence | Est. LOC | Status |
|---|------|----------|----------|----------|--------|
| 6A.1 | **Delete unused `SymbolNormalizer.cs`** — complete duplicate of `SymbolNormalization.cs` with identical methods; 0 references in codebase | `Infrastructure/Utilities/SymbolNormalizer.cs` | `SymbolNormalization.cs` has 7 active references; `SymbolNormalizer.cs` has 0 | ~80 | ✅ Done (PR #1028) |
| 6A.2 | **Delete UWP Examples folder** — 6 XAML example files + 2 markdown docs never referenced from any code or navigation | `src/MarketDataCollector.Uwp/Examples/` (6 XAML files, ~260 KB) | Zero C# references to any example file | ~260 KB | ✅ Done |
| 6A.3 | **Remove tracked build artifacts** — `build-output.log` and scratch files tracked in git | Root directory | Already addressed in `docs/audits/CLEANUP_SUMMARY.md`; verify removal is merged | N/A | |

### 6B. Duplicate Interface Consolidation (P2 — Low Risk) — COMPLETED ✅

The same service interfaces are defined in up to three separate projects. Consolidate to a single canonical location in `MarketDataCollector.Ui.Services/Contracts/`.

| # | Item | Canonical Location | Duplicate Locations | Est. LOC | Status |
|---|------|--------------------|---------------------|----------|--------|
| 6B.1 | **`IConfigService`** (127 lines canonical vs 15-line stubs) | `Ui.Services/Contracts/` | `Wpf/Services/IConfigService.cs`, `Uwp/Contracts/` | ~30 | N/A (WPF has IConfigSettingsService, different interface) |
| 6B.2 | **`IThemeService`** | `Ui.Services/Contracts/` | `Wpf/Services/IThemeService.cs`, `Uwp/Contracts/` | ~20 | ✅ Done (PR #1028) |
| 6B.3 | **`INotificationService`** | `Ui.Services/Contracts/` | `Wpf/Services/INotificationService.cs`, `Uwp/Contracts/` | ~20 | ✅ Done |
| 6B.4 | **`ILoggingService`** | `Ui.Services/Contracts/` | `Wpf/Services/ILoggingService.cs`, `Uwp/Contracts/` | ~15 | ✅ Done (PR #1028) |
| 6B.5 | **`IMessagingService`** | `Ui.Services/Contracts/` | `Wpf/Services/IMessagingService.cs`, `Uwp/Contracts/` | ~15 | ✅ Done (PR #1028) |
| 6B.6 | **`IKeyboardShortcutService`** | `Ui.Services/Contracts/` | `Wpf/Services/IKeyboardShortcutService.cs`, `Uwp/Contracts/` | ~15 | ✅ Done (unused duplicates deleted) |
| 6B.7 | **`IBackgroundTaskSchedulerService`** | `Ui.Services/Contracts/` | `Wpf/Services/IBackgroundTaskSchedulerService.cs`, `Uwp/Contracts/` | ~15 | ✅ Done (PR #1028) |
| 6B.8 | **`IPendingOperationsQueueService`** | `Ui.Services/Contracts/` | `Wpf/Services/IPendingOperationsQueueService.cs`, `Uwp/Contracts/` | ~15 | ✅ Done (PR #1028) |
| 6B.9 | **`IOfflineTrackingPersistenceService`** | `Ui.Services/Contracts/` | `Wpf/Services/IOfflineTrackingPersistenceService.cs`, `Uwp/Contracts/` | ~15 | ✅ Done (PR #1028) |

**Approach:** Delete WPF and UWP duplicate interface files. Update `using` directives to reference `Ui.Services.Contracts`. Verify build.

### 6C. WPF/UWP Service Deduplication (P2 — Medium Risk) — PARTIALLY COMPLETED

25+ services are nearly identical (95%+ copy-paste) between WPF and UWP. Extract shared logic into `MarketDataCollector.Ui.Services` and keep only platform-specific adapters in each desktop project. Cross-references: STRUCTURAL_IMPROVEMENTS C1, DUPLICATE_CODE_ANALYSIS §5.

| # | Item | Priority | Description | Est. LOC Saved | Status |
|---|------|----------|-------------|----------------|--------|
| 6C.1 | **Phase 1 — Near-identical services** (BrushRegistry, ExportPresetService, FormValidationService, InfoBarService, TooltipService) | P2 | Services with <5% variation between WPF and UWP; extract to shared project directly | ~400 | ✅ FormValidationService done (PR #1028) |
| 6C.2 | **Phase 2 — Singleton-pattern services** (ThemeService, ConfigService, NotificationService, NavigationService, ConnectionService) | P2 | Only differ in singleton pattern (`Lazy<T>` vs `lock`); parameterize the pattern in a shared base | ~600 | ✅ Done (ThemeServiceBase, ConnectionServiceBase, NavigationServiceBase extracted to Ui.Services; WPF services refactored to extend base classes) |
| 6C.3 | **Phase 3 — Services with minor platform differences** (LoggingService, MessagingService, StatusService, CredentialService, SchemaService, WatchlistService) | P2 | ~90% shared logic with small platform-specific branches; use strategy/adapter pattern | ~500 | ✅ SchemaService done (SchemaServiceBase extracted to Ui.Services; WPF SchemaService refactored to extend base, ~300 LOC saved) |
| 6C.4 | **Phase 4 — Complex services** (AdminMaintenanceService, AdvancedAnalyticsService, ArchiveHealthService, BackgroundTaskSchedulerService, OfflineTrackingPersistenceService, PendingOperationsQueueService) | P3 | Larger services requiring careful extraction of shared orchestration logic | ~300 | |

**Validation:** Full solution build + existing test suite green after each phase.

### 6D. Ambiguous Class Name Resolution (P2 — Low Risk) — PARTIALLY COMPLETED

Same-named classes in different namespaces create confusion and maintenance risk.

| # | Item | Locations | Recommendation | Status |
|---|------|-----------|----------------|--------|
| 6D.1 | **`SubscriptionManager`** — 3 classes with same name across layers | `Application/Subscriptions/`, `Infrastructure/Providers/`, `Infrastructure/Shared/` | Rename to role-specific names: `SubscriptionCoordinator`, `ProviderSubscriptionManager`, `SubscriptionHelper` | ✅ Deleted Infrastructure/Providers duplicate (PR #1028) |
| 6D.2 | **`ConfigStore`** — 2 classes with same name | `Application/Http/ConfigStore.cs`, `Ui.Shared/Services/ConfigStore.cs` | Rename UI variant to `UiConfigStore` or merge functionality | ✅ Done (Ui.Shared is wrapper) |
| 6D.3 | **`BackfillCoordinator`** — 2 classes with same name | `Application/Http/BackfillCoordinator.cs`, `Ui.Shared/Services/BackfillCoordinator.cs` | Rename UI variant to `UiBackfillCoordinator` or merge | ✅ Done (Ui.Shared is wrapper) |
| 6D.4 | **`HtmlTemplates`** — 2 classes with same name | `Application/Http/HtmlTemplates.cs`, `Ui.Shared/HtmlTemplates.cs` | Determine canonical owner; delete or rename the other | ✅ Renamed: HtmlTemplateManager (Application) & HtmlTemplateGenerator (Ui.Shared) (PR #1028) |

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

## Phase 8: Repository Organization & Optimization

Systematically organize and optimize the repository structure to improve maintainability, reduce clutter, and establish clear patterns for future development. This phase focuses on repository-level improvements that don't change functionality but significantly improve developer experience.

**Estimated Effort:** 2-3 weeks | **Dependencies:** Phase 6 completion recommended

### 8A. Documentation Organization (P1 — High Impact)

Consolidate and organize scattered documentation into a clear, navigable structure.

| # | Item | Priority | Description | Est. Hours | Status |
|---|------|----------|-------------|-----------|--------|
| 8A.1 | **Create master documentation index** — Unified `docs/README.md` with categorized links to all documentation | P1 | Single entry point for all documentation; organize by audience (developers, operators, users) | 4 | ✅ Done |
| 8A.2 | **Consolidate archived documentation** — Create `docs/archived/INDEX.md` summarizing 13 historical documents with context | P1 | Currently 13 files in `/docs/archived/` with no index or explanation | 3 | ✅ Done |
| 8A.3 | **Organize status documents** — Ensure `TODO.md`, `ROADMAP.md`, `CHANGELOG.md`, and `production-status.md` have consistent structure and cross-references | P1 | All status docs should reference each other and explain their purpose | 2 | ✅ Done |
| 8A.4 | **Consolidate improvement tracking** — Merge `IMPROVEMENTS.md` and `STRUCTURAL_IMPROVEMENTS.md` into single tracking document with clear status | P2 | Two separate tracking documents consolidated into `docs/status/IMPROVEMENTS.md` with 33 items organized by theme | 4 | ✅ Done |
| 8A.5 | **Update API documentation** — Complete `docs/reference/api-reference.md` with all 136 implemented endpoints and their schemas | P2 | Current API docs may be incomplete or outdated | 8 | |
| 8A.6 | **Create documentation contribution guide** — Document standards for writing, organizing, and updating documentation | P2 | Prevent future documentation debt | 3 | ✅ Done |
| 8A.7 | **Audit and fix broken cross-references** — Scan all markdown files for broken internal links and fix them | P2 | Many docs reference moved or renamed files | 4 | |

### 8B. Project Structure Optimization (P1 — Critical for Maintenance)

Establish clear patterns and remove ambiguity in project organization.

| # | Item | Priority | Description | Est. Hours | Status |
|---|------|----------|-------------|-----------|--------|
| 8B.1 | **Create repository organization guide** — Document file naming conventions, project structure patterns, and where new code should go | P1 | See `docs/development/repository-organization-guide.md` (to be created) | 6 | ✅ Done |
| 8B.2 | **Resolve remaining ambiguous class names** — Fix `ConfigStore` and `BackfillCoordinator` duplicates (Phase 6D.2, 6D.3) | P1 | Two classes with same name in different projects cause confusion | 3 | ✅ Done (wrappers verified) |
| 8B.3 | **Standardize service interfaces** — Ensure all `IXxxService` interfaces follow consistent naming and are in appropriate projects | P1 | Mix of `IConfigService`, `ConfigurationService`, and `IConfigurationService` patterns | 4 | |
| 8B.4 | **Organize test files to mirror source structure** — Ensure test file locations exactly match source file locations | P2 | Some test files don't mirror their source counterparts | 6 | |
| 8B.5 | **Create project dependency diagram** — Visual diagram showing allowed and forbidden dependencies between projects | P2 | Helps prevent architectural violations | 4 | |
| 8B.6 | **Document assembly boundaries** — Clear guide on which types belong in each project (Contracts, Core, Application, Infrastructure) | P2 | Prevent future namespace confusion | 4 | |

### 8C. Code Organization Cleanup (P2 — Quality of Life)

Remove clutter and organize code within projects.

| # | Item | Priority | Description | Est. Hours | Status |
|---|------|----------|-------------|-----------|--------|
| 8C.1 | **Delete UWP Examples folder** — Remove 6 unused XAML example files (~260 KB) (Phase 6A.2) | P1 | Zero references, pure clutter | 1 | ✅ Done |
| 8C.2 | **Clean up build artifacts and temp files** — Audit `.gitignore` and ensure no build outputs are tracked | P1 | `build-output.log` and other artifacts may be tracked | 2 | |
| 8C.3 | **Organize shared utilities** — Move common utilities to `Core/Utilities/` with clear naming | P2 | `SymbolNormalization`, `JsonElementExtensions`, etc. scattered across projects | 6 | |
| 8C.4 | **Split large files** — Break apart `UiServer.cs` (3,030 LOC), `HtmlTemplates.cs` (2,510 LOC), and others (Phase 6F) | P2 | Files >2,000 lines are hard to navigate and maintain | 16 | |
| 8C.5 | **Organize HTTP endpoints** — Ensure all endpoint handlers are in `Endpoints/` folders with consistent naming | P2 | Some endpoint code mixed with other concerns | 4 | |
| 8C.6 | **Standardize configuration files** — Ensure all config files follow consistent naming (`appsettings.*.json` pattern) | P3 | Mix of config file naming patterns | 2 | |

### 8D. Developer Experience Improvements (P2 — Productivity)

Make the repository easier to work with for new and existing developers.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 8D.1 | **Create comprehensive README.md** — Update root README with getting started, architecture overview, and key links | P1 | Current README may not reflect latest state | 6 |
| 8D.2 | **Update CLAUDE.md and Copilot instructions** — Ensure AI instructions reflect Phase 8 organization changes | P1 | Keep AI assistance effective | 4 |
| 8D.3 | **Create contribution guide** — Document how to add providers, features, tests, and documentation | P2 | Lower barrier for new contributors | 8 |
| 8D.4 | **Add architecture decision records** — Document remaining architectural decisions in ADRs | P2 | Only 6 ADRs exist, many decisions undocumented | 12 |
| 8D.5 | **Create troubleshooting guide** — Common errors, solutions, and diagnostic procedures | P2 | Reduce repetitive support questions | 8 |
| 8D.6 | **Improve Makefile documentation** — Add help text and examples for all targets | P3 | 67 targets but minimal documentation | 4 |
| 8D.7 | **Create development environment setup guide** — One-command setup for new developers | P3 | Reduce onboarding friction | 6 |

### 8E. CI/CD & Automation (P2 — Infrastructure)

Optimize build and deployment automation.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 8E.1 | **Consolidate GitHub Actions workflows** — Review 17 workflows for redundancy and optimize for performance | P2 | Some workflows may have overlapping responsibilities | 8 |
| 8E.2 | **Improve build observability** — Enhance build metrics collection and reporting | P2 | Current build-observability.yml may need expansion | 4 |
| 8E.3 | **Add caching to workflows** — Optimize CI/CD speed with NuGet and build caching | P2 | Reduce build times | 4 |
| 8E.4 | **Create deployment checklists** — Automated checklists for releases and deployments | P3 | Prevent forgotten steps | 4 |
| 8E.5 | **Document workflow triggers and dependencies** — Clear explanation of when each workflow runs | P3 | See `.github/workflows/README.md` updates | 3 |

**Total Estimated Effort for Phase 8:** ~170 hours (~4 weeks for one developer, ~2 weeks for two)

---

## Phase 9: Final Production Release

Complete remaining features, perform comprehensive validation, and prepare for production deployment. This phase represents the final push to v2.0.0 production release.

**Estimated Effort:** 4-6 weeks | **Dependencies:** Phases 0-8 completion

### 9A. Complete Remaining Features (P1 — Required for Production)

Finish implementing stubbed-out functionality.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 9A.1 | **Implement critical stub endpoints** — Symbol management (15), Storage operations (20), Storage quality (9) | P1 | Highest priority stub endpoints (Phase 3B) | 60 |
| 9A.2 | **Complete desktop app placeholders** — Remove "Coming Soon" pages and implement core workflows | P1 | 4 WPF pages are still placeholders (Phase 4A.5, 18 done) | 16 |
| 9A.3 | **Implement remaining data quality features** — Complete all quality monitoring endpoints and UI | P1 | Quality monitoring is key selling point | 40 |
| 9A.4 | **Finish backfill automation** — Complete scheduled backfill and gap-fill features | P1 | Critical for unattended operation | 24 |
| 9A.5 | **Complete provider integrations** — Validate all 10 historical and 5 streaming providers work end-to-end | P1 | Core functionality validation | 32 |

### 9B. Production Validation (P1 — Cannot Ship Without)

Comprehensive testing and validation for production readiness.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 9B.1 | **Achieve 80% test coverage** — Write tests for critical paths in all projects | P1 | Current: ~17% coverage (Phase 1, Test Coverage Summary) | 120 |
| 9B.2 | **End-to-end integration tests** — Validate complete workflows from subscription to storage to export | P1 | Ensure system works as a whole | 40 |
| 9B.3 | **Performance testing and benchmarking** — Validate system meets performance requirements under load | P1 | Establish baselines and identify bottlenecks | 32 |
| 9B.4 | **Security audit** — Review authentication, authorization, secret handling, and data protection | P1 | Cannot ship with security vulnerabilities | 24 |
| 9B.5 | **Load testing** — Validate system handles target throughput (1M+ events/second) | P1 | Identify scalability limits | 24 |
| 9B.6 | **Disaster recovery testing** — Validate backup, restore, and failure recovery procedures | P2 | Ensure data durability guarantees | 16 |
| 9B.7 | **Documentation review** — Ensure all docs are accurate, complete, and up-to-date | P1 | Documentation is part of the product | 24 |

### 9C. Production Deployment Preparation (P1 — Operational Readiness)

Prepare systems and processes for production operation.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 9C.1 | **Create deployment runbooks** — Step-by-step procedures for all deployment scenarios | P1 | Operators need clear instructions | 16 |
| 9C.2 | **Setup monitoring and alerting** — Configure Prometheus, Grafana, and alert routing for production | P1 | Must monitor production health | 24 |
| 9C.3 | **Create incident response procedures** — Playbooks for common failures and escalation paths | P1 | Minimize downtime when issues occur | 16 |
| 9C.4 | **Prepare backup and disaster recovery** — Automated backup procedures and recovery testing | P1 | Protect against data loss | 24 |
| 9C.5 | **Setup log aggregation** — Centralized logging for all components | P1 | Essential for troubleshooting | 16 |
| 9C.6 | **Create capacity planning guide** — Resource requirements for different usage levels | P2 | Help users plan infrastructure | 12 |
| 9C.7 | **Prepare upgrade procedures** — How to upgrade from v1.x to v2.0.0 | P2 | Support existing users | 12 |

### 9D. Release Engineering (P1 — Ship It!)

Package and release the production version.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 9D.1 | **Complete changelog for v2.0.0** — Comprehensive list of changes, fixes, and new features | P1 | Users need to know what changed | 8 |
| 9D.2 | **Create release notes** — User-friendly summary of v2.0.0 highlights and upgrade guide | P1 | Marketing and communication | 8 |
| 9D.3 | **Build release artifacts** — Docker images, desktop installers, source archives | P1 | Deliver software to users | 8 |
| 9D.4 | **Create getting started guide** — Quick start for new users (15-minute setup) | P1 | First impression matters | 12 |
| 9D.5 | **Prepare demo videos and screenshots** — Visual demonstrations of key features | P2 | Help users understand capabilities | 16 |
| 9D.6 | **Create migration guide** — Detailed guide for upgrading from v1.x | P2 | Support existing deployments | 12 |
| 9D.7 | **Setup release automation** — Automated builds, testing, and publishing | P2 | Streamline future releases | 16 |

### 9E. Post-Release Support Preparation (P2 — Sustainability)

Prepare for ongoing maintenance and support.

| # | Item | Priority | Description | Est. Hours |
|---|------|----------|-------------|-----------|
| 9E.1 | **Create support documentation** — FAQs, common issues, contact information | P1 | Reduce support burden | 12 |
| 9E.2 | **Setup issue triage workflow** — Labels, templates, and triage procedures for GitHub issues | P2 | Organize community feedback | 8 |
| 9E.3 | **Create roadmap for v2.1.0** — Plan for post-release enhancements and fixes | P2 | Communicate future direction | 8 |
| 9E.4 | **Document technical debt** — Known issues and areas for future improvement | P2 | Track what we couldn't fix in v2.0.0 | 8 |
| 9E.5 | **Create contributor onboarding guide** — How new contributors can get started | P2 | Build community | 12 |
| 9E.6 | **Setup community channels** — Discord/Slack/Discussions for user community | P3 | Foster user engagement | 8 |

**Total Estimated Effort for Phase 9:** ~808 hours (~20 weeks for one developer, ~10 weeks for two, ~5 weeks for four)

---

## Execution Timeline & Dependencies

### Phase Dependencies (Critical Path)

```
Phase 0 (DONE) ──┬─→ Phase 1 (DONE) ──┬─→ Phase 2 (DONE) ──┬─→ Phase 3 (DONE) ──┬─→ Phase 8 ──→ Phase 9
                 │                      │                    │                    │
                 ├─→ Phase 5 (DONE) ────┘                    │                    │
                 │                                            │                    │
                 └─→ Phase 6 ─────────────────────────────┬──┘                    │
                                                           │                       │
                                                           └─→ Phase 4 ────────────┘
                                                                    │
                                                                    └─→ Phase 7 (Optional)
```

### Recommended Execution Order

1. **Immediate (Critical)** — Complete any remaining Phase 6 items (duplicate code cleanup)
2. **Next Sprint (2-3 weeks)** — Execute Phase 8 (Repository Organization)
3. **Major Release (4-6 weeks)** — Execute Phase 9 (Final Production Release)
4. **Ongoing** — Phase 7 (Extended Capabilities) as post-release enhancements

### Effort Summary

| Phase | Status | Estimated Effort | Timeline |
|-------|--------|------------------|----------|
| Phase 0 | ✅ COMPLETED | — | Completed |
| Phase 1 | ✅ COMPLETED | — | Completed |
| Phase 2 | ✅ COMPLETED | — | Completed |
| Phase 3 | ✅ COMPLETED | — | Completed |
| Phase 4 | ⚠️ PARTIAL | ~20 hours remaining | <1 week |
| Phase 5 | ✅ COMPLETED | — | Completed |
| Phase 6 | ⚠️ PARTIAL | ~100 hours remaining | 2-3 weeks |
| Phase 7 | ⏸️ OPTIONAL | ~200+ hours | Post-release |
| Phase 8 | 🆕 NEW | ~170 hours | 2-4 weeks |
| Phase 9 | 🆕 NEW | ~808 hours | 4-10 weeks |
| **Total Remaining** | — | **~1,158 hours** | **10-19 weeks** |

**Note:** Timeline assumes 40 hours/week. With multiple developers working in parallel, timeline can be reduced by 40-60%.

### Minimum Viable Production Release (MVP)

If time is constrained, this minimum scope achieves production readiness:

| Phase | Required Items | Effort |
|-------|----------------|--------|
| Phase 4 | Complete remaining 4 WPF placeholder pages (18 of 22 already done) | 12 hours |
| Phase 6 | Complete service interface consolidation only (Phase 6B) | 20 hours |
| Phase 8 | Items 8A.1, 8A.2, 8B.1, 8D.1, 8D.2 (critical docs only) | 30 hours |
| Phase 9 | Items 9A.1, 9B.1-9B.5, 9C.1-9C.5, 9D.1-9D.4 (core production readiness) | 450 hours |
| **Total MVP** | — | **~540 hours (~13-14 weeks for one dev)** |

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
| `docs/status/IMPROVEMENTS.md` | **Consolidated improvement tracker** (33 items: 14 done, 4 partial, 15 open) |
| `docs/architecture/overview.md` | System architecture |
| `docs/development/uwp-to-wpf-migration.md` | WPF migration status |
| `docs/ai/ai-known-errors.md` | Recurring AI agent error patterns |
| `docs/archived/DUPLICATE_CODE_ANALYSIS.md` | Duplicate code analysis with implementation progress |
| `docs/archived/IMPROVEMENTS_2026-02.md` | Archived functional improvements (historical) |
| `docs/archived/STRUCTURAL_IMPROVEMENTS_2026-02.md` | Archived structural improvements (historical) |
| `docs/audits/CLEANUP_OPPORTUNITIES.md` | WPF-only cleanup plan with file-level detail |
| `docs/audits/CLEANUP_SUMMARY.md` | Repository hygiene cleanup results |

---

## Notes

- **Phase 0-7** represent foundational work, with Phases 0-5 now complete
- **Phase 8** focuses on repository organization, documentation, and developer experience
- **Phase 9** represents the final push to v2.0.0 production release
- Phases are ordered by dependency and risk. Cross-phase dependencies are shown in the Execution Timeline
- Within each phase, items are ordered by priority (P1 > P2 > P3)
- Provider implementations are functionally complete — the conditional compilation pattern (`#if IBAPI`, `#if STOCKSHARP`) is intentional to avoid hard dependencies on commercial SDKs
- The 178 stub endpoints are intentional placeholders that return 501 to prevent confusing 404 errors for declared routes
- Test coverage (~17% by file count) is the largest gap in the project's production readiness
- Phase 6 (Duplicate & Unused Code Cleanup) is informed by three prior audits. Many quick-win items have been completed
- Phase 8 (Repository Organization) addresses long-standing organizational debt and establishes patterns for sustainable development
- Phase 9 (Final Production Release) represents estimated ~808 hours of work but can be reduced to ~540 hours for minimum viable production release
- **Total remaining effort to v2.0.0:** ~1,158 hours (19 weeks for one developer, 10 weeks for two)
- **MVP path to production:** ~540 hours (13-14 weeks for one developer, 6-7 weeks for two)

---

## Success Metrics

To track progress toward v2.0.0 production release:

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| **Test Coverage** | ~17% | >80% | 🔴 Critical Gap |
| **Implemented Endpoints** | 136/269 (51%) | >230/269 (85%) | 🟡 In Progress |
| **Functional Desktop Pages** | 44/48 (92%) | 46/48 (96%) | 🟡 In Progress |
| **Provider Test Coverage** | 8/55 files | >40/55 files | 🔴 Critical Gap |
| **Documentation Completeness** | ~75% | >95% | 🟡 In Progress |
| **Production Readiness Score** | 6.5/10 | 9/10 | 🟡 In Progress |
| **Technical Debt Items** | ~45 known | <15 acceptable | 🟡 In Progress |

---

*Last Updated: 2026-02-13*
