# Why This Architecture (Non-Engineer Explainer)

## What this program does
Market Data Collector captures **live** and **historical** market microstructure data, validates it for quality, and stores it in audit-friendly formats so it can be replayed, analyzed, or fed into research tools.

It collects:
- **Trades:** tick-by-tick prints with sequence checks and quality validation
- **Quotes:** best bid/offer (BBO) updates for context and spread health
- **Depth:** Level 2 order book updates with integrity checks
- **Backfill:** historical bars and supplemental data from multiple providers

It also provides **monitoring dashboards**, **Prometheus metrics**, and **export tooling** for downstream analysis.

---

## Why we split it into layers

### 1) Provider Adapters (the “translators”)
Each data provider speaks its own API and protocol. We isolate them so:
- providers can be swapped or added without touching the core logic
- failures or quirks in one feed don’t poison the whole system
- historical backfill can run independently of live capture

Examples of current adapters:
- **Live providers:** Interactive Brokers, Alpaca, NYSE Direct, StockSharp (Polygon currently runs in stub mode)
- **Historical/backfill providers:** Alpaca, Yahoo Finance, Stooq, Tiingo, Finnhub, Alpha Vantage, Nasdaq Data Link, Polygon
- **Symbol resolution:** OpenFIGI-based resolution for cross-provider symbol mapping

### 2) Domain Logic (the “brains”)
This layer decides what the incoming data *means* and whether it’s valid:
- `TradeDataCollector` validates trade sequences and produces order-flow stats
- `MarketDepthCollector` maintains the order book and emits integrity events
- `QuoteCollector` tracks BBO state and quote context

Because this layer is provider-agnostic, it can be tested without a live feed.

### 3) Application Services (the “conductor”)
This is the orchestration layer that wires everything together and exposes tooling:
- CLI modes for **wizard setup**, **auto-config**, and **credential validation**
- Subscription management and backfill scheduling
- Health checks, data quality checks, and alerting hooks
- HTTP status server for dashboard and metrics endpoints

### 4) Pipeline + Storage (the “transport and memory”)
All domain events flow through a bounded, backpressured pipeline to prevent runaway memory use:
- `EventPipeline` uses a bounded channel (default **100,000 events**) with drop policies
- Storage sinks include **JSONL** and **Parquet**
- **Write-ahead logging (WAL)** for crash-safe persistence
- **Compression profiles**, **schema versioning**, **retention policies**, and **replay tooling**
- Export profiles for analytics (Python/R/Lean/SQL-friendly exports)

### 5) Presentation + Monitoring (the “eyes and dashboard”)
The system exposes status and monitoring through:
- Web dashboard (HTTP status server + UI)
- Prometheus metrics endpoint
- Native Windows UWP desktop app for monitoring and configuration

---

## Why this is safer and more “institutional”
- **Audit-first storage:** append-only JSONL/Parquet with WAL for durability
- **Data quality enforcement:** integrity events, spread checks, timestamp checks, and tick-size validation
- **Provider isolation:** adapters can fail or be replaced without corrupting core logic
- **Backpressure protection:** bounded queues prevent memory runaway under load
- **Operational visibility:** live metrics, status endpoints, and UI dashboards

---

## Current capabilities (as implemented in this repo)

### Implemented today
- Live capture from IB, Alpaca, NYSE Direct, StockSharp (Polygon adapter runs in stub mode today)
- Historical backfill with provider failover and rate limiting
- Integrity event emission for trade sequences and order book consistency
- Quote-aware analytics (BBO context)
- Storage in JSONL and Parquet with retention, compression, and WAL
- Data replay and export tooling for downstream analysis
- Monitoring via Prometheus metrics, status JSON, and web/UWP dashboards
- QuantConnect Lean integration for backtesting

### Notes on provider maturity
- Polygon is currently implemented in **stub mode** (synthetic events) until the full WebSocket client is completed.

---

**Version:** 1.6.0
**Last Updated:** 2026-01-26
**See Also:** [Architecture Overview](overview.md) | [Domains](domains.md) | [C4 Diagrams](c4-diagrams.md) | [Lean Integration](../integrations/lean-integration.md)
