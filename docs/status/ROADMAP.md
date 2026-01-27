# Market Data Collector - Roadmap

**Version:** 1.6.1
**Last Updated:** 2026-01-27
**Status:** Production Ready

This document provides the feature roadmap, backlog, and development priorities for the Market Data Collector system.

---

## Quick Stats

| Category | Implemented | Pending | Total |
|----------|-------------|---------|-------|
| Core Features | 55+ | - | 55+ |
| Technical Debt | 10 | 4 | 14 |
| Technical Debt (P0) | 3 | 0 | 3 |
| Quick Wins (≤2 days) | 44 | 81 | 125 |
| Provider Integration | 5 | 17 | 22 |
| Monitoring & Alerting | 24 | 0 | 24 |
| Data Quality | 23 | 0 | 23 |
| Storage & Archival | 9 | 4 | 13 |
| Code Refactoring | 7 | 3 | 10 |
| Cloud Integration | 0 | 100+ | 100+ |
| **Total** | **180+** | **209+** | **389+** |

---

## What's Implemented (v1.6.1)

### Core Data Collection
- [x] Multi-provider streaming (Alpaca, Interactive Brokers, NYSE, Polygon stub)
- [x] Tick-by-tick trade capture with sequence validation
- [x] Level 2 order book / market depth (configurable levels)
- [x] BBO/NBBO quote tracking with spread calculation
- [x] Historical data backfill (9+ providers with automatic failover)
- [x] Provider failover with circuit breaker pattern
- [x] Priority backfill queue with gap detection

### Storage & Archival
- [x] JSONL storage with configurable naming conventions
- [x] Apache Parquet storage (10-20x compression)
- [x] Write-Ahead Logging (WAL) with checksums
- [x] Compression profiles (LZ4 real-time, ZSTD archive)
- [x] Schema versioning and migration
- [x] Tiered storage (hot/warm/cold)
- [x] Retention policies (by age or size)
- [x] Analysis-ready exports (Python, R, Lean, SQL, Excel)
- [x] Portable Data Packager (ZIP/TAR.gz/7z with manifests)
- [x] Data dictionary generation (event schemas)

### Monitoring & Observability
- [x] Web dashboard with real-time metrics
- [x] Prometheus metrics endpoint (`/metrics`)
- [x] Health checks (`/health`, `/live`, `/ready`)
- [x] JSON status endpoint (`/status`)
- [x] Data quality scoring (multi-dimensional)
- [x] OpenTelemetry distributed tracing
- [x] Structured logging (Serilog)
- [x] Stale data detection with alerts
- [x] Connection health heartbeat monitoring
- [x] Disk space and memory warnings
- [x] System health checker (CPU, memory, threads)
- [x] Connection health monitor with latency tracking
- [x] Data quality report generator (JSON, CSV, HTML, Markdown)
- [x] Cross-provider comparison service
- [x] Last N errors endpoint (QW-58)
- [x] Events per second gauge (QW-82)
- [x] Error ring buffer for diagnostics

### Data Quality
- [x] Crossed market detector (bid > ask)
- [x] Timestamp monotonicity checker
- [x] Trading calendar integration (US markets)
- [x] Gap detection and backfill coordination
- [x] Sequence validation
- [x] Event schema validator (lightweight, pre-persistence)
- [x] Anomaly detector with statistical outliers
- [x] Completeness score calculator
- [x] Latency histogram tracking
- [x] Negative price detector (QW-107)
- [x] Future timestamp detector (QW-109)
- [x] Tick size validator (DQ-13)
- [x] Duplicate event detector (DQ-2)
- [x] Bad tick filter (DQ-20)
- [x] Price spike alert (QW-6)
- [x] Spread monitor (QW-7)
- [x] Volume spike detector (ADQ-3)
- [x] Volume drop detector (ADQ-3b)

### User Interfaces
- [x] Web Dashboard (HTML/JS, auto-refresh)
- [x] UWP Desktop Application (Windows native, 17+ pages including Admin/Maintenance and Advanced Analytics)
- [x] Monolithic architecture (simplified in v1.6.0)
- [x] CLI with hot-reload configuration

### Developer Features
- [x] HTTP endpoint authentication (API key middleware)
- [x] Bulk symbol import (CSV)
- [x] Symbol search autocomplete
- [x] QuantConnect Lean integration
- [x] F# domain library (type-safe, railway-oriented)
- [x] 50+ unit tests
- [x] Config validator CLI (`--validate-config`)
- [x] Pre-flight checks on startup
- [x] Graceful shutdown with event flush
- [x] Diagnostic bundle generator (ZIP with system info)
- [x] Config template generator (multiple deployment scenarios)
- [x] Sample data generator (realistic test data)
- [x] Technical indicator service (200+ indicators via Skender.Stock.Indicators)
- [x] Symbol management CLI commands (list, add, remove, import, export)
- [x] PlantUML diagram PNG generation
- [x] Log level runtime toggle (QW-53)
- [x] Config path override (QW-95)
- [x] Sensitive value masking (QW-78)
- [x] Dry run mode (QW-93)

---

## Recent Updates (2026-01-27)

### Major Refactoring (2026-01-27)
- **BaseHistoricalDataProvider**: Created shared base class for all historical providers
  - Migrated: Stooq, Yahoo Finance, Nasdaq Data Link, Alpha Vantage providers
  - Includes: Rate limiting, HTTP client management, response handling
- **Shared Utilities**: Centralized common functionality
  - `SymbolNormalization` - Provider-agnostic symbol formatting
  - `HttpResponseHandler` - Centralized HTTP error handling
  - `SharedResiliencePolicies` - Retry and circuit breaker policies
  - `SubscriptionManager` - Thread-safe subscription tracking
  - `EventBuffer<T>` - Generic buffering for storage sinks
  - `StorageChecksumService` - Unified SHA256 checksum computation
  - `CredentialValidator` - API credential validation

### Technical Debt Resolution (2026-01-27)
- **TD-9 (Async Void Methods)**: All P0 async void instances fixed (TD-9.1 through TD-9.7)
- **TD-10 (IHttpClientFactory)**: Complete implementation with Polly resilience policies
- **TD-11 (Thread.Sleep)**: All blocking calls replaced with async Task.Delay

### UWP Enhancements (2026-01-27)
- Collector refinements (#23, #50, #51, #63)
- Retention and storage services integrated with core API
- HttpClientFactory initialization improved

### Dependency Updates (2026-01-26)
- **OpenTelemetry**: Updated all OpenTelemetry packages to v1.15.0
  - `OpenTelemetry.Instrumentation.Http` → 1.15.0
  - `OpenTelemetry.Instrumentation.AspNetCore` → 1.15.0
  - `OpenTelemetry.Api` → 1.15.0
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol` → 1.15.0
  - `OpenTelemetry.Extensions.Hosting` → 1.15.0

### CI/CD Improvements (2026-01-26)
- Updated GitHub Actions to latest versions:
  - `actions/upload-artifact` → v6
  - `actions/download-artifact` → v7
  - `actions/labeler` → v6
  - `actions/setup-python` → v6
  - `DavidAnson/markdownlint-cli2-action` → v22

### Code Quality
- Fixed blocking sync methods converted to async patterns
- Added missing `[ImplementsAdr]` attributes for ADR compliance
- Updated CLAUDE.md with comprehensive codebase documentation

---

## Technical Debt (Critical)

Items that should be addressed before new feature development.

| ID | Item | Priority | Effort | Status |
|----|------|----------|--------|--------|
| TD-1 | Replace `double` with `decimal` for prices | High | Medium | **Done** |
| TD-2 | Add authentication to HTTP endpoints | High | Low | **Done** |
| TD-3 | Complete Alpaca quote message handling | Medium | Low | **Done** |
| TD-4 | Fix UWP/Core API endpoint mismatch | Medium | Low | **Done** |
| TD-5 | Create shared contracts library (UWP/Core) | Medium | Medium | Pending |
| TD-6 | Add missing integration tests | Low | High | Pending |
| TD-7 | Standardize error handling patterns | Medium | Medium | **Partial** (HttpResponseHandler done) |
| TD-8 | Remove deprecated `--serve-status` option | Low | Low | **Done** |
| TD-9 | Fix async void methods (30+ instances) | P0 | Medium | **Done** (TD-9.1-9.7) |
| TD-10 | Replace instance HttpClient with IHttpClientFactory | P0 | Medium | **Done** |
| TD-11 | Replace Thread.Sleep with Task.Delay in async code | P0 | Low | **Done** |
| TD-12 | Consolidate duplicate domain models | Medium | Medium | Pending (12 models) |
| TD-13 | Migrate remaining providers to BaseHistoricalDataProvider | Low | Medium | **Partial** (4 of 8) |
| TD-14 | UWP service naming conflicts | Low | Low | Pending |

---

## Micro-Tasks (Bite-Sized Work Items)

These are small, focused tasks broken down from larger features. Each can typically be completed in 1-4 hours.

### TD-5: Create Shared Contracts Library (UWP/Core)
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| TD-5.1 | Create `MarketDataCollector.Contracts` project | 1 hour | None |
| TD-5.2 | Extract common DTOs (StatusResponse, HealthResponse) | 2 hours | TD-5.1 |
| TD-5.3 | Add API request models (BackfillRequest, SubscriptionRequest) | 2 hours | TD-5.1 |
| TD-5.4 | Add API response models (BackfillResult, QueryResult) | 2 hours | TD-5.3 |
| TD-5.5 | Update Core project to use contracts | 2 hours | TD-5.2, TD-5.4 |
| TD-5.6 | Update UWP project to use contracts | 2 hours | TD-5.2, TD-5.4 |
| TD-5.7 | Remove duplicate model definitions | 1 hour | TD-5.5, TD-5.6 |

### TD-6: Add Missing Integration Tests
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| TD-6.1 | Create integration test project structure | 1 hour | None |
| TD-6.2 | Add test fixtures for provider mocking | 2 hours | TD-6.1 |
| TD-6.3 | Add Alpaca provider connection test | 2 hours | TD-6.2 |
| TD-6.4 | Add JSONL storage sink write/read test | 2 hours | TD-6.1 |
| TD-6.5 | Add Parquet storage sink write/read test | 2 hours | TD-6.4 |
| TD-6.6 | Add backfill workflow end-to-end test | 3 hours | TD-6.2 |
| TD-6.7 | Add API endpoint integration tests | 3 hours | TD-6.1 |
| TD-6.8 | Add graceful shutdown integration test | 2 hours | TD-6.1 |

### TD-7: Standardize Error Handling Patterns
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| TD-7.1 | Create ErrorCode enum with categories | 1 hour | None |
| TD-7.2 | Create standardized ErrorResponse model | 1 hour | TD-7.1 |
| TD-7.3 | Add Result<T, TError> pattern to Application layer | 2 hours | TD-7.2 |
| TD-7.4 | Update provider error handling to use ErrorCode | 2 hours | TD-7.1 |
| TD-7.5 | Update API endpoints to return ErrorResponse | 2 hours | TD-7.2 |
| TD-7.6 | Add error handling middleware for HTTP | 2 hours | TD-7.5 |
| TD-7.7 | Document error codes in API documentation | 1 hour | TD-7.1 |

### TD-8: Remove Deprecated --serve-status Option
| Sub-ID | Task | Effort | Dependencies | Status |
|--------|------|--------|--------------|--------|
| TD-8.1 | Add deprecation warning log when --serve-status is used | 0.5 hour | None | **Done** |
| TD-8.2 | Update Makefile to use --http-port instead | 0.5 hour | None | **Done** |
| TD-8.3 | Update start-collector.sh to use --http-port | 0.5 hour | None | **Done** |
| TD-8.4 | Update start-collector.ps1 to use --http-port | 0.5 hour | None | **Done** |
| TD-8.5 | Update Dockerfile CMD to use --http-port | 0.5 hour | None | **Done** |
| TD-8.6 | Update systemd service file | 0.5 hour | None | **Done** |
| TD-8.7 | Update documentation (USAGE.md, HELP.md, configuration.md) | 1 hour | None | **Done** |
| TD-8.8 | Update HtmlTemplates.cs UI references | 0.5 hour | None | **Done** |
| TD-8.9 | Update UWP UI references | 0.5 hour | None | **Done** |
| TD-8.10 | Remove --serve-status from Program.cs (breaking change) | 1 hour | TD-8.1 to TD-8.9 | **Done** |

### QW-15: Query Endpoint for Historical Data
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| QW-15.1 | Create HistoricalQueryRequest model | 1 hour | None |
| QW-15.2 | Create HistoricalQueryResponse model | 1 hour | QW-15.1 |
| QW-15.3 | Add date range parameter parsing | 1 hour | QW-15.1 |
| QW-15.4 | Implement file lookup logic for date range | 2 hours | QW-15.3 |
| QW-15.5 | Add pagination support (offset, limit) | 1 hour | QW-15.4 |
| QW-15.6 | Add format selection (JSON, CSV, JSONL) | 2 hours | QW-15.5 |
| QW-15.7 | Create `/api/historical/query` endpoint | 2 hours | QW-15.2 to QW-15.6 |
| QW-15.8 | Add endpoint to DataQualityEndpoints | 1 hour | QW-15.7 |
| QW-15.9 | Add unit tests for query logic | 2 hours | QW-15.7 |

### DEV-9: API Explorer / Swagger UI
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| DEV-9.1 | Add Swashbuckle.AspNetCore NuGet package | 0.5 hour | None |
| DEV-9.2 | Create OpenAPI endpoint documentation | 2 hours | DEV-9.1 |
| DEV-9.3 | Add XML documentation to API endpoints | 2 hours | None |
| DEV-9.4 | Configure Swagger UI hosting at /swagger | 1 hour | DEV-9.1 |
| DEV-9.5 | Add authentication documentation | 1 hour | DEV-9.2 |
| DEV-9.6 | Add example request/response models | 1 hour | DEV-9.2 |

### PERF-1/STO-5: Batch Write Optimization
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| PERF-1.1 | Add BatchWriteConfig with size and timeout settings | 1 hour | None |
| PERF-1.2 | Create BatchBuffer<T> class with thread-safe queue | 2 hours | None |
| PERF-1.3 | Add flush timer to BatchBuffer | 1 hour | PERF-1.2 |
| PERF-1.4 | Integrate BatchBuffer into JsonlStorageSink | 2 hours | PERF-1.2, PERF-1.3 |
| PERF-1.5 | Add batch write metrics (batch_size, flush_count) | 1 hour | PERF-1.4 |
| PERF-1.6 | Add configuration options to appsettings.json | 0.5 hour | PERF-1.1 |
| PERF-1.7 | Add unit tests for batching behavior | 2 hours | PERF-1.4 |

### ARCH-1: Dead Letter Queue
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| ARCH-1.1 | Create DeadLetterEntry model | 1 hour | None |
| ARCH-1.2 | Create DLQ storage directory structure | 1 hour | None |
| ARCH-1.3 | Implement DeadLetterQueue class | 2 hours | ARCH-1.1, ARCH-1.2 |
| ARCH-1.4 | Add failed event capture in EventPipeline | 2 hours | ARCH-1.3 |
| ARCH-1.5 | Implement retry mechanism with backoff | 2 hours | ARCH-1.3 |
| ARCH-1.6 | Add `/api/dlq/list` endpoint | 1 hour | ARCH-1.3 |
| ARCH-1.7 | Add `/api/dlq/retry` endpoint | 1 hour | ARCH-1.5 |
| ARCH-1.8 | Add `/api/dlq/purge` endpoint | 1 hour | ARCH-1.3 |
| ARCH-1.9 | Add DLQ metrics (dlq_size, dlq_retries) | 1 hour | ARCH-1.3 |
| ARCH-1.10 | Add unit tests for DLQ | 2 hours | ARCH-1.3 to ARCH-1.5 |

### PROV-22: Provider Config Wizard
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| PROV-22.1 | Create WizardStep model and flow | 1 hour | None |
| PROV-22.2 | Add interactive console prompts helper | 1 hour | None |
| PROV-22.3 | Implement provider selection step | 1 hour | PROV-22.2 |
| PROV-22.4 | Add credential input prompts per provider | 2 hours | PROV-22.2 |
| PROV-22.5 | Add credential validation step | 1 hour | PROV-22.4 |
| PROV-22.6 | Generate appsettings.json from wizard inputs | 1 hour | PROV-22.3 to PROV-22.5 |
| PROV-22.7 | Add symbol selection step | 1 hour | PROV-22.2 |
| PROV-22.8 | Add storage configuration step | 1 hour | PROV-22.2 |
| PROV-22.9 | Add --wizard flag to Program.cs | 0.5 hour | PROV-22.6 |

### TD-9: Fix Async Void Methods (P0) - **COMPLETE**
| Sub-ID | Task | Effort | Dependencies | Status |
|--------|------|--------|--------------|--------|
| TD-9.1 | Fix async void in OAuthRefreshService (OnRefreshTimerElapsed, OnExpirationCheckElapsed) | 1 hour | None | **Done** |
| TD-9.2 | Fix async void in DashboardViewModel (OnRefreshTimerElapsed) | 1 hour | None | **Done** |
| TD-9.3 | Fix async void in PendingOperationsQueueService (OnConnectionStateChanged) | 1 hour | None | **Done** |
| TD-9.4 | Fix async void Dispose in DataExportViewModel (implement IAsyncDisposable) | 1 hour | None | **Done** |
| TD-9.5 | Fix async void in App.xaml.cs (OnLaunched, OnAppExit) | 2 hours | None | **Done** |
| TD-9.6 | Fix async void in UWP Views (SymbolMappingPage, MainPage, SetupWizardPage, CollectionSessionPage) | 3 hours | None | **Done** |
| TD-9.7 | Fix async void in DetailedHealthCheck | 1 hour | None | **Done** |
| TD-9.8 | Add try-catch with logging to converted async Task methods | 2 hours | TD-9.1 to TD-9.7 | **Done** |
| TD-9.9 | Add unit tests for exception handling in async paths | 2 hours | TD-9.8 | Pending |

**Note:** All critical async void methods have been fixed. Error handling with logging was added during the fixes.

### TD-10: Replace Instance HttpClient with IHttpClientFactory (P0) - **COMPLETE**
| Sub-ID | Task | Effort | Dependencies | Status |
|--------|------|--------|--------------|--------|
| TD-10.1 | Register IHttpClientFactory in DI container | 1 hour | None | **Done** |
| TD-10.2 | Create named HttpClient configurations for each provider | 2 hours | TD-10.1 | **Done** |
| TD-10.3 | Refactor Backfill providers (Alpaca, Polygon, Tiingo, Yahoo, Stooq, Finnhub, AlphaVantage, Nasdaq) | 4 hours | TD-10.1 | **Done** |
| TD-10.4 | Refactor SymbolSearch providers (Alpaca, Polygon, Finnhub, OpenFigi) | 2 hours | TD-10.1 | **Done** |
| TD-10.5 | Refactor Application services (CredentialValidation, DailySummaryWebhook, Connectivity, OAuth) | 2 hours | TD-10.1 | **Done** |
| TD-10.6 | Refactor UWP services (ApiClientService, CredentialService, SetupWizardService) | 2 hours | TD-10.1 | **Done** |
| TD-10.7 | Add Polly retry policies to HttpClientFactory configuration | 2 hours | TD-10.2 | **Done** |
| TD-10.8 | Remove HttpClient instance fields and IDisposable implementations | 1 hour | TD-10.3 to TD-10.6 | N/A (kept for backward compat) |
| TD-10.9 | Add integration tests for HTTP client lifecycle | 2 hours | TD-10.8 | Pending |

**Implementation Notes:**
- Created `HttpClientConfiguration.cs` with named clients and Polly retry/circuit breaker policies
- `HttpClientFactoryProvider` enables backward-compatible static access for transitional use
- UWP project has its own HttpClientConfiguration since it cannot reference the main project
- Retry policy: 3 retries with exponential backoff (2s, 4s, 8s) for transient errors
- Circuit breaker: Opens after 5 consecutive failures, stays open for 30s

### TD-11: Replace Thread.Sleep with Task.Delay (P0) - **COMPLETE**
| Sub-ID | Task | Effort | Dependencies | Status |
|--------|------|--------|--------------|--------|
| TD-11.1 | Fix ConnectionWarmUp.cs Thread.Sleep(10) → await Task.Delay(10) | 0.5 hour | None | **Done** |
| TD-11.2 | Fix ConfigWatcher.cs Thread.Sleep(delayMs) → await Task.Delay(delayMs) | 0.5 hour | None | **Done** |
| TD-11.3 | Ensure calling methods are properly async | 1 hour | TD-11.1, TD-11.2 | **Done** |
| TD-11.4 | Add CancellationToken support to delay operations | 1 hour | TD-11.3 | **Done** |

### TD-12: Consolidate Duplicate Domain Models
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| TD-12.1 | Identify all imports of `Domain.Models` namespace | 1 hour | None |
| TD-12.2 | Create migration script for namespace changes | 2 hours | TD-12.1 |
| TD-12.3 | Update 40+ files to use `Contracts.Domain.Models` | 2 hours | TD-12.2 |
| TD-12.4 | Delete duplicate models from `Domain/Models/` | 0.5 hour | TD-12.3 |
| TD-12.5 | Update tests and verify builds | 1 hour | TD-12.4 |
| TD-12.6 | Consider global using directives | 0.5 hour | TD-12.3 |

**Note:** This affects 12 duplicate models across Domain and Contracts. See `docs/analysis/DUPLICATE_CODE_ANALYSIS.md` for details.

### TD-13: Migrate Remaining Providers to BaseHistoricalDataProvider
| Sub-ID | Task | Effort | Dependencies | Status |
|--------|------|--------|--------------|--------|
| TD-13.1 | Migrate Alpaca Historical Provider | 2 hours | None | Pending |
| TD-13.2 | Migrate Tiingo Historical Provider | 2 hours | None | Pending |
| TD-13.3 | Migrate Finnhub Historical Provider | 2 hours | None | Pending |
| TD-13.4 | Migrate Polygon Historical Provider | 2 hours | None | Pending |
| TD-13.5 | Update provider tests | 2 hours | TD-13.1-TD-13.4 | Pending |

**Completed Migrations:** Stooq, Yahoo Finance, Nasdaq Data Link, Alpha Vantage

### TD-14: UWP Service Naming Conflicts
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| TD-14.1 | Rename `AnalysisExportService` to `UwpAnalysisExportService` | 0.5 hour | None |
| TD-14.2 | Rename `DataQualityService` to `UwpDataQualityService` | 0.5 hour | None |
| TD-14.3 | Update all UWP references | 1 hour | TD-14.1, TD-14.2 |
| TD-14.4 | Document naming convention for UWP services | 0.5 hour | TD-14.3 |

### ADQ-2: Reference Price Validator
| Sub-ID | Task | Effort | Dependencies |
|--------|------|--------|--------------|
| ADQ-2.1 | Create ReferencePriceConfig model | 0.5 hour | None |
| ADQ-2.2 | Add reference price lookup interface | 1 hour | None |
| ADQ-2.3 | Implement Yahoo Finance reference lookup | 2 hours | ADQ-2.2 |
| ADQ-2.4 | Create ReferencePriceValidator class | 2 hours | ADQ-2.2 |
| ADQ-2.5 | Integrate validator into DataQualityMonitoringService | 1 hour | ADQ-2.4 |
| ADQ-2.6 | Add deviation threshold configuration | 0.5 hour | ADQ-2.1 |
| ADQ-2.7 | Add unit tests | 2 hours | ADQ-2.4 |

### ADQ-4: Data Freshness SLA Monitor
| Sub-ID | Task | Effort | Dependencies | Status |
|--------|------|--------|--------------|--------|
| ADQ-4.1 | Create SlaConfig model with thresholds | 0.5 hour | None | **Done** |
| ADQ-4.2 | Create DataFreshnessSlaMonitor class | 2 hours | ADQ-4.1 | **Done** |
| ADQ-4.3 | Track last event timestamp per symbol | 1 hour | ADQ-4.2 | **Done** |
| ADQ-4.4 | Implement SLA violation detection | 1 hour | ADQ-4.2, ADQ-4.3 | **Done** |
| ADQ-4.5 | Add SLA metrics (sla_violations, freshness_ms) | 1 hour | ADQ-4.4 | **Done** |
| ADQ-4.6 | Add `/api/sla/status` endpoint | 1 hour | ADQ-4.2 | **Done** |
| ADQ-4.7 | Add unit tests | 1 hour | ADQ-4.4 | **Done** |

---

## Sprint Roadmap

### Sprint 1: Critical Foundation - **COMPLETE**
- [x] QW-1: Stale Data Detector
- [x] QW-3: Connection Health Heartbeat
- [x] MON-16/17: Disk/Memory Warnings
- [x] QW-4: Trading Calendar Integration
- [x] QW-22: Pre-flight Checks
- [x] QW-2: Config Validator CLI
- [x] QW-5: Daily Summary Webhook
- [x] DQ-12: Crossed Market Detector
- [x] DQ-15: Timestamp Monotonicity
- [x] QW-30: Graceful Shutdown with Event Flush

### Sprint 2: Data Quality & Alerts - **COMPLETE**
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| QW-6 | Price Spike Alert | 1 day | P1 | **Done** |
| QW-7 | Spread Monitor | 1 day | P1 | **Done** |
| DQ-2 | Duplicate Event Detector | 1 day | P1 | **Done** |
| DQ-20 | Bad Tick Filter | 1 day | P1 | **Done** |
| QW-32 | Detailed Health Check Endpoint | 1 day | P1 | **Done** |
| MON-18 | Backpressure Alert | 1 day | P1 | **Done** |
| MON-6 | Connection Status Webhook | 1 day | P1 | **Done** |

### Sprint 3: Developer Experience - **IN PROGRESS**
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| QW-16 | Diagnostic Bundle Generator | 1 day | P1 | **Done** |
| TD-1 | Replace double with decimal | 3 days | P0 | **Done** |
| QW-15 | Query Endpoint for Historical Data | 2 days | P1 | Pending |
| DEV-9 | API Explorer / Swagger UI | 2 days | P2 | Pending |
| QW-17 | Sample Data Generator | 1.5 days | P2 | **Done** |

### Sprint 4: Performance & Export
| ID | Feature | Effort | Priority |
|----|---------|--------|----------|
| PERF-1 | Batch Write Optimization | 1 day | P1 |
| PERF-2 | Memory-Mapped File Reader | 1 day | P1 |
| ARCH-1 | Dead Letter Queue | 3 days | P1 |
| ARCH-2 | gRPC Streaming Endpoints | 1 week | P1 |

### Sprint 5: Code Quality & Refactoring - **COMPLETE**
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| TD-9 | Fix async void methods | 2 days | P0 | **Done** |
| TD-10 | Implement IHttpClientFactory | 2 days | P0 | **Done** |
| TD-11 | Replace Thread.Sleep with Task.Delay | 0.5 day | P0 | **Done** |
| REF-1 | Create BaseHistoricalDataProvider | 1 day | P1 | **Done** |
| REF-2 | Create shared utilities (SymbolNormalization, etc.) | 1 day | P1 | **Done** |
| REF-3 | Migrate providers to base class | 2 days | P1 | **Partial** (4/8) |
| REF-4 | HttpResponseHandler for error handling | 0.5 day | P1 | **Done** |
| REF-5 | SharedResiliencePolicies | 0.5 day | P1 | **Done** |

### Sprint 6: Code Consolidation (Next)
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| TD-12 | Consolidate duplicate domain models | 1.5 days | P1 | Pending |
| TD-13 | Migrate remaining providers to base class | 1 day | P1 | Pending |
| QW-15 | Query Endpoint for Historical Data | 2 days | P1 | Pending |
| DEV-9 | API Explorer / Swagger UI | 2 days | P2 | Pending |

---

## Provider Status

| Provider | Streaming | Historical | Status |
|----------|-----------|------------|--------|
| Alpaca | Yes | Yes | **Production Ready** |
| Interactive Brokers | Yes | No | Requires `IBAPI` build flag |
| Yahoo Finance | No | Yes | **Production Ready** |
| Stooq | No | Yes | **Production Ready** |
| Tiingo | No | Yes | **Production Ready** |
| Finnhub | No | Yes | **Production Ready** |
| Alpha Vantage | No | Yes | **Production Ready** |
| Nasdaq Data Link | No | Yes | **Production Ready** |
| Polygon | No | Yes | **Production Ready** |
| NYSE | Stub | No | WebSocket implementation needed |

---

## Feature Categories

### Quick Wins (≤2 Days) - Highest ROI

#### Health & Monitoring
| ID | Feature | Effort | Impact | Status |
|----|---------|--------|--------|--------|
| QW-32 | Detailed Health Check Endpoint | 1 day | High | **Done** |
| QW-33 | Dependency Health Checks | 1 day | High | **Done** |
| QW-58 | Last N Errors Endpoint | 0.5 day | High | **Done** |
| QW-82 | Events Per Second Gauge | 0.5 day | High | **Done** |
| QW-87 | Latency Percentiles (P50/P95/P99) | 1 day | High | **Done** |

#### Data Validation
| ID | Feature | Effort | Impact | Status |
|----|---------|--------|--------|--------|
| QW-107 | Negative Price Detector | 0.5 day | High | **Done** |
| QW-109 | Future Timestamp Detector | 0.5 day | High | **Done** |
| QW-112 | Sequence Gap Counter | 1 day | High | **Done** |
| DQ-13 | Tick Size Validator | 0.5 day | Medium | **Done** |
| DQ-16 | Price Continuity Checker | 1 day | Medium | **Done** |

#### CLI & Configuration
| ID | Feature | Effort | Impact | Status |
|----|---------|--------|--------|--------|
| QW-53 | Log Level Runtime Toggle | 0.5 day | High | **Done** |
| QW-93 | Dry Run Mode | 1 day | High | **Done** |
| QW-95 | Config Path Override | 0.5 day | High | **Done** |
| QW-78 | Sensitive Value Masking | 0.5 day | High | **Done** |
| QW-76 | Config Template Generator | 1 day | High | **Done** |

### Provider Integration
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| PROV-7 | Polygon.io WebSocket | 1 week | P2 | Pending |
| PROV-20 | Coinbase/Kraken Crypto | 1 week | P2 | Pending |
| PROV-22 | Provider Config Wizard | 2 days | P1 | Pending |
| PROV-11 | Provider Latency Histogram | 1 day | P1 | **Done** |
| PROV-4 | Multi-Provider Failover | 3 days | P1 | Pending |

### Storage & Archival
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| STO-3 | Portable Data Packager | 2 weeks | P1 | **Done** |
| STO-4 | Archive Browser & Inspector | 2-3 weeks | P1 | Pending |
| STO-5 | Batch Write Optimization | 1 day | P1 | Pending |
| STO-7 | Storage Optimization Advisor | 2 weeks | P2 | Pending |
| STO-11 | Tiered Storage Migration | 3 weeks | P2 | Pending |

### Code Refactoring & Quality
| ID | Feature | Effort | Priority | Status |
|----|---------|--------|----------|--------|
| REF-1 | BaseHistoricalDataProvider base class | 1 day | P1 | **Done** |
| REF-2 | SymbolNormalization utility | 0.5 day | P1 | **Done** |
| REF-3 | HttpResponseHandler utility | 0.5 day | P1 | **Done** |
| REF-4 | SharedResiliencePolicies (Polly) | 0.5 day | P1 | **Done** |
| REF-5 | SubscriptionManager base class | 0.5 day | P1 | **Done** |
| REF-6 | EventBuffer<T> generic class | 0.5 day | P1 | **Done** |
| REF-7 | StorageChecksumService | 0.5 day | P1 | **Done** |
| REF-8 | CredentialValidator utility | 0.5 day | P1 | **Done** |
| REF-9 | BaseStreamingDataClient | 1 day | P2 | Pending |
| REF-10 | RetentionPolicyManager consolidation | 1 day | P2 | Pending |

### Architecture & Infrastructure
| ID | Feature | Effort | Priority |
|----|---------|--------|----------|
| ARCH-1 | Dead Letter Queue | 3 days | P1 |
| ARCH-2 | gRPC Streaming Endpoints | 1 week | P1 |
| ARCH-3 | Time-Series Database Integration | 2 weeks | P1 |
| ARCH-4 | Kubernetes Helm Chart | 1 week | P2 |
| ARCH-6 | Event Sourcing with CQRS | 3 weeks | P2 |

---

## Proposed Future Features

These are new feature ideas for consideration in upcoming development cycles.

### Data Analysis & Research
| ID | Feature | Effort | Priority | Description |
|----|---------|--------|----------|-------------|
| RES-1 | Backtest Data Validator | 1 week | P1 | Validate data quality before backtesting, detect survivorship bias |
| RES-2 | Market Microstructure Dashboard | 2 weeks | P2 | Visualize order flow, bid-ask dynamics, and trade imbalances |
| RES-3 | Corporate Actions Tracker | 1 week | P2 | Track splits, dividends, mergers affecting historical data |
| RES-4 | Symbol Universe Manager | 3 days | P1 | Manage symbol lists, sectors, indices with change history |
| RES-5 | Data Lineage Tracker | 1 week | P2 | Track data provenance from source to storage |

### Automation & Scheduling
| ID | Feature | Effort | Priority | Description |
|----|---------|--------|----------|-------------|
| AUTO-1 | Scheduled Backfill Jobs | 3 days | P1 | Cron-like scheduling for periodic historical data updates |
| AUTO-2 | Market Hours Aware Scheduler | 2 days | P1 | Start/stop collection based on market calendars |
| AUTO-3 | Automated Gap Recovery | 1 week | P1 | Detect and automatically backfill data gaps |
| AUTO-4 | Provider Rotation Scheduler | 3 days | P2 | Rotate between providers to optimize rate limits |

### Integration & Interoperability
| ID | Feature | Effort | Priority | Description |
|----|---------|--------|----------|-------------|
| INT-1 | Jupyter Notebook Integration | 1 week | P1 | IPython magic commands for data access |
| INT-2 | REST API for Remote Access | 1 week | P1 | Full REST API for headless deployments |
| INT-3 | Webhook Notifications | 2 days | P2 | Configurable webhooks for events (gaps, errors, completions) |
| INT-4 | Apache Kafka Integration | 2 weeks | P2 | Stream market data to Kafka topics |
| INT-5 | Redis Cache Layer | 1 week | P2 | Cache recent data for low-latency access |

### Advanced Data Quality
| ID | Feature | Effort | Priority | Description | Status |
|----|---------|--------|----------|-------------|--------|
| ADQ-1 | Machine Learning Anomaly Detection | 2 weeks | P2 | ML-based detection of unusual patterns | Pending |
| ADQ-2 | Reference Price Validator | 3 days | P1 | Cross-validate prices against multiple sources | Pending |
| ADQ-3 | Volume Spike/Drop Detector | 1 day | P1 | Alert on unusual volume patterns (high & low) | **Done** |
| ADQ-4 | Data Freshness SLA Monitor | 2 days | P1 | Track and alert on data delivery delays | **Done** |

### Developer Experience
| ID | Feature | Effort | Priority | Description |
|----|---------|--------|----------|-------------|
| DX-1 | Interactive CLI (REPL) | 1 week | P2 | Interactive mode for exploration and debugging |
| DX-2 | VS Code Extension | 2 weeks | P3 | Syntax highlighting and IntelliSense for config files |
| DX-3 | Data Preview Tool | 3 days | P2 | Quick preview of stored data with filtering |
| DX-4 | Provider Sandbox Mode | 1 week | P2 | Test provider integrations with simulated data |

---

## Cloud Integration Roadmap

### AWS (47 features planned)
**Priority Items:**
- S3 Storage Backend with lifecycle policies
- Kinesis Data Streams for real-time distribution
- Secrets Manager integration
- CloudWatch Logs and Metrics
- ECS/Fargate deployment with auto-scaling

### Azure (29 features planned)
**Priority Items:**
- Blob Storage backend with tiering
- Event Hubs streaming
- Key Vault integration
- Azure Data Explorer (Kusto) for analytics
- AKS deployment

### GCP (25 features planned)
**Priority Items:**
- Cloud Storage backend
- Pub/Sub integration
- BigQuery integration
- Secret Manager
- Cloud Run/GKE deployment

---

## Recommended Next Steps

Based on the current state of the project, the following items are recommended for the next development cycle:

### Immediate Priorities (Sprint 6)
1. **TD-12: Domain Model Consolidation** - Eliminate 12 duplicate models between `Domain/` and `Contracts/`
2. **TD-13: Complete Provider Migration** - Migrate remaining 4 providers to `BaseHistoricalDataProvider`
3. **QW-15: Historical Data Query Endpoint** - Enable querying stored data via HTTP API

### Short-term Goals
1. **REF-9: BaseStreamingDataClient** - Apply same refactoring pattern to streaming providers
2. **DEV-9: Swagger/OpenAPI** - Improve API discoverability
3. **PROV-7: Polygon WebSocket** - Complete the stub implementation

### Technical Debt Focus
- All P0 technical debt items are now resolved (TD-9, TD-10, TD-11)
- Remaining items are medium priority code quality improvements
- See `docs/analysis/DUPLICATE_CODE_ANALYSIS.md` for detailed refactoring guidance

---

## Priority Legend

- **P0** = Critical (blocks production use)
- **P1** = High (significant user impact)
- **P2** = Medium (nice to have)
- **P3** = Low (polish/convenience)
- **P4** = Future (long-term roadmap)

## Effort Guidelines

- **0.5 day**: Simple config change, single endpoint
- **1 day**: Single feature with tests
- **2 days**: Feature with multiple components
- **1 week**: Significant feature with documentation
- **2+ weeks**: Major feature or architectural change

---

## Related Documentation

- [Production Status](production-status.md) - Deployment readiness assessment
- [Changelog](CHANGELOG.md) - Recent changes and improvements
- [Duplicate Code Analysis](../analysis/DUPLICATE_CODE_ANALYSIS.md) - Code consolidation opportunities
- [Architecture Overview](../architecture/overview.md) - System design
- [Getting Started](../guides/getting-started.md) - Setup guide
- [Configuration](../guides/configuration.md) - Config reference

---

*This is a living document. Last updated: 2026-01-27. Review and update priorities quarterly based on user feedback and operational needs.*
