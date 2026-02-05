# Market Data Collector Project Context

**Last Updated:** 2026-02-05
**Version:** 1.0.0

## Overview

Market Data Collector is a .NET 9 monolithic market data platform for collecting both real-time and historical datasets from multiple providers. The repository includes a CLI-first core runtime, web UI, shared UI services, and Windows desktop clients (WPF recommended, UWP legacy).

## Critical Rules

When contributing to this project, always follow these rules:

- **ALWAYS** pass `CancellationToken` to async methods where available
- **NEVER** store secrets in source-controlled config files
- **ALWAYS** use structured logging with named parameters
- **PREFER** streaming (`IAsyncEnumerable<T>`) for continuous data flows
- **ALWAYS** keep abstractions provider-agnostic where possible

## Architecture Principles

1. **Provider independence** through shared contracts and provider abstractions ([ADR-001](../adr/001-provider-abstraction.md)).
2. **Operational simplicity** via modular monolith architecture ([ADR-003](../adr/003-microservices-decomposition.md)).
3. **Security-first configuration** using environment variables and centralized config policy ([ADR-011](../adr/011-centralized-configuration-and-credentials.md)).
4. **Observability by default** with status, health, and metrics endpoints ([ADR-012](../adr/012-monitoring-and-alerting-pipeline.md)).
5. **Tiered storage** with JSONL hot data and archive-oriented formats ([ADR-002](../adr/002-tiered-storage-architecture.md)).

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9.0 |
| Languages | C# 13, F# 8.0 |
| Serialization | System.Text.Json |
| Metrics/Monitoring | Prometheus endpoints + status/health APIs |
| HTTP Clients | `IHttpClientFactory` patterns ([ADR-010](../adr/010-httpclient-factory.md)) |
| Storage | JSONL + Parquet + WAL |
| Desktop UI | WPF (primary), UWP (legacy) |
| Web UI | ASP.NET Core |

## Provider Map

### Streaming Providers

| Provider | Location |
|----------|----------|
| Alpaca | `src/MarketDataCollector/Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` |
| Interactive Brokers | `src/MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` |
| StockSharp | `src/MarketDataCollector/Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs` |
| NYSE | `src/MarketDataCollector/Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs` |
| Polygon | `src/MarketDataCollector/Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` |

### Historical Providers

| Provider | Location |
|----------|----------|
| Composite | `src/MarketDataCollector/Infrastructure/Providers/Historical/CompositeHistoricalDataProvider.cs` |
| Alpaca | `src/MarketDataCollector/Infrastructure/Providers/Historical/Alpaca/AlpacaHistoricalDataProvider.cs` |
| Yahoo Finance | `src/MarketDataCollector/Infrastructure/Providers/Historical/YahooFinance/YahooFinanceHistoricalDataProvider.cs` |
| Tiingo | `src/MarketDataCollector/Infrastructure/Providers/Historical/Tiingo/TiingoHistoricalDataProvider.cs` |
| Finnhub | `src/MarketDataCollector/Infrastructure/Providers/Historical/Finnhub/FinnhubHistoricalDataProvider.cs` |
| Stooq | `src/MarketDataCollector/Infrastructure/Providers/Historical/Stooq/StooqHistoricalDataProvider.cs` |
| Nasdaq Data Link | `src/MarketDataCollector/Infrastructure/Providers/Historical/NasdaqDataLink/NasdaqDataLinkHistoricalDataProvider.cs` |
| Alpha Vantage | `src/MarketDataCollector/Infrastructure/Providers/Historical/AlphaVantage/AlphaVantageHistoricalDataProvider.cs` |
| Polygon | `src/MarketDataCollector/Infrastructure/Providers/Historical/Polygon/PolygonHistoricalDataProvider.cs` |
| Interactive Brokers | `src/MarketDataCollector/Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` |
| StockSharp | `src/MarketDataCollector/Infrastructure/Providers/Historical/StockSharp/StockSharpHistoricalDataProvider.cs` |

## Project Structure

```text
Market-Data-Collector/
├── src/
│   ├── MarketDataCollector/            # Core runtime (CLI, domain, providers, storage, web host)
│   ├── MarketDataCollector.Contracts/  # Shared contracts and DTOs
│   ├── MarketDataCollector.FSharp/     # F# domain/validation/calculation modules
│   ├── MarketDataCollector.Ui/         # Web UI host assets
│   ├── MarketDataCollector.Ui.Shared/  # Shared UI endpoints and service abstractions
│   ├── MarketDataCollector.Wpf/        # Windows desktop app (recommended)
│   └── MarketDataCollector.Uwp/        # Legacy Windows desktop app
├── tests/                              # Unit/integration tests
├── benchmarks/                         # BenchmarkDotNet performance suites
├── docs/                               # Architecture, ADRs, operations, provider docs
├── deploy/                             # Docker and systemd deployment assets
└── build/                              # Build tooling/scripts (dotnet/python/node)
```

## Core Interfaces

### `IMarketDataClient`

Core abstraction for real-time market data streaming:

- File: `src/MarketDataCollector/Infrastructure/IMarketDataClient.cs`
- Inherits: `IProviderMetadata`, `IAsyncDisposable`

### `IHistoricalDataProvider`

Core abstraction for historical bar retrieval:

- File: `src/MarketDataCollector/Infrastructure/Providers/Historical/IHistoricalDataProvider.cs`
- Inherits: `IProviderMetadata`, `IDisposable`

## Core Services

| Service | Location |
|---------|----------|
| Event Pipeline | `src/MarketDataCollector/Application/Pipeline/EventPipeline.cs` |
| Backfill Worker | `src/MarketDataCollector/Infrastructure/Providers/Historical/Queue/BackfillWorkerService.cs` |
| Data Quality Monitor | `src/MarketDataCollector/Infrastructure/Providers/Historical/GapAnalysis/DataQualityMonitor.cs` |
| JSONL Storage Sink | `src/MarketDataCollector/Storage/Sinks/JsonlStorageSink.cs` |
| Parquet Storage Sink | `src/MarketDataCollector/Storage/Sinks/ParquetStorageSink.cs` |
| HTTP Client Configuration | `src/MarketDataCollector/Infrastructure/Http/HttpClientConfiguration.cs` |
| Tier Migration | `src/MarketDataCollector/Storage/Services/TierMigrationService.cs` |

## Related Documentation

- [Architecture Overview](../architecture/overview.md)
- [Getting Started](../getting-started/README.md)
- [Operator Runbook](../operations/operator-runbook.md)
- [Provider Comparison](../providers/provider-comparison.md)
- [Historical Backfill Guide](../providers/backfill-guide.md)
- [Production Status](../status/production-status.md)

### Architecture Decision Records

- [ADR-001: Provider Abstraction](../adr/001-provider-abstraction.md)
- [ADR-002: Tiered Storage](../adr/002-tiered-storage-architecture.md)
- [ADR-003: Monolith Decomposition Decision](../adr/003-microservices-decomposition.md)
- [ADR-004: Async Streaming Patterns](../adr/004-async-streaming-patterns.md)
- [ADR-005: Attribute-based Discovery](../adr/005-attribute-based-discovery.md)
- [ADR-010: `IHttpClientFactory`](../adr/010-httpclient-factory.md)
- [ADR-011: Centralized Configuration and Credentials](../adr/011-centralized-configuration-and-credentials.md)
- [ADR-012: Monitoring and Alerting Pipeline](../adr/012-monitoring-and-alerting-pipeline.md)

---

*Last Updated: 2026-02-05*
