# API Reference

This section provides generated API documentation from the source code XML documentation.

## Building the API Docs

Build the API documentation with DocFX:

```bash
docfx docs/docfx/docfx.json
```

## API Components

### Domain Layer
- `TradeDataCollector` - Tick-by-tick trade processing with sequence validation
- `MarketDepthCollector` - L2 order book maintenance with integrity checking
- `HighPerformanceMarketDepthCollector` - Optimized depth collector for high-volume streams
- `QuoteCollector` - BBO state cache and quote event emission
- `SymbolSubscriptionTracker` - Thread-safe subscription management base class

### Application Layer
- `EventPipeline` - Bounded channel for event routing (50K capacity)
- `JsonlStorageSink` - Append-only JSONL file storage
- `ParquetStorageSink` - Columnar Parquet storage (experimental)
- `TieredStorageManager` - Hot/warm/cold storage tier management
- `StatusHttpServer` - HTTP monitoring server with Prometheus metrics
- `BackfillService` - Historical data backfill job management

### Infrastructure Layer

#### Streaming Providers
- `IBMarketDataClient` - Interactive Brokers provider (requires IBAPI build flag)
- `AlpacaMarketDataClient` - Alpaca WebSocket provider
- `PolygonMarketDataClient` - Polygon provider (stub)

#### Historical Data Providers
- `AlpacaHistoricalDataProvider` - Alpaca REST API for OHLCV bars
- `YahooFinanceHistoricalDataProvider` - Yahoo Finance EOD data
- `StooqHistoricalDataProvider` - Stooq EOD data
- `NasdaqDataLinkHistoricalDataProvider` - Nasdaq Data Link (Quandl)
- `CompositeHistoricalDataProvider` - Automatic failover across providers

### Messaging Layer
- `CompositePublisher` - Publishes to local storage and message bus
- `MassTransitConfiguration` - RabbitMQ/Azure Service Bus setup
- Trade, Quote, L2Snapshot consumers

### Lean Integration
- `MarketDataCollectorTradeData` - Custom Lean BaseData for trades
- `MarketDataCollectorQuoteData` - Custom Lean BaseData for quotes
- `MarketDataCollectorDataProvider` - Lean IDataProvider implementation

---

**Version:** 1.4.0
**Last Updated:** 2026-01-04
**See Also:** [architecture.md](../architecture.md) | [domains.md](../domains.md) | [lean-integration.md](../lean-integration.md)
