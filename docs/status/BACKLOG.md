# Market Data Collector - Backlog

**Version:** 1.6.0
**Last Updated:** 2026-01-07
**Status:** Production Ready

This document provides a quick overview of implemented features and the development backlog. For the full 280+ item feature list, see [docs/status/FEATURE_BACKLOG.md](MarketDataCollector/docs/status/FEATURE_BACKLOG.md).

---

## Quick Stats

| Category | Implemented | Pending |
|----------|-------------|---------|
| Core Features | 25+ | - |
| Technical Debt | 2 | 6 |
| Quick Wins | 11 | 114 |
| Provider Integration | 4 | 20 |
| Monitoring & Alerting | 10 | 19 |
| Data Quality | 6 | 18 |
| Storage & Archival | 8 | 10 |
| Cloud Integration (AWS/Azure/GCP) | 0 | 80+ |

---

## What's Implemented (v1.6.0)

### Core Data Collection
- [x] Multi-provider streaming (Alpaca, Interactive Brokers)
- [x] Tick-by-tick trade capture
- [x] Level 2 order book / market depth
- [x] BBO/NBBO quote tracking
- [x] Historical data backfill (Alpaca, Yahoo Finance, Stooq, Nasdaq Data Link)
- [x] Provider failover with circuit breaker pattern
- [x] Priority backfill queue with gap detection

### Storage & Archival
- [x] JSONL storage with configurable naming conventions
- [x] Apache Parquet storage (10-20x compression)
- [x] Write-Ahead Logging (WAL) with checksums
- [x] Compression profiles (LZ4, ZSTD levels)
- [x] Schema versioning and migration
- [x] Tiered storage (hot/warm/cold)
- [x] Retention policies (by age or size)
- [x] Analysis-ready exports (Python, R, Lean, SQL, Excel)

### Monitoring & Observability
- [x] Web dashboard with real-time metrics
- [x] Prometheus metrics endpoint (`/metrics`)
- [x] Health checks (`/health`, `/live`, `/ready`)
- [x] JSON status endpoint (`/status`)
- [x] Data quality scoring (multi-dimensional)
- [x] OpenTelemetry distributed tracing
- [x] Structured logging (Serilog)
- [x] **NEW:** Stale data detection with alerts
- [x] **NEW:** Connection health heartbeat monitoring
- [x] **NEW:** Disk space warnings
- [x] **NEW:** Memory usage warnings
- [x] **NEW:** Daily summary webhooks (Slack/Discord/Teams)

### Data Quality (NEW in v1.6.0)
- [x] **NEW:** Crossed market detector (bid > ask)
- [x] **NEW:** Timestamp monotonicity checker
- [x] **NEW:** Trading calendar integration (US markets)

### User Interfaces
- [x] Web Dashboard (HTML/JS, auto-refresh)
- [x] UWP Desktop Application (Windows native, 8 pages)
- [x] Microservices architecture (6 services)
- [x] CLI with hot-reload configuration

### Developer Features
- [x] HTTP endpoint authentication (API key middleware)
- [x] Bulk symbol import (CSV)
- [x] Symbol search autocomplete
- [x] QuantConnect Lean integration
- [x] F# domain library (type-safe, railway-oriented)
- [x] 50+ unit tests
- [x] **NEW:** Config validator CLI (`--validate-config`)
- [x] **NEW:** Pre-flight checks on startup
- [x] **NEW:** Graceful shutdown with event flush

---

## High Priority Backlog

### Technical Debt (Critical)

| ID | Item | Status |
|----|------|--------|
| TD-1 | Replace `double` with `decimal` for prices | Pending |
| TD-5 | Create shared contracts library (UWP/Core) | Pending |
| TD-7 | Standardize error handling patterns | Pending |

### Quick Wins - Sprint 1 (Completed ✅)

| ID | Feature | Effort | Status |
|----|---------|--------|--------|
| QW-1 | Stale Data Detector | 1 day | ✅ Implemented |
| QW-3 | Connection Health Heartbeat | 1 day | ✅ Implemented |
| MON-16 | Disk Space Warning | 0.5 day | ✅ Implemented |
| MON-17 | Memory Usage Warning | 0.5 day | ✅ Implemented |
| QW-2 | Config Validator CLI | 1 day | ✅ Implemented |
| QW-4 | Trading Calendar Integration | 1 day | ✅ Implemented |
| QW-5 | Daily Summary Webhook | 1 day | ✅ Implemented |
| QW-22 | Pre-flight Checks on Startup | 1 day | ✅ Implemented |
| DQ-12 | Crossed Market Detector | 0.5 day | ✅ Implemented |
| QW-30 | Pending Event Flush on Shutdown | 1 day | ✅ Implemented |
| DQ-15 | Timestamp Monotonicity Check | 0.5 day | ✅ Implemented |

### Data Quality (P1) - Next Sprint

| ID | Feature | Effort |
|----|---------|--------|
| DQ-2 | Duplicate Event Detector | 1 day |
| DQ-20 | Bad Tick Filter | 1 day |
| QW-6 | Price Spike Alert | 1 day |
| QW-7 | Spread Monitor | 1 day |

### Monitoring & Alerting (P1) - Next Sprint

| ID | Feature | Effort |
|----|---------|--------|
| MON-6 | Connection Status Webhook | 1 day |
| MON-13 | Volume Spike Alert | 1 day |
| MON-18 | Backpressure Alert | 1 day |

---

## Provider Status

| Provider | Streaming | Historical | Status |
|----------|-----------|------------|--------|
| Alpaca | Yes | Yes | Production Ready |
| Interactive Brokers | Yes | No | Requires IBAPI build flag |
| Yahoo Finance | No | Yes | Production Ready |
| Stooq | No | Yes | Production Ready |
| Nasdaq Data Link | No | Yes | Production Ready |
| Polygon | Stub | No | Needs WebSocket implementation |

---

## Recommended Sprint Plan

### Sprint 1: Critical Foundation ✅ COMPLETE
1. ✅ QW-1: Stale Data Detector
2. ✅ QW-3: Connection Health Heartbeat
3. ✅ MON-16/17: Disk/Memory Warnings
4. ✅ QW-4: Trading Calendar
5. ✅ QW-22: Pre-flight Checks
6. ✅ QW-2: Config Validator CLI
7. ✅ QW-5: Daily Summary Webhook
8. ✅ DQ-12: Crossed Market Detector
9. ✅ DQ-15: Timestamp Monotonicity
10. ✅ QW-30: Graceful Shutdown with Event Flush

### Sprint 2: Data Quality & Alerts (Next)
1. QW-6: Price Spike Alert
2. QW-7: Spread Monitor
3. DQ-2: Duplicate Event Detector
4. DQ-20: Bad Tick Filter
5. QW-32: Detailed Health Check Endpoint

### Sprint 3: Developer Experience
1. QW-16: Diagnostic Bundle Generator
2. TD-1: Replace double with decimal
3. QW-15: Query Endpoint for Historical Data
4. DEV-9: API Explorer / Swagger UI
5. QW-17: Sample Data Generator

---

## Cloud Integration Roadmap

### AWS (47 features planned)
- S3 Storage Backend
- Kinesis Data Streams
- Lambda Triggers
- Timestream Integration
- Secrets Manager

### Azure (29 features planned)
- Blob Storage Backend
- Event Hubs Streaming
- Key Vault Integration
- Azure Data Explorer

### GCP (25 features planned)
- Cloud Storage Backend
- Pub/Sub Integration
- BigQuery Integration
- Secret Manager

---

## Feature Categories Summary

| Category | Count | Priority Focus |
|----------|-------|----------------|
| Quick Wins (≤2 days) | 125 | Immediate value |
| Provider Integration | 22 | Polygon completion |
| Monitoring & Alerting | 24 | Stale data, alerts |
| Data Quality | 23 | Validation rules |
| Storage & Archival | 13 | Cloud backends |
| Export & Analysis | 12 | Query endpoints |
| Performance | 10 | Batch optimization |
| Architecture | 10 | gRPC, CQRS |
| Enterprise | 7 | Multi-user, RBAC |
| Remote Job Management | 38 | Distributed workers |
| AWS Integration | 47 | S3, Kinesis |
| Azure Integration | 29 | Blob, Event Hubs |
| GCP Integration | 25 | BigQuery |
| Future Ideas | 12 | Mobile, ML |

**Total Backlog Items: 280+**

---

## How to Contribute

1. Check the [detailed backlog](MarketDataCollector/docs/status/FEATURE_BACKLOG.md) for full specifications
2. Pick items tagged with your priority level
3. Follow existing code patterns and architecture
4. Add tests for new functionality
5. Update documentation as needed

### Priority Legend
- **P0** = Critical (blocks production use)
- **P1** = High (significant user impact)
- **P2** = Medium (nice to have)
- **P3** = Low (polish/convenience)
- **P4** = Future (long-term roadmap)

---

## Related Documentation

- [Production Status](MarketDataCollector/docs/status/production-status.md) - Deployment readiness
- [Improvements](MarketDataCollector/docs/status/improvements.md) - Recent changes and roadmap
- [Architecture Overview](MarketDataCollector/docs/architecture/overview.md) - System design
- [Getting Started](MarketDataCollector/docs/guides/getting-started.md) - Setup guide
- [Configuration](MarketDataCollector/docs/guides/configuration.md) - Config reference

---

*This is a living document. Review and update priorities quarterly based on user feedback and operational needs.*
