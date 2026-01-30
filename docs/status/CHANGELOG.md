# Market Data Collector - Changelog

**Last Updated:** 2026-01-30
**Current Version:** 1.6.1

This changelog summarizes the current repository snapshot. Historical release notes are not curated in this repo; use git history for detailed diffs.

---

## Current Snapshot (2026-01-27)

### Core Runtime
- CLI modes for real-time collection, backfill, replay, packaging, and validation
- Auto-configuration support (`--wizard`, `--auto-config`, provider detection, credential validation)
- HTTP status server with Prometheus metrics and HTML dashboard

### Storage & Data Management
- JSONL and Parquet storage sinks with configurable naming conventions
- Write-ahead logging (WAL) for archival-first persistence
- Portable data package creation/import with manifests and checksums

### Providers
- Alpaca streaming provider (credentials required)
- Interactive Brokers provider (requires IBAPI build flag)
- Polygon provider (stub/partial streaming without credentials)
- NYSE provider (credentials required)
- StockSharp provider integration scaffold
- Historical backfill providers including Stooq, Yahoo, Tiingo, Finnhub, Alpha Vantage, Nasdaq Data Link, and Polygon

### UI & Integrations
- Web dashboard for status/metrics and API-backed backfill actions
- Windows UWP companion app (Windows-only)
- QuantConnect Lean integration types and data provider

---

## Notes

- Version numbers are defined in project files (e.g., `src/MarketDataCollector/MarketDataCollector.csproj`).
- Use `docs/status/production-status.md` for readiness and implementation status details.
