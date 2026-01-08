# Historical Data Sources Reference

**Last Updated:** 2026-01-08
**Version:** 1.5.0

This document catalogs available free and freemium data sources for historical equity backfilling, with implementation status for each.

---

## Implementation Status Summary

| Provider | Status | Coverage | Free Tier |
|----------|--------|----------|-----------|
| Alpaca | ✅ Implemented | US equities | Real-time + historical |
| Yahoo Finance | ✅ Implemented | 50K+ global | Unlimited |
| Stooq | ✅ Implemented | US equities | Unlimited |
| Nasdaq Data Link | ✅ Implemented | Alternative data | Limited |
| Composite Provider | ✅ Implemented | Multi-source | N/A |
| Interactive Brokers | ⚠️ Requires IBAPI | US equities | With account |
| Polygon | ❌ Stub only | US equities | 5 calls/min |
| Tiingo | 📋 Planned | 65K+ securities | 1K/day |

---

## Implemented Providers

### 1. Alpaca Historical Data

**File:** `Infrastructure/HistoricalData/AlpacaHistoricalDataProvider.cs`
**Status:** ✅ Production Ready

| Attribute | Details |
|-----------|---------|
| **Coverage** | US equities, ETFs |
| **Data Types** | OHLCV bars, trades, quotes, auctions |
| **Free Tier** | Unlimited with free account |
| **Feed Options** | IEX (free) or SIP (paid) |

**Features:**
- Price adjustment support
- Multiple timeframes (1min to 1month)
- Auction data for opening/closing prices

---

### 2. Yahoo Finance

**File:** `Infrastructure/HistoricalData/YahooFinanceHistoricalDataProvider.cs`
**Status:** ✅ Production Ready

| Attribute | Details |
|-----------|---------|
| **Coverage** | 50,000+ global equities, ETFs, indices, crypto, forex |
| **Data Types** | Daily/Weekly/Monthly OHLCV, adjusted close, dividends, splits |
| **Free Tier** | Unlimited (unofficial API) |
| **Rate Limits** | ~2,000 requests/hour |
| **Historical Depth** | 20+ years for major equities |

**Symbol Format:**
- US: `AAPL`, `MSFT`
- London: `TSLA.L`
- Tokyo: `7203.T`

**API Endpoint:**
```
https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={unix}&period2={unix}&interval=1d
```

---

### 3. Stooq

**File:** `Infrastructure/HistoricalData/StooqHistoricalDataProvider.cs`
**Status:** ✅ Production Ready

| Attribute | Details |
|-----------|---------|
| **Coverage** | Global equities, indices, forex, commodities |
| **Data Types** | Daily OHLCV |
| **Free Tier** | Unlimited |
| **Historical Depth** | 20+ years |

**Symbol Format:**
- US equities: `AAPL.US`, `MSFT.US`
- Indices: `^SPX`, `^DJI`
- International: `.UK`, `.DE`, `.JP` suffixes

---

### 4. Nasdaq Data Link (formerly Quandl)

**File:** `Infrastructure/HistoricalData/NasdaqDataLinkHistoricalDataProvider.cs`
**Status:** ✅ Production Ready

| Attribute | Details |
|-----------|---------|
| **Coverage** | Alternative data, macro, commodities |
| **Data Types** | Time series, tables |
| **Free Tier** | Limited datasets |
| **Rate Limits** | 300 calls/10 sec |

**Best For:**
- FRED economic data
- CFTC commitment of traders
- Commodity data

---

### 5. Composite Provider

**File:** `Infrastructure/HistoricalData/CompositeHistoricalDataProvider.cs`
**Status:** ✅ Production Ready

**Features:**
- Automatic failover across providers
- Rate-limit rotation
- Priority-based provider selection
- Health checking

**Configuration:**
```csharp
var composite = new CompositeHistoricalDataProvider(new[]
{
    yahooProvider,   // Priority 1
    stooqProvider,   // Priority 2
    alpacaProvider   // Priority 3
});
```

---

## Providers Requiring Configuration

### Interactive Brokers

**Status:** ⚠️ Requires `IBAPI` build flag

See [interactive-brokers-setup.md](interactive-brokers-setup.md) and [interactive-brokers-free-equity-reference.md](interactive-brokers-free-equity-reference.md) for detailed configuration.

| Attribute | Details |
|-----------|---------|
| **Coverage** | US equities, options, futures |
| **Data Types** | Tick, minute, daily bars |
| **Requirements** | $500 account balance, TWS/Gateway |
| **Free Data** | Cboe One + IEX streaming |

**Build Command:**
```bash
dotnet build -p:DefineConstants=IBAPI
```

---

## Planned Providers

### Tiingo

**Status:** 📋 Planned

| Attribute | Details |
|-----------|---------|
| **Coverage** | 65,000+ US/international equities, ETFs, mutual funds |
| **Data Types** | Daily OHLCV, adjusted prices, dividends, splits |
| **Free Tier** | 1,000 requests/day, 50 requests/hour |
| **Historical Depth** | 30+ years for major equities |

**Best For:** Dividend-adjusted historical data with full corporate actions.

**API Endpoint:**
```
https://api.tiingo.com/tiingo/daily/{symbol}/prices?startDate={from}&endDate={to}&token={key}
```

---

### Finnhub

**Status:** 📋 Planned

| Attribute | Details |
|-----------|---------|
| **Coverage** | 60,000+ global securities |
| **Data Types** | Daily OHLCV, company profiles, fundamentals, earnings |
| **Free Tier** | 60 API calls/minute (generous) |

**Best For:** Company fundamentals, earnings calendar, news sentiment.

---

### Alpha Vantage

**Status:** 📋 Planned (limited usefulness)

| Attribute | Details |
|-----------|---------|
| **Coverage** | US equities, global indices, forex, crypto |
| **Data Types** | Daily/Weekly/Monthly OHLCV, **intraday (1/5/15/30/60 min)** |
| **Free Tier** | 25 requests/day (severely limited) |

**Note:** Useful for intraday historical data, but free tier too limited for bulk backfill.

---

## Reference Data Sources

### FRED (Federal Reserve Economic Data)

| Attribute | Details |
|-----------|---------|
| **Coverage** | 800,000+ economic time series |
| **Data Types** | Macro indicators (rates, GDP, employment, etc.) |
| **Free Tier** | Unlimited |
| **Rate Limits** | 120 requests/min |

**Use Cases:** Federal funds rate, Treasury yields, unemployment, GDP, CPI.

---

### OpenFIGI

| Attribute | Details |
|-----------|---------|
| **Coverage** | Global financial instruments |
| **Data Types** | FIGI identifiers, security master data |
| **Free Tier** | Unlimited |
| **Rate Limits** | 25 requests/min |

**Use Cases:** Symbol normalization, instrument identification across exchanges.

---

## Data Quality Matrix

| Source | Split Adjusted | Dividend Adjusted | Corporate Actions |
|--------|---------------|-------------------|-------------------|
| Stooq | Yes | No | No |
| Yahoo Finance | Yes | Yes (adj close) | Dividends, Splits |
| Alpaca | Yes | Yes | Dividends, Splits |
| Tiingo | Yes | Yes (all OHLCV) | Full |
| Interactive Brokers | Yes | Yes | Full |

---

## Rate Limiting Strategy

For bulk backfilling with multiple free-tier APIs:

| Provider | Rate Limit | Suggested Delay |
|----------|------------|-----------------|
| Yahoo Finance | 2000/hour | 2 seconds |
| Stooq | Respectful | 1 second |
| Alpaca | Unlimited | None |
| Tiingo | 50/hour | 72 seconds |
| Finnhub | 60/min | 1 second |
| Alpha Vantage | 5/min | 12 seconds |

---

## Related Documentation

- [Configuration](../guides/configuration.md) - Provider configuration options
- [IB Setup](interactive-brokers-setup.md) - IB TWS/Gateway setup
- [IB API Reference](interactive-brokers-free-equity-reference.md) - IB API technical reference
- [Operator Runbook](../guides/operator-runbook.md) - Backfill operations
