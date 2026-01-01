# Market Data Collector – System Architecture

## Overview

The Market Data Collector is a modular, event-driven system for capturing, validating,
and persisting high-fidelity market microstructure data from multiple data providers
including Interactive Brokers (IB) and Alpaca WebSocket feeds.

The system is designed around strict separation of concerns and is safe to operate
with or without live provider connections. It supports dual-source operation for reconciliation
and provider failover.

---

## Layered Architecture

```
UI (ASP.NET)
   ↓ JSON / FS
Application (Program, ConfigWatcher, StatusWriter)
   ↓ MarketEvents
Domain (TradeDataCollector, MarketDepthCollector, QuoteCollector)
   ↓ publish()
Event Pipeline (bounded Channel<MarketEvent>)
   ↓ append()
Storage (JsonlStorageSink + JsonlStoragePolicy)
   ↑
Infrastructure (Providers: IB / Alpaca / ...)
```

### Infrastructure
* Owns all provider-specific code
* Provider implementations in `Infrastructure/Providers/`:
  - `InteractiveBrokers/IBMarketDataClient` – IB TWS/Gateway connectivity
  - `Alpaca/AlpacaMarketDataClient` – Alpaca WebSocket client
  - `Polygon/PolygonMarketDataClient` – Polygon adapter (stub implementation)
* All providers implement `IMarketDataClient` interface
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

### Application
* `Program.cs` – composition root and startup
* `ConfigWatcher` – hot reload of `appsettings.json`
* `StatusWriter` – periodic health snapshot to `data/_status/status.json`
* `StatusHttpServer` – lightweight HTTP server for monitoring (Prometheus metrics, JSON status, HTML dashboard)
* `EventSchemaValidator` – validates event schema integrity
* `Metrics` – counters for published, dropped, and integrity events

### Storage
* `EventPipeline` – bounded `Channel<MarketEvent>` with configurable capacity and drop policy
* `JsonlStorageSink` – writes events to append-only JSONL files with retention enforcement
* `JsonlStoragePolicy` – flexible file organization with multiple naming conventions and date partitioning
* `JsonlReplayer` – replays captured JSONL events for backtesting (supports gzip compression)
* `StorageOptions` – configurable naming conventions, partitioning strategies, retention policies, and capacity limits

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
