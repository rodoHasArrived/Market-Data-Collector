# Market Data Collector — Feature Inventory

**Version:** 1.6.2
**Date:** 2026-02-26
**Purpose:** Comprehensive inventory of every functional area, its current implementation status, and the remaining work required to reach full implementation.

Use this document alongside [`ROADMAP.md`](ROADMAP.md) (sprint schedule) and [`IMPROVEMENTS.md`](IMPROVEMENTS.md) (per-item tracking).

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Fully implemented and tested |
| ⚠️ | Partially implemented — functional with caveats |
| 🔑 | Requires external credentials / build flag |
| 🔄 | Framework in place, one or more sub-features pending |
| 📝 | Planned, not yet started |

---

## 1. Core Infrastructure

| Feature | Status | Notes |
|---------|--------|-------|
| Event Pipeline (`System.Threading.Channels`) | ✅ | Bounded channel, backpressure, 100 K capacity, nanosecond timing |
| Injectable `IEventMetrics` | ✅ | Static dependency removed; `TracedEventMetrics` decorator available |
| `CompositeSink` fan-out | ✅ | Per-sink fault isolation; JSONL + Parquet simultaneously |
| Write-Ahead Log (WAL) | ✅ | SHA-256 checksums, streaming recovery, uncommitted-size warnings |
| Provider Registry & DI | ✅ | `[DataSource]` scanning, `ProviderRegistry`, `ServiceCompositionRoot` |
| Config Validation Pipeline | ✅ | `ConfigValidationPipeline` with composable stages; obsoletes `ConfigValidationHelper` |
| Graceful Shutdown | ✅ | `GracefulShutdownService`, provider disconnect, flush-to-disk before exit |
| Category-accurate exit codes | ✅ | `ErrorCode.FromException()` maps to codes 3–7 for CI/CD differentiation |
| Dry-run mode (`--dry-run`) | ✅ | Full validation without starting collection; `--dry-run --offline` skips connectivity |
| Configuration hot-reload (`--watch-config`) | ✅ | `ConfigWatcher` triggers live config update |

---

## 2. Streaming Data Providers

| Provider | Status | Remaining Work |
|----------|--------|----------------|
| **Alpaca** | ✅ | Credential validation, automatic resubscription on reconnect, quote routing |
| **Interactive Brokers** | 🔑 | Build with `-p:DefineConstants=IBAPI`; stub throws `NotSupportedException` without flag |
| **Polygon** | ⚠️ | Real connection when API key present; stub mode (synthetic heartbeat/trades) without key. WebSocket parsing functional but not battle-tested against full production feed |
| **NYSE** | 🔑 | Requires NYSE Connect credentials; provider implementation complete |
| **StockSharp** | 🔑 | Requires StockSharp connector-specific credentials + connector type config. `NotSupportedException` on some tick subscription paths when connector type unset |
| **Failover-Aware Client** | ✅ | `FailoverAwareMarketDataClient` with `ProviderDegradationScorer`, per-provider health |
| **IB Simulation Client** | ✅ | `IBSimulationClient` for testing without live connection |
| **NoOp Client** | ✅ | `NoOpMarketDataClient` for dry-run / test harness scenarios |

### Remaining work to reach full provider coverage

- **Polygon**: Validate WebSocket message parsing against Polygon v2 feed schema (trades, quotes, aggregates, status messages). Add round-trip integration test with a recorded WebSocket session replay.
- **StockSharp**: Document the `ConnectorType` configuration options (QuikJSon, Transaq, etc.) and which require external connectors. Add a validated configuration example per connector type.
- **IB**: Provide scripted build instructions for IBAPI (`download → reference → define constant`). Add smoke-test CI job that builds with IBAPI constant mocked.
- **C3 (all WebSocket providers)**: Adopt `WebSocketProviderBase` in Polygon, NYSE, StockSharp to eliminate ~800 LOC of duplicated connection-management code.

---

## 3. Historical Backfill Providers

| Provider | Status | Notes |
|----------|--------|-------|
| Alpaca | ✅ | Daily bars, trades, quotes; credentials required |
| Polygon | ✅ | Daily bars and aggregates; API key required |
| Tiingo | ✅ | Daily bars; token required |
| Yahoo Finance | ✅ | Daily bars; unofficial API, no credentials |
| Stooq | ✅ | Daily bars; free, no credentials |
| Finnhub | ✅ | Daily bars; token required |
| Alpha Vantage | ✅ | Daily bars; API key required |
| Nasdaq Data Link (Quandl) | ✅ | Various; API key required |
| Interactive Brokers | 🔑 | Full implementation behind `IBAPI` compile constant |
| StockSharp | ✅ | Via StockSharp connectors; requires StockSharp setup |
| **Composite Provider** | ✅ | Priority-based fallback chain, rate-limit tracking, per-provider health |
| **Gap Backfill Service** | ✅ | `GapBackfillService` triggered on reconnect; uses `WebSocketReconnectionHelper` gap window |
| **Backfill Rate Limiting** | ✅ | `ProviderRateLimitTracker` per provider; exponential backoff with `Retry-After` parsing |
| **Backfill Scheduling** | ✅ | Cron-based `ScheduledBackfillService`; `BackfillScheduleManager` with CRUD API |
| **Backfill Progress Reporting** | ✅ | `BackfillProgressTracker`, per-symbol %, exposed at `/api/backfill/progress` |

---

## 4. Symbol Search

| Provider | Status | Notes |
|----------|--------|-------|
| Alpaca | ✅ | `AlpacaSymbolSearchProviderRefactored`; US equities + crypto |
| Finnhub | ✅ | `FinnhubSymbolSearchProviderRefactored`; US + international |
| Polygon | ✅ | `PolygonSymbolSearchProvider`; US equities |
| OpenFIGI | ✅ | `OpenFigiClient`; global instrument ID mapping |
| StockSharp | ✅ | `StockSharpSymbolSearchProvider`; multi-exchange |
| **Symbol Import/Export** | ✅ | CSV import/export via `SymbolImportExportService`; portfolio import |
| **Symbol Registry** | ✅ | `CanonicalSymbolRegistry` with persistence; `SymbolRegistryService` |
| **Symbol Normalization** | ✅ | `SymbolNormalization` utility; PCG-PA, BRK.A, ^GSPC, =SPX patterns |

---

## 5. Data Canonicalization

| Component | Status | Notes |
|-----------|--------|-------|
| Design document & field audit | ✅ | `docs/architecture/deterministic-canonicalization.md` |
| `MarketEvent` canonical fields | ✅ | `CanonicalSymbol`, `CanonicalizationVersion`, `CanonicalVenue`, `EffectiveSymbol` |
| `EventCanonicalizer` implementation | ✅ | Symbol resolution, venue normalization, typed payload extraction |
| `ConditionCodeMapper` — Alpaca (17 codes) | ✅ | CTA plan codes → `CanonicalTradeCondition`; `FrozenDictionary` |
| `ConditionCodeMapper` — Polygon (19 codes) | ✅ | SEC numeric codes → canonical |
| `ConditionCodeMapper` — IB (8 codes) | ✅ | IB field codes → canonical |
| `VenueMicMapper` — Alpaca (29 venues) | ✅ | Text names → ISO 10383 MIC |
| `VenueMicMapper` — Polygon (17 venues) | ✅ | Numeric IDs → MIC |
| `VenueMicMapper` — IB (17 venues) | ✅ | Routing names → MIC |
| `CanonicalizingPublisher` decorator | ✅ | Wraps `IMarketEventPublisher`; dual-write mode; lock-free metrics |
| Canonicalization metrics & API endpoints | ✅ | `/api/canonicalization/status`, `/parity`, `/parity/{provider}`, `/config` |
| Golden fixture test suite | 🔄 | 8 curated `.json` fixtures + `CanonicalizationGoldenFixtureTests`; **drift-canary CI job pending** |

### Remaining work

- **J8 drift canary**: Add a GitHub Actions CI job that runs `CanonicalizationGoldenFixtureTests` and fails when new unmapped condition codes or venues appear in provider feeds. Requires a recorded-fixture refresh mechanism.

---

## 6. Storage & Data Management

| Feature | Status | Notes |
|---------|--------|-------|
| JSONL storage sink | ✅ | Append-only, gzip-compressed, configurable naming conventions |
| Parquet storage sink | ✅ | Columnar, compressed; enabled via `EnableParquetSink` config |
| Tiered storage (hot/warm/cold) | ✅ | `TierMigrationService` with configurable retention per tier |
| Scheduled archive maintenance | ✅ | `ScheduledArchiveMaintenanceService`; tasks: integrity, orphan cleanup, index rebuild, compression |
| Portable data packaging | ✅ | `PortableDataPackager`; ZIP/tar.gz with manifest, checksums, SQL loaders |
| Package import | ✅ | `--import-package`, merge mode |
| Package validation | ✅ | SHA-256 integrity, schema compatibility checks |
| Storage quota enforcement | ✅ | `QuotaEnforcementService`; configurable max total and per-symbol limits |
| Data lifecycle policies | ✅ | `LifecyclePolicyEngine`; tag-based retention policies |
| Storage checksums | ✅ | `StorageChecksumService`; per-file SHA-256 tracking |
| Metadata tagging | ✅ | `MetadataTagService`; background save pattern; tag-based search |
| Analysis export (JSONL/Parquet/Arrow/XLSX/CSV) | ✅ | `AnalysisExportService`; configurable format, symbol filter, date range |
| Storage catalog | ✅ | `StorageCatalogService`; file inventory, symbol listing |
| Event replay | ✅ | `JsonlReplayer`, `MemoryMappedJsonlReader`, `EventReplayService`; pause/resume/seek; CLI `--replay` |
| File permissions service | ✅ | `FilePermissionsService`; cross-platform directory permission checks |
| Data lineage tracking | ✅ | `DataLineageService`; provenance chain per data file |
| Data quality scoring | ✅ | `DataQualityScoringService`; per-symbol quality scores |

---

## 7. Data Quality Monitoring

| Feature | Status | Notes |
|---------|--------|-------|
| Completeness scoring | ✅ | `CompletenessScoreCalculator`; expected vs. received events |
| Gap analysis | ✅ | `GapAnalyzer`; liquidity-adjusted severity (Minor → Critical) |
| Anomaly detection | ✅ | `AnomalyDetector`; price/volume outliers |
| Sequence error tracking | ✅ | `SequenceErrorTracker`; out-of-order and duplicate event detection |
| Cross-provider comparison | ✅ | `CrossProviderComparisonService` |
| Latency distribution | ✅ | `LatencyHistogram`; p50/p90/p99 tracking |
| Data freshness SLA monitoring | ✅ | `DataFreshnessSlaMonitor`; configurable thresholds, violation API |
| Quality report generation | ✅ | `DataQualityReportGenerator`; daily/on-demand reports |
| Dropped event audit trail | ✅ | `DroppedEventAuditTrail`; JSONL log + `/api/quality/drops` API |
| Bad tick filter | ✅ | `BadTickFilter`; placeholder price detection, spread sanity |
| Tick size validation | ✅ | `TickSizeValidator` |
| Spread monitoring | ✅ | `SpreadMonitor`; bid/ask spread alerts |
| Clock skew estimation | ✅ | `ClockSkewEstimator` |
| Timestamp monotonicity checking | ✅ | `TimestampMonotonicityChecker` |
| Backpressure alerts | ✅ | `BackpressureAlertService`; `/api/backpressure` endpoint |
| Provider degradation scoring | ✅ | `ProviderDegradationScorer`; composite health from latency, errors, reconnects |

---

## 8. API Surface (HTTP)

| Area | Routes | Status |
|------|--------|--------|
| Status & health | `/api/status`, `/api/health`, `/healthz`, `/readyz`, `/livez` | ✅ |
| Configuration | `/api/config/*` (8 endpoints) | ✅ |
| Providers | `/api/providers/*`, `/api/connections` | ✅ |
| Failover | `/api/failover/*` | ✅ |
| Backfill | `/api/backfill/*` (13 endpoints) | ✅ |
| Quality | `/api/quality/*`, `/api/sla/*` | ✅ |
| Maintenance | `/api/maintenance/*` | ✅ |
| Storage | `/api/storage/*` | ✅ |
| Symbols | `/api/symbols/*` | ✅ |
| Live data | `/api/live/*` | ✅ |
| Export | `/api/export/*` | ✅ |
| Packaging | `/api/packaging/*` | ✅ |
| Canonicalization | `/api/canonicalization/*` | ✅ |
| Diagnostics | `/api/diagnostics/*` | ✅ |
| Subscriptions | `/api/subscriptions/*` | ✅ |
| Historical | `/api/historical/*` | ✅ |
| Sampling | `/api/sampling/*` | ✅ |
| Alignment | `/api/alignment/*` | ✅ |
| IB-specific | `/api/ib/*` | ✅ |
| Metrics (Prometheus) | `/api/metrics` | ✅ |
| SSE stream | `/api/events/stream` | ✅ |
| OpenAPI / Swagger | `/swagger` | ✅ |
| API authentication | `X-Api-Key` header / `api_key` query | ✅ |
| Rate limiting | 120 req/min per key, sliding window | ✅ |
| **Total route constants** | **283** | **0 stubs remaining** |

### OpenAPI annotations

| Endpoint family | Typed `Produces<T>` | Descriptions | Status |
|-----------------|---------------------|--------------|--------|
| Status | ✅ | ✅ | ✅ |
| Health | ✅ | ✅ | ✅ |
| Config | ✅ | ✅ | ✅ |
| Backfill / Schedules | ✅ | ✅ | ✅ |
| Providers / Extended | ✅ | ✅ | ✅ |
| All other families | ✅ | ✅ | ✅ |

---

## 9. Web Dashboard

| Feature | Status | Notes |
|---------|--------|-------|
| HTML dashboard (auto-refreshing) | ✅ | `HtmlTemplateGenerator`; SSE-powered live updates |
| Server-Sent Events stream | ✅ | `/api/events/stream`; 2-second push cycle |
| Configuration wizard UI | ✅ | Interactive provider setup, credential entry, symbol config |
| Backfill controls | ✅ | Provider select, symbol list, date range, run/preview |
| Symbol management | ✅ | Add/remove symbols, status per symbol |
| Provider comparison table | ✅ | Feature matrix across all providers |
| Options chain display | ✅ | Derivatives configuration and data display |

---

## 10. WPF Desktop Application

### Shell & Navigation (✅ Complete)

- Workspace model (Monitor, Collect, Storage, Quality, Settings)
- Command palette (`Ctrl+K`), keyboard shortcuts
- Theme switching, notification center, info bar
- Offline indicator (single notification + warning on backend unreachable)
- Session state persistence (active workspace, last page, window bounds)

### Pages with live service connections (✅ Implemented)

| Page | Primary Service | Function |
|------|----------------|---------|
| DashboardPage | StatusService, ConnectionService | System overview, provider status |
| BackfillPage | BackfillService, BackfillApiService | Trigger/schedule backfills |
| DataSourcesPage | ConfigService, ProviderManagementService | Provider configuration |
| ProviderPage | ProviderManagementService | Provider detail + credentials |
| ProviderHealthPage | ProviderHealthService | Per-provider health metrics |
| SettingsPage | ConfigService, ThemeService | App settings |
| SymbolsPage | SymbolManagementService | Symbol list management |
| SymbolStoragePage | StorageServiceBase | Per-symbol storage view |
| SymbolMappingPage | SymbolMappingService | Cross-provider symbol mapping |
| DataQualityPage | DataQualityServiceBase | Quality metrics dashboard |
| DataSamplingPage | DataSamplingService | Data sampling configuration |
| DataCalendarPage | DataCalendarService | Calendar heat-map of collected dates |
| DataBrowserPage | ArchiveBrowserService | Browse stored data files |
| DataExportPage | AnalysisExportService | Export stored data |
| AnalysisExportPage | AnalysisExportService | Advanced export options |
| AnalysisExportWizardPage | AnalysisExportWizardService | Guided export workflow |
| ChartingPage | ChartingService | OHLCV chart display |
| LiveDataViewerPage | LiveDataService | Real-time tick viewer |
| OrderBookPage | OrderBookVisualizationService | L2 order book display |
| CollectionSessionPage | CollectionSessionService | Active session summary |
| ActivityLogPage | ApiClientService | Live event log |
| DiagnosticsPage | NavigationService, NotificationService | System diagnostics |
| SetupWizardPage | SetupWizardService | First-run onboarding |
| PackageManagerPage | PortablePackagerService | Create/import packages |
| ScheduleManagerPage | ScheduleManagerService | Backfill schedules |
| ServiceManagerPage | BackendServiceManagerBase | Backend service status |
| StorageOptimizationPage | StorageOptimizationAdvisorService | Storage optimization advice |
| ArchiveHealthPage | ArchiveHealthService | Archive integrity status |
| SystemHealthPage | SystemHealthService | Comprehensive health view |
| AdvancedAnalyticsPage | AdvancedAnalyticsServiceBase | Advanced analytics |
| EventReplayPage | EventReplayService | Historical event replay |
| ExportPresetsPage | ExportPresetServiceBase | Saved export profiles |
| LeanIntegrationPage | LeanIntegrationService | QuantConnect Lean integration |
| MessagingHubPage | (messaging infrastructure) | WebSocket messaging hub |
| NotificationCenterPage | NotificationService | Notification history |
| OptionsPage | (options infrastructure) | Options/derivatives data |
| PortfolioImportPage | PortfolioImportService | Portfolio CSV import |
| RetentionAssurancePage | (RetentionAssuranceService) | Retention policy status |
| TimeSeriesAlignmentPage | TimeSeriesAlignmentService | Multi-symbol time alignment |
| WorkspacePage | WorkspaceService | Workspace management |

### Pages requiring live-data wiring (⚠️ Static placeholder data)

| Page | Issue | Remaining Work |
|------|-------|----------------|
| StoragePage | Shows hardcoded "2.4 GB", "1,234,567 records", "45 symbols" | Connect to `StorageServiceBase` for real storage metrics |
| WelcomePage | Connection status, symbol count, storage path are placeholders | Wire to `StatusService` / `ConnectionService` / `ConfigService` |
| TradingHoursPage | Static XAML content | Verify `TradingCalendar` integration (market hours, holidays) |
| AdminMaintenancePage | *(partially connected via `AdminMaintenanceService`)* | Verify all sections load live data |
| WatchlistPage | *(WatchlistService present)* | Verify API connectivity |
| IndexSubscriptionPage | *(uses IndexSubscriptionService)* | Verify subscription wiring |

### Known WPF limitations

- Pages display static placeholder values until fully wired to live backend services (see table above).
- `DiagnosticsPage` reads from local process/environment; not connected to remote backend API.

---

## 11. CLI

| Feature | Status | Notes |
|---------|--------|-------|
| Real-time collection | ✅ | `--symbols`, `--no-trades`, `--no-depth`, `--depth-levels` |
| Backfill | ✅ | `--backfill`, `--backfill-provider`, `--backfill-symbols`, `--backfill-from/to` |
| Data packaging | ✅ | `--package`, `--import-package`, `--list-package`, `--validate-package` |
| Configuration wizard | ✅ | `--wizard`, `--auto-config`, `--detect-providers`, `--validate-credentials` |
| Dry-run | ✅ | `--dry-run`, `--dry-run --offline` |
| Self-test | ✅ | `--selftest` |
| Schema check | ✅ | `--check-schemas`, `--validate-schemas`, `--strict-schemas` |
| Configuration watch | ✅ | `--watch-config` |
| Contextual help | ✅ | `--help <topic>` for 7 topics |
| Symbol management | ✅ | `--symbols-add`, `--symbols-remove`, `--symbol-status` |
| Query | ✅ | `--query` for stored data |
| Event replay | ✅ | `--replay` |
| Generate loader | ✅ | `--generate-loader` |
| Progress reporting | ✅ | `ProgressDisplayService`; progress bars, spinners, checklists, tables |
| Error codes reference | ✅ | `--error-codes` |

---

## 12. Observability & Operations

| Feature | Status | Notes |
|---------|--------|-------|
| Prometheus metrics export | ✅ | `/api/metrics`; event throughput, provider health, backpressure, error rates |
| OpenTelemetry pipeline instrumentation | ✅ | `TracedEventMetrics` decorator; `MarketDataCollector.Pipeline` meter |
| Activity spans (batch consume, backfill, WAL recovery) | ✅ | `MarketDataTracing` extension methods |
| End-to-end trace context propagation | 🔄 | Framework complete; explicit cross-boundary wiring (provider → pipeline → storage) pending |
| Correlation IDs in structured logs | 📝 | Not yet implemented |
| API key authentication | ✅ | `ApiKeyMiddleware`; `MDC_API_KEY` env var; constant-time comparison |
| API rate limiting | ✅ | 120 req/min sliding window; `Retry-After` header on 429 |
| Kubernetes health probes | ✅ | `/healthz`, `/readyz`, `/livez` |
| Grafana/Prometheus deployment assets | ✅ | `deploy/monitoring/` with alert rules and dashboard provisioning |
| systemd service unit | ✅ | `deploy/systemd/marketdatacollector.service` |
| Docker image | ✅ | `deploy/docker/Dockerfile` + `docker-compose.yml` |
| Daily summary webhook | ✅ | `DailySummaryWebhook`; configurable endpoint |
| Connection status webhook | ✅ | `ConnectionStatusWebhook`; provider events |

### Remaining observability work

- **G2 (trace propagation)**: Wire `Activity` context from each provider's receive loop through the `EventPipeline` consumer to the storage write call. Add correlation ID to all `ILogger` structured log entries.
- **Jaeger/Zipkin export**: Document OTLP collector configuration for visual trace exploration.

---

## 13. F# Domain & Calculations

| Module | Status | Notes |
|--------|--------|-------|
| `MarketEvents.fs` — F# event types | ✅ | Discriminated union: `Trade`, `Quote`, `DepthUpdate`, `Bar`, `Heartbeat` |
| `Sides.fs` — bid/ask/neutral | ✅ | Type-safe aggressor side |
| `Integrity.fs` — sequence validation | ✅ | Gap detection, out-of-order |
| `Spread.fs` — bid-ask spread | ✅ | Absolute and relative spread calculations |
| `Imbalance.fs` — order book imbalance | ✅ | Bid/ask depth imbalance metric |
| `Aggregations.fs` — OHLCV | ✅ | Streaming bar aggregation |
| `Transforms.fs` — pipeline transforms | ✅ | Map, filter, window transforms |
| `QuoteValidator.fs` | ✅ | Price/size range validation |
| `TradeValidator.fs` | ✅ | Trade sequence and sanity validation |
| `ValidationPipeline.fs` | ✅ | Composable validation pipeline |
| C# Interop generated types | ✅ | `MarketDataCollector.FSharp.Interop.g.cs` |

---

## 14. QuantConnect Lean Integration

| Feature | Status | Notes |
|---------|--------|-------|
| Custom data types | ✅ | `LeanDataTypes.cs` — `Trade`, `Quote`, `OrderBook` Lean wrappers |
| `IDataProvider` implementation | ✅ | Reads stored JSONL/Parquet files as Lean data |
| Integration page (WPF) | ✅ | `LeanIntegrationPage` wires `LeanIntegrationService` |
| `LeanIntegrationService` | ✅ | Manages Lean engine connection and data feed |

---

## 15. Testing

| Test Project | Test Files | Methods | Focus |
|---|---|---|---|
| `MarketDataCollector.Tests` | ~110 | ~444 | Core: backfill, storage, pipeline, monitoring, providers, credentials, serialization, domain |
| `MarketDataCollector.FSharp.Tests` | 4 | ~99 | F# domain validation, calculations, transforms |
| `MarketDataCollector.Wpf.Tests` | ~20 | ~324 | WPF desktop services (navigation, config, status, connection) |
| `MarketDataCollector.Ui.Tests` | ~70 | ~927 | Desktop UI services (API client, backfill, fixtures, forms, health, watchlist) |
| **Total** | **219** | **~3,444** | |

### Key test infrastructure

| Feature | Status |
|---------|--------|
| `EndpointTestFixture` base (WebApplicationFactory) | ✅ |
| Negative-path endpoint tests (40+) | ✅ |
| Response schema validation tests (15+) | ✅ |
| `FixtureMarketDataClient` integration harness | ✅ |
| `InMemoryStorageSink` for pipeline integration | ✅ |
| Provider-specific test files (12 files, all providers) | ✅ |
| Canonicalization golden fixtures (8 curated files) | ✅ |
| Drift-canary CI job | 📝 |

---

## 16. Configuration Schema Validation

| Feature | Status | Notes |
|---------|--------|-------|
| `SchemaValidationService` — stored data format validation | ✅ | `--validate-schemas`, `--strict-schemas`, `--check-schemas` |
| `SchemaVersionManager` | ✅ | Per-event-type schema versioning |
| JSON Schema generation from C# config models | 📝 | I3 remainder: generate `appsettings.schema.json` from `AppConfig` for IDE auto-complete and config lint |

---

## Summary: Remaining Work to Full Implementation

### High priority (blocking full provider coverage)

| ID | Area | Effort | Description |
|----|------|--------|-------------|
| C3 | WebSocket Base | High | Refactor Polygon, NYSE, StockSharp to use `WebSocketProviderBase`; eliminates ~800 LOC duplication |
| — | Polygon validation | Medium | End-to-end test of WebSocket parsing against recorded production message samples |
| — | WPF StoragePage | Low | Replace static placeholder values with live storage metrics from `StorageServiceBase` |
| — | WPF WelcomePage | Low | Wire connection status, symbol count, storage path to real service data |

### Medium priority (observability & developer experience)

| ID | Area | Effort | Description |
|----|------|--------|-------------|
| G2 | OTel trace propagation | Medium | Wire `Activity` context provider → pipeline → storage; add correlation IDs to logs |
| J8 | Drift-canary CI | Low | Add CI job that detects new unmapped condition codes / venues from golden fixtures |
| I3 | Config JSON Schema | Low | Generate `appsettings.schema.json` from `AppConfig` for IDE validation |

### Low priority (architecture debt)

| ID | Area | Effort | Description |
|----|------|--------|-------------|
| H2 | Multi-instance coordination | High | Distributed locking for symbol subscriptions across multiple collector instances |
| — | DailySummaryWebhook state | Low | Persist `_dailyHistory` to disk using `MetadataTagService` save pattern |
| — | StockSharp documentation | Low | Document connector types and configuration examples |
| — | IB build instructions | Low | Scripted IBAPI download, reference, and build process |

---

## How to Read This Document

- **✅ Complete**: No action required; tested and in production code paths.
- **⚠️ Partial**: Works with caveats; see "Remaining Work" column.
- **🔑 Credentials/build flag required**: Implementation is complete but requires external setup (credentials, IBAPI download, StockSharp license).
- **🔄 Framework in place**: Core structure exists; specific sub-feature incomplete (e.g., G2 trace propagation).
- **📝 Planned**: Not started; see ROADMAP.md Phase schedule.

---

*Last Updated: 2026-02-26*
