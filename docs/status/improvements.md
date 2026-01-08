# Market Data Collector - Improvements & Roadmap

**Last Updated:** 2026-01-08
**Current Version:** 1.5.0

This document consolidates implemented improvements and future architectural proposals for the Market Data Collector system.

---

## Table of Contents

1. [Implemented Improvements](#implemented-improvements)
2. [Future Proposals](#future-proposals)
3. [Technical Debt](#technical-debt)
4. [Implementation Roadmap](#implementation-roadmap)

---

# Part 1: Implemented Improvements

The following improvements have been implemented based on recommendations from industry-leading open source projects.

---

## 1. Technical Indicators (Skender.Stock.Indicators)

**Status:** ✅ Implemented
**Implementation:** `Application/Indicators/TechnicalIndicatorService.cs`

### Features
- 200+ technical indicators available
- Real-time streaming indicator calculation
- Historical indicator analysis on OHLCV bars
- Configurable indicator parameters

### Supported Indicators
- **Trend**: SMA, EMA, MACD, ADX
- **Momentum**: RSI, Stochastic Oscillator
- **Volatility**: Bollinger Bands, ATR
- **Volume**: VWAP, OBV

### Usage
```csharp
var service = new TechnicalIndicatorService(new IndicatorConfiguration
{
    EnabledIndicators = { IndicatorType.SMA, IndicatorType.RSI, IndicatorType.MACD },
    SmaPeriods = new[] { 10, 20, 50, 200 }
});

var snapshot = service.ProcessTrade(trade);
```

---

## 2. OpenTelemetry Distributed Tracing

**Status:** ✅ Implemented
**Implementation:** `Application/Tracing/OpenTelemetrySetup.cs`

### Features
- End-to-end tracing: Provider → Collector → Storage
- Context propagation across components
- OTLP exporter for Jaeger/Tempo/etc.
- Configurable sampling rates

### Usage
```csharp
OpenTelemetrySetup.Initialize(OpenTelemetryConfiguration.Development);

using var activity = MarketDataTracing.StartReceiveActivity("Alpaca", "SPY");
MarketDataTracing.RecordLatency(activity, latency);
```

---

## 3. Apache Parquet Storage

**Status:** ✅ Implemented
**Implementation:** `Storage/Sinks/ParquetStorageSink.cs`

### Features
- 10-20x better compression than JSONL
- Columnar storage optimized for analytics
- Type-specific schemas for trades, quotes, bars
- Snappy/Gzip compression options

### Usage
```csharp
var options = new ParquetStorageOptions
{
    BufferSize = 10000,
    FlushInterval = TimeSpan.FromSeconds(30),
    CompressionMethod = CompressionMethod.Snappy
};

await using var sink = new ParquetStorageSink(storageOptions, parquetOptions);
await sink.AppendAsync(marketEvent);
```

---

## 4. System.IO.Pipelines WebSocket Processing

**Status:** ✅ Implemented
**Implementation:** `Infrastructure/Performance/PipelinesWebSocketProcessor.cs`

### Features
- Zero-copy buffer management
- ~50% reduction in memory allocations
- Backpressure propagation
- High-performance JSON parsing with Utf8JsonReader

---

## 5. System.Text.Json Source Generators

**Status:** ✅ Implemented
**Implementation:** `Application/Serialization/MarketDataJsonContext.cs`

### Features
- Reflection-free JSON serialization
- 2-3x faster serialization
- Zero reflection overhead
- Reduced startup time

---

## 6. Order Book Matching Engine

**Status:** ✅ Implemented
**Implementation:** `Infrastructure/OrderBook/OrderBookMatchingEngine.cs`

### Features
- Price-time priority matching
- Market and limit order support
- Multiple time-in-force options (GTC, IOC, FOK)
- Real-time order book state management

---

## 7. Archival-First Storage Pipeline

**Status:** ✅ Implemented
**Implementation:** `Storage/Archival/WriteAheadLog.cs`, `Storage/Archival/ArchivalStorageService.cs`

### Features
- Write-Ahead Logging (WAL) for crash-safe data persistence
- Configurable sync modes (NoSync, BatchedSync, EveryWrite)
- Per-record checksums using SHA256
- Transaction commit/rollback semantics

---

## 8. Compression Profiles

**Status:** ✅ Implemented
**Implementation:** `Storage/Archival/CompressionProfileManager.cs`

### Pre-built Profiles
- **Real-Time Collection**: LZ4 Level 1 (~500 MB/s, 2.5x ratio)
- **Warm Archive**: ZSTD Level 6 (~150 MB/s, 5x ratio)
- **Cold Archive**: ZSTD Level 19 (~20 MB/s, 10x ratio)
- **High-Volume Symbols**: ZSTD Level 3 for SPY, QQQ, etc.

---

## 9. Schema Versioning

**Status:** ✅ Implemented
**Implementation:** `Storage/Archival/SchemaVersionManager.cs`

### Features
- Semantic versioning (e.g., Trade v1.0.0, v2.0.0)
- Automatic schema migration between versions
- JSON Schema export for external tools

---

## 10. Analysis Export Service

**Status:** ✅ Implemented
**Implementation:** `Storage/Export/AnalysisExportService.cs`

### Export Profiles
- **Python/Pandas**: Parquet with datetime64[ns]
- **R Statistics**: CSV with proper NA handling
- **QuantConnect Lean**: Native Lean format
- **Microsoft Excel**: XLSX with multiple sheets
- **PostgreSQL**: CSV with DDL scripts

---

## 11. Code Cleanup

**Status:** ✅ Implemented

### Changes Made
- Extracted shared `SymbolSubscriptionTracker` base class
- Standardized logger initialization across all components
- Removed boilerplate TODO comments from consumer classes
- Added comprehensive `.gitignore`

---

## Performance Improvements Summary

| Component | Before | After | Improvement |
|-----------|--------|-------|-------------|
| JSON Serialization | Reflection | Source-gen | 2-3x faster |
| WebSocket Parsing | StringBuilder | Pipelines | ~50% less alloc |
| Storage (Archive) | JSONL | Parquet | 10-20x smaller |
| Indicator Calc | N/A | Streaming | Real-time |

---

## New NuGet Packages Added

| Package | Version | Purpose |
|---------|---------|---------|
| Skender.Stock.Indicators | 3.0.0 | Technical indicators |
| OpenTelemetry | 1.10.0 | Distributed tracing |
| Parquet.Net | 5.0.2 | Parquet storage |
| BenchmarkDotNet | 0.14.0 | Performance testing |

---

# Part 2: Future Proposals

The following improvements are proposed for future versions. These ideas are categorized by priority and complexity.

---

## High Priority Proposals

### 1. Event Sourcing with CQRS Pattern

**Problem:** Current architecture tightly couples read and write paths.

**Proposal:** Implement Command Query Responsibility Segregation (CQRS) with Event Sourcing.

**Benefits:**
- Full audit trail of all market events
- Temporal queries ("show order book at 10:30 AM")
- Replay capability for debugging and testing
- Independent scaling of read/write workloads

**Complexity:** High | **Impact:** High

---

### 2. gRPC Streaming for Real-Time Data Distribution

**Problem:** Current HTTP-based endpoints don't support efficient real-time streaming.

**Proposal:** Add gRPC streaming endpoints for real-time market data distribution.

```protobuf
service MarketDataService {
  rpc SubscribeTrades(SubscriptionRequest) returns (stream TradeEvent);
  rpc SubscribeQuotes(SubscriptionRequest) returns (stream QuoteEvent);
}
```

**Benefits:**
- Bi-directional streaming with backpressure
- Strongly-typed contracts via protobuf
- Cross-language client support (Python, Go, Java)

**Complexity:** Medium | **Impact:** High

---

### 3. Dead Letter Queue for Failed Events

**Problem:** Events dropped due to backpressure or storage failures are lost.

**Proposal:** Implement a Dead Letter Queue (DLQ) for failed events.

**Features:**
- Persistent storage for failed events
- Retry with exponential backoff
- Admin API for inspecting and replaying

**Complexity:** Medium | **Impact:** High

---

## Medium Priority Proposals

### 4. Plugin Architecture for Data Sources

**Problem:** Adding new data providers requires modifying core codebase.

**Proposal:** Implement a plugin architecture using MEF or custom loader.

**Complexity:** Medium | **Impact:** Medium

---

### 5. Time-Series Database Integration

**Problem:** JSONL files don't support efficient time-range queries at scale.

**Proposal:** Add optional TimescaleDB or QuestDB backend for analytics.

**Benefits:**
- Sub-second queries on millions of rows
- Built-in time-based aggregations
- SQL interface for ad-hoc analysis

**Complexity:** Medium | **Impact:** High

---

### 6. Kubernetes-Native Deployment

**Proposal:** Add Kubernetes manifests and Helm chart.

**Features:**
- Horizontal Pod Autoscaler
- Pod Disruption Budgets
- Resource limits and probes

**Complexity:** Medium | **Impact:** Medium

---

### 7. Real-Time Alerting Engine

**Problem:** Data quality issues discovered after the fact.

**Proposal:** Implement streaming alerting with configurable rules.

**Alert Types:**
- Price spikes
- Data gaps
- Crossed markets
- Provider disconnects

**Complexity:** Medium | **Impact:** High

---

## Lower Priority Proposals

### 8. GraphQL API
Add flexible data queries via GraphQL endpoint.

### 9. WebAssembly Dashboard
Compile F# domain models to WebAssembly for browser-based analysis.

### 10. Data Versioning with DVC
Integrate Data Version Control for dataset reproducibility.

### 11. Chaos Engineering Framework
Implement chaos testing to validate resilience.

---

# Part 3: Technical Debt

| Item | Priority | Effort | Impact |
|------|----------|--------|--------|
| Replace `double` with `decimal` for prices | High | Medium | High |
| Add authentication to HTTP endpoints | High | Low | High |
| Complete Alpaca quote message handling | Medium | Low | Medium |
| Fix UWP/Core API endpoint mismatch | Medium | Low | Medium |
| Create shared contracts library | Medium | Medium | Medium |
| Add missing integration tests | Low | High | Medium |

---

# Part 4: Implementation Roadmap

### Phase 1: Foundation
1. OpenTelemetry instrumentation ✅
2. Dead Letter Queue
3. gRPC streaming endpoints

### Phase 2: Scalability
4. Time-series database integration
5. Kubernetes deployment
6. Plugin architecture

### Phase 3: Intelligence
7. Real-time alerting engine
8. Event sourcing with CQRS
9. ML pipeline integration

### Phase 4: Enterprise
10. Multi-region replication
11. GraphQL API
12. Chaos engineering framework

---

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Provider Management](../architecture/provider-management.md)
- [Storage Organization](../architecture/storage-design.md)
- [Production Status](production-status.md)

---

*This document is a living proposal. Priorities should be reviewed quarterly based on user feedback and operational experience.*
