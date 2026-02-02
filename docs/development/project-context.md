# MarketDataCollector Project Context

**Last Updated:** 2026-01-28
**Version:** 1.6.1

## Overview

MarketDataCollector is a high-performance market data collection system on .NET 9.0 that captures real-time and historical data from multiple providers with production-grade reliability. The system features a modular monolithic architecture (see [ADR-003](../adr/003-microservices-decomposition.md)), tiered storage with compression, and supports 10+ data providers for comprehensive market coverage.

## Critical Rules

When contributing to this project, always follow these rules:

- **ALWAYS** use `CancellationToken` on async methods
- **NEVER** store secrets in code or config files - use environment variables
- **ALWAYS** use structured logging with semantic parameters
- **PREFER** `IAsyncEnumerable<T>` for streaming data over collections
- **ALWAYS** mark classes as `sealed` unless designed for inheritance

## Architecture Principles

1. **Provider Independence**: All data providers implement `IMarketDataClient` interface, enabling seamless swapping and concurrent multi-provider operations ([ADR-001](../adr/001-provider-abstraction.md))
2. **No Vendor Lock-in**: Provider-agnostic interfaces with intelligent failover strategies
3. **Security First**: Environment variable-based credential management, no plain-text secrets
4. **Observability**: Structured logging, Prometheus metrics, health check endpoints
5. **Modularity**: Separate projects for core logic, domain models (F#), web UI, UWP desktop app, and shared contracts
6. **Monolithic Simplicity**: Single deployable unit with clear module boundaries for operational simplicity ([ADR-003](../adr/003-microservices-decomposition.md))

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9.0 |
| Languages | C# 11 (infrastructure), F# 8.0 (domain modeling) |
| Serialization | System.Text.Json |
| Metrics | OpenTelemetry, Prometheus |
| Containerization | Docker, Docker Compose |
| HTTP Clients | IHttpClientFactory with Polly resilience ([ADR-010](../adr/010-httpclient-factory.md)) |
| Storage | JSONL (hot), Parquet (archive), Write-Ahead Log (durability) ([ADR-002](../adr/002-tiered-storage-architecture.md)) |
| Compression | LZ4 (real-time), ZSTD (archive), Gzip (compatibility) |
| Desktop UI | UWP/WinUI 3 (Windows 10/11) |
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
│   │   ├── Domain/                    # Business logic and collectors
│   │   ├── Infrastructure/            # Provider implementations
│   │   ├── Application/               # Services, config, pipeline
│   │   └── Storage/                   # Data persistence
│   ├── MarketDataCollector.FSharp/    # F# domain models, validation, calculations
│   ├── MarketDataCollector.Contracts/ # Shared contracts and DTOs
│   ├── MarketDataCollector.Ui/        # Web dashboard, WebSocket updates
│   └── MarketDataCollector.Uwp/       # UWP desktop application (WinUI 3)
├── tests/                              # Unit and integration tests (45 files)
├── benchmarks/                         # Performance benchmarks
├── docs/                               # Documentation
├── deploy/                             # Docker, systemd configs
├── scripts/                            # Build and diagnostic scripts
└── build-system/                       # Python build tooling
```

## Key Interfaces

### IMarketDataClient

Core abstraction for all real-time market data providers ([ADR-001](../adr/001-provider-abstraction.md)):

```csharp
[ImplementsAdr("ADR-001", "Core streaming data provider contract")]
public interface IMarketDataClient : IAsyncDisposable
{
    bool IsEnabled { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    int SubscribeMarketDepth(SymbolConfig cfg);
    void UnsubscribeMarketDepth(int subscriptionId);
    int SubscribeTrades(SymbolConfig cfg);
    void UnsubscribeTrades(int subscriptionId);
}
```

### IHistoricalDataProvider

Historical data provider abstraction ([ADR-001](../adr/001-provider-abstraction.md)):

```csharp
[ImplementsAdr("ADR-001", "Core historical data provider contract")]
public interface IHistoricalDataProvider
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }
    HistoricalDataCapabilities Capabilities { get; }
    int Priority { get; }

    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol, DateOnly? from, DateOnly? to,
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
| Creating new `HttpClient` instances | Socket exhaustion, DNS issues (see [ADR-010](../adr/010-httpclient-factory.md)) |
| Missing `CancellationToken` on async methods | Prevents graceful shutdown (see [ADR-004](../adr/004-async-streaming-patterns.md)) |

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
| Event Pipeline | `Application/Pipeline/EventPipeline.cs` |
| Backfill Worker | `Infrastructure/Providers/Backfill/BackfillWorkerService.cs` |
| Data Quality | `Application/Monitoring/DataQuality/DataQualityMonitoringService.cs` |
| JSONL Storage | `Storage/Sinks/JsonlStorageSink.cs` |
| Parquet Storage | `Storage/Sinks/ParquetStorageSink.cs` |
| HTTP Clients | `Infrastructure/Http/HttpClientConfiguration.cs` |
| Tier Migration | `Storage/Services/TierMigrationService.cs` |

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Getting Started](getting-started.md)
- [Configuration Guide](configuration.md)
- [Operator Runbook](operator-runbook.md)
- [Provider Comparison](../providers/provider-comparison.md)
- [Historical Backfill Guide](../providers/backfill-guide.md)
- [Production Status](../status/production-status.md)

### Architecture Decision Records

- [ADR-001: Provider Abstraction](../adr/001-provider-abstraction.md)
- [ADR-002: Tiered Storage](../adr/002-tiered-storage-architecture.md)
- [ADR-003: Microservices Decision](../adr/003-microservices-decomposition.md)
- [ADR-004: Async Streaming](../adr/004-async-streaming-patterns.md)
- [ADR-005: Attribute Discovery](../adr/005-attribute-based-discovery.md)
- [ADR-010: HttpClientFactory](../adr/010-httpclient-factory.md)

---

*Last Updated: 2026-01-28*
