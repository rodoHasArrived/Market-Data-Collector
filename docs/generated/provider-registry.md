# Provider Registry

> Auto-generated on 2026-02-25 17:06:11 UTC

This document lists all data providers available in the Market Data Collector.

## Real-Time Streaming Providers

| Provider | ID | Class | Type | Category | Status |
|----------|-----|-------|------|----------|--------|
| Interactive Brokers | `ib` | `IBMarketDataClient` | Realtime | Broker | ✅ Active |
| StockSharp | `stocksharp` | `StockSharpMarketDataClient` | Realtime | Aggregator | ✅ Active |
| NYSE Direct | `nyse` | `NYSEDataSource` | Hybrid | Exchange | ✅ Active |
| Polygon.io | `polygon` | `PolygonMarketDataClient` | Realtime | Aggregator | ✅ Active |
| Alpaca Markets | `alpaca` | `AlpacaMarketDataClient` | Realtime | Broker | ✅ Active |

## Historical Data Providers (Backfill)

| Provider | ID | Free Tier | Rate Limits |
|----------|-----|-----------|-------------|
| Yahoo Finance | `yahoo-finance` | Yes | Varies |
| Stooq | `stooq` | Yes | Varies |
| Tiingo | `tiingo` | Yes | Varies |
| Alpha Vantage | `alpha-vantage` | Yes | Varies |
| Finnhub | `finnhub` | Yes | Varies |
| Nasdaq Data Link | `nasdaq-data-link` | Yes | Varies |

## Provider Configuration

Providers are configured via environment variables or `appsettings.json`:

```bash
# Real-time providers
export ALPACA__KEYID=your-key-id
export ALPACA__SECRETKEY=your-secret-key
export POLYGON__APIKEY=your-api-key

# Historical providers
export TIINGO__TOKEN=your-token
export ALPHAVANTAGE__APIKEY=your-key
```

## Adding a New Provider

1. Create provider class in `src/MarketDataCollector.Infrastructure/Providers/{Name}/`
2. Implement `IMarketDataClient` (streaming) or `IHistoricalDataProvider` (backfill)
3. Add `[DataSource]` attribute with provider metadata
4. Add `[ImplementsAdr]` attributes for ADR compliance
5. Register in DI container
6. Add configuration section
7. Write tests

---

*This file is auto-generated. Do not edit manually.*
