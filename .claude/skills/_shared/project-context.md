# MarketDataCollector — Shared Project Context

> **Canonical reference.** This file is the single source of truth for project statistics, provider inventory, key abstractions with file paths, and storage design. Both `mdc-brainstorm` and `mdc-code-review` skills reference this file. Update here first; do not maintain separate copies.
>
> **Last verified:** 2026-03-16
> **Refresh command:** `python3 build/scripts/ai-repo-updater.py audit`

---

## Project Statistics

| Metric | Count |
|--------|-------|
| Total Source Files | 779 |
| C# Files | 769 |
| F# Files | 14 (8 modules + 6 interop) |
| Test Projects | 4 |
| Test Files | 266 |
| Test Methods | ~4,135 |
| Documentation Files | 163 |
| Main Projects | 13 (+ 4 test + 1 benchmark) |
| CI/CD Workflows | 27 |
| Makefile Targets | 96 |
| Provider Implementations | 5 streaming, 10 historical |
| Symbol Search Providers | 5 |
| API Route Constants | 309 |
| Endpoint Files | 39 |

---

## Solution Layout (15 main projects)

```
MarketDataCollector.sln
├── src/
│   ├── MarketDataCollector/                   # Entry point (Program.cs, UiServer.cs)
│   ├── MarketDataCollector.Application/       # App services, pipeline, commands, config
│   ├── MarketDataCollector.Contracts/         # DTOs + interfaces (LEAF — no upstream deps)
│   ├── MarketDataCollector.Core/              # Config, exceptions, logging, serialization
│   ├── MarketDataCollector.Domain/            # Collectors, events, models
│   ├── MarketDataCollector.FSharp/            # F# 8.0 domain models, validation, calculations
│   ├── MarketDataCollector.Infrastructure/    # Provider adapters, resilience, HTTP
│   ├── MarketDataCollector.ProviderSdk/       # IMarketDataClient, IHistoricalDataProvider, base SDK
│   ├── MarketDataCollector.Storage/           # WAL, sinks, packaging, export, maintenance
│   ├── MarketDataCollector.Ui/                # Web dashboard (ASP.NET)
│   ├── MarketDataCollector.Ui.Services/       # Shared UI services (platform-neutral)
│   ├── MarketDataCollector.Ui.Shared/         # Shared endpoint handlers (platform-neutral)
│   ├── MarketDataCollector.Wpf/               # WPF desktop app (recommended Windows client)
│   ├── MarketDataCollector.Execution/         # [NEW] IOrderGateway, PaperTradingGateway, broker adapters (ADR-015)
│   └── MarketDataCollector.Strategies/        # [NEW] IStrategyLifecycle, StrategyRunStore, promotion workflow (ADR-016)
├── tests/
│   ├── MarketDataCollector.Tests/             # 266 test files, ~4135 test methods
│   ├── MarketDataCollector.FSharp.Tests/      # F# unit tests (expecto/xUnit)
│   ├── MarketDataCollector.Wpf.Tests/         # WPF service tests (Windows only, 324 tests)
│   └── MarketDataCollector.Ui.Tests/          # UI service tests (Windows only, 927 tests)
└── benchmarks/
    └── MarketDataCollector.Benchmarks/        # BenchmarkDotNet performance benchmarks
```

---

## Dependency Graph

**Allowed:**
```
Wpf host        → Ui.Services, Contracts, Core
ViewModels      → Ui.Services, Contracts, Core
Ui.Services     → Contracts, Core, Application
Ui.Shared       → Ui.Services, Contracts (platform-neutral only)
Application     → Contracts, Core, Domain, Infrastructure, Storage
Infrastructure  → Contracts, Core, ProviderSdk
ProviderSdk     → Contracts only
FSharp          → Contracts only
Contracts       → nothing (leaf project)
Storage         → Contracts, Core
Domain          → Contracts, Core
Web host        → Ui.Services, Ui.Shared, Contracts
Execution       → Contracts, Core, ProviderSdk  (ADR-015)
Strategies      → Contracts, Core, Backtesting.Sdk, Execution    (ADR-016)
```

**Forbidden (flag as CRITICAL in reviews):**
```
Ui.Services     → Wpf host types          (reverse dependency)
Ui.Services     → WPF-only APIs           (platform leak)
Ui.Shared       → WPF-only APIs           (platform leak)
Ui.Shared       → UWP/WinRT APIs          (platform leak + deprecated)
Any project     → MarketDataCollector.Uwp  (UWP fully removed)
ProviderSdk     → anything except Contracts
FSharp          → anything except Contracts
Contracts       → Infrastructure           (dependency inversion)
Core/Domain     → Infrastructure           (dependency inversion)
Backtesting.*   → Execution.*              (backtesting is simulation-only; no live concepts)
Execution.*     → Backtesting.* (except Backtesting.Sdk)  (execution must not depend on simulation infra; Backtesting.Sdk is the allowed adapter per ADR-016)
Strategies.*    → any concrete Execution.* type  (strategies depend only on IOrderGateway/IExecutionContext)
DataCollection  → Strategies.* or Execution.*   (data layer is infrastructure, not strategy-aware)
```

---

## Key Abstractions & File Paths

### IMarketDataClient (Streaming)
**File:** `src/MarketDataCollector.ProviderSdk/IMarketDataClient.cs`

```csharp
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

### IHistoricalDataProvider (Backfill)
**File:** `src/MarketDataCollector.Infrastructure/Adapters/Core/IHistoricalDataProvider.cs`

```csharp
public interface IHistoricalDataProvider
{
    string Name { get; }
    string DisplayName { get; }
    HistoricalDataCapabilities Capabilities { get; }
    int Priority { get; }
    Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
```

### IStorageSink (Persistence)
**File:** `src/MarketDataCollector.Storage/Interfaces/IStorageSink.cs`

```csharp
public interface IStorageSink : IAsyncDisposable
{
    ValueTask WriteAsync(MarketEvent evt, CancellationToken ct = default);
    Task FlushAsync(CancellationToken ct = default);
}
```

### EventPipeline (Hot-Path Coordinator)
**File:** `src/MarketDataCollector.Application/Pipeline/EventPipeline.cs`

- BoundedChannel capacity: 50,000 events
- Backpressure policy: `BoundedChannelFullMode.DropOldest`
- Must use `EventPipelinePolicy.*.CreateChannel<T>()` — not `Channel.CreateBounded<T>()` directly (ADR-013)
- WAL written before in-memory queue; flushed on shutdown

### WriteAheadLog (WAL Durability)
**File:** `src/MarketDataCollector.Storage/Archival/WriteAheadLog.cs`

- Entries must be flushed (not just written) before acknowledging ingest
- `FlushAsync(ct)` must be called before disposal
- Uses `AtomicFileWriter` for crash-safe writes — never write directly to the file
- ADR-007 governs WAL behavior

### AtomicFileWriter (Crash-Safe Writes)
**File:** `src/MarketDataCollector.Storage/Archival/AtomicFileWriter.cs`

- Write to temp file, then rename — guarantees no partial records
- All `IStorageSink` implementations must route through this, not direct `FileStream`

### JsonlStorageSink / ParquetStorageSink
**Files:** `src/MarketDataCollector.Storage/Sinks/JsonlStorageSink.cs`, `ParquetStorageSink.cs`

- Must use `AtomicFileWriter` — not raw `File.WriteAllText` or `FileStream`
- Must implement `IFlushable` for orderly shutdown
- Serialization via source-generated `JsonSerializerContext` — never reflection-based (ADR-014)

### BindableBase (MVVM Base)
**File:** `src/MarketDataCollector.Wpf/ViewModels/BindableBase.cs`

```csharp
public abstract class BindableBase : INotifyPropertyChanged
{
    protected bool SetProperty<T>(ref T field, T value,
        [CallerMemberName] string? propertyName = null) { ... }
    protected void RaisePropertyChanged([CallerMemberName] string? name = null) { ... }
}
```

### RelayCommand (ICommand)
**File:** `src/MarketDataCollector.Wpf/ViewModels/` (or `Ui.Services` shared equivalent)

### MarketDataJsonContext (Source-Generated JSON)
**File:** `src/MarketDataCollector.Core/Serialization/MarketDataJsonContext.cs`

- All serialization must reference this context — no `JsonSerializer.Serialize(obj)` without context
- Add new types with `[JsonSerializable(typeof(T))]` on this context

---

## Provider Inventory

### Streaming Providers (IMarketDataClient)

| Provider | Class | File Path |
|----------|-------|-----------|
| Alpaca | `AlpacaMarketDataClient` | `src/MarketDataCollector.Infrastructure/Adapters/Alpaca/` |
| Polygon | `PolygonMarketDataClient` | `src/MarketDataCollector.Infrastructure/Adapters/Polygon/` |
| Interactive Brokers | `IBMarketDataClient` | `src/MarketDataCollector.Infrastructure/Adapters/InteractiveBrokers/` |
| StockSharp | `StockSharpMarketDataClient` | `src/MarketDataCollector.Infrastructure/Adapters/StockSharp/` |
| NYSE | `NYSEDataSource` | `src/MarketDataCollector.Infrastructure/Adapters/NYSE/` |
| Failover | `FailoverAwareMarketDataClient` | `src/MarketDataCollector.Infrastructure/Adapters/Failover/` |
| NoOp | `NoOpMarketDataClient` | `src/MarketDataCollector.Infrastructure/NoOpMarketDataClient.cs` |

### Historical Providers (IHistoricalDataProvider)

| Provider | Free Tier | Rate Limits |
|----------|-----------|-------------|
| Alpaca | Yes (with account) | 200/min |
| Polygon | Limited | Varies by plan |
| Tiingo | Yes | 500/hour |
| Yahoo Finance | Yes | Unofficial |
| Stooq | Yes | Low |
| StockSharp | Yes | Varies |
| Finnhub | Yes | 60/min |
| Alpha Vantage | Yes | 5/min |
| Nasdaq Data Link | Limited | Varies |
| Interactive Brokers | Yes (with account) | IB pacing rules |

---

## Storage Architecture

### File Layout
```
data/
├── live/                    # Hot tier (real-time)
│   └── {provider}/{date}/
│       ├── {symbol}_trades.jsonl.gz
│       └── {symbol}_quotes.jsonl.gz
├── historical/              # Backfill data
│   └── {provider}/{date}/{symbol}_bars.jsonl
├── _wal/                    # Write-ahead log
└── _archive/                # Cold tier (Parquet)
    └── parquet/
```

### Naming Conventions (Storage Org Modes)
| Mode | Pattern | Default |
|------|---------|---------|
| BySymbol | `{root}/{symbol}/{type}/{date}.jsonl` | ✓ Recommended |
| ByDate | `{root}/{date}/{symbol}/{type}.jsonl` | |
| ByType | `{root}/{type}/{symbol}/{date}.jsonl` | |
| Flat | `{root}/{symbol}_{type}_{date}.jsonl` | |

### Tiered Storage
| Tier | Purpose | Default Retention |
|------|---------|-------------------|
| Hot | Recent data, fast access | 7 days |
| Warm | Compressed older data | 30 days |
| Cold | Archive (Parquet) | Indefinite |

---

## Architecture Decision Records (Quick Reference)

| ADR | Decision | Enforcement |
|-----|----------|-------------|
| ADR-001 | Provider abstraction via interfaces | `[ImplementsAdr("ADR-001")]` on all providers |
| ADR-004 | Async streaming via `IAsyncEnumerable<T>` | Flag any `IEnumerable<T>` return on hot paths |
| ADR-006 | Domain events: sealed record with static factories | Flag mutable event types |
| ADR-007 | WAL + pipeline durability | `AtomicFileWriter` required for all sink writes |
| ADR-008 | JSONL + Parquet simultaneous writes | `CompositeSink` for dual-format output |
| ADR-009 | F# type-safe domain with C# interop | Handle `FSharpOption<T>` properly at boundary |
| ADR-013 | Bounded channel with `DropOldest` policy | `EventPipelinePolicy.*.CreateChannel<T>()` only |
| ADR-014 | JSON source generators — no reflection | `MarketDataJsonContext` on all `JsonSerializer` calls |

---

## Naming & Coding Conventions

| Rule | Good | Bad |
|------|------|-----|
| Async suffix | `LoadDataAsync` | `LoadData` |
| CancellationToken name | `ct` or `cancellationToken` | `token`, `cts` |
| Private fields | `_fieldName` | `fieldName`, `m_field` |
| Structured logging | `_logger.LogInfo("Got {Count}", n)` | `_logger.LogInfo($"Got {n}")` |
| Sealed classes | `public sealed class Foo` | `public class Foo` (unless designed for inheritance) |
| Exception types | `throw new DataProviderException(...)` | `throw new Exception(...)` |
| JSON serialization | `JsonSerializer.Serialize(obj, MyContext.Default.MyType)` | `JsonSerializer.Serialize(obj)` |
| IOptions for hot config | `IOptionsMonitor<T>` for runtime-changeable | `IOptions<T>` only for truly static settings |
| Central packages | No `Version=` in `<PackageReference>` | `<PackageReference Include="Foo" Version="1.0" />` |

---

## F# Interop Rules (C# consumers)

- `FSharpOption<T>` in C#: use `.IsSome` / `.Value`, not null checks
- Discriminated unions: match ALL cases — the `_ =>` catch-all hides new DU cases
- F# record types are immutable — no property assignment; use `with` expressions from F#
- Never add property setters to F# record types
- `[AllowNull]` needed at nullable boundaries

---

## WPF MVVM Role Assignments

| Concern | Location |
|---------|----------|
| UI state (loading, error text) | ViewModel property |
| Domain data (counts, symbols) | ViewModel property |
| Commands (start/stop/export) | ViewModel `RelayCommand` |
| Timer for periodic refresh | ViewModel `PeriodicTimer` — NOT `DispatcherTimer` in code-behind |
| UI thread marshal | View code-behind (thin) |
| Brush/resource caching | View static field |
| Service dependencies | ViewModel constructor injection — NOT Page constructor |
| Business logic | ViewModel or `Ui.Services` — NEVER in `.xaml.cs` |

**UWP is fully removed.** Flag any `using Windows.*` or `using MarketDataCollector.Uwp.*` as CRITICAL.
