# Market Data Collector - Changelog

**Last Updated:** 2026-02-22
**Current Version:** 1.6.2

This changelog summarizes the current repository snapshot. Historical release notes are not curated in this repo; use git history for detailed diffs.

---

## Current Snapshot (2026-02-22)

### Project Scale
- 664 source files (652 C#, 12 F#), 219 test files (215 C#, 4 F#), 135 documentation files
- 13 main projects, 4 test projects, 1 benchmark project, 2 build tool projects
- 22 CI/CD workflows, 78 Makefile targets, 283 API route constants

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
- WPF desktop application (recommended for Windows; workspace/navigation is implemented but some pages remain placeholder-only)
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

### Testing & Quality
- 219 test files across 4 test projects with ~3,444 test methods
- Core tests: 444 methods (backfill, storage, pipeline, monitoring, providers)
- F# tests: 99 methods (domain validation, calculations, transforms)
- WPF desktop service tests: 324 methods (navigation, config, status, connection)
- Desktop UI service tests: 927 methods (API client, backfill, fixtures, forms, health, watchlist)
- 12 provider test files covering all streaming providers + failover + backfill
- Negative-path and schema validation endpoint integration tests
- Integration test harness with fixture providers for full-pipeline testing

### Observability
- OpenTelemetry pipeline instrumentation via `TracedEventMetrics` decorator
- Typed OpenAPI response annotations across all endpoint families
- API authentication (API key) and rate limiting middleware
- Category-accurate process exit codes for CI/CD integration

### Improvement Tracking (as of 2026-02-22)
- 33/35 core improvement items completed (94.3%)
- C1/C2 architecture items (unified provider registry, single DI composition path) verified complete
- F3 (first-run onboarding), E3 (GC pressure optimization), B3/B4 (provider/service tests) verified complete
- H3 (event replay) and I2 (CLI progress reporting) verified complete
- Remaining: C3 (WebSocket base class), G2 (trace propagation partial)

### Recent Changes (since 2026-02-17)
- Desktop improvements executive summary updated (PR #1372)
- README.md modernized to reflect current state (PR #1371)
- GitHub Actions fixes and missing using statements resolved (PR #1369)
- Code simplification across codebase (PR #1367)
- Code quality CI fixes and test failures resolved (PR #1365)
- AI Claude documentation updates (PR #1364)
- Documentation automation consolidation

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
