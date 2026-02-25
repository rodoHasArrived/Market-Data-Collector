# Market Data Collector - Improvement Tracking

**Version:** 1.6.2
**Last Updated:** 2026-02-25
**Status:** Active tracking document

This document consolidates **functional improvements** (features, reliability, UX) and **structural improvements** (architecture, modularity, code quality) into a single source of truth for tracking. For phased execution timeline, see [`ROADMAP.md`](ROADMAP.md).

---

## Table of Contents

- [Overview](#overview)
- [Theme A: Reliability & Resilience](#theme-a-reliability--resilience)
- [Theme B: Testing & Quality](#theme-b-testing--quality)
- [Theme C: Architecture & Modularity](#theme-c-architecture--modularity)
- [Theme D: API & Integration](#theme-d-api--integration)
- [Theme E: Performance & Scalability](#theme-e-performance--scalability)
- [Theme F: User Experience](#theme-f-user-experience)
- [Theme G: Operations & Monitoring](#theme-g-operations--monitoring)
- [Priority Matrix](#priority-matrix)
- [Execution Strategy](#execution-strategy)
- [Delivery Operating Model](#delivery-operating-model)
- [Dependency Map](#dependency-map)
- [Definition of Done Checklist](#definition-of-done-checklist)
- [Review Cadence & Reporting](#review-cadence--reporting)

---

## Overview

### Progress Summary

| Status | Count | Items |
|--------|-------|-------|
| ✅ **Completed** | 33 | A1, A2, A3, A4, A5, A6, A7, B1, B2, B3, B4, B5, C1, C2, C4, C5, C6, C7, D1, D2, D3, D4, D5, D6, D7, E1, E2, E3, F1, F2, F3, G1, G3 |
| 🔄 **Partially Complete** | 1 | G2 |
| 📝 **Open** | 1 | C3 |
| **Total** | 35 | All improvement items (core) |

### By Theme

| Theme | Completed | Partial | Open | Total |
|-------|-----------|---------|------|-------|
| A: Reliability & Resilience | 7 | 0 | 0 | 7 |
| B: Testing & Quality | 5 | 0 | 0 | 5 |
| C: Architecture & Modularity | 6 | 0 | 1 | 7 |
| D: API & Integration | 7 | 0 | 0 | 7 |
| E: Performance & Scalability | 3 | 0 | 0 | 3 |
| F: User Experience | 3 | 0 | 0 | 3 |
| G: Operations & Monitoring | 2 | 1 | 0 | 3 |
| J: Data Canonicalization | 7 | 1 | 0 | 8 |

### Portfolio Health Snapshot

- **Completion ratio:** 94.3% complete (33/35), 2.9% partial (1/35), 2.9% open (1/35).
- **Remaining open item:** C3 (WebSocket Provider Base Class Adoption) — refactor to reduce ~200-300 LOC duplication across WebSocket providers.
- **Remaining partial item:** G2 (OpenTelemetry trace context propagation) — framework complete, explicit cross-boundary propagation pending.
- **Recommended focus:** C3 refactor (1 week), G2 trace propagation (1 week), then shift to Theme H/I new capabilities.

### Next Sprint Backlog (Recommended)

| Sprint | Primary Goals | Exit Criteria | Status |
|--------|---------------|---------------|--------|
| 1 | C4, C5 | `EventPipeline` no longer depends on static metrics; config validation pipeline in place | ✅ Done |
| 2 | D4, B1 remainder | `/api/quality/drops` and `/api/quality/drops/{symbol}` are live and documented | ✅ Done |
| 3 | C6, A7 | Multi-sink fan-out merged; error handling convention documented and enforced in startup path | ✅ Done |
| 4 | B3 tranche 1, G2 partial, D7 partial | Provider tests for Polygon + StockSharp; OTel pipeline metrics; typed OpenAPI annotations | ✅ Done |
| 5 | B2 tranche 1, D7 remainder | Negative-path + schema validation tests; typed annotations across all endpoint families | ✅ Done |
| 6 | C1/C2, H1, H4, I1 | Provider registration unified under DI; per-provider backfill rate limiting; degradation scoring; test harness | ✅ Done |
| 7 | H2, B3 tranche 2 | Multi-instance coordination; IB + Alpaca provider tests | 🔄 Partial (B3 tranche 2 done; H2 pending) |
| 8 | H3, G2 remainder | Event replay infrastructure; full trace propagation | 🔄 Partial (H3 done; G2 trace propagation pending) |

---

## Theme A: Reliability & Resilience

### A1. ✅ WebSocket Automatic Resubscription (COMPLETED)

**Impact:** Critical | **Effort:** Low | **Priority:** P0 | **Status:** ✅ DONE

**Problem:** WebSocket providers lost all subscriptions on reconnect, requiring manual intervention.

**Solution Implemented:**
- `SubscriptionManager` tracks subscriptions by kind with `GetSymbolsByKind()` for recovery
- `AlpacaMarketDataClient.OnConnectionLostAsync()` passes `onReconnected` callback for re-auth + resubscribe
- `PolygonMarketDataClient.ResubscribeAllAsync()` replays trades, quotes, aggregates after reconnect
- All providers log successful resubscription events

**Files:**
- `Infrastructure/Resilience/WebSocketConnectionManager.cs`
- `Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs`
- `Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs`
- `Infrastructure/Providers/Shared/SubscriptionManager.cs`

**ROADMAP:** Phase 0 (Critical Fixes)

---

### A2. ✅ Storage Sink Disposal Race Condition Fix (COMPLETED)

**Impact:** High | **Effort:** Low | **Priority:** P0 | **Status:** ✅ DONE

**Problem:** `JsonlStorageSink` and `ParquetStorageSink` could lose data during shutdown if flush timer fired during disposal.

**Solution Implemented:**
- Cancel disposal token first to stop new writes
- Dispose flush timer (waiting for pending callbacks)
- Execute guaranteed final flush under semaphore gate
- Then dispose writers and remaining resources

**Files:**
- `Storage/Sinks/JsonlStorageSink.cs`
- `Storage/Sinks/ParquetStorageSink.cs`

**Remaining Work (Low Priority):** Extract shared buffering/flushing logic into `BufferedSinkBase` to prevent future divergence.

**ROADMAP:** Phase 0 (Critical Fixes)

---

### A3. ✅ Backfill Rate Limit Exponential Backoff (COMPLETED)

**Impact:** High | **Effort:** Low | **Priority:** P0 | **Status:** ✅ DONE

**Problem:** Backfill workers retry rate-limited requests without proper backoff, wasting time and API quota.

**Solution Implemented:**
- Exponential backoff (2s base, 60s cap) with jitter in `BackfillWorkerService`
- Retry budget enforced at 3 attempts per request
- `RateLimitException` includes `RetryAfter` property for provider-specified cooldown periods
- `Retry-After` response header parsing implemented in `ProviderHttpUtilities` and `SharedResiliencePolicies`
- `ProviderRateLimitTracker` tracks per-provider rate limit state with sliding window `RateLimiter`
- Providers honor `Retry-After` values from HTTP 429 responses

**Files:**
- `Infrastructure/Providers/Historical/Queue/BackfillWorkerService.cs`
- `Core/Exceptions/RateLimitException.cs`
- `ProviderSdk/ProviderHttpUtilities.cs`
- `Infrastructure/Http/SharedResiliencePolicies.cs`
- `Infrastructure/Providers/Core/ProviderRateLimitTracker.cs`

**ROADMAP:** Phase 1 (Core Stability)

---

### A4. ✅ Subscription Memory Leak Fix (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P0 | **Status:** ✅ DONE

**Problem:** `SubscriptionManager` never removed entries from internal dictionaries, causing memory leak over time.

**Solution Implemented:**
- `Unsubscribe()` and `UnsubscribeSymbol()` properly remove entries
- Added `Count` property for monitoring active subscriptions
- All lifecycle events logged at Debug level

**Files:**
- `Infrastructure/Providers/Shared/SubscriptionManager.cs`

**ROADMAP:** Phase 0 (Critical Fixes)

---

### A5. ✅ Provider Factory with Runtime Switching (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Provider selection was hardcoded in `Program.cs` with switch statement, no runtime switching.

**Solution Implemented:**
- `IMarketDataClientFactory` and `MarketDataClientFactory` replace switch statement
- Supports IB, Alpaca, Polygon, StockSharp, NYSE providers
- Runtime provider switching via `/api/config/data-source` POST endpoint
- Failover chain creates client instances dynamically from factory

**Files:**
- `Infrastructure/Providers/MarketDataClientFactory.cs`
- `Program.cs`
- `Ui.Shared/Endpoints/ConfigEndpoints.cs`

**ROADMAP:** Phase 2 (Architecture)

---

### A6. ✅ Write-Ahead Log Recovery Hardening (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** WAL recovery could fail or hang on large uncommitted files.

**Solution Implemented:**
- `GetUncommittedRecordsAsync()` uses `IAsyncEnumerable<WalRecord>` with streaming reads
- Processes records in batches of 10,000
- Configurable `UncommittedSizeWarningThreshold` (default 50MB) logs warnings
- Full SHA256 checksums replace previous truncated 8-byte variant

**Files:**
- `Storage/Archival/WriteAheadLog.cs`

**ROADMAP:** Phase 5 (Operational Readiness)

---

### A7. ✅ Standardize Error Handling Strategy (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** Codebase uses three concurrent error handling approaches inconsistently:
1. **Exceptions** - 9 custom exception types in `Core/Exceptions/`
2. **Result<T, TError>** - functional result type in `Application/Results/Result.cs`
3. **Hard-coded `return 1`** - all error paths in `Program.cs` returned exit code 1 regardless of error category

**Solution Implemented:**
- Added `ErrorCodeExtensions.FromException(Exception)` method that maps domain exceptions and standard .NET exceptions to the correct `ErrorCode` enum value
- Replaced all hard-coded `return 1` in `Program.cs` with category-accurate exit codes via `ErrorCode.ToExitCode()`:
  - Configuration errors → exit code 3 (via `ErrorCode.ConfigurationInvalid`)
  - File permission errors → exit code 7 (via `ErrorCode.FileAccessDenied`)
  - Schema validation errors → exit code 6 (via `ErrorCode.SchemaMismatch`)
  - Backfill failures → exit code 5 (via `ErrorCode.ProviderError`)
  - Connection failures → exit code 4 (via `ErrorCode.ConnectionFailed`)
  - Fatal catch-all → dynamically mapped from exception type
- Connection failure handler now returns error code instead of re-throwing
- All error log messages include `ErrorCode` and `ExitCode` for diagnostics
- **NEW**: 22 comprehensive tests covering exception-to-ErrorCode mapping, exit code ranges, category names, and transient error identification

**Files:**
- `Application/Results/ErrorCode.cs` (`FromException` method added)
- `Program.cs` (6 exit code locations updated)
- `tests/.../Application/Services/ErrorCodeMappingTests.cs` (22 tests)

**Benefit:** Process exit codes now reflect the actual error category, enabling operators and CI/CD to distinguish configuration errors (3), connection failures (4), provider errors (5), schema issues (6), and storage problems (7) from generic failures.

**ROADMAP:** Phase 2 (Architecture)

---

## Theme B: Testing & Quality

### B1. ✅ Dropped Event Audit Trail (COMPLETED)

**Impact:** Medium-High | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Events dropped due to backpressure were not tracked, making data quality assessment impossible.

**Solution Implemented:**
- `DroppedEventAuditTrail` logs dropped events to `_audit/dropped_events.jsonl`
- JSONL format with timestamp, event type, symbol, sequence, source, drop reason
- Integrated with `EventPipeline`
- Tracks drop counts per symbol via `ConcurrentDictionary`
- **NEW**: `/api/quality/drops` HTTP endpoint exposing `DroppedEventStatistics`
- **NEW**: `/api/quality/drops/{symbol}` for per-symbol drill-down
- **NEW**: 10 comprehensive integration tests covering all scenarios

**Files:**
- `Application/Pipeline/DroppedEventAuditTrail.cs`
- `Application/Pipeline/EventPipeline.cs`
- `Ui.Shared/Endpoints/QualityDropsEndpoints.cs` (implemented)
- `tests/.../EndpointTests/QualityDropsEndpointTests.cs` (10 tests)

**ROADMAP:** Phase 3 (API Completeness)

---

### B2. ✅ HTTP Endpoint Integration Tests (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** The HTTP API layer (136 implemented endpoints) had no integration tests using `WebApplicationFactory<T>`. Only `EndpointStubDetectionTests.cs` validated route format.

**Solution Implemented:**
- `EndpointTestFixture` base class with shared `WebApplicationFactory<T>` setup
- 16 endpoint test files covering core API surface
- Tests assert status codes, content types, response schema shapes
- Negative cases (invalid input, missing config, auth failures) included
- Coverage spans status, health, config, backfill, providers, quality, SLA, maintenance, packaging, and more
- **Sprint 5 additions:**
  - `NegativePathEndpointTests.cs` — 40+ tests for negative-path and edge-case behavior across all endpoint families (404s, invalid POST bodies, path traversal rejection, reversed date ranges, symbol count limits, non-existent providers, method-not-allowed)
  - `ResponseSchemaValidationTests.cs` — 15+ tests validating JSON response schemas for core endpoints (field presence, types, structural contracts for /api/status, /api/health, /api/health/summary, /api/config, /api/config/data-sources, /api/providers/comparison, /api/backpressure)

**Files:**
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/EndpointTestFixture.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/StatusEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/HealthEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/ConfigEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/BackfillEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/ProviderEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/QualityEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/QualityDropsEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/SlaEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/MaintenanceEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/PackagingEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/FailoverEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/SymbolEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/SubscriptionEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/LiveDataEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/DiagnosticsEndpointTests.cs`
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/NegativePathEndpointTests.cs` (new, Sprint 5)
- `tests/MarketDataCollector.Tests/Integration/EndpointTests/ResponseSchemaValidationTests.cs` (new, Sprint 5)

**ROADMAP:** Phase 1 (Core Stability) - Item 1A

---

### B3. ✅ Infrastructure Provider Unit Tests (COMPLETED)

**Impact:** High | **Effort:** High | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** 55 provider implementation files but only 8 test files. Major streaming providers had no dedicated unit tests.

**Solution Implemented:**
- 12 provider test files now covering all core providers:
  - Polygon: subscription, message parsing, main client logic
  - StockSharp: subscription, message conversion
  - Alpaca: credential validation, reconnect behavior, quote routing
  - IB: simulation client tests (15 tests)
  - NYSE: message parsing
  - Failover: client switching, health check, service coordination
  - Backfill: retry-after header handling

**Files:**
- `tests/.../Infrastructure/Providers/AlpacaCredentialAndReconnectTests.cs`
- `tests/.../Infrastructure/Providers/AlpacaQuoteRoutingTests.cs`
- `tests/.../Infrastructure/Providers/BackfillRetryAfterTests.cs`
- `tests/.../Infrastructure/Providers/FailoverAwareMarketDataClientTests.cs`
- `tests/.../Infrastructure/Providers/IBSimulationClientTests.cs`
- `tests/.../Infrastructure/Providers/NYSEMessageParsingTests.cs`
- `tests/.../Infrastructure/Providers/PolygonMarketDataClientTests.cs`
- `tests/.../Infrastructure/Providers/PolygonMessageParsingTests.cs`
- `tests/.../Infrastructure/Providers/PolygonSubscriptionTests.cs`
- `tests/.../Infrastructure/Providers/StockSharpMessageConversionTests.cs`
- `tests/.../Infrastructure/Providers/StockSharpSubscriptionTests.cs`
- `tests/.../Infrastructure/Providers/StreamingFailoverServiceTests.cs`

**ROADMAP:** Sprint 4 (tranche 1), Sprint 7 (tranche 2) — both complete

---

### B4. ✅ Application Service Tests (COMPLETED)

**Impact:** Medium-High | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** 19 application services had zero test coverage, including critical ones like `TradingCalendar` and data quality services.

**Solution Implemented:**
- 13 core application service test files covering critical paths:
  - `TradingCalendar` — 50+ tests (market hours, holidays, half-days, pre/post sessions)
  - Data quality services — 62 tests across 4 services (GapAnalyzer, AnomalyDetector, CompletenessScoreCalculator, SequenceErrorTracker)
  - `ConfigurationService` — config loading, validation, self-healing
  - `CliModeResolver` — CLI mode detection
  - `CronExpressionParser` — cron expression parsing
  - `ErrorCodeMapping` — error code resolution
  - `GracefulShutdown` — shutdown coordination
  - `OperationalScheduler` — backfill scheduling
  - `OptionsChainService` — options data handling
  - `PreflightChecker` — startup validation
- Additional 293 UI service tests and 142 WPF desktop service tests

**Files:**
- `tests/.../Application/Services/TradingCalendarTests.cs`
- `tests/.../Application/Services/DataQuality/GapAnalyzerTests.cs`
- `tests/.../Application/Services/DataQuality/AnomalyDetectorTests.cs`
- `tests/.../Application/Services/DataQuality/CompletenessScoreCalculatorTests.cs`
- `tests/.../Application/Services/DataQuality/SequenceErrorTrackerTests.cs`
- `tests/.../Application/Services/ConfigurationServiceTests.cs`
- `tests/.../Application/Services/CliModeResolverTests.cs`
- `tests/.../Application/Services/ErrorCodeMappingTests.cs`
- `tests/.../Application/Services/GracefulShutdownTests.cs`
- `tests/.../Application/Services/OperationalSchedulerTests.cs`
- `tests/.../Application/Services/OptionsChainServiceTests.cs`
- `tests/.../Application/Services/PreflightCheckerTests.cs`
- `tests/.../Application/Services/CronExpressionParserTests.cs`

**ROADMAP:** Phase 1 (Core Stability) - Item 1C

---

### B5. ✅ Provider SDK Tests (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** Provider SDK classes (`DataSourceRegistry`, `CredentialValidator`) used by all providers had minimal test coverage.

**Solution Implemented:**
- `DataSourceRegistryTests` (14 tests): assembly discovery, deduplication, metadata validation, service registration
- `CredentialValidatorTests` (16 tests): API key validation, key-secret pairs, throw helpers, env var retrieval
- `ExceptionTypeTests` (24 tests): all 8 custom exception types tested for properties, hierarchy, sealed checks
- `DataSourceAttributeTests` (14 tests): attribute construction, metadata mapping, IsRealtime/IsHistorical properties

**Files:**
- `tests/MarketDataCollector.Tests/ProviderSdk/DataSourceRegistryTests.cs` (14 tests)
- `tests/MarketDataCollector.Tests/ProviderSdk/CredentialValidatorTests.cs` (16 tests)
- `tests/MarketDataCollector.Tests/ProviderSdk/ExceptionTypeTests.cs` (24 tests)
- `tests/MarketDataCollector.Tests/ProviderSdk/DataSourceAttributeTests.cs` (14 tests)

**ROADMAP:** Phase 1 (Core Stability) - Item 1D

---

## Theme C: Architecture & Modularity

### C1. ✅ Unified Provider Registry (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Three separate provider creation mechanisms existed and competed:
1. `MarketDataClientFactory` - switch-based factory
2. `ProviderFactory` - parallel factory system
3. Direct instantiation in `Program.cs`

**Solution Implemented:**
- `ProviderFactory` is the single creation mechanism for backfill and symbol search providers
- `ProviderRegistry` is the unified entry point used by `Program.cs` via DI (`hostStartup.GetRequiredService<ProviderRegistry>()`)
- `[DataSource]` attribute scanning wired into startup — all 5 streaming providers use `[DataSource]` attributes
- `ServiceCompositionRoot` orchestrates all registrations centrally (lines 69-133)
- `Program.cs` resolves providers exclusively through DI; no direct `new` instantiation of providers

**Files:**
- `Infrastructure/Providers/Core/ProviderFactory.cs` (unified creation)
- `Infrastructure/Providers/Core/ProviderRegistry.cs` (single entry point)
- `Application/Composition/ServiceCompositionRoot.cs` (centralized DI registration)
- `Program.cs` (DI-only resolution)
- `ProviderSdk/DataSourceAttribute.cs`
- `ProviderSdk/DataSourceRegistry.cs`

**Benefit:** Adding new provider becomes: implement interface, add attribute, done.

**ROADMAP:** Phase 2 (Architecture) - Item 2A

---

### C2. ✅ Single DI Composition Path (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** `ServiceCompositionRoot.cs` registered services in DI, but `Program.cs` bypassed DI for critical components — collectors created via `new`, storage pipeline created via `new`, configuration loaded twice.

**Solution Implemented:**
- All collectors resolved from DI: `hostStartup.GetRequiredService<QuoteCollector>()`, `GetRequiredService<TradeDataCollector>()`, `GetRequiredService<MarketDepthCollector>()`
- Storage pipeline resolved from DI via `hostStartup.Pipeline`
- `ServiceCompositionRoot` registers everything centrally:
  - `AddCoreConfigurationServices()` — ConfigStore, ConfigurationService
  - `AddStorageServices()` — Storage sinks, file services
  - `AddProviderServices()` — ProviderRegistry, ProviderFactory
  - `AddBackfillServices()` — Backfill coordinator, scheduling
  - `AddPipelineServices()` — EventPipeline, Publisher
  - `AddCollectorServices()` — All collectors
- No orphaned registrations — all registered services are actively consumed

**Files:**
- `Program.cs` (DI-only resolution throughout)
- `Application/Composition/ServiceCompositionRoot.cs` (centralized registration)
- `Application/Composition/HostStartup.cs` (composition host)

**Benefit:** One composition path. DI registrations are source of truth. Testable via service replacement.

**ROADMAP:** Phase 2 (Architecture) - Item 2B

---

### C3. 📝 WebSocket Provider Base Class Adoption (OPEN)

**Impact:** High | **Effort:** High | **Priority:** P2 | **Status:** 📝 OPEN

**Problem:** `WebSocketProviderBase` exists with connection lifecycle, heartbeat, resilience, reconnection logic. However, none of the major WebSocket providers use it:
- Polygon (1,263 lines) manages `ClientWebSocket` directly
- NYSE manages `ClientWebSocket` directly
- StockSharp (1,325 lines) has custom task-based reconnection
- Only Alpaca uses separate `WebSocketConnectionManager` helper

~800 lines of WebSocket management are duplicated across providers.

**Proposed Solution:**
- Refactor Polygon, NYSE, StockSharp to extend `WebSocketProviderBase`
- Override `ConnectionUri`, `ProviderName`, and message handling hooks
- Move reconnection, heartbeat, receive loop into base class
- Keep provider-specific auth and message parsing in subclasses

**Files:**
- `Infrastructure/Shared/WebSocketProviderBase.cs`
- `Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs`
- `Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs`
- `Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs`

**Benefit:** Eliminates ~800 lines duplicated connection management. Bug fixes apply everywhere.

**ROADMAP:** Phase 2 (Architecture) - Item 2C

---

### C4. ✅ Injectable Metrics Interface (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** `EventPipeline` called `Metrics.IncPublished()` and `Metrics.IncDropped()` via static methods. This prevented substitution in tests and coupled pipeline to specific metrics backend.

**Solution Implemented:**
- Extracted `IEventMetrics` interface: `IncPublished()`, `IncDropped()`, `IncConsumed()`, `IncRecovered()`, etc.
- Injected `IEventMetrics` into `EventPipeline` via constructor parameter
- `DefaultEventMetrics` implementation delegates to existing `Metrics`/`PrometheusMetrics` static classes
- ServiceCompositionRoot registers `IEventMetrics` as singleton
- BackfillCoordinator now accepts and passes IEventMetrics to EventPipeline
- **NEW**: 7 comprehensive tests for injectable metrics behavior

**Files:**
- `Application/Monitoring/IEventMetrics.cs` (interface + DefaultEventMetrics)
- `Application/Pipeline/EventPipeline.cs:98` (accepts metrics parameter)
- `Application/Http/BackfillCoordinator.cs:42, 155` (injection)
- `Application/Composition/ServiceCompositionRoot.cs` (DI registration)
- `tests/.../Pipeline/EventPipelineMetricsTests.cs` (7 tests)

**Benefit:** Pipeline testable without side effects. Opens door to alternative metrics backends.

**ROADMAP:** Phase 2 (Architecture) - Item 2D

---

### C5. ✅ Consolidated Configuration Validation (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Configuration validation was spread across three classes with overlapping responsibilities:
1. `ConfigValidationHelper` - field-level validation
2. `ConfigValidatorCli` - CLI-oriented validation with output formatting
3. `PreflightChecker` - pre-startup validation including connectivity

No clear contract for what each validates or when it runs.

**Solution Implemented:**
- Defined `IConfigValidator` with `Validate(AppConfig) -> ConfigValidationResult[]`
- Implemented `ConfigValidationPipeline` with composable stages: FieldValidationStage → SemanticValidationStage
- ConfigurationPipeline migrated to use ConfigValidationPipeline
- ConfigurationService.ValidateConfig migrated to use ConfigValidationPipeline
- ConfigValidationHelper methods marked obsolete with migration guidance
- **NEW**: 11 comprehensive tests for validation pipeline including error cases, warnings, and edge conditions

**Files:**
- `Application/Config/IConfigValidator.cs` (interface + pipeline + stages)
- `Application/Config/ConfigurationPipeline.cs:224-231` (migrated)
- `Application/Services/ConfigurationService.cs:327-346` (migrated)
- `Application/Config/ConfigValidationHelper.cs` (marked obsolete)
- `tests/.../Config/ConfigValidationPipelineTests.cs` (11 tests)

**Benefit:** Single validation pipeline. Clear ordering. Easy to add new rules. Better testability.

**ROADMAP:** Phase 2 (Architecture) - Item 2E

---

### C6. ✅ Composite Storage Sink Plugin Architecture (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** `EventPipeline` accepts single `IStorageSink`. Multi-sink scenarios (JSONL + Parquet simultaneously, or JSONL + analytics sink) require external composition. No built-in fan-out.

**Solution Implemented:**
- `CompositeSink : IStorageSink` wraps `IReadOnlyList<IStorageSink>` with per-sink fault isolation
- `AppendAsync` fans out to all sinks; individual sink failures are logged but don't block other sinks
- `FlushAsync` collects exceptions and throws `AggregateException` for visibility
- `DisposeAsync` gracefully disposes all sinks, logging per-sink failures
- `ServiceCompositionRoot` conditionally creates `CompositeSink` when `EnableParquetSink` is enabled
- Default mode uses single `JsonlStorageSink`; Parquet mode creates `CompositeSink` wrapping both
- **8 comprehensive tests** covering fan-out, fault isolation, flush aggregation, disposal, and constructor guards

**Files:**
- `Storage/Sinks/CompositeSink.cs` (87 lines)
- `Application/Composition/ServiceCompositionRoot.cs:636-666` (conditional composition)
- `tests/.../Storage/CompositeSinkTests.cs` (8 tests)

**Benefit:** Multi-format storage without pipeline changes. New sinks (CSV, database, cloud) can be added independently. Per-sink fault isolation prevents one failing sink from blocking others.

**ROADMAP:** Phase 2 (Architecture) - Item 2F

---

### C7. ✅ WPF/UWP Service Deduplication (COMPLETED)

**Impact:** High | **Effort:** High | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** 25-30 services were nearly identical between WPF and UWP desktop projects.

**Solution Implemented:**
- UWP project (`src/MarketDataCollector.Uwp/`) fully removed from the codebase
- WPF is the sole desktop client; no duplicate services remain
- Shared service interfaces and base classes consolidated in `MarketDataCollector.Ui.Services`
- Platform-specific adapters exist only in `MarketDataCollector.Wpf/Services/`

**Files:**
- `MarketDataCollector.Wpf/Services/*.cs` (51 service files)
- `MarketDataCollector.Ui.Services/` (shared base classes and interfaces)

**Benefit:** UWP removal eliminated all service duplication. Single desktop platform simplifies maintenance.

**ROADMAP:** Phase 6 (Cleanup) - Phase 6C

---

## Theme D: API & Integration

### D1. ✅ API Route Implementation Gap Closure (COMPLETED - Phase 1)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE (Phase 1)

**Problem:** Many declared routes returned no response or generic error, breaking web dashboard.

**Solution Implemented:**
- All unimplemented routes return `501 Not Implemented` with structured JSON
- `StubEndpoints.cs` registers 180 stub routes with clear messaging
- Core endpoints fully functional: status, config, backfill, failover, providers

**Remaining Work (Phase 2-3):** Implement handler logic for highest-value stub groups.

**Files:**
- `Ui.Shared/Endpoints/StubEndpoints.cs`
- `Contracts/Api/UiApiRoutes.cs`

**ROADMAP:** Phase 3 (API Completeness)

---

### D2. ✅ Real-Time Dashboard Updates via SSE (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Dashboard required manual refresh to see updated metrics.

**Solution Implemented:**
- SSE endpoint at `/api/events/stream` pushes status every 2 seconds
- Includes event throughput, active subscriptions, provider health, backpressure, recent errors
- JavaScript `EventSource` client with automatic fallback to polling
- Reconnects after 10 seconds on connection drop

**Files:**
- `Ui.Shared/Endpoints/StatusEndpoints.cs`
- `Ui.Shared/HtmlTemplates.cs`

**ROADMAP:** Phase 3 (API Completeness)

---

### D3. ✅ Backfill Progress Reporting (COMPLETED)

**Impact:** Medium-High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** No visibility into backfill progress - users couldn't tell if job was stuck or progressing.

**Solution Implemented:**
- `BackfillProgressTracker` tracks per-symbol progress with date ranges
- Calculates percentage complete per symbol and overall
- `BackfillProgressSnapshot` with detailed metrics (completed, failed, errors)
- Exposed via `/api/backfill/progress` endpoint

**Files:**
- `Infrastructure/Providers/Backfill/BackfillProgressTracker.cs`
- `Infrastructure/Providers/Historical/Queue/BackfillWorkerService.cs`
- `Ui.Shared/Endpoints/BackfillEndpoints.cs`

**ROADMAP:** Phase 3 (API Completeness)

---

### D4. ✅ Quality Metrics API Endpoints (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** `DataQualityMonitoringService` computes completeness, gap, anomaly metrics internally but quality endpoints remained stubs. `DroppedEventAuditTrail` had no HTTP exposure.

**Solution Implemented:**
- Implemented `GET /api/quality/drops` returning `DroppedEventStatistics`
- Implemented `GET /api/quality/drops/{symbol}` for per-symbol drill-down
- Endpoints handle case normalization (symbols converted to uppercase)
- Graceful handling when audit trail is not configured
- Wired into UiServer and UiEndpoints
- **NEW**: Expanded from 2 baseline tests to 10 comprehensive integration tests

**Files:**
- `Ui.Shared/Endpoints/QualityDropsEndpoints.cs` (fully implemented)
- `Application/Pipeline/DroppedEventAuditTrail.cs` (provides statistics)
- `tests/.../EndpointTests/QualityDropsEndpointTests.cs` (10 tests)

**Benefit:** Completes observability story. Dashboards can display data quality in real time. Endpoints tested for edge cases (case handling, special characters, empty symbols, missing audit trail).

**ROADMAP:** Phase 3 (API Completeness)

---

### D5. ✅ OpenAPI/Swagger Documentation (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** No API documentation for external integrations or third-party developers.

**Solution Implemented:**
- `Swashbuckle.AspNetCore` and `Microsoft.AspNetCore.OpenApi` integrated
- Swagger UI served at `/swagger` in development mode
- OpenAPI spec at `/swagger/v1/swagger.json`
- `ApiDocumentationService` provides additional documentation generation

**Remaining Work:** Add `[ProducesResponseType]` annotations for complete schema documentation.

**Files:**
- `Ui.Shared/Endpoints/UiEndpoints.cs`
- `MarketDataCollector.Ui.Shared.csproj`

**ROADMAP:** Phase 3 (API Completeness)

---

### D6. ✅ API Authentication and Rate Limiting (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** HTTP endpoints had no authentication, allowing unrestricted access.

**Solution Implemented:**
- `ApiKeyMiddleware` enforces API key via `X-Api-Key` header or `api_key` query param
- Reads from `MDC_API_KEY` environment variable
- Constant-time comparison prevents timing attacks
- `ApiKeyRateLimitMiddleware` enforces 120 req/min per key with sliding window
- Returns `429 Too Many Requests` with `Retry-After` header
- Health endpoints (`/healthz`, `/readyz`, `/livez`) exempt
- If `MDC_API_KEY` not set, all requests allowed (backward compatible)

**Files:**
- `Ui.Shared/Endpoints/ApiKeyMiddleware.cs`
- `Ui.Shared/Endpoints/UiEndpoints.cs`

**ROADMAP:** Phase 5 (Operational Readiness)

---

### D7. ✅ OpenAPI Response Type Annotations (COMPLETED)

**Impact:** Low-Medium | **Effort:** Medium | **Priority:** P3 | **Status:** ✅ DONE

**Problem:** Swagger infrastructure exists but generated OpenAPI spec lacks response type documentation. Shows generic `200 OK` for all endpoints with no schema information.

**Solution Implemented:**
- Added typed `Produces<T>()` annotations to core health and status endpoints (`StatusEndpoints.cs`, `HealthEndpoints.cs`)
- Added `WithDescription()` metadata for endpoint documentation
- Created typed `HealthSummaryResponse` and `HealthSummaryProviders` models in `StatusModels.cs`
- Typed annotations for `HealthCheckResponse`, `StatusResponse` on corresponding endpoints
- **Sprint 5**: Extended typed `Produces<T>()` and `.WithDescription()` annotations across all remaining endpoint families:
  - `BackfillEndpoints.cs` — 5 endpoints annotated with `Produces<BackfillProviderInfo[]>`, `Produces<BackfillResult>`
  - `BackfillScheduleEndpoints.cs` — 15 endpoints annotated with descriptions and typed produces
  - `ConfigEndpoints.cs` — 8 endpoints annotated with descriptions
  - `ProviderEndpoints.cs` — 12 endpoints annotated with `Produces<ProviderComparisonResponse>`, `Produces<ProviderStatusResponse[]>`, `Produces<ProviderMetricsResponse[]>`, `Produces<ProviderCatalogEntry>`
  - `ProviderExtendedEndpoints.cs` — 11 endpoints annotated with descriptions and typed produces
  - `HealthEndpoints.cs` — 7 remaining endpoints annotated with descriptions
  - `StatusEndpoints.cs` — remaining endpoints annotated with `Produces<ErrorsResponseDto>`, `Produces<BackpressureStatusDto>`, `Produces<ProviderLatencySummaryDto>`, `Produces<ConnectionHealthSnapshotDto>`

**Files:**
- `Ui.Shared/Endpoints/StatusEndpoints.cs`
- `Ui.Shared/Endpoints/HealthEndpoints.cs`
- `Ui.Shared/Endpoints/BackfillEndpoints.cs`
- `Ui.Shared/Endpoints/BackfillScheduleEndpoints.cs`
- `Ui.Shared/Endpoints/ConfigEndpoints.cs`
- `Ui.Shared/Endpoints/ProviderEndpoints.cs`
- `Ui.Shared/Endpoints/ProviderExtendedEndpoints.cs`
- `Contracts/Api/StatusModels.cs`
- `Contracts/Api/StatusEndpointModels.cs`

**ROADMAP:** Sprint 4 (core endpoints), Sprint 5 (all endpoint families)

---

## Theme E: Performance & Scalability

### E1. ✅ CLI Argument Parser Extraction (COMPLETED)

**Impact:** Low-Medium | **Effort:** Low | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** Each command in `Application/Commands/` re-implemented argument parsing inline. Pattern like `GetArgValue(args, "--flag")` duplicated across 9 files.

**Solution Implemented:**
- `CliArgumentParser` utility class created
- Methods: `HasFlag(args, flag)`, `GetValue(args, flag)`, `GetValues(args, flag)`, `GetDateValue(args, flag)`
- All `ICliCommand` implementations refactored to use it

**Files:**
- `Application/Commands/CliArgumentParser.cs`
- `Application/Commands/*.cs` (9 files)

**Benefit:** Eliminates parsing duplication. Consistent error messages. Easier to add new commands.

**ROADMAP:** Phase 2 (Architecture)

---

### E2. ✅ UiServer Endpoint Extraction (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** `UiServer.cs` was 3,030 lines with all endpoint logic inline, making it unmaintainable.

**Solution Implemented:**
- Extracted all inline endpoint definitions to 30+ dedicated endpoint modules
- `UiServer.cs` reduced from 3,030 to 191 lines (93.7% reduction)
- Removed all legacy `Configure*Routes()` methods
- Delegates to modules in `Ui.Shared/Endpoints/`

**Files:**
- `Application/Http/UiServer.cs` (3,030 → 191 lines)
- `Ui.Shared/Endpoints/` (30+ modules)

**Benefit:** Maintainable code structure. Clear separation of concerns. Easy to add new endpoints.

**ROADMAP:** Phase 6 (Cleanup) - Phase 6A

---

### E3. ✅ Reduce GC Pressure in Hot Paths (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P3 | **Status:** ✅ DONE

**Problem:** High-frequency message parsing allocated per-message via `JsonDocument.Parse()`, `Encoding.UTF8.GetString()`, `List<T>` construction at ~100 Hz.

**Solution Implemented:**
- StockSharp `MessageConverter` uses `ObjectPool<List<OrderBookLevel>>` with pre-sized lists and try/finally return-to-pool patterns
- Polygon `PolygonMarketDataClient` uses `ArrayPool<byte>.Shared` throughout WebSocket receive loop:
  - Buffers rented before use, returned immediately after in `try/finally` blocks
  - 4KB initial buffer and 65KB large buffer both pool-managed
  - Zero per-message allocation after warmup
  - Proper exception-safe return-to-pool patterns

**Files:**
- `Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` (ArrayPool)
- `Infrastructure/Providers/Streaming/StockSharp/StockSharpMessageConversion.cs` (ObjectPool)

**ROADMAP:** Phase 7 (Extended Capabilities)

---

## Theme F: User Experience

### F1. ✅ Desktop Navigation Consolidation (COMPLETED)

**Impact:** Medium | **Effort:** High | **Priority:** P3 | **Status:** ✅ DONE

**Problem:** WPF consolidated into 5 workspaces (~15 navigation items) with command palette (Ctrl+K). UWP had 40+ pages in flat navigation list.

**Solution Implemented:**
- WPF has workspace model (Monitor, Collect, Storage, Quality, Settings)
- WPF command palette functional (Ctrl+K)
- UWP project removed — no remaining flat navigation to consolidate

**Files:**
- `MarketDataCollector.Wpf/Views/MainPage.xaml`
- `MarketDataCollector.Wpf/Services/NavigationService.cs`

**ROADMAP:** Phase 4 (Desktop App Maturity)

---

### F2. ✅ Contextual CLI Help System (COMPLETED)

**Impact:** Low-Medium | **Effort:** Low | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** `HelpCommand` displayed wall of flags. Users had to read full output to find what they need. No contextual help, no `--help backfill` sub-command support.

**Solution Implemented:**
- `--help <topic>` support for focused help across 7 topics
- Available topics: `backfill`, `symbols`, `config`, `storage`, `providers`, `packaging`, `diagnostics`
- Each topic shows description, available flags, and copy-paste examples
- Default `--help` (no topic) shows summary with topic list
- Topic content drawn from existing `docs/HELP.md` documentation

**Files:**
- `Application/Commands/HelpCommand.cs`
- `Application/Commands/CliArguments.cs`

**Benefit:** Users find relevant help faster. Reduces support burden and documentation lookups.

**ROADMAP:** Phase 4 (Desktop App Maturity)

---

### F3. ✅ First-Run Onboarding Experience (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** New users faced blank dashboard with no guidance on next steps.

**Solution Implemented:**
- `ConfigurationWizard` provides an 8-step interactive onboarding process:
  1. Provider detection (async)
  2. Use case selection
  3. Data source configuration (credentials)
  4. Symbol configuration
  5. Storage configuration (paths, compression)
  6. Backfill configuration
  7. Review and confirmation
  8. Save to file
- `AutoConfigurationService` detects providers from environment variables (Alpaca, Polygon, Tiingo, Finnhub, AlphaVantage, IB, NYSE, Yahoo) with credential resolution, recommended priorities, and structured `AutoConfigResult`
- CLI flags: `--wizard` for interactive setup, `--auto-config` for env-var auto-detection, `--detect-providers` for available providers
- Desktop `SetupWizardPage` and `OnboardingTourService` provide GUI onboarding
- `FirstRunService` (WPF) detects first-run state and triggers setup flow

**Files:**
- `Application/Services/ConfigurationWizard.cs`
- `Application/Services/AutoConfigurationService.cs`
- `MarketDataCollector.Wpf/Services/FirstRunService.cs`
- `MarketDataCollector.Ui.Services/Services/SetupWizardService.cs`
- `MarketDataCollector.Ui.Services/Services/OnboardingTourService.cs`

**ROADMAP:** Phase 4 (Desktop App Maturity)

---

## Theme G: Operations & Monitoring

### G1. ✅ Prometheus Metrics Export (COMPLETED)

**Impact:** High | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** No standardized metrics export for production monitoring.

**Solution Implemented:**
- `PrometheusMetrics` class exposes standard Prometheus metrics
- `/api/metrics` endpoint for scraping
- Metrics for event throughput, provider health, backpressure, error rates
- Histograms for latency tracking

**Files:**
- `Application/Monitoring/PrometheusMetrics.cs`
- `Ui.Shared/Endpoints/StatusEndpoints.cs`

**ROADMAP:** Phase 5 (Operational Readiness)

---

### G2. 🔄 Observability Tracing with OpenTelemetry (PARTIALLY COMPLETE)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** 🔄 PARTIAL

**Problem:** No distributed tracing for request flows across services. Hard to diagnose latency issues.

**Solution Implemented (Partial):**
- `TracedEventMetrics` decorator wraps `IEventMetrics` with `System.Diagnostics.Metrics` counters and histograms
- Pipeline meter (`MarketDataCollector.Pipeline`) exports published/dropped/trade/depth/quote/integrity/historical counters via OTLP
- Latency histogram (`mdc.pipeline.latency`) tracks event processing time in milliseconds
- `OpenTelemetrySetup` updated to register pipeline meter alongside existing application meters
- `CompositionOptions.EnableOpenTelemetry` flag gates decorator registration in DI
- `MarketDataTracing` extended with `StartBatchConsumeActivity`, `StartBackfillActivity`, `StartWalRecoveryActivity`

**Remaining Work:**
- Wire trace context propagation from provider receive through pipeline to storage write
- Add correlation IDs to structured log messages
- Integrate distributed tracing for backfill worker service
- Export traces to Jaeger/Zipkin for visualization

**Files:**
- `Application/Tracing/TracedEventMetrics.cs` (new)
- `Application/Tracing/OpenTelemetrySetup.cs` (updated)
- `Application/Composition/ServiceCompositionRoot.cs` (updated)
- `tests/MarketDataCollector.Tests/Application/Monitoring/TracedEventMetricsTests.cs` (new)

**ROADMAP:** Sprint 4 (partial), Sprint 8 (full trace propagation)

---

### G3. ✅ Scheduled Maintenance and Archive Management (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** No automated maintenance for old files, index rebuilding, or archive optimization.

**Solution Implemented:**
- `ScheduledArchiveMaintenanceService` runs maintenance tasks on schedule
- `ArchiveMaintenanceScheduleManager` manages CRON-based schedules
- Tasks: file integrity validation, orphan cleanup, index rebuild, compression optimization
- `/api/maintenance/*` endpoints for manual triggering

**Files:**
- `Storage/Maintenance/ScheduledArchiveMaintenanceService.cs`
- `Storage/Maintenance/ArchiveMaintenanceScheduleManager.cs`
- `Ui.Shared/Endpoints/MaintenanceScheduleEndpoints.cs`

**ROADMAP:** Phase 5 (Operational Readiness)

---

## Theme J: Data Canonicalization

### J1. ✅ Deterministic Canonicalization Design (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Equivalent market events from different providers for the same instrument produce structurally incomparable records. The same instrument appears as different symbol strings, condition codes use different encoding systems, and venue identifiers are inconsistent across providers.

**Solution Implemented:**
- Comprehensive design document with provider field audit covering timestamp formats, aggressor side determination, venue identifiers, condition codes, and sequence numbers across Alpaca, Polygon, IB, and StockSharp
- Detailed canonicalization stage design with `IEventCanonicalizer` interface and `EventCanonicalizer` class
- Condition code mapping registry specification (CTA plan codes, SEC numeric codes, IB field codes → canonical enum)
- Venue normalization to ISO 10383 MIC codes with complete Polygon exchange mapping
- 3-phase rollout plan: Contract + Mapping Inventory → Dual-Write Validation → Default Canonical Read Path
- Acceptance criteria, operational metrics, risk mitigations, and test strategy

**Files:**
- `docs/architecture/deterministic-canonicalization.md`

**ROADMAP:** Theme J (Data Canonicalization)

---

### J2. ✅ MarketEvent Canonical Fields (COMPLETED)

**Impact:** High | **Effort:** Low | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** `MarketEvent` envelope lacks fields to distinguish raw vs. canonicalized events and carry resolved identifiers.

**Solution Implemented:**
- Added `CanonicalSymbol` (`string?`), `CanonicalizationVersion` (`int`), `CanonicalVenue` (`string?`) fields to `MarketEvent` sealed record
- Updated `MarketDataJsonContext` source generator attributes for new fields and canonicalization types
- New fields use `WhenWritingNull`/default omission for backward compatibility with existing JSONL files
- Added `EffectiveSymbol` property (`CanonicalSymbol ?? Symbol`) for downstream consumers
- Both Domain and Contracts `MarketEvent` records updated in sync

**Files:**
- `src/MarketDataCollector.Domain/Events/MarketEvent.cs`
- `src/MarketDataCollector.Core/Serialization/MarketDataJsonContext.cs`

**Dependencies:** None (additive change)

---

### J3. ✅ EventCanonicalizer Implementation (COMPLETED)

**Impact:** High | **Effort:** Medium | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** No canonicalization step exists between provider adapters and `EventPipeline`.

**Solution Implemented:**
- `IEventCanonicalizer` interface with `Canonicalize(MarketEvent raw, CancellationToken ct)` method
- `EventCanonicalizer` class using `with` expression pattern (same as `StampReceiveTime()`)
- Resolves symbols via `CanonicalSymbolRegistry.ResolveToCanonical()`, maps venues via `VenueMicMapper`, extracts venue from typed payloads (Trade, BboQuote, LOBSnapshot, L2Snapshot, OrderFlowStatistics, IntegrityEvent)
- Skips heartbeats and already-canonicalized events (idempotent)
- Sets `Tier = Enriched` on canonicalized events
- 12+ unit tests covering symbol resolution, venue normalization, idempotency, and edge cases

**Files:**
- `src/MarketDataCollector.Application/Canonicalization/IEventCanonicalizer.cs`
- `src/MarketDataCollector.Application/Canonicalization/EventCanonicalizer.cs`
- `tests/MarketDataCollector.Tests/Application/Services/EventCanonicalizerTests.cs`

**Dependencies:** J2 (canonical fields on MarketEvent)

---

### J4. ✅ Condition Code Mapping Registry (COMPLETED)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** Trade condition codes stored as raw `string[]?` with no cross-provider normalization.

**Solution Implemented:**
- `CanonicalTradeCondition` enum with 16+ canonical values (Regular, FormT_ExtendedHours, OddLot, Intermarket_Sweep, OpeningPrint, ClosingPrint, etc.)
- `ConditionCodeMapper` class with `FrozenDictionary<(provider, raw_code), CanonicalTradeCondition>` for zero-allocation hot-path lookups
- Mapping table in `config/condition-codes.json` covering 17 Alpaca CTA plan codes, 19 Polygon SEC numeric codes, 8 IB field codes
- `MapConditions()` returns both canonical and raw arrays for auditability; `MapSingle()` for individual lookups
- Loaded from JSON at startup with graceful fallback if file missing

**Files:**
- `src/MarketDataCollector.Application/Canonicalization/ConditionCodeMapper.cs`
- `config/condition-codes.json`
- `tests/MarketDataCollector.Tests/Application/Services/ConditionCodeMapperTests.cs`

**Dependencies:** J3 (EventCanonicalizer to invoke mapper)

---

### J5. ✅ Venue Normalization to ISO 10383 MIC (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** Venue identifiers differ across providers for the same exchange.

**Solution Implemented:**
- `VenueMicMapper` class with `FrozenDictionary<(provider, rawVenue), string?>` for zero-allocation lookups
- Mapping table in `config/venue-mapping.json`: 29 Alpaca text mappings, 17 Polygon numeric ID mappings, 17 IB routing name mappings (including `SMART → null` for unmappable IB meta-venues)
- Case-insensitive venue matching with provider-scoped lookups
- `CanonicalVenue` field populated on `MarketEvent` envelope via `EventCanonicalizer`
- Loaded from JSON at startup with graceful fallback if file missing

**Files:**
- `src/MarketDataCollector.Application/Canonicalization/VenueMicMapper.cs`
- `config/venue-mapping.json`
- `tests/MarketDataCollector.Tests/Application/Services/VenueMicMapperTests.cs`

**Dependencies:** J3 (EventCanonicalizer to invoke mapper)

---

### J6. ✅ Provider Adapter Wiring (COMPLETED)

**Impact:** High | **Effort:** High | **Priority:** P1 | **Status:** ✅ DONE

**Problem:** Provider adapters publish raw events without canonicalization.

**Solution Implemented:**
- `CanonicalizingPublisher` decorator wraps `IMarketEventPublisher` with transparent canonicalization
- DI wiring in `ServiceCompositionRoot.AddCanonicalizationServices()` — decorates the existing pipeline publisher
- Configurable pilot symbol list for phased rollout (clear `PilotSymbols` for all-symbol canonicalization)
- Dual-write mode: publishes both raw and canonicalized events for parity validation
- Lock-free metrics tracking (canonicalized count, skipped count, unresolved count, average duration)
- `CanonicalizationConfig` in `appsettings.json` controls `Enabled`, `PilotSymbols`, `DualWriteRawAndCanonical`, `ConditionCodesPath`, `VenueMappingPath`, and `Version`
- 17+ unit tests covering pilot filtering, dual-write, metrics, and edge cases

**Files:**
- `src/MarketDataCollector.Application/Canonicalization/CanonicalizingPublisher.cs`
- `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`
- `src/MarketDataCollector.Core/Config/CanonicalizationConfig.cs`
- `tests/MarketDataCollector.Tests/Application/Services/CanonicalizingPublisherTests.cs`

**Dependencies:** J3 (EventCanonicalizer implementation)

---

### J7. ✅ Canonicalization Metrics and Monitoring (COMPLETED)

**Impact:** Medium | **Effort:** Low | **Priority:** P2 | **Status:** ✅ DONE

**Problem:** No observability into canonicalization success rates, latency, or unresolved mappings.

**Solution Implemented:**
- `CanonicalizationMetrics` static class with thread-safe counters: success, soft-fail, hard-fail, dual-write totals
- Per-provider parity statistics (`ProviderParityStats`) tracking match rates, unresolved breakdowns (symbol/venue/condition)
- `CanonicalizationSnapshot` immutable record for point-in-time metric export
- API endpoints via `CanonicalizationEndpoints`:
  - `GET /api/canonicalization/status` — overall canonicalization metrics
  - `GET /api/canonicalization/parity` — per-provider parity breakdown
  - `GET /api/canonicalization/parity/{provider}` — single provider detail with unresolved field breakdown
  - `GET /api/canonicalization/config` — current canonicalization configuration
- `CanonicalizingPublisher` also exposes per-instance metrics (canonicalized count, average duration)

**Files:**
- `src/MarketDataCollector.Application/Canonicalization/CanonicalizationMetrics.cs`
- `src/MarketDataCollector.Ui.Shared/Endpoints/CanonicalizationEndpoints.cs`
- `src/MarketDataCollector.Contracts/Api/UiApiRoutes.cs` (route constants)

**Dependencies:** J3 (EventCanonicalizer must emit metrics)

---

### J8. 🔄 Golden Fixture Test Suite (PARTIAL)

**Impact:** Medium | **Effort:** Medium | **Priority:** P2 | **Status:** 🔄 PARTIAL

**Problem:** No test fixtures for verifying canonicalization correctness across providers.

**What exists:**
- Unit tests for `EventCanonicalizer`, `ConditionCodeMapper`, `VenueMicMapper`, and `CanonicalizingPublisher` cover core correctness, idempotency, and edge cases
- Property tests for idempotency (canonicalize twice = same result), raw symbol preservation, and tier progression are covered in `EventCanonicalizerTests`
- **8 curated fixture JSON files** in `tests/MarketDataCollector.Tests/Application/Canonicalization/Fixtures/` covering Alpaca and Polygon regular, extended-hours, and odd-lot trade scenarios plus cross-provider XNAS identity checks
- **`CanonicalizationGoldenFixtureTests`** loads all `.json` fixture files at runtime using `[Theory][MemberData]`, constructs `MarketEvent` from fixture inputs, runs production `condition-codes.json` and `venue-mapping.json` mappings, and asserts canonical symbol, venue, tier, and version fields match expected values

**Remaining:**
- Backward compatibility tests replaying archived JSONL files through canonicalizer
- Drift canary CI job for detecting new unmapped codes

**Files:**
- `tests/MarketDataCollector.Tests/Application/Canonicalization/Fixtures/*.json` (8 fixture files)
- `tests/MarketDataCollector.Tests/Application/Canonicalization/CanonicalizationGoldenFixtureTests.cs` (new)

**Dependencies:** J3 (EventCanonicalizer to test against)

---

## Priority Matrix

### By Impact and Effort

| Priority | Items | Description |
|----------|-------|-------------|
| **P0** | A1-A4 | Critical reliability fixes - ALL DONE ✅ |
| **P1** | A3, A5, B1-B2, C1-C2, C4-C6, D4, G1 | High impact, low-medium effort - A3, B2 DONE ✅ |
| **P2** | A6-A7, B3-B5, C3, D5-D6, E1, F2-F3, G2-G3 | Medium impact or higher effort - B5, F2 DONE ✅ |
| **P3** | D7, E3 | Lower priority or high effort - C7, F1 DONE ✅ |

### Recommended Execution Order

**Phase 1 — Quick Wins (4-6 weeks):**
1. C4 — Injectable metrics (unblocks testability)
2. C5 — Consolidated config validation (cleaner startup)
3. C6 — Composite storage sink (new capability)
4. D4 — Quality metrics API (immediate UX value)
5. B1 — Complete dropped event audit with API endpoint

**Phase 2 — Core Architecture (6-8 weeks):**
6. C1 — Unified provider registry
7. C2 — Single DI composition path
8. B2 — HTTP endpoint integration tests
9. A7 — Standardized error handling

**Phase 3 — Testing Foundation (8-10 weeks):**
10. B3 — Infrastructure provider tests
11. B4 — Application service tests
12. B5 — Provider SDK tests

**Phase 4 — Larger Refactors (12-16 weeks):**
13. C3 — WebSocket base class adoption
14. ~~C7 — WPF/UWP service deduplication~~ ✅ Done (UWP removed)
15. E3 — GC pressure reduction (Polygon optimization)
16. ~~F1 — UWP navigation consolidation~~ ✅ Done (UWP removed)

---

## Execution Strategy

### Parallel Tracks

1. **Testing Track** — Can run parallel to architecture work
   - B2-B5: Build test suite incrementally
   - Target 80% coverage for production readiness

2. **Architecture Track** — Core refactoring
   - C1-C6: Improve modularity and maintainability
   - A7: Standardize error handling

3. **API Track** — Complete HTTP API surface
   - D4, D7: Finish quality endpoints and OpenAPI annotations

4. **Performance Track** — Optimization (lower priority)
   - E3: Polygon zero-alloc parsing
   - G2: OpenTelemetry tracing

### Success Metrics

| Metric | Current | Target | Phase |
|--------|---------|--------|-------|
| Completed Improvements | 33/35 | 35/35 | All |
| Test Files | 219 | 250+ | Phase 1-3 |
| Test Methods | ~3,444 | 4,000+ | Phase 1-3 |
| Route Constants | 283 | 283 | Phase 3 |
| Provider Test Files | 12 | 15+ | Phase 3 |

### Risk Mitigation

- **Break Large Refactors** — C3, C7 should be split into smaller PRs
- **Test First** — B2-B5 should precede major refactoring
- **Incremental Rollout** — F1 (UWP consolidation) can be phased
- **Benchmark Performance** — E3 must include before/after metrics

---

## Delivery Operating Model

### Workstream Ownership

| Workstream | Scope | Suggested Owner | Supporting Roles |
|------------|-------|-----------------|------------------|
| Reliability | A1-A7 | Platform/Core lead | Provider maintainers, SRE |
| Test Foundation | B1-B5 | QA automation lead | Service owners, API maintainers |
| Architecture | C1-C7 | Principal engineer | App architecture guild |
| API & UX | D1-D7, F1-F3 | Full-stack lead | Frontend + API contributors |
| Ops/Observability | G1-G3 | DevOps lead | Infra + on-call engineers |

### PR Sizing Guidance

- **Small PR (preferred):** 1 improvement item or one coherent subset (<500 LOC net change).
- **Medium PR:** 1 item with migration shims + tests (<1,200 LOC net).
- **Large PR (exception):** C3/C7-level refactors; require design note and staged rollout plan.

### Quality Gates per Improvement Item

Each item should not be marked complete until all gates are met:
1. Code merged behind existing CI checks.
2. Test coverage added or updated for touched behavior.
3. Operational visibility updated (logs/metrics/traces where applicable).
4. Documentation updated in `ROADMAP.md`, this file, and endpoint docs (if API-facing).

---

## Dependency Map

| Item | Depends On | Why Dependency Exists |
|------|------------|-----------------------|
| B2 | C2 | Stable DI composition needed to host test server predictably |
| C1 | C2 | Registry unification should land after single composition path |
| D4 | B1 | Drop statistics model from audit trail is prerequisite for API exposure |
| B3 | C3 (optional) | Tests can start now, but base-class refactor will reduce fixture duplication |
| D7 | D4 | Response annotations are easier once quality endpoints are concrete |
| G2 | C4 | Injectable metrics/tracing abstractions reduce instrumentation coupling |

### Critical Path (Shortest Path to Production Readiness Lift)

1. ~~C4 → C5 → D4/B1 completion~~ ✅ Done
2. ~~C6, A7~~ ✅ Done
3. ~~B2 → B3 (provider confidence)~~ ✅ Done
4. ~~C1/C2 (composition + provider extensibility)~~ ✅ Done
5. C3 (WebSocket base class) → G2 remainder (trace propagation)

---

## Definition of Done Checklist

Use this checklist before changing any item status from 📝/🔄 to ✅:

- [ ] Acceptance criteria met for the item’s “Proposed Solution.”
- [ ] Unit/integration tests included and passing in CI.
- [ ] No new TODO/FIXME left without linked backlog issue.
- [ ] Telemetry impact evaluated (log/metric/trace).
- [ ] Backward compatibility validated (config, endpoints, file formats).
- [ ] Documentation and status tables updated in this file.

---

## Review Cadence & Reporting

- **Weekly (engineering sync):** Update item-level status and blockers.
- **Bi-weekly (architecture review):** Re-score priorities P0-P3 based on new risk and customer impact.
- **Per release:** Recompute completion metrics and validate roadmap phase alignment.

### Suggested Reporting Snippet (for release notes / standups)

```md
Improvements Tracker Update
- Completed this period: <items>
- Partially complete: <items>
- Newly opened risks: <items>
- Next period focus: <top 3 items>
```

---

## Reference Documents

- **[ROADMAP.md](ROADMAP.md)** — Phased execution timeline (Phases 0-10)
- **[EVALUATIONS_AND_AUDITS.md](EVALUATIONS_AND_AUDITS.md)** — Consolidated architecture evaluations, code audits, and assessments
- **[CHANGELOG.md](CHANGELOG.md)** — Historical changes and version history
- **[TODO.md](TODO.md)** — Auto-generated TODO tracking from code comments
- **[DEPENDENCIES.md](../DEPENDENCIES.md)** — NuGet package dependencies
- **[production-status.md](production-status.md)** — Current production readiness assessment
- **[deterministic-canonicalization.md](../architecture/deterministic-canonicalization.md)** — Cross-provider canonicalization design (Theme J)

### Archived Improvement Documents

These documents are superseded by this consolidated tracker:

- `docs/archived/IMPROVEMENTS_2026-02.md` — Original functional improvements (10 completed)
- `docs/archived/STRUCTURAL_IMPROVEMENTS_2026-02.md` — Original structural improvements (15 items)
- `docs/archived/REDESIGN_IMPROVEMENTS.md` — UI redesign quality summary
- `docs/archived/2026-02_UI_IMPROVEMENTS_SUMMARY.md` — Visual UI improvements summary
- `docs/archived/CHANGES_SUMMARY.md` — Historical changes (v1.5.0 and earlier)
- `docs/archived/ROADMAP_UPDATE_SUMMARY.md` — Roadmap expansion summary (archived)

See [`archived/INDEX.md`](../archived/INDEX.md) for context on archived documents.

---

**Last Updated:** 2026-02-25
**Maintainer:** Project Team
**Status:** ✅ Active tracking document — 94.3% complete (33/35 core items) + Theme J canonicalization (7/8)
**Next Review:** Weekly engineering sync (or immediately after any status change)
