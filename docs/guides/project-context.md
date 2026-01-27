# MarketDataCollector Project Context

**Last Updated:** 2026-01-08
**Version:** 1.0.0

## Overview

MarketDataCollector is a high-performance market data collection system on .NET 9.0 that captures real-time and historical data from multiple providers with production-grade reliability. The system features a microservices architecture, tiered storage with compression, and supports 9+ data providers for comprehensive market coverage.

## Critical Rules

When contributing to this project, always follow these rules:

- **ALWAYS** use `CancellationToken` on async methods
- **NEVER** store secrets in code or config files - use environment variables
- **ALWAYS** use structured logging with semantic parameters
- **PREFER** `IAsyncEnumerable<T>` for streaming data over collections
- **ALWAYS** mark classes as `sealed` unless designed for inheritance

## Architecture Principles

1. **Provider Independence**: All data providers implement `IMarketDataClient` interface, enabling seamless swapping and concurrent multi-provider operations
2. **No Vendor Lock-in**: Provider-agnostic interfaces with intelligent failover strategies
3. **Security First**: Environment variable-based credential management, no plain-text secrets
4. **Observability**: Structured logging, Prometheus metrics, health check endpoints
5. **Modularity**: Separate projects for core logic, domain models (F#), web UI, UWP desktop app, and microservices
6. **Microservices Architecture**: Decomposed data ingestion into specialized services with MassTransit messaging

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9.0 |
| Languages | C# 11 (infrastructure), F# 8.0 (domain modeling) |
| Serialization | System.Text.Json |
| Metrics | OpenTelemetry, Prometheus |
| Containerization | Docker, Docker Compose |
| Messaging | MassTransit (RabbitMQ, Azure Service Bus) |
| Storage | JSONL (hot), Parquet (archive), Write-Ahead Log (durability) |
| Compression | LZ4 (real-time), ZSTD (archive), Gzip (compatibility) |
| Desktop UI | UWP (Windows 10/11) |
| Web UI | ASP.NET Core with WebSocket |

### Streaming Data Providers

| Provider | Status | Notes |
|----------|--------|-------|
| Alpaca Markets | ✅ Production | REST + WebSocket, IEX/SIP feeds |
| Interactive Brokers | ⚠️ Requires IBAPI | TWS API, L2 depth support |
| StockSharp | ✅ Production | Multi-exchange support |
| NYSE | ✅ Production | NYSE-specific feeds |
| Polygon.io | ❌ Stub | Future implementation |

### Historical Data Providers

| Provider | Status | Notes |
|----------|--------|-------|
| Alpaca | ✅ Production | Unlimited free historical |
| Yahoo Finance | ✅ Production | No auth, 50K+ securities |
| Stooq | ✅ Production | Global coverage, no auth |
| Nasdaq Data Link | ✅ Production | Alternative/economic data |
| Tiingo | ✅ Production | Best for dividend-adjusted |
| Finnhub | ✅ Production | Includes fundamentals |
| Alpha Vantage | ✅ Production | Limited free tier |
| Polygon | ✅ Production | High-quality tick data |
| IB Historical | ⚠️ Requires IBAPI | Requires streaming subscription |

## Project Structure

```
MarketDataCollector/
├── src/
│   ├── MarketDataCollector/           # Main application, entry point
│   ├── MarketDataCollector.FSharp/    # F# domain models, validation, calculations
│   ├── MarketDataCollector.Contracts/ # Shared contracts and DTOs
│   ├── MarketDataCollector.Ui/        # Web dashboard, WebSocket updates
│   ├── MarketDataCollector.Uwp/       # UWP desktop application (15 pages)
│   └── Microservices/                 # Decomposed data ingestion services
│       ├── Gateway/                   # API Gateway (port 5000)
│       ├── TradeIngestion/            # Trade data service (port 5001)
│       ├── QuoteIngestion/            # Quote data service (port 5002)
│       ├── OrderBookIngestion/        # Order book service (port 5003)
│       ├── HistoricalDataIngestion/   # Historical data service (port 5004)
│       ├── DataValidation/            # Validation service (port 5005)
│       └── Shared/                    # Shared contracts
├── tests/                              # Unit and integration tests (33 files)
├── docs/                               # Documentation
├── deploy/                             # Kubernetes, systemd configs
└── data/                               # Runtime data storage
```

## Key Interfaces

### IMarketDataClient

Core abstraction for all real-time market data providers:

```csharp
public interface IMarketDataClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task SubscribeAsync(SymbolSubscription subscription, CancellationToken ct = default);
    Task UnsubscribeAsync(string symbol, CancellationToken ct = default);
    IAsyncEnumerable<MarketDataEvent> GetEventsAsync(CancellationToken ct = default);
    ConnectionState State { get; }
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
}
```

### IHistoricalDataProvider

Historical data provider abstraction:

```csharp
public interface IHistoricalDataProvider
{
    Task<IReadOnlyList<OhlcBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default);

    IAsyncEnumerable<OhlcBar> StreamHistoricalBarsAsync(
        string symbol,
        DateTime start,
        DateTime end,
        BarTimeframe timeframe,
        CancellationToken ct = default);
}
```

## Coding Conventions

### Logging

Use structured logging with semantic parameters:

```csharp
_logger.LogInformation("Received {Count} bars for {Symbol}", bars.Count, symbol);
```

### Configuration

Configuration uses the .NET Options pattern with environment variable overrides:

```csharp
// Environment variables use double underscore for nesting
// ALPACA__KEYID maps to Alpaca:KeyId
services.Configure<AlpacaOptions>(configuration.GetSection("Alpaca"));
```

### Error Handling

- Log all errors with context
- Use exponential backoff for retries
- Throw `ArgumentException` for bad inputs, `InvalidOperationException` for state errors
- Use `Result<T, TError>` in F# code

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad |
|--------------|--------------|
| Swallowing exceptions silently | Hides bugs, makes debugging impossible |
| Hardcoding connection strings or credentials | Security risk, inflexible deployment |
| Using `Task.Run` for I/O-bound operations | Wastes thread pool threads |
| Blocking async code with `.Result` or `.Wait()` | Can cause deadlocks |
| Creating new `HttpClient` instances | Socket exhaustion, DNS issues |

## Storage Architecture

### Tiered Storage

```
Market Data → Event Pipeline → WAL → JSONL (hot) → Parquet (archive)
                                        ↓
                                 Compression Layer
                                 ├─ LZ4 (real-time)
                                 ├─ Gzip (compatibility)
                                 └─ ZSTD-19 (archive)
```

### File Organization

```
{DataRoot}/
├── live/                    # Real-time data (hot tier)
│   ├── {provider}/
│   │   └── {date}/
│   │       ├── {symbol}_trades.jsonl.gz
│   │       └── {symbol}_quotes.jsonl.gz
├── historical/              # Backfill data
│   └── {provider}/
│       └── {date}/
│           └── {symbol}_bars.jsonl
└── _archive/                # Compressed archives (cold tier)
    └── parquet/
        └── bars/
            └── {symbol}_{year}.parquet
```

### Compression Profiles

| Profile | Algorithm | Level | Use Case |
|---------|-----------|-------|----------|
| RealTime | LZ4 | Fast | Live streaming data |
| Standard | Gzip | 6 | General purpose |
| Archive | ZSTD | 19 | Long-term storage |

## Key File Locations

### Streaming Providers

| Provider | Location |
|----------|----------|
| Alpaca | `Infrastructure/Providers/Alpaca/AlpacaMarketDataClient.cs` |
| Interactive Brokers | `Infrastructure/Providers/InteractiveBrokers/IBMarketDataClient.cs` |
| StockSharp | `Infrastructure/Providers/StockSharp/StockSharpClient.cs` |
| NYSE | `Infrastructure/Providers/NYSE/NyseMarketDataClient.cs` |
| Polygon | `Infrastructure/Providers/Polygon/PolygonMarketDataClient.cs` (stub) |

### Historical Providers

| Provider | Location |
|----------|----------|
| Composite | `Infrastructure/Providers/Backfill/CompositeHistoricalDataProvider.cs` |
| Alpaca | `Infrastructure/Providers/Backfill/AlpacaHistoricalDataProvider.cs` |
| Yahoo Finance | `Infrastructure/Providers/Backfill/YahooFinanceHistoricalDataProvider.cs` |
| Tiingo | `Infrastructure/Providers/Backfill/TiingoHistoricalDataProvider.cs` |
| Finnhub | `Infrastructure/Providers/Backfill/FinnhubHistoricalDataProvider.cs` |
| Stooq | `Infrastructure/Providers/Backfill/StooqHistoricalDataProvider.cs` |
| Nasdaq Data Link | `Infrastructure/Providers/Backfill/NasdaqDataLinkHistoricalDataProvider.cs` |
| Alpha Vantage | `Infrastructure/Providers/Backfill/AlphaVantageHistoricalDataProvider.cs` |
| Polygon | `Infrastructure/Providers/Backfill/PolygonHistoricalDataProvider.cs` |

### Core Services

| Service | Location |
|---------|----------|
| Subscription Management | `Application/Subscriptions/SubscriptionManager.cs` |
| Event Pipeline | `Application/Pipeline/EventPipeline.cs` |
| Historical Backfill | `Application/Subscriptions/HistoricalBackfillService.cs` |
| Data Quality | `Storage/Services/DataQualityService.cs` |
| Storage Writer | `Storage/Services/JsonlStorageWriter.cs` |
| Parquet Archive | `Storage/Services/ParquetArchiveService.cs` |

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Getting Started](getting-started.md)
- [Configuration Guide](configuration.md)
- [Operator Runbook](operator-runbook.md)
- [Provider Comparison](../providers/provider-comparison.md)
- [Historical Backfill Guide](../providers/backfill-guide.md)
- [Production Status](../status/production-status.md)

---

*Last Updated: 2026-01-08*
