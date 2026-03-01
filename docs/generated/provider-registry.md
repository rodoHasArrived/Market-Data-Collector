# Provider Registry

> Auto-generated on 2026-03-01 UTC

This document lists all data providers available in the Market Data Collector, their capabilities, rate limits, and configuration requirements.

---

## Real-Time Streaming Providers

All streaming providers implement `IMarketDataClient`. Use `[DataSource]` attribute for discovery.

| Provider | ID | Trades | Quotes | Depth | Priority | Rate Limit | Notes |
|----------|-----|:------:|:------:|:-----:|----------|------------|-------|
| Interactive Brokers | `ib` | Yes | Yes | L2 (10 levels) | 1 | 50 req/sec | Requires TWS or IB Gateway |
| NYSE Direct | `nyse` | Yes | Yes | L1/L2 | 5 | 100 req/min | Hybrid streaming + historical; OAuth2 |
| Alpaca Markets | `alpaca` | Yes | Yes | No | 10 | 200 req/min | WebSocket; IEX (free) or SIP (paid) feeds |
| Polygon.io | `polygon` | Yes | Yes | Yes | 15 | Configurable | Circuit breaker + exponential backoff |
| StockSharp | `stocksharp` | Yes | Yes | Yes | 20 | Varies | 90+ exchange connectors |

### Special Providers

| Provider | Purpose |
|----------|---------|
| `FailoverAwareMarketDataClient` | Wraps primary + backup providers with automatic switching |
| `IBSimulationClient` | IB simulation mode for testing without live connection |
| `NoOpMarketDataClient` | No-operation placeholder for headless/test mode |

---

## Historical Data Providers (Backfill)

All historical providers implement `IHistoricalDataProvider`. Priority determines failover order (lower = tried first).

| Provider | ID | Priority | Free Tier | Data Types | Rate Limit | Markets |
|----------|-----|----------|-----------|------------|------------|---------|
| Alpaca Markets | `alpaca` | 5 | Yes (with account) | Bars, quotes, trades, auctions | 200/min | US |
| Polygon.io | `polygon` | 12 | 2-year history | Bars (intraday) | 5/min | US equities, options, forex, crypto |
| Tiingo | `tiingo` | 15 | Yes | Bars (adjusted) | 50/hour | US, UK, DE, CA, AU |
| Stooq | `stooq` | 15 | Yes (no key needed) | Daily bars | Undocumented | US |
| Finnhub | `finnhub` | 18 | Yes | Bars, dividends, splits | 60/min | US, UK, DE, CA, AU, HK, JP, CN |
| Yahoo Finance | `yahoo` | 22 | Yes (no key needed) | Daily bars (adjusted) | 2000/hour | 50K+ global securities |
| Alpha Vantage | `alphavantage` | 25 | 25 req/day | Intraday bars (unique) | 5/min | US, forex, crypto |
| StockSharp | `stocksharp` | 25 | Connector-dependent | Varies by connector | Connector-specific | Multi-market |
| Nasdaq Data Link | `nasdaq` | 30 | Limited | Bars | 50/day | Dataset-dependent |
| Interactive Brokers | `ibkr` | 80 | Yes (with account) | Bars (intraday, adjusted) | 60/10min window | US, EU, APAC |

### Composite Provider

The `CompositeHistoricalDataProvider` (`composite`) orchestrates all historical providers:
- Automatic failover when primary provider fails or returns no data
- Rate-limit-aware rotation (switches before hitting limits)
- Provider health tracking and degradation scoring
- Configurable priority ordering via `Backfill:ProviderPriority`

---

## Symbol Search Providers

All symbol search providers implement `ISymbolSearchProvider`.

| Provider | ID | Priority | Exchanges | Asset Types | Rate Limit |
|----------|-----|----------|-----------|-------------|------------|
| Alpaca Markets | `alpaca` | 5 | NASDAQ, NYSE, ARCA, AMEX, BATS | US equities, crypto | 200/min |
| Finnhub | `finnhub` | 10 | US, OTC, LSE, TSX, FRA, XETRA, ASX, NSE, SGX, HKEX, TSE + more | Stocks, ADRs, ETFs, warrants, REITs, preferred | 60/min |
| Polygon.io | `polygon` | 15 | XNYS, XNAS, XASE, ARCX, BATS, IEXG + more | CS, ETF, ETN, INDEX, warrants, rights, preferred | 5/min |
| StockSharp | `stocksharp` | 20 | Connector-dependent | Connector-dependent | Varies |
| OpenFIGI | `openfigi` | — | Global (ID mapping) | FIGI ↔ ticker resolution | 25/min (300 with key) |

---

## Provider Capability Matrix

| Capability | IB | Alpaca | Polygon | NYSE | StockSharp | Yahoo | Tiingo | Stooq | Finnhub |
|-----------|:--:|:------:|:-------:|:----:|:----------:|:-----:|:------:|:-----:|:-------:|
| Real-time trades | Yes | Yes | Yes | Yes | Yes | — | — | — | — |
| Real-time quotes | Yes | Yes | Yes | Yes | Yes | — | — | — | — |
| L2 order book | Yes | — | Yes | Yes | Yes | — | — | — | — |
| Historical daily bars | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| Historical intraday | Yes | Yes | Yes | Yes | Yes | — | — | — | — |
| Quote/trade tick data | Yes | Yes | Yes | Yes | Yes | — | — | — | — |
| Adjusted prices | Yes | Yes | Yes | — | Varies | Yes | Yes | — | — |
| Symbol search | — | Yes | Yes | — | Yes | — | — | — | Yes |
| Free tier | Account | Account | Limited | Key needed | Varies | Yes | Yes | Yes | Yes |
| No API key needed | — | — | — | — | — | Yes | — | Yes | — |

---

## Provider Configuration

### Environment Variables

Credentials must be set via environment variables (never in config files):

```bash
# Alpaca
export ALPACA_KEY_ID=your-key-id
export ALPACA_SECRET_KEY=your-secret-key

# Polygon.io
export POLYGON_API_KEY=your-api-key

# Tiingo
export TIINGO_API_TOKEN=your-token

# Finnhub
export FINNHUB_API_KEY=your-key

# Alpha Vantage
export ALPHA_VANTAGE_API_KEY=your-key

# Nasdaq Data Link
export NASDAQ_API_KEY=your-key

# NYSE Connect
export NYSE_API_KEY=your-key
export NYSE_API_SECRET=your-secret
export NYSE_CLIENT_ID=your-client-id

# OpenFIGI (optional, higher rate limits)
export OPENFIGI_API_KEY=your-key

# StockSharp
export MDC_STOCKSHARP_CONNECTOR=Rithmic  # or IQFeed, CQG, InteractiveBrokers
```

### Adding a New Provider

1. Create provider class in `src/MarketDataCollector.Infrastructure/Providers/{Name}/`
2. Implement `IMarketDataClient` (streaming) or `IHistoricalDataProvider` (backfill)
3. Add `[DataSource("provider-id")]` attribute with provider metadata
4. Add `[ImplementsAdr("ADR-001", "reason")]` attribute for ADR compliance
5. Register in DI container via `ServiceCompositionRoot`
6. Add configuration section in `config/appsettings.sample.json`
7. Write tests in `tests/MarketDataCollector.Tests/Infrastructure/Providers/`

See [Provider Implementation Guide](../development/provider-implementation.md) for detailed patterns.

---

*Generated from code annotations and `[DataSource]` attributes — 2026-03-01*
