# Structural Improvement Proposal: nautilus_trader-Inspired Patterns

**Date:** 2026-03-01
**Status:** Proposal
**Author:** Architecture Review
**Inspired By:** [nautechsystems/nautilus_trader](https://github.com/nautechsystems/nautilus_trader)

---

## Executive Summary

This proposal identifies **7 structural improvements** and **5 procedural/code enhancements** for the Market Data Collector repository, inspired by organizational patterns in the nautilus_trader trading system. All changes are **backward-compatible** — existing public APIs, namespaces, and build targets remain stable. The goal is to improve developer ergonomics, reduce coupling, and make the provider subsystem more modular and self-documenting.

---

## Table of Contents

1. [Structural Changes](#1-structural-changes)
   - [1.1 Unified Per-Provider Directories](#11-unified-per-provider-directories)
   - [1.2 Provider Template Scaffold](#12-provider-template-scaffold)
   - [1.3 Co-located Provider Configuration](#13-co-located-provider-configuration)
   - [1.4 Explicit Parsing Layer Per Provider](#14-explicit-parsing-layer-per-provider)
   - [1.5 Per-Provider Factory Classes](#15-per-provider-factory-classes)
   - [1.6 Consolidated Domain Enums](#16-consolidated-domain-enums)
   - [1.7 Persistence Read/Write/Transform Separation](#17-persistence-readwritetransform-separation)
2. [Code & Procedural Enhancements](#2-code--procedural-enhancements)
   - [2.1 Component Lifecycle FSM Base Class](#21-component-lifecycle-fsm-base-class)
   - [2.2 Provider-Local Common Types](#22-provider-local-common-types)
   - [2.3 Module-Scoped Message Types](#23-module-scoped-message-types)
   - [2.4 Credential Isolation at Provider Boundary](#24-credential-isolation-at-provider-boundary)
   - [2.5 ArchUnitNET Dependency Rules](#25-archunitnet-dependency-rules)
3. [Migration Strategy](#3-migration-strategy)
4. [Risk Assessment](#4-risk-assessment)
5. [Appendix: Side-by-Side Comparison](#appendix-side-by-side-comparison)

---

## 1. Structural Changes

### 1.1 Unified Per-Provider Directories

**Problem:** Provider code for the same vendor (e.g., Alpaca) is currently split across three separate directory trees:

```
Infrastructure/Providers/
├── Streaming/Alpaca/AlpacaMarketDataClient.cs      # streaming
├── Historical/Alpaca/AlpacaHistoricalDataProvider.cs  # backfill
└── SymbolSearch/AlpacaSymbolSearchProviderRefactored.cs  # search (flat, no subdirectory)
```

A developer working on "Alpaca" must navigate three locations. Configuration lives in a fourth place (`Core/Config/AlpacaOptions.cs`). This scattering makes it hard to understand the full surface of a provider.

**nautilus_trader pattern:** Each exchange adapter is a **self-contained directory** containing all concerns:

```
adapters/interactive_brokers/
├── config.py          # IB-specific configuration
├── data.py            # Streaming data client
├── execution.py       # Order execution
├── factories.py       # DI factory
├── providers.py       # Instrument provider
├── common.py          # IB-internal shared types
├── client/            # Low-level API wrapper
├── historical/        # Historical data client
└── parsing/           # Wire format → domain model
```

**Proposed change:** Reorganize providers by vendor, with capability sub-files:

```
Infrastructure/Providers/
├── Core/                              # (unchanged) shared base classes
│   ├── ProviderFactory.cs
│   ├── ProviderRegistry.cs
│   ├── WebSocketProviderBase.cs
│   └── ProviderTemplate.cs
├── Alpaca/                            # NEW: unified Alpaca directory
│   ├── AlpacaConfig.cs               # moved from Core/Config/AlpacaOptions.cs
│   ├── AlpacaMarketDataClient.cs     # moved from Streaming/Alpaca/
│   ├── AlpacaHistoricalDataProvider.cs  # moved from Historical/Alpaca/
│   ├── AlpacaSymbolSearchProvider.cs  # moved from SymbolSearch/ (renamed)
│   └── AlpacaFactory.cs              # NEW: extracted from ProviderFactory.cs
├── Polygon/
│   ├── PolygonConfig.cs
│   ├── PolygonMarketDataClient.cs
│   ├── PolygonHistoricalDataProvider.cs
│   ├── PolygonSymbolSearchProvider.cs
│   └── PolygonFactory.cs
├── InteractiveBrokers/
│   ├── IBConfig.cs
│   ├── IBMarketDataClient.cs
│   ├── IBHistoricalDataProvider.cs
│   ├── IBSimulationClient.cs
│   ├── IBFactory.cs
│   ├── Client/                       # low-level IB API (existing files)
│   │   ├── ContractFactory.cs
│   │   ├── IBConnectionManager.cs
│   │   ├── EnhancedIBConnectionManager.cs
│   │   ├── IBCallbackRouter.cs
│   │   └── IBApiLimits.cs
│   └── Parsing/                      # NEW: extracted wire-format translation
│       └── IBDataParser.cs
├── NYSE/
│   ├── NYSEConfig.cs
│   ├── NYSEDataSource.cs
│   ├── NYSEFactory.cs
│   └── NYSEServiceExtensions.cs
├── StockSharp/
│   ├── StockSharpConfig.cs
│   ├── StockSharpMarketDataClient.cs
│   ├── StockSharpHistoricalDataProvider.cs
│   ├── StockSharpSymbolSearchProvider.cs
│   ├── StockSharpFactory.cs
│   └── Converters/                   # existing
│       ├── MessageConverter.cs
│       └── SecurityConverter.cs
├── Finnhub/
│   ├── FinnhubConfig.cs
│   ├── FinnhubHistoricalDataProvider.cs
│   ├── FinnhubSymbolSearchProvider.cs
│   └── FinnhubFactory.cs
├── Tiingo/
│   ├── TiingoConfig.cs
│   ├── TiingoHistoricalDataProvider.cs
│   └── TiingoFactory.cs
├── YahooFinance/
│   ├── YahooFinanceHistoricalDataProvider.cs
│   └── YahooFinanceFactory.cs
├── Stooq/
│   ├── StooqHistoricalDataProvider.cs
│   └── StooqFactory.cs
├── AlphaVantage/
│   ├── AlphaVantageConfig.cs
│   ├── AlphaVantageHistoricalDataProvider.cs
│   └── AlphaVantageFactory.cs
├── NasdaqDataLink/
│   ├── NasdaqDataLinkConfig.cs
│   ├── NasdaqDataLinkHistoricalDataProvider.cs
│   └── NasdaqDataLinkFactory.cs
├── Failover/                         # cross-cutting failover stays separate
│   ├── FailoverAwareMarketDataClient.cs
│   ├── StreamingFailoverRegistry.cs
│   └── StreamingFailoverService.cs
└── Shared/                           # cross-cutting shared infrastructure
    ├── GapAnalysis/                  # moved from Historical/GapAnalysis/
    ├── Queue/                        # moved from Historical/Queue/
    ├── RateLimiting/                 # moved from Historical/RateLimiting/
    └── SymbolResolution/             # moved from Historical/SymbolResolution/
```

**Benefits:**
- "Everything about Alpaca" is in one place
- New provider onboarding: copy a template directory, implement the required files
- Reduces cognitive overhead when debugging provider-specific issues
- Aligns streaming + historical + search under one namespace per vendor

**Namespace impact:** `MarketDataCollector.Infrastructure.Providers.Alpaca` replaces separate `Providers.Streaming.Alpaca`, `Providers.Historical.Alpaca`, and `Providers.SymbolSearch` namespaces. Internal references update; public `IMarketDataClient` / `IHistoricalDataProvider` interfaces remain unchanged.

---

### 1.2 Provider Template Scaffold

**Problem:** The existing `ProviderTemplate.cs` is a metadata record, not a code scaffold. When a developer wants to add a new provider, they must read documentation and manually study existing providers to know which files to create.

**nautilus_trader pattern:** A `_template/` directory contains skeleton files (`core.py`, `data.py`, `execution.py`, `providers.py`) that define the exact file structure every adapter must have.

**Proposed change:** Add a `_Template/` directory with skeleton C# files:

```
Infrastructure/Providers/_Template/
├── README.md                          # Step-by-step guide
├── TemplateConfig.cs                  # Configuration skeleton
├── TemplateMarketDataClient.cs        # IMarketDataClient skeleton (streaming)
├── TemplateHistoricalDataProvider.cs  # IHistoricalDataProvider skeleton (backfill)
├── TemplateSymbolSearchProvider.cs    # ISymbolSearchProvider skeleton (search)
└── TemplateFactory.cs                 # Factory skeleton
```

Each skeleton file contains:
- Required attributes (`[DataSource]`, `[ImplementsAdr]`)
- Interface methods with `NotImplementedException` stubs
- Structured logging patterns
- `CancellationToken` on all async methods
- Comments marking required vs. optional implementations

**Example skeleton** (`TemplateMarketDataClient.cs`):

```csharp
// Copy this file to your provider directory and rename.
// Replace "Template" with your provider name throughout.
// Delete capabilities you don't support (e.g., remove depth methods if no L2).

[DataSource("template")]
[ImplementsAdr("ADR-001", "Streaming data provider contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class TemplateMarketDataClient : IMarketDataClient
{
    private readonly ILogger _log;

    // TODO: Add provider-specific dependencies (HttpClient, config, etc.)

    public bool IsEnabled => true; // TODO: Wire to configuration

    public Task ConnectAsync(CancellationToken ct = default)
        => throw new NotImplementedException("TODO: Implement connection logic");

    public Task DisconnectAsync(CancellationToken ct = default)
        => throw new NotImplementedException("TODO: Implement disconnection logic");

    // ... remaining interface methods
}
```

**Benefits:**
- Self-documenting contract: the template _is_ the specification
- Reduces provider implementation time from "read docs + study examples" to "copy + fill in"
- Enforces consistent patterns (attributes, logging, cancellation) from day one
- The `README.md` replaces the need to hunt through `docs/development/provider-implementation.md`

---

### 1.3 Co-located Provider Configuration

**Problem:** Provider configuration classes are scattered:

| Provider | Config location |
|----------|----------------|
| Alpaca | `Core/Config/AlpacaOptions.cs` (Application layer) |
| StockSharp | `Core/Config/StockSharpConfig.cs` |
| NYSE | `Streaming/NYSE/NYSEOptions.cs` (co-located, inconsistent) |
| Backfill providers | `Application/Config/BackfillConfig.cs` (all 10 in one file) |

NYSE already follows the co-located pattern. The rest don't.

**nautilus_trader pattern:** Each adapter owns a `config.py` with frozen dataclasses for that adapter's configuration. The configuration _source of truth_ lives next to the code that consumes it.

**Proposed change:** Move each provider's configuration into its own provider directory:

```
Providers/Alpaca/AlpacaConfig.cs        # Contains: AlpacaStreamingConfig, AlpacaBackfillConfig
Providers/Polygon/PolygonConfig.cs      # Contains: PolygonStreamingConfig, PolygonBackfillConfig
Providers/InteractiveBrokers/IBConfig.cs
...
```

The global `BackfillConfig.cs` retains the **aggregated** shape for `appsettings.json` deserialization but delegates to per-provider records:

```csharp
// In Application/Config/BackfillConfig.cs (slimmed down):
public sealed record BackfillProvidersConfig(
    AlpacaBackfillConfig? Alpaca,
    PolygonBackfillConfig? Polygon,
    // ... other providers
);

// In Providers/Alpaca/AlpacaConfig.cs (co-located):
public sealed record AlpacaBackfillConfig(
    bool Enabled = true,
    string? KeyId = null,
    string? SecretKey = null,
    string Feed = "iex",
    string Adjustment = "all",
    int Priority = 5,
    int RateLimitPerMinute = 200);
```

**Benefits:**
- Provider authors find configuration next to implementation
- Eliminates scrolling through a multi-hundred-line BackfillConfig to find one provider's settings
- Consistent with NYSE pattern already in use

---

### 1.4 Explicit Parsing Layer Per Provider

**Problem:** Wire-format parsing (JSON deserialization, field mapping, type conversion) is currently embedded inside provider client classes. For example, `AlpacaMarketDataClient.cs` handles both WebSocket connection management and JSON message parsing in the same class.

**nautilus_trader pattern:** Every adapter has a `parsing/` subdirectory with dedicated files per concern (`parsing/data.py`, `parsing/instruments.py`, `parsing/execution.py`).

**Proposed change:** For complex providers (IB, Polygon, StockSharp), extract parsing into named subdirectories:

```
Providers/InteractiveBrokers/
├── Parsing/
│   ├── IBDataParser.cs          # Tick/quote/depth wire format → domain events
│   ├── IBContractParser.cs      # Contract definitions → SymbolConfig
│   └── IBErrorParser.cs         # Error codes → typed exceptions
```

```
Providers/Polygon/
├── Parsing/
│   ├── PolygonMessageParser.cs  # WebSocket JSON → domain events
│   └── PolygonRestParser.cs     # REST responses → HistoricalBar
```

For simpler providers (Stooq, AlphaVantage) with minimal parsing, a separate directory isn't warranted — keep parsing inline.

**Benefits:**
- Client classes focus on connection lifecycle and subscription management
- Parsing logic is independently testable (unit tests with raw JSON fixtures)
- Changes to provider wire format don't touch connection management code

---

### 1.5 Per-Provider Factory Classes

**Problem:** The centralized `ProviderFactory.cs` (338 lines) contains creation logic for **all** providers. Adding a new backfill provider means modifying this file, adding type aliases, and risking merge conflicts with other provider work.

**nautilus_trader pattern:** Each adapter has a `factories.py` that knows how to construct that adapter's components. The composition root just calls each factory.

**Proposed change:** Extract per-provider factory classes from `ProviderFactory.cs`:

```csharp
// In Providers/Alpaca/AlpacaFactory.cs
[ImplementsAdr("ADR-001", "Alpaca provider factory")]
public static class AlpacaFactory
{
    public static AlpacaHistoricalDataProvider? CreateBackfillProvider(
        AlpacaBackfillConfig? cfg,
        ICredentialResolver credentials,
        ILogger log)
    {
        if (!(cfg?.Enabled ?? true)) return null;
        var (keyId, secretKey) = credentials.ResolveAlpacaCredentials(cfg?.KeyId, cfg?.SecretKey);
        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secretKey)) return null;
        return new AlpacaHistoricalDataProvider(keyId, secretKey, cfg?.Feed ?? "iex", ...);
    }

    public static AlpacaSymbolSearchProvider? CreateSearchProvider(
        AlpacaBackfillConfig? cfg,
        ICredentialResolver credentials,
        ILogger log)
    { ... }
}
```

The central `ProviderFactory.cs` becomes a thin orchestrator:

```csharp
public IReadOnlyList<IHistoricalDataProvider> CreateBackfillProviders()
{
    var providers = new List<IHistoricalDataProvider>();
    TryAdd(providers, () => AlpacaFactory.CreateBackfillProvider(_config.Backfill?.Providers?.Alpaca, _creds, _log));
    TryAdd(providers, () => PolygonFactory.CreateBackfillProvider(_config.Backfill?.Providers?.Polygon, _creds, _log));
    // ... one line per provider
    return providers.OrderBy(p => p.Priority).ToList();
}
```

**Benefits:**
- Adding a new provider doesn't touch existing factory code
- Each factory class is testable in isolation
- Merge conflicts reduced — parallel provider work is in separate files
- Central factory becomes a thin composition list

---

### 1.6 Consolidated Domain Enums

**Problem:** Domain enumerations are spread across multiple files and namespaces:

```
Contracts/Domain/Enums/          # Some enums here
Core/Config/DataSourceKind.cs    # Some here
Contracts/Configuration/         # Some here
```

**nautilus_trader pattern:** A single `model/enums.py` file contains all trading enumerations, providing a single reference point.

**Proposed change:** Consolidate all domain-level enums into `Contracts/Domain/Enums/` with clear grouping:

```
Contracts/Domain/Enums/
├── MarketDataEnums.cs     # EventType, DataFeedType, AssetClass
├── ProviderEnums.cs       # DataSourceKind, ProviderType, ProviderTypeKind
├── StorageEnums.cs        # StorageTier, CompressionProfile, NamingConvention
└── TradingEnums.cs        # OrderSide, TimeInForce (if applicable)
```

Keep individual enum files if they have associated logic (e.g., `DataSourceKindConverter.cs`), but ensure all pure enum definitions live in the canonical location.

**Benefits:**
- Developers looking for "what enums exist" check one directory
- Eliminates scattered enum discovery across 4+ projects
- Prevents duplicate enum definitions

---

### 1.7 Persistence Read/Write/Transform Separation

**Problem:** The Storage project organizes by technical concept (Sinks, Archival, Export) but mixes read and write concerns within some services.

**nautilus_trader pattern:** Persistence has explicit named layers: `loaders.py` (read), `writer.py` (write), `wranglers.py` (transform).

**Proposed change:** This is a lighter-touch reorganization — add a `README.md` documenting the read/write/transform roles of existing classes, and ensure new storage code follows the pattern:

```
Storage/
├── Read/                          # NEW grouping (or just documentation)
│   ├── JsonlReplayer.cs          # existing, moved from Replay/
│   └── MemoryMappedJsonlReader.cs # existing, moved from Replay/
├── Write/
│   ├── JsonlStorageSink.cs       # existing, moved from Sinks/
│   ├── ParquetStorageSink.cs     # existing, moved from Sinks/
│   └── CompositeSink.cs          # existing, moved from Sinks/
├── Transform/
│   └── ParquetConversionService.cs # existing, moved from Services/
├── Archival/                      # existing (unchanged)
├── Catalog/                       # NEW: inspired by nautilus_trader catalog/
│   └── StorageCatalogService.cs  # existing, moved from Services/
├── Export/                        # existing (unchanged)
├── Maintenance/                   # existing (unchanged)
└── Packaging/                     # existing (unchanged)
```

**Alternative (lower risk):** Keep existing directories but add clear `Read/`, `Write/`, `Transform/` XML doc tags and a `Storage/README.md` mapping file that documents which class handles which concern.

---

## 2. Code & Procedural Enhancements

### 2.1 Component Lifecycle FSM Base Class

**Problem:** Provider components use implicit lifecycle management via `IHostedService` and `IAsyncDisposable`. There's no standard way to query a component's state (is it starting? connected? degraded? stopping?).

**nautilus_trader pattern:** Every component extends `Component`, which embeds a finite state machine with explicit states: `PRE_INITIALIZED → READY → RUNNING → DEGRADED → STOPPED → DISPOSED`. State transitions are guarded and logged.

**Proposed enhancement:** Create a `ComponentBase` class in `Core/`:

```csharp
// Core/ComponentBase.cs
public abstract class ComponentBase : IAsyncDisposable
{
    public ComponentState State { get; private set; } = ComponentState.Created;

    protected async Task TransitionToAsync(ComponentState target, CancellationToken ct)
    {
        ValidateTransition(State, target);
        var previous = State;
        State = target;
        _log.LogInformation("{Component} transitioned {From} → {To}", GetType().Name, previous, target);
        await OnStateChangedAsync(previous, target, ct);
    }

    protected virtual Task OnStateChangedAsync(ComponentState from, ComponentState to, CancellationToken ct)
        => Task.CompletedTask;

    private static void ValidateTransition(ComponentState from, ComponentState to) { /* guard table */ }
}

public enum ComponentState
{
    Created,
    Initializing,
    Ready,
    Starting,
    Running,
    Degraded,
    Stopping,
    Stopped,
    Disposed,
    Faulted
}
```

**Adoption:** Streaming providers inherit from `ComponentBase`. This gives the monitoring dashboard observable lifecycle states and enables health checks like "is the Alpaca client in Degraded state?".

**Benefits:**
- Observable component states for dashboards and health checks
- Prevents invalid state transitions (e.g., calling `Connect` on a `Disposed` component)
- Structured lifecycle logging (consistent "X transitioned Running → Stopping" messages)
- Aligns with ADR-012 (Monitoring & Alerting Pipeline)

---

### 2.2 Provider-Local Common Types

**Problem:** Provider-specific constants and types (like IB contract types, Polygon message enums, Alpaca feed names) often end up in shared namespaces or as magic strings scattered through client code.

**nautilus_trader pattern:** Each adapter has a `common.py` (or `common/` directory) for adapter-internal shared types. These types are **not exported** to the rest of the system.

**Proposed enhancement:** Add a `Common.cs` or `Constants.cs` file to each provider directory:

```csharp
// Providers/InteractiveBrokers/IBConstants.cs
internal static class IBConstants
{
    public const int DefaultPort = 7496;
    public const int GatewayPort = 4001;
    public const int MaxSubscriptionsPerConnection = 100;
    public const string MarketDataType_RealTime = "1";
    public const string MarketDataType_Frozen = "2";
    // ...
}
```

Mark these `internal` so they don't leak into the public API surface.

**Benefits:**
- Magic numbers and strings are named and co-located
- `internal` visibility enforces that provider-specific constants don't couple other code to a specific provider
- Easier to audit for hard-coded values

---

### 2.3 Module-Scoped Message Types

**Problem:** The event pipeline uses a single `MarketEvent` wrapper with polymorphic payloads (per ADR-006). While this is architecturally sound, pipeline-specific command/request types (like "subscribe to this symbol" or "trigger a backfill") are not always clearly separated from domain events.

**nautilus_trader pattern:** Each module defines its own message types in a `messages.py` file (e.g., `data/messages.py` defines `SubscribeData`, `UnsubscribeData`, `RequestData`).

**Proposed enhancement:** Create explicit command types per subsystem:

```csharp
// Application/Pipeline/PipelineCommands.cs
public sealed record SubscribeSymbolCommand(string Symbol, DataFeedType FeedType);
public sealed record UnsubscribeSymbolCommand(string Symbol);

// Application/Backfill/BackfillCommands.cs
public sealed record RunBackfillCommand(string Symbol, DateOnly From, DateOnly To, string? Provider);
public sealed record CancelBackfillCommand(string JobId);
```

These replace ad-hoc parameter bags or string-based dispatching with typed, self-documenting command objects.

**Benefits:**
- Commands are discoverable via "find all references" on the type
- Enables command validation at the type level
- Improves testability — commands can be asserted without parsing strings

---

### 2.4 Credential Isolation at Provider Boundary

**Problem:** The current `ICredentialResolver` has methods for every provider (`ResolveAlpacaCredentials`, `ResolvePolygonCredentials`, etc.). Adding a new provider requires modifying this shared interface.

**nautilus_trader pattern:** Each adapter has its own `credentials.py` or loads environment variables in its `config.py`. Credentials are sourced at the adapter boundary and never passed downstream.

**Proposed enhancement:** Replace the monolithic `ICredentialResolver` with per-provider credential resolution in each factory:

```csharp
// In Providers/Alpaca/AlpacaFactory.cs
private static (string? KeyId, string? SecretKey) ResolveCredentials(AlpacaBackfillConfig? cfg)
{
    var keyId = cfg?.KeyId
        ?? Environment.GetEnvironmentVariable("ALPACA__KEYID")
        ?? Environment.GetEnvironmentVariable("ALPACA_KEY_ID");
    var secretKey = cfg?.SecretKey
        ?? Environment.GetEnvironmentVariable("ALPACA__SECRETKEY")
        ?? Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");
    return (keyId, secretKey);
}
```

Keep a lightweight `ISecretProvider` interface for vault integration, but move the environment-variable-name knowledge into each provider's own code.

**Benefits:**
- Adding a new provider doesn't require modifying a shared interface
- Environment variable names are co-located with the provider that uses them
- The credential resolution contract is self-contained per provider

---

### 2.5 ArchUnitNET Dependency Rules

**Problem:** Layer boundary violations are documented in `repository-organization-guide.md` and `layer-boundaries.md` but not enforced programmatically. Violations are caught only during code review.

**nautilus_trader pattern:** While nautilus_trader doesn't use ArchUnit specifically, their strict module boundaries (no cross-adapter imports, no persistence importing from adapters) are enforced by Python's import system and CI checks.

**Proposed enhancement:** Add an ArchUnitNET test class that enforces documented dependency rules:

```csharp
// tests/MarketDataCollector.Tests/Architecture/LayerBoundaryTests.cs
public class LayerBoundaryTests
{
    private static readonly Architecture Architecture =
        new ArchLoader().LoadAssemblies(
            typeof(MarketDataCollector.Core.Config.AppConfig).Assembly,
            typeof(MarketDataCollector.Domain.Collectors.TradeDataCollector).Assembly,
            typeof(MarketDataCollector.Infrastructure.Providers.Core.ProviderFactory).Assembly
        ).Build();

    [Fact]
    public void Domain_Should_Not_Reference_Infrastructure()
    {
        Types().That().ResideInNamespace("MarketDataCollector.Domain")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("MarketDataCollector.Infrastructure"))
            .Check(Architecture);
    }

    [Fact]
    public void Core_Should_Not_Reference_Application()
    {
        Types().That().ResideInNamespace("MarketDataCollector.Core")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("MarketDataCollector.Application"))
            .Check(Architecture);
    }

    [Fact]
    public void Providers_Should_Not_Cross_Reference()
    {
        // Alpaca should not reference Polygon internals, etc.
        Types().That().ResideInNamespace("MarketDataCollector.Infrastructure.Providers.Alpaca")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("MarketDataCollector.Infrastructure.Providers.Polygon"))
            .Check(Architecture);
    }
}
```

**Benefits:**
- Layer violations caught at build/test time, not code review
- Documents architecture-as-code alongside the tests
- Prevents gradual erosion of boundaries over time
- Minimal setup — one NuGet package, one test file

---

## 3. Migration Strategy

### Phase 1: Foundation (Low Risk)

| Change | Effort | Risk |
|--------|--------|------|
| 1.2 Provider Template Scaffold | Small | None (additive) |
| 2.2 Provider-Local Common Types | Small | None (additive) |
| 2.5 ArchUnitNET tests | Small | None (additive) |
| 1.6 Consolidated Domain Enums | Medium | Low (moves, no API change) |

### Phase 2: Provider Restructuring (Medium Risk)

| Change | Effort | Risk |
|--------|--------|------|
| 1.1 Unified Per-Provider Directories | Large | Medium (namespace changes) |
| 1.3 Co-located Provider Configuration | Medium | Low (records can stay API-compatible) |
| 1.5 Per-Provider Factory Classes | Medium | Low (internal refactoring) |

### Phase 3: Enhanced Patterns (Medium Risk)

| Change | Effort | Risk |
|--------|--------|------|
| 1.4 Explicit Parsing Layer | Medium | Low (extract, don't rewrite) |
| 2.1 Component Lifecycle FSM | Medium | Medium (base class change) |
| 2.3 Module-Scoped Message Types | Small | Low (additive) |
| 2.4 Credential Isolation | Small | Low (internal refactoring) |

### Phase 4: Storage Reorganization (Lower Priority)

| Change | Effort | Risk |
|--------|--------|------|
| 1.7 Persistence Read/Write/Transform | Medium | Medium (file moves) |

### Migration Approach Per Phase

1. **Create the target directory structure** (empty directories)
2. **Move files one provider at a time** using `git mv` to preserve history
3. **Update namespaces** (use IDE refactoring tools)
4. **Update `using` directives** across consuming projects
5. **Run full build + test suite** after each provider move
6. **Update CLAUDE.md** and `repository-organization-guide.md` to reflect new structure

---

## 4. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Namespace changes break consuming code | Medium | High | Use IDE refactoring; run full test suite after each move |
| Merge conflicts with in-flight PRs | Medium | Medium | Coordinate timing; do restructuring in a single focused sprint |
| Build breaks from moved files | Low | High | Move one provider at a time; verify build between each |
| Test failures from changed namespaces | Low | Medium | Tests reference interfaces (not concrete types); impact limited |
| Documentation becomes outdated | High | Low | Update CLAUDE.md and org guide as part of restructuring PR |

---

## Appendix: Side-by-Side Comparison

### Current vs. Proposed: Finding "Everything About Alpaca"

**Current (4 locations):**
```
src/MarketDataCollector.Application/Config/AlpacaOptions.cs          # config
src/MarketDataCollector.Infrastructure/Providers/Streaming/Alpaca/    # streaming
src/MarketDataCollector.Infrastructure/Providers/Historical/Alpaca/   # backfill
src/MarketDataCollector.Infrastructure/Providers/SymbolSearch/Alpaca* # search
src/MarketDataCollector.Infrastructure/Providers/Core/ProviderFactory.cs  # factory (shared)
```

**Proposed (1 location):**
```
src/MarketDataCollector.Infrastructure/Providers/Alpaca/
├── AlpacaConfig.cs
├── AlpacaMarketDataClient.cs
├── AlpacaHistoricalDataProvider.cs
├── AlpacaSymbolSearchProvider.cs
└── AlpacaFactory.cs
```

### Current vs. Proposed: Adding a New Provider

**Current steps:**
1. Create streaming client in `Providers/Streaming/NewProvider/`
2. Create historical provider in `Providers/Historical/NewProvider/`
3. Create search provider in `Providers/SymbolSearch/`
4. Add config class in `Core/Config/` or `Application/Config/`
5. Modify `ProviderFactory.cs` to add creation logic
6. Modify `ICredentialResolver` to add credential resolution
7. Register in `ServiceCompositionRoot.cs`
8. Read `docs/development/provider-implementation.md` for guidance

**Proposed steps:**
1. Copy `Providers/_Template/` to `Providers/NewProvider/`
2. Rename classes and implement methods (template shows exactly what's needed)
3. Register in `ServiceCompositionRoot.cs`
4. Delete any template files for capabilities you don't support

---

### nautilus_trader Patterns Not Adopted (And Why)

| Pattern | Reason for Exclusion |
|---------|---------------------|
| `execution.py` per adapter | Market Data Collector doesn't handle order execution |
| Singleton metaclass for catalog | .NET DI container handles singleton lifecycle |
| Cython/Rust FFI layer | Not applicable to .NET; already uses source generators for perf |
| `msgbus/` module | The bounded-channel `EventPipeline` serves this role (ADR-013) |
| Frozen config base class | .NET records with `init` setters achieve similar immutability |
| `actors` pattern | Not applicable; the system uses `IHostedService` + DI |

---

---

## 5. Current Structural Issues Discovered During Analysis

The following concrete issues were identified in the current codebase during this analysis. These are independent of the nautilus_trader-inspired proposals and represent existing inconsistencies, naming violations, and misplaced code that should be addressed regardless of whether the larger restructuring is adopted.

### 5.1 Provider Organization Issues

| Issue | Severity | Location | Fix |
|-------|----------|----------|-----|
| **Orphaned `BackfillProgressTracker.cs`** from incomplete Backfill→Historical migration | Medium | `Infrastructure/Providers/Backfill/BackfillProgressTracker.cs` | Move to `Historical/` or unified `Providers/Shared/` |
| **`StockSharpSymbolSearchProvider.cs` in wrong category** — a `ISymbolSearchProvider` living in `Streaming/StockSharp/` | Medium | `Providers/Streaming/StockSharp/StockSharpSymbolSearchProvider.cs` | Move to `SymbolSearch/` (or unified `Providers/StockSharp/`) |
| **`Refactored` suffix** on canonical implementations | Low | `SymbolSearch/AlpacaSymbolSearchProviderRefactored.cs`, `FinnhubSymbolSearchProviderRefactored.cs` | Rename to `AlpacaSymbolSearchProvider.cs`, `FinnhubSymbolSearchProvider.cs` |
| **Inconsistent base class naming** — `BaseHistoricalDataProvider` (prefix) vs `WebSocketProviderBase` (suffix) | Low | `Historical/`, `Core/` | Standardize on one pattern (suffix preferred per guide) |

### 5.2 Configuration Scattering

| Issue | Severity | Location | Fix |
|-------|----------|----------|-----|
| **Provider-specific options in Core** — `AlpacaOptions.cs`, `StockSharpConfig.cs` in `Core/Config/` | Low | `Core/Config/` | Move to provider directories (Section 1.3) |
| **`ICredentialStore.cs` isolated** — single file in `Application/Credentials/` | Low | `Application/Credentials/` | Merge into `Application/Config/Credentials/` |
| **`Application/Http/` uses `Application.UI` namespace** — directory renamed from `UI/` to `Http/` but namespaces not updated | Medium | `Application/Http/*.cs` | Update namespaces to `MarketDataCollector.Application.Http` |

### 5.3 Layer Boundary Violations

| Issue | Severity | Location | Fix |
|-------|----------|----------|-----|
| **`Core.csproj` references `Domain`** — Core should be a lower layer than Domain | Medium | `Core/MarketDataCollector.Core.csproj` | Move shared types to `Contracts`; remove dependency |
| **`IHistoricalDataProvider` and `ISymbolSearchProvider` defined in Infrastructure** — provider interfaces should be in `ProviderSdk` | Medium | `Infrastructure/Providers/Historical/`, `Infrastructure/Providers/SymbolSearch/` | Move to `ProviderSdk/` alongside `IMarketDataClient` |
| **`ImplementsAdrAttribute` in ProviderSdk** — used by all layers, not provider-specific | Low | `ProviderSdk/ImplementsAdrAttribute.cs` | Move to `Contracts/` or `Core/` |
| **`Results/ErrorCode.cs` in Application** — lower layers can't use error codes without referencing Application | Medium | `Application/Results/` | Move to `Core/` or `Contracts/` |
| **`MigrationDiagnostics.cs`** — lives in `Core/Monitoring/` but uses namespace `MarketDataCollector.Application.Monitoring` | Medium | `Core/Monitoring/MigrationDiagnostics.cs` | Align namespace to `MarketDataCollector.Core.Monitoring` |

### 5.4 Duplicate/Ambiguous Names

| Issue | Severity | Location | Fix |
|-------|----------|----------|-----|
| **Two `MarketEvent` records** — near-identical definitions in `Domain/Events/` and `Contracts/Domain/Events/` | Medium | Both projects | Consolidate into one canonical location |
| **`BackfillCoordinator`** — same class name in `Application/Http/` and `Ui.Shared/Services/` | Medium | Both projects | Rename to `CoreBackfillCoordinator` / `UiBackfillCoordinator` |
| **`ConfigStore`** — same class name in `Application/Http/` and `Ui.Shared/Services/` | Medium | Both projects | Rename to `InMemoryConfigStore` / `UiConfigStore` |

### 5.5 Test Structure Issues

| Issue | Severity | Location | Fix |
|-------|----------|----------|-----|
| **24 test files using flat `namespace MarketDataCollector.Tests;`** despite being in correct subdirectories | Medium | Various test files | Update namespaces to match folder (e.g., `MarketDataCollector.Tests.Domain.Collectors`) |
| **`BackfillWorkerServiceTests.cs` tests Infrastructure code but lives in `Application/Backfill/`** | Medium | `tests/.../Application/Backfill/` | Move to `tests/.../Infrastructure/Providers/` |
| **`BackfillProgressTrackerTests.cs` tests Infrastructure code but lives in `Application/Pipeline/`** | Medium | `tests/.../Application/Pipeline/` | Move to `tests/.../Infrastructure/Providers/` |
| **`CronExpressionParserTests.cs` tests `Core/Scheduling/` but lives in `Application/Services/`** | Low | `tests/.../Application/Services/` | Move to `tests/.../Core/Scheduling/` |
| **5 backfill tests use old namespace `MarketDataCollector.Tests.Backfill`** | Low | `tests/.../Application/Backfill/` | Update to `MarketDataCollector.Tests.Application.Backfill` |
| **`SymbolSearch/` tests not mirroring source path** — should be under `Infrastructure/Providers/` | Low | `tests/.../SymbolSearch/` | Move to `tests/.../Infrastructure/Providers/SymbolSearch/` |

### 5.6 Other Structural Issues

| Issue | Severity | Location | Fix |
|-------|----------|----------|-----|
| **Double-nested `Core/Performance/Performance/`** folder | Low | `Core/Performance/Performance/` | Flatten to `Core/Performance/` |
| **`StorageOptions.cs` and `StorageProfiles.cs` at project root** instead of in `Config/` | Low | `Storage/StorageOptions.cs` | Move to `Storage/Config/` |
| **`Storage/Services/` has 15 mixed-concern services** — grab-bag folder | Low | `Storage/Services/` | Sub-folder by concern (e.g., `Quality/`, `Catalog/`, `Lifecycle/`) |
| **4 endpoints stranded in `Application/Http/Endpoints/`** while 35 live in `Ui.Shared/Endpoints/` | Medium | `Application/Http/Endpoints/` | Move to `Ui.Shared/Endpoints/` for consistency |
| **`FSharp` project not actually referenced** by any other project (CLAUDE.md says it is) | Low | `FSharp.fsproj` | Update CLAUDE.md to reflect reality |
| **`DepthBufferSelfTests.cs`** — runtime self-test code in production Application assembly | Low | `Application/Testing/` | Consider moving to test project or marking clearly as diagnostic |

### Quick Win Priority Order

These can be fixed independently with minimal risk:

1. **Rename `Refactored` suffix files** (2 files, zero API impact)
2. **Fix namespace/folder mismatch in `Application/Http/`** (namespace update only)
3. **Fix double-nested `Core/Performance/Performance/`** (1 `git mv`)
4. **Move orphaned `BackfillProgressTracker.cs`** to correct location
5. **Move `StockSharpSymbolSearchProvider.cs`** to `SymbolSearch/`
6. **Update 24 test file namespaces** to match folder structure
7. **Fix test files testing wrong layer** (move 3 test files)
8. **Rename ambiguous `BackfillCoordinator`/`ConfigStore` duplicates**

---

*This proposal is a living document. Feedback and prioritization adjustments are welcome before implementation begins.*
