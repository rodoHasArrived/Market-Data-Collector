# Market Data Collector - Production Status

**Last Updated:** 2026-02-20
**Version:** 1.6.1
**Status:** Development / Pilot Ready

This document consolidates the architecture assessment and production readiness information for the Market Data Collector system.

---

## Executive Summary

The Market Data Collector is a feature-rich system with a working CLI, backfill pipeline, storage, and UI tooling. Some providers require credentials or build-time flags, and certain integrations (notably Polygon streaming) remain partially implemented.

### Overall Assessment: **DEVELOPMENT / PILOT READY**

| Category | Status | Notes |
|----------|--------|-------|
| Core Event Pipeline | ✅ Implemented | Channel-based processing with backpressure, injectable metrics |
| Storage Layer | ✅ Implemented | JSONL/Parquet composite sink with WAL support |
| Backfill Providers | ✅ Implemented | Multiple providers; credentials required for some |
| Alpaca Provider (Streaming) | ✅ Implemented | Requires Alpaca credentials |
| NYSE Provider | ⚠️ Needs credentials | NYSE Connect credentials required |
| Interactive Brokers | ⚠️ Requires build flag | Compile with `IBAPI` and reference IBApi |
| Polygon Provider | ⚠️ Partial | Stub mode unless configured; WebSocket parsing in progress |
| StockSharp Provider | ⚠️ Integration scaffold | Requires StockSharp setup |
| Monitoring | ✅ Implemented | HTTP server + Prometheus metrics + OpenTelemetry |
| Data Quality | ✅ Implemented | Completeness, gap analysis, anomaly detection, SLA monitoring |
| WPF Desktop App | ⚠️ Partial UX parity | Windows desktop UI (sole desktop client); several navigable pages still show placeholder "Coming Soon" content |
| QuantConnect Lean | ✅ Implemented | Custom data types + IDataProvider |
| Symbol Search Providers | ✅ Implemented | 5 providers (Alpaca, Finnhub, Polygon, OpenFIGI, StockSharp) |
| API Surface | ✅ Implemented | ~136 endpoints with typed OpenAPI annotations |
| Architecture | ✅ Monolithic | Single-process runtime |
| Improvement Tracking | 🔄 In progress | 27/35 items completed (77.1%), see [IMPROVEMENTS.md](IMPROVEMENTS.md) |

---

## Architecture Strengths

### 1. Provider Abstraction Layer

The provider system is well-designed with:
- `IDataProvider` - Core contract for all providers
- `IStreamingDataProvider` - Real-time streaming extension
- `IHistoricalDataProvider` - Historical data retrieval
- `ProviderRegistry` - Dynamic discovery and registration
- `ProviderCapabilities` - Feature flags for provider selection

### 2. Event Pipeline (High Performance)

The `EventPipeline` class is production-grade:
- System.Threading.Channels for high-throughput
- Bounded capacity with configurable backpressure (DropOldest)
- 100,000 event capacity (configurable)
- Nanosecond precision timing
- Thread-safe statistics

### 3. Monolithic Architecture

The application runs as a single-process monolith:
- Direct in-process communication (no external messaging)
- Simplified deployment and configuration
- Reduced operational complexity

### 4. Storage Layer

Multiple storage strategies:
- JSONL (append-only, human-readable)
- Parquet (columnar, compressed)
- Write-ahead logging (WAL) for durability
- Configurable naming conventions

---

## Component Integration Map

```
┌─────────────────────────────────────────────────────────────────────┐
│              DESKTOP APPLICATION (WPF)                               │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │
│  │ ConfigService│  │StatusService │  │ BackfillService          │   │
│  └─────────────┘  └──────────────┘  └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                           │ HTTP API
┌─────────────────────────────────────────────────────────────────────┐
│                      CORE APPLICATION (Monolithic)                   │
│  ┌──────────────────┐      ┌─────────────────────────────────────┐  │
│  │ StatusHttpServer │◄────►│ EventPipeline                       │  │
│  │ /status /metrics │      │ System.Threading.Channels           │  │
│  └──────────────────┘      └─────────────────────────────────────┘  │
│           │                              │                          │
│  ┌────────┴───────────────────────────────────────────────────────┐ │
│  │                    DOMAIN COLLECTORS                            │ │
│  │  TradeDataCollector │ MarketDepthCollector │ QuoteCollector     │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│           │                              │                          │
│  ┌───────────────────┐      ┌──────────────────────────────────┐   │
│  │ PipelinePublisher │      │ IStorageSink                      │   │
│  │ (direct in-proc)  │      │ ├─JsonlStorageSink               │   │
│  │                   │      │ └─ParquetStorageSink             │   │
│  └───────────────────┘      └──────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Known Issues

### WPF UX Parity

**Status:** ⚠️ Partial implementation  
The WPF shell and navigation model are in place, but multiple pages are still placeholder screens. Treat current WPF status as "navigation-complete, workflow-parity in progress" until those pages are functionally implemented.

### Polygon Streaming

**Status:** ⚠️ Partial implementation  
The Polygon streaming client operates in stub mode when no API key is provided. The WebSocket parsing path is still being completed.

### Interactive Brokers Build Flag

**Status:** ⚠️ Build-time requirement  
Interactive Brokers connectivity requires the IBAPI compile flag and a referenced IBApi package/dll.

---

## Items Requiring Configuration

### 1. Interactive Brokers Integration

The IB provider requires the official IB API and a build-time constant:

```bash
dotnet build -p:DefineConstants=IBAPI
```

**Action Required:**
1. Obtain IB API from Interactive Brokers
2. Add IBApi reference to project
3. Build with IBAPI constant defined

### 2. Polygon Provider

**Status:** Partial implementation  
The Polygon provider runs in stub mode without credentials and requires full WebSocket message parsing for complete streaming support.

---

## Stub/Partial Implementations

| Component | File | Current Behavior | Production Action |
|-----------|------|------------------|-------------------|
| Polygon Provider | `Infrastructure/Providers/Polygon/PolygonMarketDataClient.cs` | Stub or partial streaming | Complete WebSocket message parsing |
| IB Provider (no IBAPI) | `Infrastructure/Providers/InteractiveBrokers/EnhancedIBConnectionManager.cs` | Throws NotSupportedException | Build with IBAPI flag |

---

## Extension Points

### Data Providers

To add a new market data provider:
1. Implement `IMarketDataClient` interface
2. Register in DI container
3. Add configuration options
4. Wire up to EventPipeline

### Historical Data Providers

To add a new historical data source:
1. Implement `IHistoricalDataProvider` interface
2. Register with `HistoricalBackfillService`
3. Add to backfill coordinator

### Storage Formats

To add a new storage format:
1. Implement `IStorageSink` interface
2. Create corresponding `IStoragePolicy`
3. Register in storage factory

---

## Conditional Compilation

### IBAPI Constant

When `IBAPI` is defined:
- Full IB EWrapper implementation is available
- Connection retry with exponential backoff
- Heartbeat monitoring
- Market depth subscription

When `IBAPI` is NOT defined:
- Stub implementation throws `NotSupportedException`
- Project builds without IB API dependency

---

## Deprecated Features

### Legacy Status File (`--serve-status`)

**Status:** ❌ Removed

**Reason:** The file-based status approach has been superseded by the HTTP monitoring server which provides real-time access to status, metrics, and health endpoints.

**Migration:** Use `--ui` to start the web dashboard and access:
- `/status` for JSON status
- `/metrics` for Prometheus metrics
- `/health` for health checks

---

## Pre-Production Checklist

### Required Steps

- [ ] **Secrets Management**
  - [ ] Configure environment variables for API credentials
  - [ ] Ensure `appsettings.json` with real credentials is in `.gitignore`
  - [ ] Consider using Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault

- [ ] **Provider Configuration**
  - [ ] For IB: Build with `-p:DefineConstants=IBAPI` and configure TWS/Gateway
  - [ ] For Alpaca: Set `ALPACA_KEY_ID` and `ALPACA_SECRET_KEY`
  - [ ] Verify market data entitlements with chosen provider

- [ ] **Storage Configuration**
  - [ ] Set appropriate `DataRoot` path
  - [ ] Configure retention policies (`RetentionDays`, `MaxTotalMegabytes`)
  - [ ] Verify disk space requirements

- [ ] **Monitoring Setup**
  - [ ] Enable HTTP monitoring server (`--http-port`)
  - [ ] Configure Prometheus scraping
  - [ ] Set up alerting for integrity events

### Recommended Steps

- [ ] **Performance Tuning**
  - [ ] Review pipeline capacity settings
  - [ ] Configure appropriate depth levels per symbol
  - [ ] Enable compression for high-volume scenarios

- [ ] **High Availability**
  - [ ] Configure systemd service (Linux)
  - [ ] Set up health check monitoring
  - [ ] Plan for provider failover

- [ ] **Testing**
  - [ ] Run `--selftest` mode
  - [ ] Verify data integrity with sample symbols
  - [ ] Test hot-reload configuration changes

---

## Testing Notes

The project has 164 test files (160 C#, 4 F#) across 4 test projects:

| Test Project | Focus | Tests |
|--------------|-------|-------|
| `MarketDataCollector.Tests` | Core unit/integration tests (backfill, storage, pipeline, monitoring, providers, credentials, serialization, domain) | ~130+ test classes |
| `MarketDataCollector.FSharp.Tests` | F# domain validation, calculations, pipeline transforms | 4 test files |
| `MarketDataCollector.Wpf.Tests` | WPF desktop service tests (navigation, config, status, connection) | 58 tests |
| `MarketDataCollector.Ui.Tests` | Desktop UI service tests (API client, backfill, fixtures, forms, health, watchlist, collections) | 71 tests |

Refer to the `tests/` directory for the current suite and to the CI pipelines for test execution coverage.

---

## Related Documentation

- [Roadmap](ROADMAP.md) - Feature backlog and development priorities
- [Changelog](CHANGELOG.md) - Recent changes and improvements
- [Configuration](../HELP.md#configuration) - Detailed configuration reference
- [Troubleshooting](../HELP.md#troubleshooting) - Common issues and solutions
- [Operator Runbook](../operations/operator-runbook.md) - Operations guide
- [Architecture](../architecture/overview.md) - System architecture overview

---

*Last Updated: 2026-02-20*
