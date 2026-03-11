# Market Data Collector - Changelog

**Last Updated:** 2026-03-11
**Current Version:** 1.0.0

This changelog summarizes the current repository snapshot. Historical release notes are not curated in this repo; use git history for detailed diffs.

---

## Current Snapshot (2026-03-11)

### Project Scale
- 743 source files (729 C#, 14 F#), 254 test files (248 C#, 6 F#), 148 documentation files
- 13 main projects, 4 test projects, 1 benchmark project, 2 build tool projects
- 25 CI/CD workflows, 106 Makefile targets, 300 API route constants

### Core Runtime
- CLI modes for real-time collection, backfill, replay, packaging, and validation
- Auto-configuration support (`--wizard`, `--auto-config`, provider detection, credential validation)
- HTTP status server with Prometheus metrics and HTML dashboard
- Dry-run mode for validation without starting collection
- Contextual CLI help system (`--help <topic>` for 7 topics)

### Storage & Data Management
- JSONL and Parquet storage sinks with configurable naming conventions (BySymbol, ByDate, ByType, Flat)
- Write-ahead logging (WAL) for archival-first persistence
- Portable data package creation/import with manifests and checksums
- Tiered storage (hot/warm/cold) with automatic migration
- Composite storage sink with per-sink fault isolation (JSONL + Parquet simultaneously)

### Providers
- Alpaca streaming provider (credentials required)
- Interactive Brokers provider (requires IBAPI build flag) with simulation client
- Polygon provider (stub/partial streaming without credentials)
- NYSE provider (credentials required)
- StockSharp streaming and historical provider
- Failover-aware client for automatic provider switching
- Historical backfill from 10 providers: Alpaca, Polygon, Tiingo, Yahoo Finance, Stooq, Finnhub, Alpha Vantage, Nasdaq Data Link, Interactive Brokers, StockSharp
- Symbol search from 5 providers: Alpaca, Finnhub, Polygon, OpenFIGI, StockSharp

### UI & Integrations
- Web dashboard for status/metrics and API-backed backfill actions
- WPF desktop application with 51 pages (Windows only; most pages wired to live services; a few show placeholder data)
- UWP desktop application removed (WPF is the sole desktop client)
- Shared UI services project (`MarketDataCollector.Ui.Services`)
- QuantConnect Lean integration types and data provider

### Data Quality & Monitoring
- Comprehensive data quality monitoring with SLA enforcement
- Completeness scoring, gap analysis, anomaly detection
- Cross-provider data comparison
- Latency distribution tracking
- Dropped event audit trail with HTTP API exposure
- Provider degradation scoring for intelligent failover

### Data Canonicalization
- Cross-provider canonical field model (`CanonicalSymbol`, `CanonicalVenue`, `CanonicalizationVersion`) on `MarketEvent`
- `EventCanonicalizer`, `ConditionCodeMapper` (44 codes across 3 providers), `VenueMicMapper` (63 venues → ISO 10383 MIC)
- `CanonicalizingPublisher` decorator with DI wiring and lock-free metrics
- Canonicalization API endpoints (`/api/canonicalization/status`, `/parity`, `/config`)
- Golden fixture test suite (8 curated fixtures + `CanonicalizationGoldenFixtureTests`); drift-canary CI pending (J8)

### Testing & Quality
- 254 test files across 4 test projects with ~3,858 test methods
- Core tests: ~2,508 methods (backfill, storage, pipeline, monitoring, providers, domain, integration)
- F# tests: 99 methods (domain validation, calculations, transforms)
- WPF desktop service tests: 324 methods (navigation, config, status, connection)
- Desktop UI service tests: 927 methods (API client, backfill, fixtures, forms, health, watchlist)
- Provider test files covering all streaming providers + failover + backfill
- Negative-path and schema validation endpoint integration tests
- Integration test harness with fixture providers for full-pipeline testing

### Observability
- OpenTelemetry pipeline instrumentation via `TracedEventMetrics` decorator
- Typed OpenAPI response annotations across all endpoint families
- API authentication (API key) and rate limiting middleware
- Category-accurate process exit codes for CI/CD integration

### Improvement Tracking (as of 2026-03-11)
- 33/35 core improvement items completed (94.3%)
- Themes H (Scalability) and I (Developer Experience): 6 of 8 items complete; I3 partial, H2 open
- Theme J (Data Canonicalization): 7 of 8 items complete; J8 (golden fixture drift-canary CI) partial
- Remaining open items: C3 (WebSocket base class), H2 (multi-instance coordination)
- Remaining partial items: G2 (trace propagation), I3 (config JSON schema), J8 (drift-canary CI)

### Recent Changes (since 2026-02-22)
- Documentation consolidated and automation updated
- Data canonicalization pipeline (J2–J7) fully implemented
- Event replay infrastructure added (`JsonlReplayer`, `MemoryMappedJsonlReader`, `EventReplayService`)
- CLI progress reporting (`ProgressDisplayService`) with ETA, throughput, and spinner support
- Additional test coverage across application, domain, infrastructure, storage, and integration areas
- Makefile expanded to 106 targets (was 78)
- API route surface expanded to 300 constants (was 283)
- WPF views expanded to 51 pages (was ~49)

---

## Previous Snapshot (2026-02-22)

### Project Scale
- 664 source files (652 C#, 12 F#), 219 test files (215 C#, 4 F#), 135 documentation files
- 13 main projects, 4 test projects, 1 benchmark project, 2 build tool projects
- 22 CI/CD workflows, 78 Makefile targets, 283 API route constants

---

## Previous Snapshot (2026-02-22)

### Project Scale
- 664 source files (652 C#, 12 F#), 219 test files (215 C#, 4 F#), 135 documentation files
- 13 main projects, 4 test projects, 1 benchmark project, 2 build tool projects
- 22 CI/CD workflows, 78 Makefile targets, 283 API route constants

---

## Previous Snapshot (2026-02-20)

### Project Scale
- 647 source files (635 C#, 12 F#), 164 test files (160 C#, 4 F#), 133 documentation files
- 13 main projects, 4 test projects, 1 benchmark project, 2 build tool projects
- 22 CI/CD workflows, 78 Makefile targets

---

## Previous Snapshot (2026-02-17)

### Project Scale
- 635 source files (623 C#, 12 F#), 163 test files, 130 documentation files
- 13 main projects, 4 test projects, 1 benchmark project, 2 build tool projects
- 22 CI/CD workflows, 72 Makefile targets

### Core Runtime
- CLI modes for real-time collection, backfill, replay, packaging, and validation
- Auto-configuration support (`--wizard`, `--auto-config`, provider detection, credential validation)
- HTTP status server with Prometheus metrics and HTML dashboard

---

## Previous Snapshot (2026-01-27)

### Core Runtime
- CLI modes for real-time collection, backfill, replay, packaging, and validation
- Auto-configuration support (`--wizard`, `--auto-config`, provider detection, credential validation)
- HTTP status server with Prometheus metrics and HTML dashboard

---

## Notes

- Version numbers are defined in project files (e.g., `src/MarketDataCollector/MarketDataCollector.csproj`).
- Use `docs/status/production-status.md` for readiness and implementation status details.
- Use `docs/status/IMPROVEMENTS.md` for detailed improvement item tracking.
