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
- `QuoteCollector` - BBO state cache and quote event emission
- `SymbolSubscriptionTracker` - Thread-safe subscription management base class

### Application Layer
- `EventPipeline` - Bounded channel for event routing (50K capacity)
- `JsonlStorageSink` - Append-only JSONL file storage
- `ParquetStorageSink` - Columnar Parquet storage (experimental)
- `TieredStorageManager` - Hot/warm/cold storage tier management
- `StatusHttpServer` - HTTP monitoring server with Prometheus metrics
- `BackfillService` - Historical data backfill job management

### Storage & Archival Layer (v1.5.0)
- `WriteAheadLog` - Crash-safe persistence with transaction semantics
- `ArchivalStorageService` - WAL-backed archival storage with checksums
- `CompressionProfileManager` - Tiered compression profiles (LZ4/ZSTD/Gzip)
- `SchemaVersionManager` - Schema versioning and automatic migration
- `AnalysisExportService` - Analysis-ready export with tool-specific profiles
- `AnalysisQualityReport` - Pre-export data quality assessment and reporting

### Infrastructure Layer

#### Streaming Providers
- `IBMarketDataClient` - Interactive Brokers provider (requires IBAPI build flag)
- `AlpacaMarketDataClient` - Alpaca WebSocket provider
- `PolygonMarketDataClient` - Polygon provider (stub)
- `StockSharpMarketDataClient` - StockSharp connector (90+ sources)

#### Historical Data Providers
- `AlpacaHistoricalDataProvider` - Alpaca REST API for OHLCV bars
- `YahooFinanceHistoricalDataProvider` - Yahoo Finance EOD data
- `StooqHistoricalDataProvider` - Stooq EOD data
- `NasdaqDataLinkHistoricalDataProvider` - Nasdaq Data Link (Quandl)
- `AlphaVantageHistoricalDataProvider` - Alpha Vantage intraday data
- `FinnhubHistoricalDataProvider` - Finnhub financial data
- `TiingoHistoricalDataProvider` - Tiingo premium data
- `PolygonHistoricalDataProvider` - Polygon aggregated data
- `CompositeHistoricalDataProvider` - Automatic failover across providers

### Lean Integration
- `MarketDataCollectorTradeData` - Custom Lean BaseData for trades
- `MarketDataCollectorQuoteData` - Custom Lean BaseData for quotes
- `MarketDataCollectorDataProvider` - Lean IDataProvider implementation

### F# Domain Library
- `MarketEvents` - Type-safe discriminated unions for all event types
- `ValidationPipeline` - Railway-oriented validation with error accumulation
- `Spread/Imbalance/Aggregations` - Pure functional calculation modules
- `Transforms` - Pipeline transformation functions for stream processing

---

**Version:** 1.6.0
**Last Updated:** 2026-01-19
**See Also:** [Architecture Overview](../architecture/overview.md) | [Domain Model](../architecture/domains.md) | [Lean Integration](../integrations/lean-integration.md)
