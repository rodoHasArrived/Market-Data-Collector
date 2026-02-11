# Market Data Collector - Changelog

**Last Updated:** 2026-02-06
**Current Version:** 1.6.1

This changelog summarizes the current repository snapshot. Historical release notes are not curated in this repo; use git history for detailed diffs.

---

## Current Snapshot (2026-02-06)

### Project Scale
- 734 source files (717 C#, 17 F#), 85 test files, 104 documentation files
- 9 main projects, 2 test projects, 1 benchmark project, 2 build tool projects
- 17 CI/CD workflows, 66 Makefile targets

### Core Runtime
- CLI modes for real-time collection, backfill, replay, packaging, and validation
- Auto-configuration support (`--wizard`, `--auto-config`, provider detection, credential validation)
- HTTP status server with Prometheus metrics and HTML dashboard
- Dry-run mode for validation without starting collection

### Storage & Data Management
- JSONL and Parquet storage sinks with configurable naming conventions
- Write-ahead logging (WAL) for archival-first persistence
- Portable data package creation/import with manifests and checksums
- Tiered storage (hot/warm/cold) with automatic migration

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
- UWP desktop application (legacy, Windows 10+)
- Shared UI services project (`MarketDataCollector.Ui.Services`)
- QuantConnect Lean integration types and data provider

### Data Quality & Monitoring
- Comprehensive data quality monitoring with SLA enforcement
- Completeness scoring, gap analysis, anomaly detection
- Cross-provider data comparison
- Latency distribution tracking

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
