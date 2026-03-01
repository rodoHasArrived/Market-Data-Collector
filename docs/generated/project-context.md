# MarketDataCollector Project Context

**Generated:** 2026-03-01 UTC
**Source:** Auto-generated from code annotations and `[ImplementsAdr]` attributes

---

## Key Interfaces

| Interface | Location | Purpose |
|-----------|----------|---------|
| `IMarketDataClient` | `ProviderSdk/IMarketDataClient.cs` | Core streaming provider contract (trades, quotes, depth) |
| `IHistoricalDataProvider` | `Infrastructure/Providers/Historical/IHistoricalDataProvider.cs` | Backfill provider contract (bars, quotes, trades, auctions) |
| `IDataSource` | `ProviderSdk/IDataSource.cs` | Unified base interface for all data sources |
| `IRealtimeDataSource` | `ProviderSdk/IRealtimeDataSource.cs` | Real-time streaming extension (trades, quotes, depth) |
| `IHistoricalDataSource` | `ProviderSdk/IHistoricalDataSource.cs` | Historical data retrieval (bars, dividends, splits) |
| `IMarketEventPublisher` | `Domain/Events/IMarketEventPublisher.cs` | Non-blocking event publication for collectors |
| `IEventCanonicalizer` | `Application/Canonicalization/IEventCanonicalizer.cs` | Symbol resolution, condition/venue code mapping |
| `IStorageSink` | `Storage/Interfaces/IStorageSink.cs` | Event sink abstraction (JSONL/Parquet persistence) |
| `ISymbolSearchProvider` | `Infrastructure/Providers/SymbolSearch/ISymbolSearchProvider.cs` | Symbol search and autocomplete |
| `IOptionsChainProvider` | `ProviderSdk/IOptionsChainProvider.cs` | Options chain data retrieval |
| `ICredentialStore` | `Application/Credentials/ICredentialStore.cs` | Centralized credential management with caching |
| `IConfigValidator` | `Application/Config/IConfigValidator.cs` | Configuration validation pipeline |
| `IFlushable` | `Core/Services/IFlushable.cs` | Components that buffer data and can flush to storage |
| `IHealthCheckProvider` | `Core/Monitoring/Core/IHealthCheckProvider.cs` | Unified health check for all components |
| `IAlertDispatcher` | `Core/Monitoring/Core/IAlertDispatcher.cs` | Centralized monitoring alert dispatcher |
| `IQualityAnalyzer` | `Application/Monitoring/DataQuality/IQualityAnalyzer.cs` | Quality analyzer plugin interface |
| `IOperationalScheduler` | `Application/Scheduling/IOperationalScheduler.cs` | Trading-hours-aware scheduling |
| `IConfigurationProvider` | `Core/Config/IConfigurationProvider.cs` | Unified configuration provider |

---

## ADR Implementations

Summary of Architecture Decision Record compliance across the codebase. Over 110 `[ImplementsAdr]` annotations found.

### ADR-001 — Provider Abstraction (62 implementations)

Core contract for all data providers, centralized management, and failover.

**Streaming Providers:**

| Type | Location | Description |
|------|----------|-------------|
| `IBMarketDataClient` | `Infrastructure/Providers/Streaming/InteractiveBrokers/` | Interactive Brokers TWS/Gateway |
| `AlpacaMarketDataClient` | `Infrastructure/Providers/Streaming/Alpaca/` | Alpaca WebSocket streaming |
| `PolygonMarketDataClient` | `Infrastructure/Providers/Streaming/Polygon/` | Polygon.io with circuit breaker |
| `NYSEDataSource` | `Infrastructure/Providers/Streaming/NYSE/` | NYSE Direct hybrid streaming + historical |
| `StockSharpMarketDataClient` | `Infrastructure/Providers/Streaming/StockSharp/` | StockSharp 90+ connectors |
| `FailoverAwareMarketDataClient` | `Infrastructure/Providers/Streaming/Failover/` | Automatic provider switching |
| `NoOpMarketDataClient` | `Infrastructure/NoOpMarketDataClient.cs` | No-op for disabled scenarios |

**Historical Providers:**

| Type | Location | Description |
|------|----------|-------------|
| `AlpacaHistoricalDataProvider` | `Infrastructure/Providers/Historical/Alpaca/` | Bars, quotes, trades, auctions |
| `PolygonHistoricalDataProvider` | `Infrastructure/Providers/Historical/Polygon/` | Intraday bars |
| `TiingoHistoricalDataProvider` | `Infrastructure/Providers/Historical/Tiingo/` | Dividend-adjusted bars |
| `FinnhubHistoricalDataProvider` | `Infrastructure/Providers/Historical/Finnhub/` | Bars, dividends, splits |
| `StooqHistoricalDataProvider` | `Infrastructure/Providers/Historical/Stooq/` | Free EOD bars |
| `YahooFinanceHistoricalDataProvider` | `Infrastructure/Providers/Historical/YahooFinance/` | 50K+ global securities |
| `AlphaVantageHistoricalDataProvider` | `Infrastructure/Providers/Historical/AlphaVantage/` | Unique intraday historical |
| `IBHistoricalDataProvider` | `Infrastructure/Providers/Historical/InteractiveBrokers/` | IB pacing-compliant |
| `NasdaqDataLinkHistoricalDataProvider` | `Infrastructure/Providers/Historical/NasdaqDataLink/` | Alternative datasets |
| `StockSharpHistoricalDataProvider` | `Infrastructure/Providers/Historical/StockSharp/` | Multi-connector |
| `CompositeHistoricalDataProvider` | `Infrastructure/Providers/Historical/` | Auto-failover with rate limit rotation |
| `BaseHistoricalDataProvider` | `Infrastructure/Providers/Historical/` | Shared base with HTTP handling |

**Symbol Search Providers:**

| Type | Location | Description |
|------|----------|-------------|
| `AlpacaSymbolSearchProviderRefactored` | `Infrastructure/Providers/SymbolSearch/` | US equities + crypto |
| `FinnhubSymbolSearchProviderRefactored` | `Infrastructure/Providers/SymbolSearch/` | Global coverage |
| `PolygonSymbolSearchProvider` | `Infrastructure/Providers/SymbolSearch/` | US equities |
| `StockSharpSymbolSearchProvider` | `Infrastructure/Providers/Streaming/StockSharp/` | Multi-exchange |
| `BaseSymbolSearchProvider` | `Infrastructure/Providers/SymbolSearch/` | Shared base |

**Core Infrastructure:**

| Type | Location | Description |
|------|----------|-------------|
| `ProviderRegistry` | `Infrastructure/Providers/Core/` | Centralized provider registry |
| `ProviderFactory` | `Infrastructure/Providers/Core/` | Capability-driven registration |
| `WebSocketProviderBase` | `Infrastructure/Providers/Core/` | Unified WebSocket base class |
| `StreamingFailoverService` | `Infrastructure/Providers/Streaming/Failover/` | Runtime failover orchestration |
| `SubscriptionManager` | `Infrastructure/Shared/` | Centralized subscription management |
| `ServiceCompositionRoot` | `Application/Composition/` | Centralized DI composition root |
| `HostStartup` | `Application/Composition/` | Unified host startup for all modes |
| `ServiceRegistry` | `Application/Services/` | Service organization and discovery |
| `BackfillCoordinator` | `Application/Http/` | Unified provider discovery for backfill |
| `OperationalScheduler` | `Application/Scheduling/` | Trading-hours-aware scheduling |
| `HealthCheckAggregator` | `Application/Monitoring/Core/` | Health check aggregation |
| `AlertDispatcher` | `Application/Monitoring/Core/` | Alert dispatch implementation |
| `DataQualityScoringService` | `Storage/Services/` | Multi-source quality scoring |

### ADR-002 — Tiered Storage Architecture (4 implementations)

| Type | Location | Description |
|------|----------|-------------|
| `QuotaEnforcementService` | `Storage/Services/` | Capacity management with quota enforcement |
| `LifecyclePolicyEngine` | `Storage/Services/` | Tiered storage lifecycle enforcement |
| `DataLineageService` | `Storage/Services/` | Data lineage tracking |
| `PortableDataPackager` | `Storage/Packaging/` | Portable packaging for tiered storage export |

### ADR-004 — Async Streaming Patterns (27 implementations)

All async methods across the codebase support `CancellationToken`. Implementations span all streaming providers, historical providers, symbol search providers, and core services. Key examples:

| Type | Description |
|------|-------------|
| `IMarketDataClient` | Interface contract requires CancellationToken |
| `IHistoricalDataProvider` | Interface contract requires CancellationToken |
| All streaming providers | ConnectAsync, DisconnectAsync with CancellationToken |
| All historical providers | GetDailyBarsAsync with CancellationToken |
| `OptionsChainService` | Graceful cancellation for chain queries |

### ADR-005 — Attribute-Based Discovery (5 implementations)

| Type | DataSource ID | Description |
|------|-------------|-------------|
| `DataSourceAttribute` | — | Core attribute definition |
| `AlpacaMarketDataClient` | `alpaca` | Attribute-based discovery |
| `IBMarketDataClient` | `ib` | Attribute-based discovery |
| `PolygonMarketDataClient` | `polygon` | Attribute-based discovery |
| `StockSharpMarketDataClient` | `stocksharp` | Attribute-based discovery |

### ADR-006 — Domain Events Polymorphic Payload (1 implementation)

| Type | Location | Description |
|------|----------|-------------|
| `OptionDataCollector` | `Domain/Collectors/` | Polymorphic MarketEvent payloads for option data types |

### ADR-010 — HttpClient Factory (1 implementation)

| Type | Location | Description |
|------|----------|-------------|
| `HttpClientConfiguration` | `Infrastructure/Http/` | HttpClientFactory for proper lifecycle management |

### ADR-012 — Monitoring and Alerting Pipeline (1 implementation)

| Type | Location | Description |
|------|----------|-------------|
| `TracedEventMetrics` | `Application/Tracing/` | OpenTelemetry metrics instrumentation |

---

## Entry Points

| Mode | Entry Point | Description |
|------|------------|-------------|
| Console | `Program.cs` → `HostStartup` | Standalone data collection |
| Web | `UiServer.cs` → `ServiceCompositionRoot` | Web dashboard + API |
| Desktop | `App.xaml.cs` (WPF) | Windows desktop application |
| Headless | `Program.cs --mode headless` | Service/daemon mode |

---

## Project Dependency Graph

```
MarketDataCollector (host)
├── MarketDataCollector.Application
│   ├── MarketDataCollector.Core
│   ├── MarketDataCollector.Domain
│   ├── MarketDataCollector.Contracts
│   ├── MarketDataCollector.ProviderSdk
│   ├── MarketDataCollector.Infrastructure
│   ├── MarketDataCollector.Storage
│   └── MarketDataCollector.FSharp
├── MarketDataCollector.Ui.Shared
│   ├── MarketDataCollector.Application
│   └── MarketDataCollector.Contracts
└── MarketDataCollector.Wpf
    ├── MarketDataCollector.Ui.Services
    └── MarketDataCollector.Contracts
```

---

*Generated from `[ImplementsAdr]` attributes and interface declarations — 2026-03-01*
