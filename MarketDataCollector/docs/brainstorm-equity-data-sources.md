# Brainstorming: Free Equity Data Sources for Historical Backfilling

> **Date:** January 2026
> **Context:** Market Data Collector v1.0 - Expanding historical data provider coverage
> **Current State:** Stooq (free daily OHLCV for US equities)

---

## Executive Summary

This document explores free and freemium data sources for backfilling historical equity data. The goal is to expand beyond the current Stooq provider to improve:

1. **Geographic coverage** - International equities beyond US markets
2. **Data resolution** - Intraday bars (minute/hourly) in addition to daily
3. **Data richness** - Adjusted prices, dividends, splits, corporate actions
4. **Reliability** - Multiple fallback sources for resilience
5. **Asset expansion** - ETFs, indices, and eventually options/futures

---

## Current Architecture

The existing `IHistoricalDataProvider` interface makes adding new sources straightforward:

```csharp
public interface IHistoricalDataProvider
{
    Task<IEnumerable<HistoricalBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}
```

Each provider normalizes data to `HistoricalBar` (Symbol, Date, OHLCV, Source, Sequence).

---

## Tier 1: Primary Free Data Sources (Recommended)

### 1. Yahoo Finance (yfinance)

| Attribute | Details |
|-----------|---------|
| **Coverage** | 50,000+ global equities, ETFs, indices, crypto, forex |
| **Data Types** | Daily/Weekly/Monthly OHLCV, adjusted close, dividends, splits |
| **Free Tier** | Unlimited (unofficial API, community-maintained) |
| **Rate Limits** | ~2,000 requests/hour (soft limit, respectful usage) |
| **Historical Depth** | 20+ years for major equities |
| **Formats** | JSON (via API libraries) |

**Pros:**
- Broadest free coverage available
- Includes adjusted prices (stock splits, dividends)
- No API key required
- Active community support (yfinance Python, YahooFinanceAPI .NET)
- Real-time quotes available (15-min delayed)

**Cons:**
- Unofficial API (could break without notice)
- No intraday historical data (only live)
- Rate limiting can be aggressive during high usage
- Data quality inconsistencies for smaller securities

**Implementation Notes:**
```
URL Pattern: https://query1.finance.yahoo.com/v8/finance/chart/{symbol}
Parameters: ?period1={unix_start}&period2={unix_end}&interval=1d
Symbol Format: AAPL, MSFT (US), TSLA.L (London), 7203.T (Tokyo)
```

**Priority: HIGH** - Best overall free coverage for daily OHLCV

---

### 2. Alpha Vantage

| Attribute | Details |
|-----------|---------|
| **Coverage** | US equities, global indices, forex, crypto |
| **Data Types** | Daily/Weekly/Monthly OHLCV, **intraday (1/5/15/30/60 min)** |
| **Free Tier** | 25 requests/day (previously 500/day) |
| **Rate Limits** | 5 calls/minute |
| **Historical Depth** | 20+ years daily, 1-2 months intraday |
| **Formats** | JSON, CSV |

**Pros:**
- **Intraday historical data** - unique among free sources
- Adjusted prices available
- Official, stable API
- Good documentation
- Technical indicators (SMA, EMA, RSI, etc.) pre-computed

**Cons:**
- Severely limited free tier (25 calls/day as of 2024)
- Rate limiting requires careful batching
- Premium ($50/mo) needed for production usage

**Implementation Notes:**
```
Endpoint: https://www.alphavantage.co/query
Daily: ?function=TIME_SERIES_DAILY_ADJUSTED&symbol={symbol}&outputsize=full&apikey={key}
Intraday: ?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval=1min&outputsize=full&apikey={key}
```

**Priority: MEDIUM** - Valuable for intraday, but free tier too limited for bulk backfill

---

### 3. Polygon.io (Free Tier)

| Attribute | Details |
|-----------|---------|
| **Coverage** | US equities, options, forex, crypto |
| **Data Types** | Daily OHLCV, aggregates, trades, quotes, reference data |
| **Free Tier** | 5 API calls/minute, delayed data, 2 years history |
| **Rate Limits** | 5 calls/min (free), unlimited (paid) |
| **Historical Depth** | 2 years (free), full history (paid) |
| **Formats** | JSON |

**Pros:**
- Already have WebSocket integration (live data)
- REST API well-documented
- High data quality
- Reference data (ticker details, exchanges)
- Corporate actions, dividends, splits included

**Cons:**
- Free tier has delayed data (15 min)
- Limited to 2 years historical
- Aggressive rate limiting on free tier

**Implementation Notes:**
```
Endpoint: https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{from}/{to}
Headers: Authorization: Bearer {api_key}
Date Format: YYYY-MM-DD
```

**Priority: HIGH** - Leverage existing Polygon relationship, good data quality

---

### 4. Tiingo

| Attribute | Details |
|-----------|---------|
| **Coverage** | 65,000+ US/international equities, ETFs, mutual funds |
| **Data Types** | Daily OHLCV, **adjusted prices, dividends, splits** |
| **Free Tier** | 1,000 requests/day, 50 requests/hour |
| **Rate Limits** | 50/hour, 1000/day |
| **Historical Depth** | 30+ years for major equities |
| **Formats** | JSON, CSV |

**Pros:**
- Excellent dividend/split adjustment
- Corporate actions data
- Mutual fund coverage
- Good international coverage
- Stable, reliable API

**Cons:**
- Limited free tier for bulk operations
- No intraday historical data
- Requires registration

**Implementation Notes:**
```
Endpoint: https://api.tiingo.com/tiingo/daily/{symbol}/prices
Parameters: ?startDate={from}&endDate={to}&token={api_key}
Includes: adjOpen, adjHigh, adjLow, adjClose, adjVolume, divCash, splitFactor
```

**Priority: HIGH** - Best for dividend-adjusted historical data

---

### 5. Finnhub

| Attribute | Details |
|-----------|---------|
| **Coverage** | 60,000+ global securities |
| **Data Types** | Daily OHLCV, company profiles, fundamentals, earnings, news |
| **Free Tier** | 60 API calls/minute |
| **Rate Limits** | 60/min (generous) |
| **Historical Depth** | Varies by endpoint |
| **Formats** | JSON |

**Pros:**
- **Generous free tier** (60 calls/min)
- Company fundamentals included
- Earnings calendar
- News and sentiment data
- Insider trading data

**Cons:**
- Historical stock data requires premium for full depth
- Free tier has limited historical range

**Implementation Notes:**
```
Stock Candles: /stock/candle?symbol={symbol}&resolution=D&from={unix}&to={unix}&token={key}
Company Profile: /stock/profile2?symbol={symbol}&token={key}
Earnings: /calendar/earnings?from={date}&to={date}&token={key}
```

**Priority: MEDIUM** - Good for fundamentals, generous rate limits

---

## Tier 2: Secondary Free Data Sources

### 6. IEX Cloud (Legacy Free Tier)

| Attribute | Details |
|-----------|---------|
| **Coverage** | US equities, ETFs |
| **Data Types** | Historical OHLCV, real-time quotes, fundamentals |
| **Free Tier** | 50,000 core messages/month (previously more generous) |
| **Rate Limits** | 100 requests/second |
| **Historical Depth** | 5+ years |

**Notes:**
- IEX has progressively limited free tier
- Still valuable for reference data and corporate actions
- Best for US-only strategies

**Priority: LOW** - Free tier too limited now

---

### 7. Nasdaq Data Link (formerly Quandl)

| Attribute | Details |
|-----------|---------|
| **Coverage** | Alternative data, macro, commodities, some equities |
| **Data Types** | Time series, tables |
| **Free Tier** | Limited datasets |
| **Rate Limits** | 300 calls/10 sec |
| **Historical Depth** | Varies by dataset |

**Notes:**
- Best for alternative/macro data, not equity prices
- Wiki Prices dataset was discontinued
- Still useful for FRED, CFTC, commodity data

**Priority: LOW** - Limited equity coverage

---

### 8. EOD Historical Data

| Attribute | Details |
|-----------|---------|
| **Coverage** | 150,000+ securities globally |
| **Data Types** | Daily OHLCV, fundamentals, dividends, splits |
| **Free Tier** | 20 API calls/day |
| **Rate Limits** | 20/day (very limited) |
| **Historical Depth** | 30+ years |

**Notes:**
- Excellent global coverage
- Free tier insufficient for meaningful backfill
- Good for targeted international symbol fills

**Priority: LOW** - Too limited for free usage

---

### 9. EODHD (EOD Historical Data)

Similar to #8 - global coverage but limited free tier.

---

### 10. Stooq (Current Provider)

| Attribute | Details |
|-----------|---------|
| **Coverage** | Global equities, indices, forex, commodities |
| **Data Types** | Daily OHLCV |
| **Free Tier** | Unlimited |
| **Rate Limits** | Respectful usage expected |
| **Historical Depth** | 20+ years |

**Current Status:** Already implemented. Excellent for US equities with `.us` suffix.

**Expansion Opportunity:**
- Add support for international markets (`.uk`, `.de`, `.jp`, etc.)
- Add index data (e.g., `^SPX`, `^DJI`)

---

## Tier 3: Specialized/Alternative Sources

### 11. FRED (Federal Reserve Economic Data)

| Attribute | Details |
|-----------|---------|
| **Coverage** | 800,000+ economic time series |
| **Data Types** | Macro indicators (rates, GDP, employment, etc.) |
| **Free Tier** | Unlimited |
| **Rate Limits** | 120 requests/min |

**Use Cases:**
- Federal funds rate
- Treasury yields
- Unemployment rate
- GDP growth
- Inflation (CPI, PCE)

**Priority: MEDIUM** - Essential for macro-aware strategies

---

### 12. SEC EDGAR

| Attribute | Details |
|-----------|---------|
| **Coverage** | All US public companies |
| **Data Types** | 10-K, 10-Q, 8-K filings, insider transactions |
| **Free Tier** | Unlimited |
| **Rate Limits** | 10 requests/second |

**Use Cases:**
- Quarterly/annual financial statements
- Insider trading Form 4
- Material events (8-K)
- Ownership (13F)

**Priority: LOW** - For fundamental analysis enhancement

---

### 13. OpenFIGI

| Attribute | Details |
|-----------|---------|
| **Coverage** | Global financial instruments |
| **Data Types** | FIGI identifiers, security master data |
| **Free Tier** | Unlimited |
| **Rate Limits** | 25 requests/min |

**Use Cases:**
- Symbol normalization across exchanges
- Instrument identification
- Mapping between ticker systems

**Priority: MEDIUM** - Essential for multi-market symbol resolution

---

### 14. Exchange Calendars

Several free sources for trading calendars:

- **trading_calendars** (Python library)
- **Exchange websites** (NYSE, NASDAQ, LSE)
- **ISO 10383 MIC codes**

**Priority: MEDIUM** - Important for accurate date handling

---

## Data Quality Considerations

### Adjustment Factors

| Source | Split Adjusted | Dividend Adjusted | Corporate Actions |
|--------|---------------|-------------------|-------------------|
| Stooq | Yes | No | No |
| Yahoo Finance | Yes | Yes (adj close) | Dividends, Splits |
| Alpha Vantage | Yes | Yes | Dividends, Splits |
| Polygon | Yes | Yes | Full corporate actions |
| Tiingo | Yes | Yes (all OHLCV) | Full corporate actions |
| Finnhub | Partial | No | Limited |

### Recommended Adjustment Strategy

```
1. Prefer Tiingo for dividend-adjusted prices (adjOpen, adjHigh, adjLow, adjClose)
2. Use Yahoo Finance for broad coverage with basic adjustments
3. Cross-validate with multiple sources for critical symbols
4. Store both raw and adjusted prices when possible
```

---

## Implementation Recommendations

### Phase 1: Immediate (High Impact)

1. **Yahoo Finance Provider**
   - Broadest free coverage
   - No API key required
   - Implementation: 1-2 days

2. **Polygon REST Provider**
   - Leverage existing WebSocket integration
   - Good data quality
   - Implementation: 1 day

3. **Stooq Enhancement**
   - Add international market suffixes
   - Add index support
   - Implementation: 0.5 days

### Phase 2: Short-term (Enhanced Quality)

4. **Tiingo Provider**
   - Best dividend-adjusted data
   - Corporate actions support
   - Implementation: 1-2 days

5. **Multi-Source Fallback**
   - Primary/secondary/tertiary source configuration
   - Automatic failover on errors
   - Cross-validation for data integrity

### Phase 3: Future (Specialized)

6. **Alpha Vantage** (for intraday when needed)
7. **Finnhub** (for fundamentals integration)
8. **FRED** (for macro data)
9. **OpenFIGI** (for symbol normalization)

---

## Proposed Provider Interface Enhancement

To support multiple sources with fallback and cross-validation:

```csharp
public interface IHistoricalDataProviderV2 : IHistoricalDataProvider
{
    // Provider metadata
    string Name { get; }
    int Priority { get; }  // Lower = higher priority
    TimeSpan RateLimit { get; }

    // Capabilities
    bool SupportsAdjustedPrices { get; }
    bool SupportsIntraday { get; }
    bool SupportsDividends { get; }
    bool SupportsSplits { get; }
    IEnumerable<string> SupportedMarkets { get; }

    // Health check
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

public class CompositeHistoricalDataProvider : IHistoricalDataProvider
{
    private readonly IEnumerable<IHistoricalDataProviderV2> _providers;

    public async Task<IEnumerable<HistoricalBar>> GetHistoricalBarsAsync(
        string symbol, DateTime from, DateTime to, CancellationToken ct)
    {
        foreach (var provider in _providers.OrderBy(p => p.Priority))
        {
            if (!await provider.IsAvailableAsync(ct)) continue;

            try
            {
                var bars = await provider.GetHistoricalBarsAsync(symbol, from, to, ct);
                if (bars.Any()) return bars;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Name} failed for {Symbol}",
                    provider.Name, symbol);
            }
        }

        throw new NoDataAvailableException(symbol, from, to);
    }
}
```

---

## Rate Limiting Strategy

For bulk backfilling with multiple free-tier APIs:

```csharp
public class RateLimitedProviderWrapper
{
    private readonly SemaphoreSlim _semaphore;
    private readonly RateLimiter _rateLimiter;

    // Suggested limits by provider
    // Yahoo: 2000/hour = ~33/min = 1 every 2 seconds
    // Alpha Vantage: 5/min = 1 every 12 seconds
    // Polygon: 5/min = 1 every 12 seconds
    // Tiingo: 50/hour = 1 every 72 seconds
    // Finnhub: 60/min = 1/second
}
```

---

## Summary: Priority Matrix

| Provider | Coverage | Free Tier | Data Quality | Priority |
|----------|----------|-----------|--------------|----------|
| Yahoo Finance | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | **HIGH** |
| Polygon REST | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **HIGH** |
| Tiingo | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **HIGH** |
| Finnhub | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | **MEDIUM** |
| Alpha Vantage | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | **MEDIUM** |
| FRED | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **MEDIUM** |
| IEX Cloud | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | **LOW** |
| EOD Historical | ⭐⭐⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐ | **LOW** |

---

## Next Steps

1. [ ] Implement Yahoo Finance provider
2. [ ] Implement Polygon REST historical endpoint
3. [ ] Add Tiingo provider for dividend-adjusted data
4. [ ] Create composite provider with fallback logic
5. [ ] Add rate limiting infrastructure
6. [ ] Enhance HistoricalBar to include adjustment metadata
7. [ ] Add provider health monitoring to dashboard

---

## Appendix: API Quick Reference

### Yahoo Finance
```
GET https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={unix}&period2={unix}&interval=1d
```

### Polygon
```
GET https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{from}/{to}?apiKey={key}
```

### Tiingo
```
GET https://api.tiingo.com/tiingo/daily/{symbol}/prices?startDate={from}&endDate={to}&token={key}
```

### Alpha Vantage
```
GET https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED&symbol={symbol}&apikey={key}
```

### Finnhub
```
GET https://finnhub.io/api/v1/stock/candle?symbol={symbol}&resolution=D&from={unix}&to={unix}&token={key}
```

### FRED
```
GET https://api.stlouisfed.org/fred/series/observations?series_id={id}&api_key={key}&file_type=json
```

---

**Version:** 1.1.0
**Last Updated:** 2026-01-02
**See Also:** [DEPENDENCIES.md](../MarketDataCollector/DEPENDENCIES.md) | [CONFIGURATION.md](../MarketDataCollector/docs/CONFIGURATION.md)
