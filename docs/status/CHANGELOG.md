# Market Data Collector - Changelog

**Last Updated:** 2026-01-27
**Current Version:** 1.6.1

This document tracks implemented improvements and version history for the Market Data Collector system.

---

## Version 1.6.0 (Current)

### New Features

#### Simplified Architecture (2026-01-19)
- **Monolithic Core**: Removed microservices layer for improved simplicity and maintainability
- **Dead Code Removal**: Cleaned up unused code, deprecated features, and legacy components
- **Streamlined Codebase**: Reduced complexity by consolidating services into the core application

#### Documentation Updates (2026-01-19)
- **Comprehensive Refresh**: Updated all documentation to reflect current v1.6.0 architecture
- **Fixed Broken Links**: Resolved links to non-existent files in documentation hub
- **Removed Microservices References**: Updated guides to reflect simplified architecture
- **Version Sync**: Synchronized version numbers across all documentation files

#### CI/CD Improvements (2026-01-19)
- **Optimized GitHub Actions**: Faster builds with improved caching strategies
- **Reduced Workflow Complexity**: Streamlined CI/CD pipelines for better reliability

### Breaking Changes
- Microservices architecture has been removed; use the monolithic core for all deployments
- MassTransit messaging is no longer used; the application uses direct in-process communication

### Migration Notes
- If you were using the microservices deployment, migrate to the monolithic core
- Remove any MassTransit/RabbitMQ configuration from your deployment

---

## Version 1.5.0

### New Features

#### Auto-Configuration & Onboarding (2026-01-14)
- **Configuration Wizard**: Interactive step-by-step setup wizard (`--wizard`)
- **Auto-Configuration**: Automatic provider detection from environment variables (`--auto-config`)
- **Provider Detection**: Discover available providers and their status (`--detect-providers`)
- **Credential Validation**: Validate API credentials before running (`--validate-credentials`)
- **First-Run Detection**: Automatic detection and guidance for new users
- **Friendly Error Messages**: User-friendly error formatting with actionable suggestions
- **Progress Display**: Visual progress indicators for long-running operations
- **Startup Summary**: Configuration summary display at application startup

**New Services:**
- `AutoConfigurationService` - Auto-detect providers from environment
- `ConfigurationWizard` - Interactive setup wizard
- `ConnectivityTestService` - Test provider connectivity
- `CredentialValidationService` - Validate API credentials
- `FirstRunDetector` - Detect first-run conditions
- `FriendlyErrorFormatter` - User-friendly error messages
- `ProgressDisplayService` - Display progress of operations
- `StartupSummary` - Show startup configuration summary

#### Symbol Management CLI (2026-01-14)
- **CLI Commands**: Full symbol management from command line
  - `symbols list` - List all subscribed symbols
  - `symbols add <symbol>` - Add new symbol
  - `symbols remove <symbol>` - Remove symbol
  - `symbols import <file>` - Bulk import from CSV
  - `symbols export <file>` - Export to CSV

#### UWP Desktop App Enhancements (2026-01-14)
- **Admin & Maintenance Page**: Comprehensive administrative interface
  - Quick system check with health status indicators
  - Maintenance scheduling with cron expressions
  - Storage tier usage visualization (Hot/Warm/Cold)
  - Retention policy management per tier
  - Maintenance history with status tracking

- **Advanced Analytics Page**: Deep data quality analysis
  - Quality summary cards with letter grade system (A+, A, B, C, D, F)
  - Per-symbol quality reports with drill-down capability
  - Gap analysis with date range selection and symbol filtering
  - Cross-provider comparison tools
  - Latency histogram visualization with percentiles
  - AI-powered recommendations for data quality improvement

**New UWP Services:**
- `SymbolManagementService` - Full symbol CRUD operations
- `AdminMaintenanceService` - Archive scheduling, tier migration, retention policies
- `AdvancedAnalyticsService` - Gap analysis, cross-provider comparison, quality reports
- `ProviderManagementService` - Failover configuration, rate limit tracking

#### Documentation & Diagrams (2026-01-14)
- **PlantUML PNG Generation**: Automated PNG image generation from PlantUML diagrams
- **Updated Architecture Diagrams**: All diagrams refreshed to reflect current application state

#### Technical Indicators (Skender.Stock.Indicators)
- 200+ technical indicators available via `TechnicalIndicatorService`
- Real-time streaming indicator calculation
- Historical indicator analysis on OHLCV bars
- Supported indicators: SMA, EMA, MACD, ADX, RSI, Bollinger Bands, ATR, VWAP, OBV

#### OpenTelemetry Distributed Tracing
- End-to-end tracing: Provider → Collector → Storage
- Context propagation across components
- OTLP exporter for Jaeger/Tempo
- Configurable sampling rates

#### Apache Parquet Storage
- 10-20x better compression than JSONL
- Columnar storage optimized for analytics
- Type-specific schemas for trades, quotes, bars
- Snappy/Gzip compression options

#### Order Book Matching Engine
- Price-time priority matching
- Market and limit order support
- Multiple time-in-force options (GTC, IOC, FOK)
- Real-time order book state management

### Performance Improvements

| Component | Before | After | Improvement |
|-----------|--------|-------|-------------|
| JSON Serialization | Reflection | Source-gen | 2-3x faster |
| WebSocket Parsing | StringBuilder | Pipelines | ~50% less allocations |
| Storage (Archive) | JSONL | Parquet | 10-20x smaller |
| Indicator Calc | N/A | Streaming | Real-time |

#### System.IO.Pipelines WebSocket Processing
- Zero-copy buffer management
- Backpressure propagation
- High-performance JSON parsing with `Utf8JsonReader`

#### System.Text.Json Source Generators
- Reflection-free JSON serialization
- 2-3x faster serialization
- Reduced startup time

### Storage Enhancements

#### Archival-First Storage Pipeline
- Write-Ahead Logging (WAL) for crash-safe persistence
- Configurable sync modes (NoSync, BatchedSync, EveryWrite)
- Per-record checksums using SHA256
- Transaction commit/rollback semantics

#### Compression Profiles
| Profile | Algorithm | Speed | Ratio | Use Case |
|---------|-----------|-------|-------|----------|
| Real-Time | LZ4 Level 1 | ~500 MB/s | 2.5x | Live streaming |
| Warm Archive | ZSTD Level 6 | ~150 MB/s | 5x | Recent data |
| Cold Archive | ZSTD Level 19 | ~20 MB/s | 10x | Long-term storage |

#### Schema Versioning
- Semantic versioning for event types (e.g., Trade v1.0.0, v2.0.0)
- Automatic schema migration between versions
- JSON Schema export for external tools

### Export Features

#### Analysis Export Service
- **Python/Pandas**: Parquet with datetime64[ns]
- **R Statistics**: CSV with proper NA handling
- **QuantConnect Lean**: Native Lean format
- **Microsoft Excel**: XLSX with multiple sheets
- **PostgreSQL**: CSV with DDL scripts

### Monitoring & Alerts

- Stale data detection with configurable thresholds
- Connection health heartbeat monitoring
- Disk space and memory usage warnings
- Daily summary webhooks (Slack/Discord/Teams)
- Crossed market detector (bid > ask)
- Timestamp monotonicity checker

### Developer Experience

- Config validator CLI (`--validate-config`)
- Pre-flight checks on startup
- Graceful shutdown with event flush
- HTTP endpoint authentication (API key middleware)

### Code Quality

- Extracted shared `SymbolSubscriptionTracker` base class
- Standardized logger initialization across components
- Comprehensive `.gitignore`
- Added `[ImplementsAdr]` attributes for ADR traceability

### Dependencies Added

| Package | Version | Purpose |
|---------|---------|---------|
| Skender.Stock.Indicators | 3.0.0 | Technical indicators |
| OpenTelemetry | 1.10.0 | Distributed tracing |
| Parquet.Net | 5.0.2 | Parquet storage |
| BenchmarkDotNet | 0.14.0 | Performance testing |

### Bug Fixes

- Fixed UWP/Core API endpoint mismatch (StatusHttpServer now supports `/api/*` routes)
- Completed Alpaca quote message handling

---

## Version 1.4.0

### Features
- Multi-provider streaming (Alpaca, Interactive Brokers)
- Level 2 order book / market depth collection
- Historical data backfill with automatic failover
- Web dashboard with real-time metrics
- UWP Desktop Application

### Storage
- JSONL storage with configurable naming conventions
- Tiered storage (hot/warm/cold)
- Retention policies

---

## Version 1.3.0

### Features
- MassTransit messaging integration
- Prometheus metrics endpoint
- Health check endpoints
- Circuit breaker pattern for provider failover

---

## Upgrade Notes

### Migrating to v1.5.0

1. **Configuration Changes**
   - New `Compression` section for compression profiles
   - New `Tracing` section for OpenTelemetry configuration
   - `--serve-status` is deprecated; use `--http-port` instead

2. **Breaking Changes**
   - None in this release

3. **Recommended Actions**
   - Review compression settings for storage optimization
   - Enable OpenTelemetry for better observability
   - Consider migrating historical data to Parquet format

---

## Related Documentation

- [Roadmap](ROADMAP.md) - Feature backlog and development priorities
- [Production Status](production-status.md) - Deployment readiness
- [Architecture Overview](../architecture/overview.md) - System design
- [Configuration Guide](../guides/configuration.md) - Config reference

---

*For the full feature backlog and future plans, see [ROADMAP.md](ROADMAP.md).*
