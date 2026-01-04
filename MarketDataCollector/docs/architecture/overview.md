# Market Data Collector – System Architecture

## Overview

The Market Data Collector is a modular, event-driven system for capturing, validating,
and persisting high-fidelity market microstructure data from multiple data providers
including Interactive Brokers (IB), Alpaca, NYSE Direct, and extensible provider plugins.

The system is designed around strict separation of concerns and is safe to operate
with or without live provider connections. It supports multi-source operation for reconciliation,
provider failover, and data quality monitoring.

### Core Principles

- **Provider-Agnostic Design** – Unified abstraction layer supporting any data source
- **Archival-First Storage** – Write-ahead logging (WAL) ensures crash-safe persistence
- **Type-Safe Domain Models** – F# discriminated unions with exhaustive pattern matching
- **Quality-Driven Operations** – Multi-dimensional data quality scoring and gap repair

The architecture supports multiple deployment modes:
- **Standalone Console Application** – Single-process data collection with local storage
- **Distributed Microservices** – Horizontally scalable architecture with message bus coordination
- **UWP Desktop Application** – Native Windows app for configuration and monitoring
- **Web Dashboard** – Browser-based monitoring and management interface

---

## Layered Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           Presentation Layer                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐  │
│  │  Web Dashboard  │  │  UWP Desktop    │  │  Microservices (6)      │  │
│  │  (ASP.NET)      │  │  (WinUI 3)      │  │  Gateway/Ingestion/etc  │  │
│  └────────┬────────┘  └────────┬────────┘  └────────────┬────────────┘  │
└───────────┼────────────────────┼────────────────────────┼───────────────┘
            │ JSON/FS            │ Config/Status          │ REST/MassTransit
┌───────────┼────────────────────┼────────────────────────┼───────────────┐
│           ▼                    ▼                        ▼               │
│                       Application Layer                                  │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  Program.cs | ConfigWatcher | StatusWriter | StatusHttpServer   │   │
│  │  BackfillService | Metrics | EventSchemaValidator               │   │
│  └─────────────────────────────────┬───────────────────────────────┘   │
└────────────────────────────────────┼────────────────────────────────────┘
                                     │ MarketEvents
┌────────────────────────────────────┼────────────────────────────────────┐
│                          Domain Layer                                    │
│  ┌─────────────────────────────────┴───────────────────────────────┐   │
│  │  TradeDataCollector | MarketDepthCollector | QuoteCollector     │   │
│  │  HighPerformanceMarketDepthCollector | SymbolSubscriptionTracker│   │
│  │  Domain Models: Trade, LOBSnapshot, BboQuote, OrderFlow, etc.   │   │
│  └─────────────────────────────────┬───────────────────────────────┘   │
└────────────────────────────────────┼────────────────────────────────────┘
                                     │ publish()
┌────────────────────────────────────┼────────────────────────────────────┐
│                       Event Pipeline Layer                               │
│  ┌─────────────────────────────────┴───────────────────────────────┐   │
│  │  EventPipeline (bounded Channel<MarketEvent>, 50K capacity)     │   │
│  │  CompositePublisher → Local Storage + MassTransit Bus           │   │
│  └──────────────┬────────────────────────────────┬─────────────────┘   │
└─────────────────┼────────────────────────────────┼──────────────────────┘
                  │ append()                        │ publish()
┌─────────────────┼────────────────────────────────┼──────────────────────┐
│           Storage Layer                    Messaging Layer               │
│  ┌─────────────┴────────────┐     ┌─────────────┴────────────────┐     │
│  │ JsonlStorageSink         │     │ MassTransit (InMemory/       │     │
│  │ ParquetStorageSink       │     │ RabbitMQ/Azure Service Bus)  │     │
│  │ TieredStorageManager     │     │ Trade/Quote/Depth Consumers  │     │
│  │ DataRetentionPolicy      │     └──────────────────────────────┘     │
│  └──────────────────────────┘                                           │
└─────────────────────────────────────────────────────────────────────────┘
                  ↑
┌─────────────────┼───────────────────────────────────────────────────────┐
│                 │              Infrastructure Layer                      │
│  ┌──────────────┴───────────────────────────────────────────────────┐  │
│  │ Streaming Providers                  Historical Data Providers    │  │
│  │ ├─ IBMarketDataClient               ├─ AlpacaHistoricalProvider  │  │
│  │ ├─ AlpacaMarketDataClient           ├─ YahooFinanceProvider      │  │
│  │ └─ PolygonMarketDataClient (stub)   ├─ StooqProvider             │  │
│  │                                      ├─ NasdaqDataLinkProvider   │  │
│  │ Connection Management                └─ CompositeProvider        │  │
│  │ ├─ EnhancedIBConnectionManager       (automatic failover)        │  │
│  │ ├─ IBCallbackRouter                                               │  │
│  │ └─ WebSocketResiliencePolicy                                      │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Infrastructure
* Owns all provider-specific code
* **Unified Data Source Abstraction** in `Infrastructure/DataSources/`:
  - `IDataSource` – Base interface for all data sources
  - `IRealtimeDataSource` – Real-time streaming extension (trades, quotes, depth)
  - `IHistoricalDataSource` – Historical data retrieval (bars, dividends, splits)
  - `DataSourceCapabilities` – Declarative capability flags for feature discovery
  - `DataSourceRegistry` – Attribute-based automatic discovery via `[DataSource]`
* **Streaming Provider implementations** in `Infrastructure/Providers/`:
  - `InteractiveBrokers/IBMarketDataClient` – IB TWS/Gateway connectivity with free equity data support
  - `Alpaca/AlpacaMarketDataClient` – Alpaca WebSocket client with IEX/SIP feeds
  - `NYSE/NYSEDataSource` – NYSE Direct connection for real-time and historical US equity data
  - `Polygon/PolygonMarketDataClient` – Polygon adapter (stub implementation)
* **Historical Data Providers** for backfill operations:
  - `AlpacaHistoricalDataProvider` – Historical OHLCV bars, trades, quotes, and auctions
  - `YahooFinanceHistoricalDataProvider` – Free EOD data for 50K+ global securities
  - `StooqHistoricalDataProvider` – US equities EOD data
  - `NasdaqDataLinkHistoricalDataProvider` – Alternative datasets via Quandl API
  - `CompositeHistoricalDataProvider` – Automatic failover with rate-limit rotation
  - `OpenFIGI Symbol Resolver` – Cross-provider symbol normalization
* **Resilience Layer**:
  - `CircuitBreaker` – Open/Closed/HalfOpen states with automatic recovery
  - `ConcurrentProviderExecutor` – Parallel operations with configurable strategies
  - `RateLimiter` – Per-provider rate limit tracking and throttling
  - `WebSocketResiliencePolicy` – Automatic reconnection with exponential backoff
* All streaming providers implement `IMarketDataClient` interface
* All historical providers implement `IHistoricalDataProvider` interface
* `IBCallbackRouter` normalizes IB callbacks into domain updates
* `ContractFactory` resolves symbol configurations to IB contracts
* No domain logic – replaceable / mockable

### Domain
* Pure business logic – deterministic and testable
* `SymbolSubscriptionTracker` – base class providing thread-safe subscription management (registration, unregistration, auto-subscription)
* `TradeDataCollector` – tick-by-tick trades with sequence validation and order-flow statistics
* `MarketDepthCollector` – L2 order book maintenance with integrity checking (extends `SymbolSubscriptionTracker`)
* `HighPerformanceMarketDepthCollector` – lock-free order book with immutable snapshots (extends `SymbolSubscriptionTracker`)
* `QuoteCollector` – BBO state cache and `BboQuote` event emission
* Domain models: `Trade`, `LOBSnapshot`, `BboQuotePayload`, `OrderFlowStatistics`, integrity events

### F# Domain Library (`MarketDataCollector.FSharp`)
* **Type-Safe Domain Models** – Discriminated unions with exhaustive pattern matching
  - `MarketEvent` – Trade, Quote, Bar, Depth, Integrity variants
  - `ValidationError` – Rich error types with context
* **Railway-Oriented Validation** – Composable validation with error accumulation
  - `TradeValidator` – Price, size, timestamp, symbol validation
  - `QuoteValidator` – Bid/ask spread, price consistency
* **Pure Functional Calculations**:
  - `SpreadCalculations` – Absolute, percentage, basis points
  - `ImbalanceCalculations` – Order book imbalance metrics
  - `Aggregations` – VWAP, TWAP, microprice, order flow
* **Pipeline Transforms** – Declarative stream processing
  - Filtering, enrichment, aggregation operations
* **C# Interop** – Wrapper classes with nullable-friendly APIs

### Application
* `Program.cs` – composition root and startup
* `ConfigWatcher` – hot reload of `appsettings.json`
* `StatusWriter` – periodic health snapshot to `data/_status/status.json`
* `StatusHttpServer` – lightweight HTTP server for monitoring (Prometheus metrics, JSON status, HTML dashboard)
* `EventSchemaValidator` – validates event schema integrity
* `Metrics` – counters for published, dropped, and integrity events

### Storage
* `EventPipeline` – bounded `Channel<MarketEvent>` with configurable capacity and drop policy
* **Archival-First Storage Pipeline**:
  - `ArchivalStorageService` – Write-Ahead Logging (WAL) for crash-safe persistence
  - `WriteAheadLog` – Append-only log with checksums and transaction semantics
  - `AtomicFileWriter` – Safe file operations with temp-file rename pattern
* **Storage Sinks**:
  - `JsonlStorageSink` – writes events to append-only JSONL files with retention enforcement
  - `ParquetStorageSink` – columnar storage format for efficient analytics
* **Compression & Archival**:
  - `CompressionProfileManager` – Storage tier-optimized compression (LZ4, ZSTD, Gzip)
  - `SchemaVersionManager` – Schema versioning with migration support and JSON Schema export
* **Export System**:
  - `AnalysisExportService` – Pre-built profiles for Python, R, Lean, Excel, PostgreSQL
  - `AnalysisQualityReportGenerator` – Quality metrics with outlier detection and gap analysis
* **File Organization**:
  - `JsonlStoragePolicy` – flexible file organization with multiple naming conventions and date partitioning
  - `JsonlReplayer` – replays captured JSONL events for backtesting (supports gzip compression)
  - `TieredStorageManager` – hot/warm/cold storage tier management with automatic migration
  - `DataRetentionPolicy` – time-based and capacity-based retention enforcement
* `StorageOptions` – configurable naming conventions, partitioning strategies, retention policies, and capacity limits

### Messaging (MassTransit)
* Optional distributed messaging layer for microservices and event streaming
* `CompositePublisher` – publishes events to both local storage and message bus
* Supports multiple transports:
  - `InMemory` – for testing and single-process deployments
  - `RabbitMQ` – for distributed deployments
  - `AzureServiceBus` – for cloud deployments
* Message consumers for Trade, Quote, L2Snapshot, and Integrity events
* Configurable retry policies with exponential backoff

---

## Event Flow

1. Provider sends raw data (IB callbacks **or** Alpaca WebSocket messages)
2. Provider client normalizes data into domain update structs
3. Domain collectors process updates:
   - `TradeDataCollector.OnTrade(MarketTradeUpdate)`
   - `MarketDepthCollector.OnDepth(MarketDepthUpdate)`
   - `QuoteCollector.OnQuote(MarketQuoteUpdate)`
4. Collectors emit strongly-typed `MarketEvent` objects via `IMarketEventPublisher`
5. `EventPipeline` routes events through a bounded channel to decouple producers from I/O
6. `JsonlStorageSink` appends events as JSONL
7. `StatusWriter` periodically dumps health snapshots for UI/monitoring

### Event Pipeline Details

* **Bounded channel** – `EventPipeline` uses `System.Threading.Channels` with configurable capacity (default 50,000) and `DropOldest` backpressure policy.
* **Storage policy** – `JsonlStoragePolicy` supports multiple file organization strategies:
  - **BySymbol**: `{root}/{symbol}/{type}/{date}.jsonl` (default)
  - **ByDate**: `{root}/{date}/{symbol}/{type}.jsonl`
  - **ByType**: `{root}/{type}/{symbol}/{date}.jsonl`
  - **Flat**: `{root}/{symbol}_{type}_{date}.jsonl`
* **Date partitioning** – Daily (default), Hourly, Monthly, or None
* **Compression** – Optional gzip compression for all JSONL files
* **Metrics** – `Metrics.Published`, `Metrics.Dropped`, `Metrics.Integrity` track event throughput and data quality.

### Quote/BBO Path

* `QuoteCollector` maintains the latest BBO per symbol and emits `BboQuote` events with bid/ask price/size, spread, and mid-price when both sides are valid.
* Multiple providers can supply quote updates; sequence numbers (`SequenceNumber`) and stream identifiers (`StreamId`) are preserved to support reconciliation.
* `TradeDataCollector` uses `IQuoteStateStore` (implemented by `QuoteCollector`) to infer aggressor side when the upstream feed provides `AggressorSide.Unknown`.

### Resilience and Integrity

* **Trade sequence validation** – `TradeDataCollector` emits `IntegrityEvent.OutOfOrder` or `IntegrityEvent.SequenceGap` when sequence numbers regress or skip; trades causing out-of-order are rejected.
* **Depth integrity** – `MarketDepthCollector` freezes a symbol and emits `DepthIntegrityEvent` if insert/update/delete operations target invalid positions; operators must call `ResetSymbolStream` to resume.
* **Config hot reload** – `ConfigWatcher` listens for changes to `appsettings.json` and resubscribes symbols without process restart.
* **Pluggable data source** – the `IMarketDataClient` abstraction allows switching between different providers via the `DataSource` configuration option.

---

## Monitoring and Observability

### StatusHttpServer

The `StatusHttpServer` component provides lightweight HTTP-based monitoring without requiring ASP.NET:

* **Prometheus metrics** (`/metrics`) – Exposes counters and gauges in Prometheus text format:
  - `mdc_published` – Total events published
  - `mdc_dropped` – Events dropped due to backpressure
  - `mdc_integrity` – Integrity validation events
  - `mdc_trades`, `mdc_depth_updates`, `mdc_quotes` – Event type counters
  - `mdc_events_per_second` – Current throughput rate
  - `mdc_drop_rate` – Drop rate percentage

* **JSON status** (`/status`) – Machine-readable status including:
  - Current metrics snapshot
  - Pipeline statistics (channel depth, backpressure state)
  - Recent integrity events with timestamps and descriptions

* **HTML dashboard** (`/`) – Auto-refreshing browser dashboard showing:
  - Real-time metrics display
  - Table of recent integrity events
  - Links to Prometheus and JSON endpoints

The server uses `HttpListener` to avoid ASP.NET overhead, making it suitable for lightweight deployments and embedded scenarios.

### Event Schema Validation

The `EventSchemaValidator` component validates that emitted events conform to expected schemas, catching serialization issues and schema drift early.

---

## Storage Management

### Retention Policies

Storage retention is enforced eagerly during writes by `JsonlStorageSink`:

* **Time-based retention** – `RetentionDays` configuration automatically deletes files older than the specified window
* **Capacity-based retention** – `MaxTotalBytes` configuration enforces a storage cap by removing oldest files first when the limit is exceeded
* Retention enforcement runs during each write operation, keeping disk usage predictable

### File Organization

The `FileNamingConvention` enum provides four organization strategies optimized for different access patterns:

1. **BySymbol** – Best for analyzing individual symbols across time
2. **ByDate** – Best for daily batch processing and archival workflows
3. **ByType** – Best for analyzing specific event types (trades, quotes) across all symbols
4. **Flat** – Simplest structure for small datasets and ad-hoc analysis

Date partitioning strategies (`DatePartition` enum) allow fine-tuning file granularity:
- **None** – Single file per symbol/type (append-only)
- **Daily** – One file per day (default, balances file size and access)
- **Hourly** – High-volume scenarios requiring smaller files
- **Monthly** – Long-term storage with less granular access

### Data Replay

The `JsonlReplayer` component enables backtesting and analysis by streaming previously captured events:

* Reads JSONL files in chronological order across directories
* Automatically decompresses gzip-compressed files (`.jsonl.gz`)
* Deserializes events back into strongly-typed `MarketEvent` objects
* Supports filtering and selective replay through standard LINQ operations

Example usage:
```csharp
var replayer = new JsonlReplayer("./data");
await foreach (var evt in replayer.ReadEventsAsync(cancellationToken))
{
    // Process historical event
    if (evt.EventType == MarketEventType.Trade)
    {
        // Analyze trade
    }
}
```

---

---

## Microservices Architecture

For high-throughput deployments, the system can be deployed as a set of microservices:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            API Gateway (Port 5000)                       │
│  ├── Request routing and rate limiting                                  │
│  ├── Provider connection management                                     │
│  └── Subscription management                                            │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
┌───────▼───────┐         ┌─────────▼─────────┐       ┌─────────▼─────────┐
│ Trade Service │         │ OrderBook Service │       │ Quote Service     │
│ (Port 5001)   │         │ (Port 5002)       │       │ (Port 5003)       │
│ ├─ High-      │         │ ├─ Snapshot/delta │       │ ├─ Spread calc    │
│ │  throughput │         │ │  processing     │       │ ├─ Crossed/locked │
│ ├─ Sequence   │         │ ├─ Integrity      │       │ │  detection      │
│ │  validation │         │ │  checking       │       │ └─ Quote state    │
│ └─ Order flow │         │ └─ Book freeze    │       │    tracking       │
└───────────────┘         └───────────────────┘       └───────────────────┘
        │                           │                           │
        └───────────────────────────┼───────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
┌───────▼───────────┐     ┌─────────▼─────────┐     ┌───────────▼───────┐
│ Historical Service│     │ Validation Service│     │     Storage       │
│ (Port 5004)       │     │ (Port 5005)       │     │   (JSONL/DB)      │
│ ├─ Backfill jobs  │     │ ├─ Quality rules  │     │                   │
│ ├─ Progress       │     │ ├─ Metrics agg    │     │                   │
│ │  tracking       │     │ └─ Alert gen      │     │                   │
│ └─ Multi-source   │     │                   │     │                   │
└───────────────────┘     └───────────────────┘     └───────────────────┘
```

### Microservices Components

| Service | Port | Purpose |
|---------|------|---------|
| **Gateway** | 5000 | API entry point, routing, rate limiting, provider management |
| **TradeIngestion** | 5001 | High-throughput trade processing, sequence validation, deduplication |
| **OrderBookIngestion** | 5002 | L2 order book state, snapshot/delta processing, integrity checking |
| **QuoteIngestion** | 5003 | BBO/NBBO quotes, spread calculation, crossed market detection |
| **HistoricalDataIngestion** | 5004 | Backfill job management, progress tracking, multi-source support |
| **DataValidation** | 5005 | Quality rules, metrics aggregation, alerting |

See [Microservices README](../src/Microservices/README.md) for deployment instructions.

---

## Historical Data Backfill

The system supports historical data backfill from multiple providers with automatic failover:

### Provider Priority

| Priority | Provider | Data Type | Notes |
|----------|----------|-----------|-------|
| 5 | **NYSE Direct** | OHLCV bars, dividends, splits | Exchange-direct with adjustments |
| 5 | **Alpaca** | OHLCV bars, trades, quotes | IEX/SIP feeds with adjustments |
| 10 | **Yahoo Finance** | OHLCV bars | 50K+ global securities, free |
| 20 | **Stooq** | EOD bars | US equities |
| 30 | **Nasdaq Data Link** | Various | Alternative datasets |

### Backfill Features

* **Priority Backfill Queue** – Sophisticated job scheduling with priority levels:
  - Critical (0), High (10), Normal (50), Low (100), Deferred (200)
  - Dependency chains for ordered execution
  - Batch enqueue with automatic prioritization
* **Composite Provider** – Automatic failover when primary provider fails or hits rate limits
* **Rate-Limit Rotation** – Switches providers when approaching API limits
* **Gap Detection & Repair** – `DataGapRepairService` with automatic repair:
  - `DataGapAnalyzer` identifies missing data periods
  - Multi-provider gap repair with fallback
  - Gap types: Missing, Partial, Holiday, Suspicious
* **Data Quality Monitoring** – Multi-dimensional quality scoring:
  - Completeness (30%), Accuracy (25%), Timeliness (20%)
  - Consistency (15%), Validity (10%)
  - Quality grade: A+ to F with alerts
* **Fill-Only Mode** – Skip dates with existing data
* **Job Persistence** – Resume interrupted backfills after restart
* **Progress Tracking** – Real-time progress and ETA via API/dashboard

---

## QuantConnect Lean Integration

The system integrates with QuantConnect's Lean algorithmic trading engine for backtesting:

* **Custom Data Types** – `MarketDataCollectorTradeData`, `MarketDataCollectorQuoteData`
* **Data Provider** – `MarketDataCollectorDataProvider` implements Lean's `IDataProvider`
* **Sample Algorithms** – Spread arbitrage, order flow strategies
* **JSONL Reader** – Automatic decompression and parsing of collected data

See [lean-integration.md](lean-integration.md) for detailed integration guide.

---

---

## Archival Storage Pipeline

The system implements an archival-first storage strategy for crash-safe persistence:

### Write-Ahead Logging (WAL)

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Ingest    │────►│     WAL     │────►│   Buffer    │────►│   Storage   │
│   Events    │     │  (Durable)  │     │  (Memory)   │     │  (JSONL/    │
│             │     │             │     │             │     │   Parquet)  │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
                           │                                       │
                           │         ┌─────────────────────────────┘
                           ▼         ▼
                    ┌─────────────────────┐
                    │   Commit Point      │
                    │   (Acknowledge)     │
                    └─────────────────────┘
```

* **Crash Recovery** – Uncommitted records recovered on restart
* **Configurable Sync** – NoSync, BatchedSync, or EveryWrite modes
* **Per-Record Checksums** – SHA256 integrity validation
* **Automatic Truncation** – Old WAL segments cleaned after commit

### Compression Profiles

| Profile | Codec | Level | Throughput | Ratio | Use Case |
|---------|-------|-------|------------|-------|----------|
| Real-Time | LZ4 | 1 | ~500 MB/s | 2.5x | Hot data collection |
| Warm Archive | ZSTD | 6 | ~150 MB/s | 5x | Recent archives |
| Cold Archive | ZSTD | 19 | ~20 MB/s | 10x | Long-term storage |
| High-Volume | ZSTD | 3 | ~300 MB/s | 4x | SPY, QQQ, etc. |
| Portable | Gzip | 6 | ~80 MB/s | 3x | External sharing |

### Schema Versioning

* Semantic versioning for all event types (e.g., Trade v1.0.0, v2.0.0)
* Automatic migration between schema versions
* JSON Schema export for external tool compatibility
* Schema registry with version history

---

## Credential Management

The system supports multiple credential sources with priority resolution:

1. **Environment Variables** – `NYSE_API_KEY`, `ALPACA_API_KEY`, etc.
2. **Windows Credential Store** – Via UWP CredentialPicker
3. **Configuration File** – `appsettings.json` (development only)
4. **Azure Key Vault** – For cloud deployments (planned)

### Credential Testing & Expiration

* **Validation on Startup** – Credentials tested before data collection begins
* **Expiration Tracking** – Alerts for expiring API keys
* **Rotation Support** – Hot-swap credentials without restart

---

## Performance Optimizations

The system includes several high-performance features:

* **System.IO.Pipelines** – Zero-copy WebSocket processing with `Utf8JsonReader`
* **Source-Generated JSON** – 2-3x faster serialization via `MarketDataJsonContext`
* **Lock-Free Order Book** – `LockFreeOrderBook` for high-frequency updates
* **Object Pooling** – Reusable buffers to reduce GC pressure
* **Parallel Provider Execution** – Concurrent backfill across multiple providers

---

**Version:** 1.5.0
**Last Updated:** 2026-01-04
**See Also:** [c4-diagrams.md](c4-diagrams.md) | [domains.md](domains.md) | [why-this-architecture.md](why-this-architecture.md) | [Microservices README](../../src/Microservices/README.md) | [provider-management.md](provider-management.md) | [F# Integration](../integrations/fsharp-integration.md)
