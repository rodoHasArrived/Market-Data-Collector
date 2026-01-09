# Market Data Collector - Changelog

**Last Updated:** 2026-01-09
**Current Version:** 1.5.0

This document tracks implemented improvements and version history for the Market Data Collector system.

---

## Version 1.5.0 (Current)

### New Features

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
