# API Reference

This section describes how API documentation is generated, where to find the key namespaces, and how to keep the published API surface up to date.

## How API docs are generated

API pages are generated from XML documentation comments in the .NET projects and built with DocFX.

### Prerequisites

- .NET SDK (matching the repository's target framework)
- DocFX CLI installed and available on `PATH`

### Build API docs

```bash
docfx docs/docfx/docfx.json
```

### Serve locally for preview

```bash
docfx docs/docfx/docfx.json --serve
```

## API map

Use this map as a starting point before diving into generated type/member pages.

### Domain layer

- `TradeDataCollector` - Tick-by-tick trade processing with sequence validation.
- `MarketDepthCollector` - L2 order book maintenance with integrity checks.
- `QuoteCollector` - BBO cache and quote event emission.
- `SymbolSubscriptionTracker` - Thread-safe subscription management base class.

### Application layer

- `EventPipeline` - Bounded channel for event routing.
- `EventPipelinePolicy` - Shared bounded-channel policy configuration.
- `ConfigurationService` - Wizard, auto-config, validation, and reload lifecycle.
- `JsonlStorageSink` - Append-only JSONL persistence.
- `ParquetStorageSink` - Columnar Parquet persistence (experimental).
- `TieredStorageManager` - Hot/warm/cold storage tiering.
- `StatusHttpServer` - Status + Prometheus metrics endpoint.
- `BackfillService` - Historical backfill orchestration.

### Storage and archival layer

- `WriteAheadLog` - Crash-safe persistence with transaction semantics.
- `ArchivalStorageService` - WAL-backed archival writes with checksums.
- `CompressionProfileManager` - Compression profile selection (LZ4/ZSTD/Gzip).
- `SchemaVersionManager` - Schema versioning + migration coordination.
- `AnalysisExportService` - Analysis-oriented export profiles.
- `AnalysisQualityReport` - Pre-export quality checks and reporting.

### Infrastructure layer

#### Streaming providers

- `IBMarketDataClient` - Interactive Brokers streaming client (IBAPI build flag).
- `AlpacaMarketDataClient` - Alpaca WebSocket client.
- `PolygonMarketDataClient` - Polygon streaming client (stub).
- `StockSharpMarketDataClient` - StockSharp connector (multi-source).

#### Historical providers

- `AlpacaHistoricalDataProvider` - Alpaca REST OHLCV bars.
- `YahooFinanceHistoricalDataProvider` - Yahoo Finance EOD data.
- `StooqHistoricalDataProvider` - Stooq EOD data.
- `NasdaqDataLinkHistoricalDataProvider` - Nasdaq Data Link (Quandl).
- `AlphaVantageHistoricalDataProvider` - Alpha Vantage intraday data.
- `FinnhubHistoricalDataProvider` - Finnhub historical fundamentals/market data.
- `TiingoHistoricalDataProvider` - Tiingo premium market data.
- `PolygonHistoricalDataProvider` - Polygon aggregated historical data.
- `IBHistoricalDataProvider` - Interactive Brokers historical data.
- `CompositeHistoricalDataProvider` - Provider failover and fallback orchestration.
- `BaseHistoricalDataProvider` - Shared HTTP/retry/rate-limit base implementation.

#### Symbol discovery and normalization

- `AlpacaSymbolSearchProvider` - Symbol search via Alpaca.
- `FinnhubSymbolSearchProvider` - Global symbol search via Finnhub.
- `PolygonSymbolSearchProvider` - US equities symbol search via Polygon.
- `OpenFigiSymbolResolver` - Cross-provider symbol normalization via OpenFIGI.
- `SymbolNormalization` - Canonical symbol/venue normalization utilities.

#### Infrastructure utilities

- `HttpResponseHandler` - Centralized API error handling and response parsing.
- `CredentialValidator` - API credential and configuration validation.

### Lean integration

- `MarketDataCollectorTradeData` - Lean `BaseData` implementation for trades.
- `MarketDataCollectorQuoteData` - Lean `BaseData` implementation for quotes.
- `MarketDataCollectorDataProvider` - Lean `IDataProvider` integration.

### F# domain library

- `MarketEvents` - Type-safe discriminated unions for domain events.
- `ValidationPipeline` - Railway-oriented validation and error accumulation.
- `Spread`/`Imbalance`/`Aggregations` - Pure calculation modules.
- `Transforms` - Functional stream transformation pipeline.

## Maintenance checklist

When adding or changing public APIs:

1. Update XML documentation comments in source.
2. Regenerate docs with DocFX.
3. Verify generated pages include new members and examples.
4. Cross-link relevant architecture or provider docs when behavior changes.

---

**See also:** [Architecture Overview](../architecture/overview.md) · [Domain Model](../architecture/domains.md) · [Provider Comparison](../providers/provider-comparison.md)
