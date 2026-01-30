# Historical Data Backfill Guide

**Last Updated:** 2026-01-30
**Version:** 1.6.1

This document provides a comprehensive guide for backfilling historical market data using the Market Data Collector.

---

## Overview

Historical backfill is the process of retrieving past market data to fill gaps in your data archive. The Market Data Collector supports 9 historical data providers with automatic failover and rate limiting.

### Key Features

- **9 Data Providers**: Alpaca, Yahoo Finance, Stooq, Tiingo, Finnhub, Alpha Vantage, Polygon, Nasdaq Data Link, IB
- **Composite Provider**: Automatic failover across providers
- **Rate Limiting**: Built-in throttling to respect API limits
- **Priority Queue**: Prioritize important symbols
- **Gap Detection**: Automatically identify missing data
- **Quality Reports**: Data quality assessment before export

---

## Quick Start

### 1. Configure Providers

Set API keys for your preferred providers:

```bash
# Required for Tiingo
export TIINGO_API_TOKEN="your-token"

# Required for Alpaca
export ALPACA_KEY_ID="your-key"
export ALPACA_SECRET_KEY="your-secret"

# Required for Finnhub
export FINNHUB_API_KEY="your-key"

# Optional providers (no auth required)
# Yahoo Finance - no key needed
# Stooq - no key needed
```

### 2. Run Backfill

**Via CLI:**
```bash
dotnet run -- --backfill AAPL,MSFT,GOOGL --from 2025-01-01 --to 2026-01-01
```

**Via UWP App:**
1. Open the **Backfill** page
2. Enter symbols (comma-separated)
3. Select date range
4. Click **Start Backfill**

### 3. Monitor Progress

Check backfill status via:
- **HTTP API**: `GET /api/backfill/status`
- **UWP App**: Backfill page progress indicators
- **Logs**: Serilog structured logging

---

## Backfill Architecture

### Component Overview

```
┌─────────────────┐     ┌──────────────────────┐     ┌─────────────────┐
│ BackfillRequest │────▶│HistoricalBackfillSvc │────▶│ CompositeProvider│
└─────────────────┘     └──────────────────────┘     └─────────────────┘
                                   │                         │
                                   ▼                         ▼
                        ┌──────────────────┐      ┌──────────────────┐
                        │  Priority Queue  │      │ Individual       │
                        │  (Symbol Jobs)   │      │ Providers        │
                        └──────────────────┘      │ ├─ Alpaca        │
                                   │              │ ├─ Yahoo         │
                                   ▼              │ ├─ Tiingo        │
                        ┌──────────────────┐      │ └─ etc.          │
                        │ EventPipeline    │      └──────────────────┘
                        └──────────────────┘
                                   │
                                   ▼
                        ┌──────────────────┐
                        │ Storage Layer    │
                        │ (JSONL/Parquet)  │
                        └──────────────────┘
```

### Key Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `HistoricalBackfillService` | `Application/Subscriptions/` | Orchestrates backfill jobs |
| `CompositeHistoricalDataProvider` | `Infrastructure/Providers/Backfill/` | Multi-provider failover |
| `BackfillJobQueue` | `Application/Subscriptions/` | Priority queue management |
| `DataQualityService` | `Storage/Services/` | Quality assessment |

---

## Provider Configuration

### Provider Priority

The `CompositeHistoricalDataProvider` tries providers in priority order:

```csharp
// Default priority (configurable)
1. Tiingo       // Best for dividend-adjusted data
2. Alpaca       // Good quality, generous limits
3. Yahoo        // Wide coverage, no auth
4. Finnhub      // Good fundamentals
5. Stooq        // Fallback, global coverage
6. Polygon      // Limited free tier
7. AlphaVantage // Very limited free tier
8. NasdaqDataLink // Alternative data
```

### Configuring Priority

```json
{
  "Backfill": {
    "Providers": [
      { "Name": "tiingo", "Priority": 1, "Enabled": true },
      { "Name": "alpaca", "Priority": 2, "Enabled": true },
      { "Name": "yahoo", "Priority": 3, "Enabled": true },
      { "Name": "finnhub", "Priority": 4, "Enabled": true },
      { "Name": "stooq", "Priority": 5, "Enabled": true }
    ],
    "MaxConcurrentJobs": 3,
    "RetryAttempts": 3,
    "RetryDelayMs": 5000
  }
}
```

---

## Rate Limiting Strategy

### Provider Rate Limits

| Provider | Rate Limit | Suggested Delay | Daily Limit |
|----------|------------|-----------------|-------------|
| Yahoo Finance | ~2000/hr | 2 seconds | Unlimited |
| Stooq | Respectful | 1 second | Unlimited |
| Alpaca | 200/min | 300ms | Unlimited |
| Tiingo | 50/hr | 72 seconds | 1,000 |
| Finnhub | 60/min | 1 second | Unlimited |
| Alpha Vantage | 5/min | 12 seconds | 25 |
| Polygon | 5/min | 12 seconds | Unlimited |

### Built-in Rate Limiting

Each provider has a built-in `RateLimiter`:

```csharp
// Rate limiter example (TiingoHistoricalDataProvider)
_rateLimiter = new RateLimiter(
    maxRequests: 50,
    window: TimeSpan.FromHours(1),
    minDelay: TimeSpan.FromSeconds(1.5)
);
```

### Rotating Across Providers

The `CompositeProvider` automatically rotates when rate limits are hit:

```
Request 1: Tiingo → Success
Request 2: Tiingo → Rate limited → Fallback to Alpaca
Request 3: Alpaca → Success
Request 4: Tiingo → (Recovered) → Success
```

---

## Backfill Modes

### Full Backfill

Retrieve all available history for a symbol:

```bash
dotnet run -- --backfill AAPL --from 2000-01-01 --to 2026-01-01
```

### Incremental Backfill

Only fill gaps in existing data:

```bash
dotnet run -- --backfill AAPL --incremental
```

### Date Range Backfill

Specific date range:

```bash
dotnet run -- --backfill AAPL,MSFT --from 2025-06-01 --to 2025-12-31
```

### Bulk Backfill

Multiple symbols with priority:

```json
{
  "BackfillJobs": [
    { "Symbol": "SPY", "Priority": 1, "From": "2020-01-01" },
    { "Symbol": "QQQ", "Priority": 1, "From": "2020-01-01" },
    { "Symbol": "AAPL", "Priority": 2, "From": "2020-01-01" },
    { "Symbol": "MSFT", "Priority": 2, "From": "2020-01-01" }
  ]
}
```

---

## Gap Detection

### Automatic Gap Detection

The `DataQualityService` identifies gaps in your data:

```csharp
var gaps = await dataQualityService.DetectGapsAsync(
    symbol: "AAPL",
    from: DateTime.Parse("2025-01-01"),
    to: DateTime.Parse("2026-01-01"),
    expectedFrequency: TimeSpan.FromDays(1)
);

// Returns list of missing date ranges
foreach (var gap in gaps)
{
    Console.WriteLine($"Missing: {gap.Start} to {gap.End}");
}
```

### Gap Types

| Gap Type | Description | Action |
|----------|-------------|--------|
| **Weekend** | Expected (Sat-Sun) | Skip |
| **Holiday** | Market closed | Skip |
| **Unexpected** | Data missing | Backfill |
| **Delisted** | Symbol no longer trading | Mark as complete |

### Gap Detection Report

```bash
dotnet run -- --gap-report AAPL --from 2020-01-01
```

Output:
```
Gap Detection Report: AAPL
==========================
Period: 2020-01-01 to 2026-01-08
Expected Trading Days: 1,508
Actual Data Points: 1,495
Missing Days: 13

Gaps Found:
- 2020-03-15 to 2020-03-17 (3 days) - Unexpected
- 2024-12-26 to 2024-12-27 (2 days) - Holiday
...

Recommendation: Run incremental backfill
```

---

## Data Quality

### Quality Checks

Before exporting, run quality assessment:

```csharp
var report = await analysisQualityReport.GenerateAsync(
    symbol: "AAPL",
    from: DateTime.Parse("2020-01-01"),
    to: DateTime.Parse("2026-01-01")
);

Console.WriteLine($"Quality Grade: {report.Grade}");  // A+, A, B, C, D, F
Console.WriteLine($"Completeness: {report.Completeness:P1}");
Console.WriteLine($"Outliers Found: {report.OutlierCount}");
```

### Quality Metrics

| Metric | Description | Weight |
|--------|-------------|--------|
| **Completeness** | % of expected trading days | 40% |
| **Consistency** | No price jumps > 4σ | 25% |
| **Recency** | Data up to current date | 15% |
| **Accuracy** | Cross-provider validation | 20% |

### Quality Grades

| Grade | Score | Suitability |
|-------|-------|-------------|
| **A+** | 95-100% | Production backtesting |
| **A** | 90-94% | Research |
| **B** | 80-89% | Development |
| **C** | 70-79% | Limited use |
| **D** | 60-69% | Needs attention |
| **F** | < 60% | Not suitable |

---

## Storage Integration

### Storage Flow

```
Historical Data → Event Pipeline → WAL → JSONL → Parquet Archive
```

### File Organization

```
{DataRoot}/
├── historical/
│   ├── alpaca/
│   │   └── 2026-01-08/
│   │       ├── AAPL_bars.jsonl
│   │       └── MSFT_bars.jsonl
│   ├── yahoo/
│   │   └── 2026-01-08/
│   │       └── AAPL_bars.jsonl
│   └── composite/
│       └── 2026-01-08/
│           └── AAPL_bars.jsonl
└── _archive/
    └── parquet/
        └── bars/
            ├── AAPL_2020.parquet
            └── AAPL_2021.parquet
```

### Compression Options

| Tier | Format | Compression | Use Case |
|------|--------|-------------|----------|
| Hot | JSONL | Gzip | Recent data, active access |
| Warm | Parquet | Snappy | Historical, frequent queries |
| Cold | Parquet | ZSTD-19 | Archive, rare access |

---

## UWP Backfill Interface

### Backfill Page Features

1. **Symbol Input**: Comma-separated or file upload
2. **Date Range**: Calendar picker with presets
3. **Provider Selection**: Enable/disable specific providers
4. **Priority Setting**: High/Medium/Low per symbol
5. **Progress Tracking**: Real-time progress bars
6. **Quality Preview**: Quick quality check before archival

### Batch Import

Upload a CSV file with symbols:

```csv
Symbol,Priority,FromDate,ToDate
AAPL,1,2020-01-01,2026-01-01
MSFT,1,2020-01-01,2026-01-01
GOOGL,2,2020-01-01,2026-01-01
```

---

## Troubleshooting

### Common Issues

**Issue: Rate limit errors**
```
Error: 429 Too Many Requests
```
**Solution**: Increase delays between requests, or add more providers to rotation

**Issue: Missing data for certain dates**
```
Warning: No data returned for AAPL 2020-03-15
```
**Solution**: Check if market was closed (holiday/weekend), try alternative provider

**Issue: Data quality too low**
```
Quality Grade: D (65%)
```
**Solution**: Run incremental backfill, cross-validate with multiple providers

**Issue: Provider authentication failed**
```
Error: 401 Unauthorized for Tiingo
```
**Solution**: Verify API token in environment variables

### Debug Mode

Enable detailed logging:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "MarketDataCollector.Infrastructure.Providers.Backfill": "Debug"
      }
    }
  }
}
```

### Retry Configuration

```json
{
  "Backfill": {
    "RetryPolicy": {
      "MaxRetries": 3,
      "BaseDelayMs": 1000,
      "MaxDelayMs": 30000,
      "ExponentialBackoff": true
    }
  }
}
```

---

## Best Practices

### 1. Start Small

Test with a single symbol before bulk backfill:
```bash
dotnet run -- --backfill AAPL --from 2025-01-01 --dry-run
```

### 2. Prioritize Quality

Use providers with best data quality first:
- Tiingo for dividend-adjusted data
- Alpaca for recent/accurate data
- Yahoo as fallback for coverage

### 3. Monitor Rate Limits

Watch for rate limit warnings in logs:
```
[WRN] Rate limit approaching for Tiingo (45/50 in window)
[INF] Rotating to Yahoo Finance
```

### 4. Schedule Off-Hours

Run large backfills during off-market hours:
- Less API competition
- More stable connections
- Better rate limit availability

### 5. Validate Results

Always run quality checks after backfill:
```bash
dotnet run -- --quality-report AAPL
```

### 6. Archive Incrementally

Don't archive until quality is acceptable:
1. Backfill → JSONL (hot storage)
2. Quality check
3. Fix gaps if needed
4. Archive → Parquet (cold storage)

---

## API Reference

### CLI Commands

```bash
# Full backfill
dotnet run -- --backfill <symbols> --from <date> --to <date>

# Incremental backfill
dotnet run -- --backfill <symbols> --incremental

# Gap detection
dotnet run -- --gap-report <symbol> --from <date>

# Quality report
dotnet run -- --quality-report <symbol>

# Dry run (no writes)
dotnet run -- --backfill <symbols> --dry-run
```

### HTTP API

```
POST /api/backfill/start
GET  /api/backfill/status
GET  /api/backfill/jobs/{jobId}
POST /api/backfill/cancel/{jobId}
GET  /api/backfill/gaps/{symbol}
GET  /api/backfill/quality/{symbol}
```

---

## Related Documentation

- [Data Sources Reference](data-sources.md)
- [Provider Comparison](provider-comparison.md)
- [Alpaca Setup](alpaca-setup.md)
- [Interactive Brokers Setup](interactive-brokers-setup.md)
- [Storage Architecture](../architecture/storage-design.md)
- [Operator Runbook](../guides/operator-runbook.md)

---

*Last Updated: 2026-01-30*
