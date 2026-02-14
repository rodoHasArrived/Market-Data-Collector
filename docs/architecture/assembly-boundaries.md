# Assembly Boundaries

This document defines the responsibilities and dependency rules for each assembly (project) in the solution. It serves as a reference for contributors to avoid circular dependencies and maintain clean layer separation.

## Layer Diagram

```
┌───────────────────────────────────────────────────────────────┐
│                    Entry Points / Hosts                        │
│  MarketDataCollector (CLI)  ·  Ui (Web)  ·  Wpf (Desktop)    │
└───────────────┬───────────────────────────────┬───────────────┘
                │                               │
┌───────────────▼───────────────┐  ┌────────────▼───────────────┐
│    MarketDataCollector.       │  │  MarketDataCollector.       │
│    Ui.Shared                  │  │  Ui.Services (Win only)     │
│    (Shared endpoints/HTML)    │  │  (Desktop service layer)    │
└───────────────┬───────────────┘  └────────────────────────────┘
                │
┌───────────────▼───────────────────────────────────────────────┐
│              MarketDataCollector.Application                   │
│   (Orchestration, commands, scheduling, monitoring, HTTP)      │
└────────┬──────────────────────┬───────────────────────────────┘
         │                      │
┌────────▼──────────┐ ┌────────▼──────────┐
│   Infrastructure   │ │     Storage        │
│  (Providers, HTTP, │ │  (JSONL, Parquet,  │
│   WebSocket, etc.) │ │   WAL, packaging)  │
└────────┬──────────┘ └────────┬──────────┘
         │                      │
┌────────▼──────────────────────▼──────────┐
│           MarketDataCollector.Core        │
│   (Config, logging, serialization,       │
│    scheduling, performance)              │
└────────┬──────────────────────┬──────────┘
         │                      │
┌────────▼──────────┐ ┌────────▼──────────┐
│      Domain        │ │   ProviderSdk     │
│  (Collectors,      │ │  (IDataSource,    │
│   events, models)  │ │   attributes,     │
│                    │ │   discovery)      │
└────────┬──────────┘ └────────┬──────────┘
         │                      │
┌────────▼──────────────────────▼──────────┐
│          MarketDataCollector.Contracts    │
│  (DTOs, enums, events, API models)       │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│       MarketDataCollector.FSharp          │
│  (Standalone – no project dependencies)  │
└──────────────────────────────────────────┘
```

## Assembly Reference

### MarketDataCollector.Contracts

| | |
|---|---|
| **Layer** | Foundation |
| **References** | None |
| **Referenced by** | All other assemblies |
| **Key packages** | System.Text.Json |

**Responsibility:** Shared data transfer objects, domain enums, event records, API models, and configuration DTOs. This assembly is the common language spoken by every layer.

**Rules:**
- Must have **zero** project references.
- Must not contain business logic, only data shapes.
- Types should be `sealed record` or `sealed class` with no side effects.

---

### MarketDataCollector.ProviderSdk

| | |
|---|---|
| **Layer** | Foundation |
| **References** | Contracts |
| **Referenced by** | Domain, Core, Infrastructure, Storage, Application |
| **Key packages** | Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions |

**Responsibility:** Provider abstraction layer. Contains `IDataSource`, `IHistoricalDataSource`, `DataSourceAttribute` for discovery (ADR-005), `DataSourceRegistry`, and `CredentialValidator`.

**Rules:**
- Must only depend on Contracts.
- Must not reference any concrete provider implementation.
- New provider interfaces go here; implementations go in Infrastructure.

---

### MarketDataCollector.Domain

| | |
|---|---|
| **Layer** | Domain |
| **References** | Contracts, ProviderSdk |
| **Referenced by** | Core, Infrastructure, Storage, Application |
| **Key packages** | None |

**Responsibility:** Domain collectors (`TradeDataCollector`, `MarketDepthCollector`, `QuoteCollector`), event publishing (`MarketEvent` factory), and core market models.

**Rules:**
- Must not reference Infrastructure, Storage, or Application.
- Must not have external NuGet dependencies (pure domain).
- `BannedReferences.txt` enforces this at build time.

---

### MarketDataCollector.Core

| | |
|---|---|
| **Layer** | Core Services |
| **References** | Contracts, Domain, ProviderSdk |
| **Referenced by** | Infrastructure, Storage, Application |
| **Key packages** | Serilog, Microsoft.Extensions.Configuration, System.Threading.Channels |

**Responsibility:** Cross-cutting concerns: logging setup, configuration models (`AppConfig`), exception types, serialization (`MarketDataJsonContext` for AOT), cron expression parsing, and performance utilities.

**Rules:**
- Must not reference Infrastructure or Storage (no concrete I/O).
- Configuration models live here; configuration loading lives in Application.

---

### MarketDataCollector.Infrastructure

| | |
|---|---|
| **Layer** | Infrastructure |
| **References** | Core, Domain, Contracts, ProviderSdk |
| **Referenced by** | Application, main host |
| **Key packages** | Polly, Microsoft.Extensions.Http, Websocket.Client, System.Reactive |

**Responsibility:** All external I/O: streaming provider clients (Alpaca, Polygon, IB, NYSE, StockSharp), historical data providers (Stooq, Tiingo, Yahoo, Finnhub, Alpha Vantage, etc.), symbol search providers, WebSocket management, HTTP resilience policies.

**Rules:**
- **Must not reference Storage** (avoids circular dependency).
- Provider implementations use `IStorageSink` via DI, never direct Storage types.
- Each provider lives in its own subfolder under `Providers/`.

---

### MarketDataCollector.Storage

| | |
|---|---|
| **Layer** | Persistence |
| **References** | Core, Domain, Contracts, ProviderSdk |
| **Referenced by** | Application, main host |
| **Key packages** | Parquet.Net, Apache.Arrow, prometheus-net |

**Responsibility:** Data persistence: JSONL and Parquet sinks, Write-Ahead Log, archival with compression profiles, portable data packager, tier migration, export service, catalog and registry services.

**Rules:**
- **Must not reference Infrastructure** (avoids circular dependency).
- Storage and Infrastructure are sibling layers; they communicate via abstractions in Core/ProviderSdk.

---

### MarketDataCollector.Application

| | |
|---|---|
| **Layer** | Application |
| **References** | Infrastructure, Storage, Core, Domain, Contracts, ProviderSdk |
| **Referenced by** | Ui.Shared, main host |
| **Key packages** | OpenTelemetry, prometheus-net, Skender.Stock.Indicators, FluentValidation |

**Responsibility:** Application orchestration: CLI commands, backfill scheduling, event pipeline, data quality monitoring, configuration wizard, graceful shutdown, HTTP endpoints, subscription management, technical indicators.

**Rules:**
- This is the composition layer—it is allowed to reference both Infrastructure and Storage.
- New CLI commands go in `Commands/`.
- New HTTP endpoint logic goes in `Http/Endpoints/`.

---

### MarketDataCollector.Ui.Shared

| | |
|---|---|
| **Layer** | Presentation (shared) |
| **References** | Contracts, Application |
| **Referenced by** | Main host, Ui (web) |
| **Key packages** | Swashbuckle.AspNetCore |

**Responsibility:** Shared HTTP endpoint registrations (Minimal API), HTML template generation, and DTO extensions used by both the web dashboard and CLI web mode.

**Rules:**
- Endpoint files are organized by domain area (one file per area).
- Uses `WebApplication.MapGroup()` with tags for OpenAPI grouping.

---

### MarketDataCollector (Main Host)

| | |
|---|---|
| **Layer** | Entry Point |
| **References** | Application, Infrastructure, Storage, Core, Domain, Contracts, ProviderSdk, Ui.Shared |
| **Key packages** | QuantConnect.Lean, BenchmarkDotNet |

**Responsibility:** Console entry point. Composes the DI container, parses CLI arguments, starts web/headless/desktop mode, and hosts the QuantConnect Lean integration.

---

### MarketDataCollector.Ui

| | |
|---|---|
| **Layer** | Entry Point (web) |
| **References** | Ui.Shared |

**Responsibility:** Standalone web dashboard host. Thin wrapper that configures ASP.NET Core and delegates to Ui.Shared endpoints.

---

### MarketDataCollector.Ui.Services (Windows only)

| | |
|---|---|
| **Layer** | Desktop Services |
| **References** | None (uses Contracts types via source inclusion) |
| **Referenced by** | Wpf |
| **Key packages** | CommunityToolkit.Mvvm, ZstdSharp.Port |

**Responsibility:** Desktop-specific UI services: API client, backfill coordination, charting, form validation, fixture data, error handling. Shared between potential desktop hosts.

**Rules:**
- Compiles to an empty stub on non-Windows platforms.
- Includes Contracts types as source files to avoid WinRT compatibility issues.

---

### MarketDataCollector.Wpf (Windows only)

| | |
|---|---|
| **Layer** | Entry Point (desktop) |
| **References** | Ui.Services |
| **Key packages** | MaterialDesignThemes, CommunityToolkit.Mvvm |

**Responsibility:** WPF desktop application with Material Design UI. XAML pages, view models, and WPF-specific service implementations.

**Rules:**
- Grid elements must not use `Padding` property (WPF limitation; use `Border` wrapper instead).
- Compiles to an empty stub on non-Windows platforms.

---

### MarketDataCollector.FSharp

| | |
|---|---|
| **Layer** | Standalone |
| **References** | None |
| **Key packages** | None |

**Responsibility:** F# domain library for type-safe market data operations: spread calculations, order book imbalance, validation pipelines, integrity checks. Generates C# interop code via a build target.

**Rules:**
- Must remain standalone with zero project references (ADR-009).
- C# code consumes F# via the generated interop layer in `Generated/`.

---

## Dependency Rules Summary

| Rule | Description |
|------|-------------|
| **No circular dependencies** | All references flow downward/inward through the layer stack |
| **Infrastructure ↮ Storage** | These sibling layers must never reference each other directly |
| **Domain has no NuGet deps** | Pure domain logic, enforced by `BannedReferences.txt` |
| **Contracts has no project refs** | Foundation layer spoken by all assemblies |
| **FSharp is standalone** | No project references; interop via generated code |
| **Application composes** | Only layer allowed to reference both Infrastructure and Storage |
| **ProviderSdk is abstract** | Contains interfaces and attributes, never concrete implementations |

## Adding a New Assembly

1. Place it in the correct layer per the diagram above.
2. Reference only assemblies at the same or lower layers.
3. Add it to `MarketDataCollector.sln`.
4. Add any new packages to `Directory.Packages.props` (no version in `.csproj`).
5. Update this document with the new assembly's responsibilities and rules.
