# Market Data Collector - Roadmap

**Version:** 1.6.0
**Last Updated:** 2026-01-26
**Status:** Production Ready

This document provides the feature roadmap, backlog, and development priorities for the Market Data Collector system.

---

## Quick Stats

| Category | Implemented | Pending | Total |
|----------|-------------|---------|-------|
| Core Features | 55+ | - | 55+ |
| Technical Debt | 5 | 3 | 8 |
| Quick Wins (≤2 days) | 42 | 83 | 125 |
| Provider Integration | 5 | 17 | 22 |
| Monitoring & Alerting | 24 | 0 | 24 |
| Data Quality | 21 | 2 | 23 |
| Storage & Archival | 9 | 4 | 13 |
| Cloud Integration | 0 | 100+ | 100+ |
| **Total** | **161** | **209+** | **370+** |

---

## What's Implemented (v1.6.0)

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

---

## Recent Updates (2026-01-26)

### Dependency Updates
- **OpenTelemetry**: Updated all OpenTelemetry packages to v1.15.0
  - `OpenTelemetry.Instrumentation.Http` → 1.15.0
  - `OpenTelemetry.Instrumentation.AspNetCore` → 1.15.0
  - `OpenTelemetry.Api` → 1.15.0
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol` → 1.15.0
  - `OpenTelemetry.Extensions.Hosting` → 1.15.0

### CI/CD Improvements
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
| TD-7 | Standardize error handling patterns | Medium | Medium | Pending |
| TD-8 | Remove deprecated `--serve-status` option | Low | Low | Pending |

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

### Sprint 3: Developer Experience - **COMPLETE**
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
| QW-93 | Dry Run Mode | 1 day | High | Pending |
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
| ID | Feature | Effort | Priority | Description |
|----|---------|--------|----------|-------------|
| ADQ-1 | Machine Learning Anomaly Detection | 2 weeks | P2 | ML-based detection of unusual patterns |
| ADQ-2 | Reference Price Validator | 3 days | P1 | Cross-validate prices against multiple sources |
| ADQ-3 | Volume Spike Detector | 1 day | P1 | Alert on unusual volume patterns |
| ADQ-4 | Data Freshness SLA Monitor | 2 days | P1 | Track and alert on data delivery delays |

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
- [Architecture Overview](../architecture/overview.md) - System design
- [Getting Started](../guides/getting-started.md) - Setup guide
- [Configuration](../guides/configuration.md) - Config reference

---

*This is a living document. Review and update priorities quarterly based on user feedback and operational needs.*
