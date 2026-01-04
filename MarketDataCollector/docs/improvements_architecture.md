# Market Data Collector – Architecture Improvement Ideas

**Created:** 2026-01-04
**Status:** Proposal Document
**Target Version:** v2.0

This document outlines potential architectural improvements for the Market Data Collector system. These ideas are categorized by priority and complexity to guide future development efforts.

---

## Executive Summary

The current architecture (v1.5) is production-ready with solid foundations. The following improvements focus on:

1. **Scalability** – Horizontal scaling and cloud-native deployment
2. **Reliability** – Enhanced fault tolerance and disaster recovery
3. **Performance** – Reduced latency and increased throughput
4. **Observability** – Better monitoring, tracing, and debugging
5. **Developer Experience** – Easier onboarding and maintenance

---

## High Priority Improvements

### 1. Event Sourcing with CQRS Pattern

**Problem:** Current architecture tightly couples read and write paths, limiting query flexibility.

**Proposal:** Implement Command Query Responsibility Segregation (CQRS) with Event Sourcing.

```
┌─────────────────┐                    ┌─────────────────┐
│   Write Side    │                    │   Read Side     │
│                 │                    │                 │
│  ┌───────────┐  │     Event Bus     │  ┌───────────┐  │
│  │ Commands  │──┼───────────────────┼──│  Queries  │  │
│  └─────┬─────┘  │                    │  └─────┬─────┘  │
│        │        │                    │        │        │
│  ┌─────▼─────┐  │  ┌─────────────┐  │  ┌─────▼─────┐  │
│  │ Aggregates│──┼──│Event Store  │──┼──│Projections│  │
│  └───────────┘  │  └─────────────┘  │  └───────────┘  │
└─────────────────┘                    └─────────────────┘
```

**Benefits:**
- Full audit trail of all market events
- Temporal queries ("show order book at 10:30 AM")
- Replay capability for debugging and testing
- Independent scaling of read/write workloads

**Implementation Notes:**
- Use EventStoreDB or Marten for event persistence
- Create projections for common query patterns (OHLCV, order flow, etc.)
- Maintain compatibility with existing JSONL storage as a projection

**Complexity:** High | **Impact:** High

---

### 2. gRPC Streaming for Real-Time Data Distribution

**Problem:** Current HTTP-based status endpoints don't support efficient real-time streaming to multiple consumers.

**Proposal:** Add gRPC streaming endpoints for real-time market data distribution.

```protobuf
service MarketDataService {
  rpc SubscribeTrades(SubscriptionRequest) returns (stream TradeEvent);
  rpc SubscribeQuotes(SubscriptionRequest) returns (stream QuoteEvent);
  rpc SubscribeDepth(SubscriptionRequest) returns (stream DepthEvent);
  rpc GetHistoricalBars(HistoricalRequest) returns (stream BarEvent);
}
```

**Benefits:**
- Bi-directional streaming with backpressure
- Strongly-typed contracts via protobuf
- HTTP/2 multiplexing for efficient connections
- Cross-language client support (Python, Go, Java)

**Implementation Notes:**
- Use `Grpc.AspNetCore` for server implementation
- Implement server-side filtering to reduce network traffic
- Add authentication via gRPC interceptors
- Consider gRPC-Web for browser compatibility

**Complexity:** Medium | **Impact:** High

---

### 3. Distributed Tracing with OpenTelemetry

**Problem:** Debugging issues across providers, collectors, and storage is difficult without distributed tracing.

**Proposal:** Fully instrument the pipeline with OpenTelemetry.

```
Provider → Collector → Pipeline → Storage → Consumer
    │          │           │          │          │
    └──────────┴───────────┴──────────┴──────────┘
                    TraceContext
```

**Spans to Implement:**
- `provider.receive` – Time from provider to callback
- `collector.process` – Collector processing time
- `pipeline.enqueue` – Channel enqueue latency
- `storage.write` – Storage sink write time
- `wal.append` – WAL append latency

**Benefits:**
- End-to-end latency visibility
- Bottleneck identification
- Correlation of issues across components
- Integration with Jaeger, Zipkin, Datadog

**Implementation Notes:**
- Activity sources already exist in `OpenTelemetrySetup.cs`
- Add trace propagation through MassTransit messages
- Export to OTLP endpoint for production

**Complexity:** Medium | **Impact:** High

---

### 4. Dead Letter Queue for Failed Events

**Problem:** Events dropped due to backpressure or storage failures are lost forever.

**Proposal:** Implement a Dead Letter Queue (DLQ) for failed events.

```
┌─────────────────────────────────────────────────────────┐
│                     Event Pipeline                       │
│  ┌─────────┐    ┌─────────┐    ┌─────────────────────┐  │
│  │ Channel │───►│ Storage │───►│ Success             │  │
│  └────┬────┘    └────┬────┘    └─────────────────────┘  │
│       │              │                                   │
│  ┌────▼────┐    ┌────▼────┐    ┌─────────────────────┐  │
│  │ Dropped │    │ Failed  │───►│ Dead Letter Queue   │  │
│  │ (Full)  │    │ (Error) │    │ (Retry & Analysis)  │  │
│  └─────────┘    └─────────┘    └─────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

**DLQ Features:**
- Persistent storage for failed events
- Retry with exponential backoff
- Max retry count before permanent failure
- Admin API for inspecting and replaying
- Metrics on failure rates and causes

**Complexity:** Medium | **Impact:** High

---

## Medium Priority Improvements

### 5. Plugin Architecture for Data Sources

**Problem:** Adding new data providers requires modifying core codebase.

**Proposal:** Implement a plugin architecture using MEF or custom loader.

```
MarketDataCollector/
├── plugins/
│   ├── MDC.Provider.Binance/
│   │   └── BinanceProvider.dll
│   ├── MDC.Provider.Coinbase/
│   │   └── CoinbaseProvider.dll
│   └── MDC.Provider.Custom/
│       └── CustomProvider.dll
```

**Plugin Contract:**
```csharp
public interface IDataSourcePlugin
{
    string Id { get; }
    string DisplayName { get; }
    DataSourceCapabilities Capabilities { get; }

    Task<IDataSource> CreateAsync(IServiceProvider services);
    Task<bool> ValidateConfigurationAsync(IConfiguration config);
}
```

**Benefits:**
- Third-party provider development
- Hot-reload of providers without restart
- Reduced core complexity
- Community contributions

**Complexity:** Medium | **Impact:** Medium

---

### 6. Time-Series Database Integration

**Problem:** JSONL files don't support efficient time-range queries at scale.

**Proposal:** Add optional TimescaleDB or QuestDB backend for analytics.

**Query Capabilities:**
```sql
-- OHLCV aggregation
SELECT time_bucket('1 hour', timestamp) AS bucket,
       symbol,
       first(price, timestamp) AS open,
       max(price) AS high,
       min(price) AS low,
       last(price, timestamp) AS close,
       sum(size) AS volume
FROM trades
WHERE timestamp > now() - interval '1 day'
GROUP BY bucket, symbol;

-- Order flow analysis
SELECT symbol,
       sum(CASE WHEN side = 'buy' THEN size ELSE 0 END) AS buy_volume,
       sum(CASE WHEN side = 'sell' THEN size ELSE 0 END) AS sell_volume
FROM trades
WHERE timestamp > now() - interval '5 minutes'
GROUP BY symbol;
```

**Benefits:**
- Sub-second queries on millions of rows
- Built-in time-based aggregations
- Efficient compression (10-20x)
- SQL interface for ad-hoc analysis

**Complexity:** Medium | **Impact:** High

---

### 7. Kubernetes-Native Deployment

**Problem:** Current deployment targets single-node or basic microservices.

**Proposal:** Add Kubernetes manifests and Helm chart.

**Components:**
```yaml
# Deployments
- market-data-gateway (3 replicas, HPA)
- trade-ingestion (5 replicas, HPA based on queue depth)
- orderbook-ingestion (3 replicas)
- quote-ingestion (3 replicas)
- historical-worker (2 replicas)
- validation-service (2 replicas)

# StatefulSets
- rabbitmq (3 replicas)
- timescaledb (2 replicas with streaming replication)

# ConfigMaps/Secrets
- provider-credentials
- storage-config
- monitoring-config
```

**Features:**
- Horizontal Pod Autoscaler based on event throughput
- Pod Disruption Budgets for rolling updates
- Network policies for security isolation
- Resource limits and requests
- Liveness/readiness probes

**Complexity:** Medium | **Impact:** Medium

---

### 8. Multi-Region Data Replication

**Problem:** Single-region deployment creates SPOF and latency issues.

**Proposal:** Implement multi-region active-passive or active-active replication.

```
┌─────────────────────┐         ┌─────────────────────┐
│   US-East (Primary) │         │   US-West (Replica) │
│                     │         │                     │
│  ┌───────────────┐  │  Sync   │  ┌───────────────┐  │
│  │ Market Data   │──┼─────────┼──│ Market Data   │  │
│  │ Collector     │  │         │  │ Collector     │  │
│  └───────────────┘  │         │  └───────────────┘  │
│         │           │         │         │           │
│  ┌──────▼────────┐  │  Async  │  ┌──────▼────────┐  │
│  │ Storage       │──┼─────────┼──│ Storage       │  │
│  └───────────────┘  │  Repl   │  └───────────────┘  │
└─────────────────────┘         └─────────────────────┘
```

**Benefits:**
- Disaster recovery capability
- Lower latency for global users
- Regional compliance (data residency)
- Load distribution

**Complexity:** High | **Impact:** Medium

---

### 9. Real-Time Alerting Engine

**Problem:** Data quality issues discovered after the fact, not in real-time.

**Proposal:** Implement streaming alerting with configurable rules.

**Alert Types:**
```yaml
alerts:
  - name: price_spike
    condition: "abs(price - previous_price) / previous_price > 0.05"
    window: "1 minute"
    severity: warning

  - name: data_gap
    condition: "time_since_last_trade > 5 minutes"
    symbols: ["SPY", "QQQ", "IWM"]
    severity: critical

  - name: crossed_market
    condition: "bid_price > ask_price"
    severity: error

  - name: provider_disconnect
    condition: "provider_status != 'connected' for 30 seconds"
    severity: critical
```

**Notification Channels:**
- Slack/Teams webhooks
- Email via SendGrid
- PagerDuty/OpsGenie
- Custom webhooks

**Complexity:** Medium | **Impact:** High

---

### 10. Machine Learning Pipeline Integration

**Problem:** No built-in support for ML feature engineering or model inference.

**Proposal:** Add ML feature extraction and inference pipeline.

**Feature Engineering:**
```python
# Built-in feature extractors
features:
  - name: rolling_vwap
    type: VWAP
    windows: [5m, 15m, 1h]

  - name: order_imbalance
    type: OrderImbalance
    depth_levels: 5

  - name: spread_stats
    type: SpreadStatistics
    percentiles: [50, 90, 99]

  - name: trade_flow
    type: OrderFlow
    aggressor_side: true
```

**Model Serving:**
- ONNX runtime for model inference
- Feature store integration
- Real-time prediction API
- A/B testing support

**Complexity:** High | **Impact:** Medium

---

## Lower Priority Improvements

### 11. GraphQL API

Add GraphQL endpoint for flexible data queries:

```graphql
query {
  trades(symbol: "AAPL", from: "2026-01-01", to: "2026-01-02") {
    timestamp
    price
    size
    side
  }
  quotes(symbol: "AAPL", last: 100) {
    bidPrice
    askPrice
    spread
  }
}
```

**Complexity:** Low | **Impact:** Low

---

### 12. WebAssembly Dashboard

Compile F# domain models to WebAssembly for browser-based analysis:

- Run calculations client-side
- Reduce server load
- Interactive visualizations
- Offline capability

**Complexity:** Medium | **Impact:** Low

---

### 13. Data Versioning with DVC

Integrate Data Version Control for dataset reproducibility:

- Track dataset versions like code
- Reproduce exact datasets for backtests
- Share datasets with team members
- Storage backends: S3, GCS, Azure Blob

**Complexity:** Low | **Impact:** Medium

---

### 14. Chaos Engineering Framework

Implement chaos testing to validate resilience:

- Provider failure injection
- Network partition simulation
- Storage failure scenarios
- Load spike testing

**Complexity:** Medium | **Impact:** Medium

---

### 15. Cost Optimization Dashboard

Add visibility into operational costs:

- Storage costs by symbol/provider
- API call costs per provider
- Network transfer costs
- Recommendations for cost reduction

**Complexity:** Low | **Impact:** Low

---

## Implementation Roadmap

### Phase 1: Foundation (Q1)
1. OpenTelemetry instrumentation
2. Dead Letter Queue
3. gRPC streaming endpoints

### Phase 2: Scalability (Q2)
4. Time-series database integration
5. Kubernetes deployment
6. Plugin architecture

### Phase 3: Intelligence (Q3)
7. Real-time alerting engine
8. Event sourcing with CQRS
9. ML pipeline integration

### Phase 4: Enterprise (Q4)
10. Multi-region replication
11. GraphQL API
12. Chaos engineering framework

---

## Technical Debt to Address

| Item | Priority | Effort | Impact |
|------|----------|--------|--------|
| Replace `double` with `decimal` for prices | High | Medium | High |
| Add authentication to HTTP endpoints | High | Low | High |
| Complete Alpaca quote message handling | Medium | Low | Medium |
| Fix UWP/Core API endpoint mismatch | Medium | Low | Medium |
| Create shared contracts library | Medium | Medium | Medium |
| Add missing integration tests | Low | High | Medium |

---

## Related Documentation

- [Architecture Overview](architecture.md)
- [Provider Management](PROVIDER_MANAGEMENT_ARCHITECTURE.md)
- [Storage Organization](STORAGE_ORGANIZATION_DESIGN.md)
- [Code Improvements](code-improvements.md)
- [Architecture Audit Report](ARCHITECTURE_AUDIT_REPORT.md)

---

*This document is a living proposal. Priorities should be reviewed quarterly based on user feedback and operational experience.*
