# Code Improvement Analysis

Targeted improvements to increase modularity, abstraction clarity, and extensibility across the Market Data Collector codebase.

---

## 1. Complete the Command Pattern extraction in Program.cs

**Impact: High | Effort: Medium**

`Program.cs` is 1,049 lines. While `ConfigCommands`, `DiagnosticsCommands`, `PackageCommands`, and `SchemaCheckCommand` have been extracted into `ICliCommand` implementations, **symbol management commands** (lines 133-275), **validate-config**, **dry-run**, **selftest**, and the **main streaming entrypoint** remain inline as long `if`-chains.

### Current problem
```
Program.RunAsync():
  - ConfigCommands.CanHandle()        ✓ extracted
  - DiagnosticsCommands.CanHandle()   ✓ extracted
  - SchemaCheckCommand.CanHandle()    ✓ extracted
  - 7 inline symbol commands          ✗ ~140 lines inline
  - --validate-config                 ✗ inline
  - --dry-run                         ✗ inline
  - --selftest                        ✗ inline
  - PackageCommands.CanHandle()       ✓ extracted
  - main streaming/backfill entrypoint ✗ ~200 lines inline
```

### Recommended changes
- Extract `SymbolCommands : ICliCommand` for all `--symbols*` / `--symbol-status` flags.
- Extract `ValidationCommands : ICliCommand` for `--validate-config`, `--dry-run`, `--selftest`.
- Use the existing `CommandDispatcher` to wire them all together — it already exists but is unused in `Program.cs`.
- Extract the streaming/backfill startup into a dedicated `StreamingHostCommand` or a `CollectorHostBuilder` class.

### Result
`Program.Main()` becomes ~50 lines: load config, build dispatcher, dispatch or fall through to the collector host. Each command is independently testable.

---

## 2. Eliminate 66-file service duplication between UWP and WPF

**Impact: Very High | Effort: High**

The UWP (`MarketDataCollector.Uwp/Services/`) and WPF (`MarketDataCollector.Wpf/Services/`) projects share **66 identically-named service files** totaling **~72,000 lines combined** (~36K each). These are near-copies with only namespace and minor API surface differences.

### Current problem
- Any bug fix or feature in one project must be manually replicated in the other.
- Divergence is inevitable — the WPF `ConfigService` (260 lines) already has a different structure from the UWP version (423 lines).
- Interface contracts are inconsistent: UWP has 4 interfaces in `Contracts/`, WPF has 11 interface files mixed into `Services/`.

### Recommended changes
1. Create `MarketDataCollector.Ui.Services` shared class library with platform-agnostic service implementations.
2. Services that need platform-specific behavior should implement a shared interface with platform adapters (Strategy pattern). For example:
   - `IThemeService` — shared contract in the library, `UwpThemeAdapter` / `WpfThemeAdapter` in each UI project.
   - `INavigationService` — same pattern.
3. Move the 50+ services that are truly identical (HTTP-calling services like `ApiClientService`, `BackfillService`, `StorageService`, etc.) directly into the shared library.
4. Consolidate interfaces into a single `Contracts/` folder in the shared library.

### Result
~30,000 lines of duplicated code eliminated. Single place to fix bugs. Clear separation of platform-agnostic logic vs platform-specific adapters.

---

## 3. Unify the two DI composition paths

**Impact: High | Effort: Medium**

The application has two independent startup paths that build the service graph:

1. **`ServiceCompositionRoot.AddMarketDataServices()`** — proper DI with `IServiceCollection`.
2. **`Program.RunAsync()`** — manual `new` construction of services (lines 430-567).

The streaming/headless path in `Program.cs` manually constructs `StorageOptions`, `JsonlStoragePolicy`, `JsonlStorageSink`, `EventPipeline`, `PipelinePublisher`, `MarketDataClientFactory`, `SubscriptionManager`, collectors, etc. — duplicating what `ServiceCompositionRoot` already wires up.

### Current problem
- Adding a new cross-cutting concern (e.g., a new metric, a new middleware) requires changes in both paths.
- The `ServiceCompositionRoot` is only used by `HostStartupFactory` (backfill and web paths). The main streaming path bypasses it entirely.
- `HttpClientFactory` is initialized via a throwaway `ServiceCollection` in `Program.InitializeHttpClientFactory()` rather than through the composition root.

### Recommended changes
- Route **all** startup modes through `ServiceCompositionRoot` using appropriate `CompositionOptions` presets (the presets already exist: `Streaming`, `BackfillOnly`, `WebDashboard`).
- Replace manual construction in `Program.RunAsync()` with `sp.GetRequiredService<T>()` calls.
- Use `IHost` / `HostBuilder` for the streaming path, matching what the web path already does.

### Result
Single composition root. No duplicate wiring. New services automatically available to all modes.

---

## 4. Introduce a Storage Sink abstraction layer

**Impact: Medium-High | Effort: Medium**

The pipeline is hardcoded to `JsonlStorageSink`:

```csharp
// Program.cs line 431-432
await using var sink = new JsonlStorageSink(storageOpt, policy);
await using var pipeline = new EventPipeline(sink, EventPipelinePolicy.HighThroughput);
```

`ParquetStorageSink` exists in the codebase but can't be swapped in without code changes. There's no runtime sink selection.

### Recommended changes
- Register `IStorageSink` in the composition root, resolved by configuration:
  ```csharp
  services.AddSingleton<IStorageSink>(sp => cfg.Storage.Format switch {
      "parquet" => new ParquetStorageSink(...),
      _ => new JsonlStorageSink(...)
  });
  ```
- Add a `CompositeSink` that writes to multiple sinks (e.g., JSONL for hot tier + Parquet for archive) — useful for tiered storage.
- `EventPipeline` already accepts `IStorageSink`, so no pipeline changes needed.

### Result
Storage format becomes a configuration choice. Multi-sink pipelines enable simultaneous hot/cold writes.

---

## 5. Extract CLI argument parsing into a typed model

**Impact: Medium | Effort: Low-Medium**

CLI arguments are parsed via scattered `args.Any()` and `GetArgValue()` calls throughout `Program.cs` and individual command handlers. There's no single model that represents the parsed CLI state.

### Current problem
- Same argument parsed in multiple places (e.g., `--backfill` checked on line 444, then again in `BuildBackfillRequest()`).
- No validation of mutually exclusive flags.
- No completion/help generation from a schema.

### Recommended changes
- Create `CliArguments` record parsed once at startup:
  ```csharp
  public sealed record CliArguments(
      RunMode Mode,
      bool Backfill, BackfillArgs? BackfillOptions,
      bool DryRun, bool Offline,
      string? ConfigPath, int HttpPort,
      SymbolCommandArgs? SymbolCommand,
      PackageCommandArgs? PackageCommand,
      ...);
  ```
- Use `System.CommandLine` or a simple hand-rolled parser to populate it.
- Pass `CliArguments` to command handlers instead of raw `string[] args`.

### Result
Arguments parsed once, validated once, typed everywhere. Commands receive structured input instead of re-parsing raw strings.

---

## 6. Standardize provider registration with attribute-based discovery

**Impact: Medium | Effort: Medium**

The `[DataSource]` attribute exists and ADR-005 mandates attribute-based discovery, but provider registration still relies on manual factory methods and switch statements in `MarketDataClientFactory` and `ProviderFactory`.

### Recommended changes
- Implement assembly scanning at startup to discover all types decorated with `[DataSource("...")]`.
- Auto-register discovered providers in `ProviderRegistry`.
- New providers become available by simply adding the class with the attribute — no factory modifications needed.

### Result
Adding a new provider requires only implementing the interface and adding the attribute. Zero registration boilerplate.

---

## 7. Add a shared UI ViewModel layer

**Impact: Medium | Effort: Medium**

UWP has 5 ViewModels (`BackfillViewModel`, `DashboardViewModel`, `DataExportViewModel`, `DataQualityViewModel`, `MainViewModel`). WPF has only `BindableBase`. Both UI projects have pages with code-behind that likely duplicates presentation logic.

### Recommended changes
- Create `MarketDataCollector.Ui.ViewModels` in the shared library.
- Move ViewModels to the shared project, parameterized by platform-specific services via constructor injection.
- Both UWP and WPF reference the shared ViewModels; code-behind becomes minimal wiring.

### Result
Consistent behavior across platforms. ViewModel logic tested once.

---

## 8. Increase test coverage for critical paths

**Impact: High | Effort: Ongoing**

78 test files cover 307 source files in the core project (~25% file coverage). Key untested areas:

| Area | Source files | Test files | Gap |
|------|-------------|------------|-----|
| Application/Services/ | ~30 | 2 | `ConfigurationService`, `TradingCalendar`, `GracefulShutdownService` |
| Application/Monitoring/DataQuality/ | ~10 | 9 | Good coverage |
| Infrastructure/Providers/ | ~20 | 1 | Only Polygon tested; Alpaca, IB, StockSharp untested |
| Storage/Sinks/ | 3 | 1 | `ParquetStorageSink` untested |
| Application/Pipeline/ | 3 | 1 | `EventPipeline` has tests, but `PipelinePublisher` and policies don't |
| CLI Commands | 5 | 0 | No command handler tests |
| UI projects (UWP+WPF) | 130+ | 0 | Zero UI service tests |

### Priority test additions
1. **CLI command handlers** — each `ICliCommand` implementation is a pure function of args-to-exit-code, highly testable.
2. **`EventPipeline` integration tests** — test backpressure, flush behavior, and shutdown under load.
3. **Provider client mocking** — test `AlpacaMarketDataClient` and `IBMarketDataClient` with recorded payloads.
4. **UI services** — once shared (see item 2), test the platform-agnostic services.

---

## 9. Decouple Serilog from the domain layer

**Impact: Medium | Effort: Low**

`Serilog.ILogger` is used directly throughout the codebase, including `ServiceCompositionRoot`, `ProviderFactory`, and core services. This couples the domain/application layers to a specific logging implementation.

### Recommended changes
- Use `Microsoft.Extensions.Logging.ILogger<T>` consistently in application and domain layers (some classes already do this, e.g., `EventPipeline`).
- Keep Serilog as the concrete provider, configured only in `Program.cs` and `LoggingSetup`.
- Remove `LoggingSetup.ForContext<T>()` calls from DI registrations; let `ILogger<T>` injection handle it.

### Result
Domain layer depends only on `Microsoft.Extensions.Logging` abstractions. Logging provider becomes swappable.

---

## 10. Consolidate configuration model access

**Impact: Medium | Effort: Low**

Configuration is accessed through multiple mechanisms:
- `ConfigStore.Load()` — returns `AppConfig` from file.
- `ConfigurationService.LoadAndPrepareConfig()` — returns enriched `AppConfig`.
- Direct `JsonSerializer.Deserialize<AppConfig>()` in `Program.LoadConfigMinimal()`.
- `CompositionOptions.ConfigPath` — another config path reference.

Several DI registrations call `configStore.Load()` eagerly during registration (e.g., `AddBackfillServices`, `AddMaintenanceServices`), capturing config values at startup that won't update if the file changes.

### Recommended changes
- Register `AppConfig` as a singleton resolved through `ConfigurationService`.
- Use `IOptionsMonitor<AppConfig>` or a similar reactive pattern for hot-reload scenarios.
- Remove eager `configStore.Load()` calls from DI lambdas.

### Result
Single config access pattern. Hot-reload works consistently. No stale config snapshots in long-lived singletons.

---

## Priority Matrix

| # | Improvement | Impact | Effort | Priority |
|---|-------------|--------|--------|----------|
| 2 | Eliminate UWP/WPF duplication | Very High | High | **P0** |
| 3 | Unify DI composition paths | High | Medium | **P0** |
| 1 | Complete command pattern in Program.cs | High | Medium | **P1** |
| 8 | Increase test coverage | High | Ongoing | **P1** |
| 5 | Typed CLI argument parsing | Medium | Low-Medium | **P1** |
| 4 | Storage sink abstraction | Medium-High | Medium | **P2** |
| 6 | Attribute-based provider discovery | Medium | Medium | **P2** |
| 10 | Consolidate config access | Medium | Low | **P2** |
| 9 | Decouple Serilog from domain | Medium | Low | **P3** |
| 7 | Shared UI ViewModel layer | Medium | Medium | **P3** |
