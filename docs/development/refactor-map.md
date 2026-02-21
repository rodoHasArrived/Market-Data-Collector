# Concrete Refactor Map (Dependency-Safe)

**Goal:** Reduce implementation complexity while preserving runtime behavior and public functionality.

**Scope constraints:**
- Preserve all existing CLI commands, API routes, and provider capabilities.
- Keep architecture layer boundaries intact.
- Prefer additive adapters first, then swaps, then cleanup.

## Risk Scale

- **1-2 (Low):** Localized internal refactor, easy rollback.
- **3 (Medium):** Cross-project wiring changes, test updates likely.
- **4 (High):** Runtime behavior surface impacted, migration sequencing critical.
- **5 (Very High):** Broad architecture migration; requires staged rollout + feature flags.

---

## Phase 0 — Baseline & Safety Rails ✅ COMPLETE

### Step 0.1 — Lock baseline behavior snapshots ✅
- **Status:** Complete.
- **What was done:**
  - 18 integration endpoint test files covering status, health, config, backfill, provider, storage, symbol, maintenance, failover, quality, and negative-path endpoints.
  - `ResponseSchemaSnapshotTests` and `ResponseSchemaValidationTests` validate JSON schema structure (fields, types, required keys).
  - Provider message parsing tests for Polygon, NYSE, StockSharp.
- **Key files:**
  - `tests/MarketDataCollector.Tests/Integration/EndpointTests/*` (18 files)
  - `tests/MarketDataCollector.Tests/Infrastructure/Providers/*` (12 files)

### Step 0.2 — Add temporary observability counters for migration ✅
- **Status:** Complete.
- **What was done:**
  - `MigrationDiagnostics` static class with factory hit counts (streaming, backfill, symbol search), reconnect counters (attempts, successes, failures by provider), resubscribe counters, and registration counters.
  - `GetSnapshot()` returns immutable record for monitoring.
- **Key files:**
  - `src/MarketDataCollector.Core/Monitoring/MigrationDiagnostics.cs`

---

## Phase 1 — Unify Provider Construction (No Feature Change) ✅ COMPLETE

### Step 1.1 — Introduce `ProviderRegistry` abstraction ✅
- **Status:** Complete.
- **What was done:**
  - `ProviderRegistry` in `Infrastructure/Providers/Core/` serves as the single source of truth for all provider types.
  - Streaming factories registered as `Dictionary<DataSourceKind, Func<IMarketDataClient>>`.
  - Universal queries: `GetAllProviderMetadata()`, `GetProvider<T>()`, `GetProviders<T>()`, `GetBestAvailableProviderAsync<T>()`.
  - `IProviderMetadata` unified identity and capabilities contract.
- **Key files:**
  - `src/MarketDataCollector.Infrastructure/Providers/Core/ProviderRegistry.cs`

### Step 1.2 — Wire attribute-based discovery into registry (behind switch) ✅
- **Status:** Complete.
- **What was done:**
  - Added `ProviderRegistryConfig` record with `UseAttributeDiscovery` flag to `AppConfig`.
  - `ServiceCompositionRoot.AddProviderServices()` checks `config.ProviderRegistry?.UseAttributeDiscovery` flag.
  - When true, `RegisterStreamingFactoriesFromAttributes()` iterates `DataSourceRegistry.Sources` to auto-register `IMarketDataClient` implementations via `[DataSource]` attribute discovery.
  - `TryMapToDataSourceKind()` maps attribute IDs to `DataSourceKind` enum values.
  - Default: false (manual lambda registration preserved as fallback).
- **Key files:**
  - `src/MarketDataCollector.Core/Config/AppConfig.cs` (added `ProviderRegistryConfig`)
  - `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs` (added discovery methods)

### Step 1.3 — Remove direct provider instantiation from host startup ✅
- **Status:** Complete.
- **What was done:**
  - `HostStartup.CreateStreamingClient()` delegates to `ProviderRegistry.CreateStreamingClient()`.
  - `ServiceCompositionRoot` is the single source of truth for DI registration.
  - `Program.cs` resolves providers through DI/registry, not `new` statements.
- **Key files:**
  - `src/MarketDataCollector.Application/Composition/HostStartup.cs`
  - `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`

---

## Phase 2 — Single Composition Root (DI Everywhere) ✅ COMPLETE

### Step 2.1 — Move pipeline construction entirely to DI ✅
- **Status:** Complete.
- **What was done:**
  - `JsonlStoragePolicy`, `JsonlStorageSink`, `ParquetStorageSink`, `CompositeSink`, `WriteAheadLog`, `DroppedEventAuditTrail`, and `EventPipeline` all registered as singletons in `ServiceCompositionRoot.AddPipelineServices()`.
  - `IStorageSink` resolved as `CompositeSink` when Parquet enabled, otherwise `JsonlStorageSink`.
  - `IMarketEventPublisher` wraps `EventPipeline` via `PipelinePublisher`.
- **Key files:**
  - `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`

### Step 2.2 — Single config load path ✅
- **Status:** Complete.
- **What was done:**
  - `ConfigStore` registered as singleton in DI, loads config once.
  - `Program.cs` uses `LoadConfigMinimal()` only for pre-DI logging initialization (justified).
  - All other config access goes through `ConfigStore.Load()` via DI.
- **Key files:**
  - `src/MarketDataCollector/Program.cs`
  - `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`

---

## Phase 3 — WebSocket Lifecycle Consolidation 🔄 IN PROGRESS

### Step 3.1 — Define migration contract on `WebSocketProviderBase` ✅
- **Status:** Complete.
- **What was done:**
  - Created `WebSocketProviderBase` abstract class in `Infrastructure/Providers/Core/`.
  - Delegates connection lifecycle to `WebSocketConnectionManager` (resilience, heartbeat, reconnection gating).
  - Template method hooks: `BuildWebSocketUri()`, `AuthenticateAsync()`, `HandleMessageAsync()`, `ResubscribeAsync()`, `ConfigureWebSocket()`.
  - Automatic reconnection with `MigrationDiagnostics` counter integration.
  - Clean `IAsyncDisposable` implementation.
- **Key files:**
  - `src/MarketDataCollector.Infrastructure/Providers/Core/WebSocketProviderBase.cs` (new)

### Step 3.2 — Migrate Polygon reconnection to shared helper ✅
- **Status:** Complete (partial migration).
- **What was done:**
  - Replaced Polygon's ~60-line manual reconnection logic (`SemaphoreSlim` gating, `CalculateReconnectDelay`, manual attempt tracking) with `WebSocketReconnectionHelper.TryReconnectAsync()`.
  - Polygon still manages its own `ClientWebSocket` directly (required for protocol-specific handshake: sync message exchange for `WaitForConnectionMessage` and `Authenticate` before receive loop).
  - Full migration to `WebSocketProviderBase` deferred due to Polygon's sync handshake pattern (send auth → wait for response → then start receive loop).
- **Key files:**
  - `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs`

### Step 3.3 — Migrate NYSE to base class ⏳ DEFERRED
- **Status:** Deferred.
- **Reason:** NYSE implements `DataSourceBase` + `IRealtimeDataSource` + `IHistoricalDataSource` (hybrid pattern), not `IMarketDataClient`. Migrating requires interface refactoring beyond WebSocket consolidation scope.

### Step 3.4 — Migrate StockSharp to base class ⏳ DEFERRED
- **Status:** Deferred.
- **Reason:** StockSharp wraps a third-party `Connector` (not raw WebSocket) behind `#if STOCKSHARP` conditional compilation. `WebSocketProviderBase` doesn't apply to connector-based providers.

### Step 3.5 — Remove redundant reconnect implementations ✅
- **Status:** Complete (for Polygon).
- **What was done:**
  - Removed Polygon's manual `SemaphoreSlim _reconnectGate`, `_reconnectAttempts`, `MaxReconnectAttempts`, `ReconnectBaseDelay`, `ReconnectMaxDelay` fields.
  - Removed `CalculateReconnectDelay()` method.
  - Reconnection now delegated to `WebSocketReconnectionHelper` which provides identical behavior (gated exponential backoff with jitter).
- **Key files:**
  - `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs`

---

## Phase 4 — Metrics Abstraction (Decouple from Statics) ✅ COMPLETE

### Step 4.1 — Introduce `IEventMetrics` ✅
- **Status:** Complete.
- **What was done:**
  - `IEventMetrics` interface with properties (`Published`, `Dropped`, `Trades`, etc.) and increment methods (`IncPublished()`, etc.).
  - `DefaultEventMetrics` delegates to static `Metrics` class with `[MethodImpl(AggressiveInlining)]` for zero-allocation hot path.
  - `TracedEventMetrics` wraps `DefaultEventMetrics` for OpenTelemetry export.
- **Key files:**
  - `src/MarketDataCollector.Application/Monitoring/IEventMetrics.cs`

### Step 4.2 — Inject metrics into hot pipeline paths ✅
- **Status:** Complete.
- **What was done:**
  - `EventPipeline` accepts `IEventMetrics` via constructor.
  - `PipelinePublisher` accepts `IEventMetrics` for integrity tracking.
  - `DataQualityMonitoringService` accepts `IEventMetrics` via constructor.
  - DI registration in `ServiceCompositionRoot.AddPipelineServices()` with optional `TracedEventMetrics` wrapper.
- **Key files:**
  - `src/MarketDataCollector.Application/Pipeline/EventPipeline.cs`
  - `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`

---

## Phase 5 — Desktop Service Consolidation (WPF-only) ✅ COMPLETE

> **Note:** The UWP desktop application has been fully removed from the codebase. WPF is the sole desktop client.

### Step 5.1 — Promote shared service interfaces into `Ui.Services` ✅
- **Status:** Complete.
- **What was done:**
  - 16 shared interfaces in `Ui.Services/Contracts/`: `IConfigService`, `IStatusService`, `IThemeService`, `IMessagingService`, `INotificationService`, `ILoggingService`, `ICredentialService`, `IAdminMaintenanceService`, `IArchiveHealthService`, `ISchemaService`, `IBackgroundTaskSchedulerService`, `IOfflineTrackingPersistenceService`, `IPendingOperationsQueueService`, `IWatchlistService`.
  - Shared types: `ConnectionState`, `ConnectionSettings`, `NavigationEntry`, `NavigationEventArgs`.
- **Key files:**
  - `src/MarketDataCollector.Ui.Services/Contracts/*`

### Step 5.2 — Move shared implementations where possible ✅
- **Status:** Complete.
- **What was done:**
  - 5 shared base classes: `ThemeServiceBase`, `NavigationServiceBase`, `ConfigServiceBase` (432 LOC), `StatusServiceBase` (350 LOC), `ConnectionServiceBase` (440 LOC).
  - Template method pattern: base classes define algorithms, WPF overrides platform-specific methods.
  - WPF services delegate to base classes for state machines, polling loops, validation logic.
- **Key files:**
  - `src/MarketDataCollector.Ui.Services/Services/*Base.cs`
  - `src/MarketDataCollector.Wpf/Services/*`

---

## Phase 6 — Validation Pipeline Unification ✅ COMPLETE

### Step 6.1 — Introduce `IConfigValidator` pipeline ✅
- **Status:** Complete.
- **What was done:**
  - `IConfigValidator` interface with `Validate(AppConfig)` returning `IReadOnlyList<ConfigValidationResult>`.
  - `ConfigValidationPipeline` with composable stages: `FieldValidationStage` (FluentValidation rules) + `SemanticValidationStage` (cross-property constraints).
  - `ConfigValidationHelper` deprecated static methods removed (Phase 7.1).
  - FluentValidation validators preserved: `AppConfigValidator`, `AlpacaOptionsValidator`, `StockSharpConfigValidator`, `StorageConfigValidator`, `SymbolConfigValidator`.
- **Key files:**
  - `src/MarketDataCollector.Application/Config/IConfigValidator.cs`
  - `src/MarketDataCollector.Application/Config/ConfigValidationHelper.cs` (validators only)

---

## Phase 7 — Final Cleanup & Hardening ✅ COMPLETE

### Step 7.1 — Remove deprecated code paths and flags ✅
- **Status:** Complete.
- **What was done:**
  - Removed `ConfigValidationHelper` static class (3 obsolete methods: `ValidateAndLog()` × 2, `ValidateOrThrow()`).
  - Preserved all FluentValidation validator classes (`AppConfigValidator`, `AlpacaOptionsValidator`, etc.) as they're used by `ConfigValidationPipeline`.
  - Polygon reconnection logic consolidated to `WebSocketReconnectionHelper`.
- **Key files:**
  - `src/MarketDataCollector.Application/Config/ConfigValidationHelper.cs`

### Step 7.2 — Update architecture docs and ADRs ✅
- **Status:** Complete.
- **What was done:**
  - This file updated with completion status for all phases.
  - Phase completion markers added to each step.
  - Deferred items documented with rationale.

---

## Suggested Execution Order (Strict)

1. ~~Phase 0 (tests + telemetry)~~ ✅
2. ~~Phase 1 (provider registry)~~ ✅
3. ~~Phase 2 (DI composition root)~~ ✅
4. Phase 3 (WebSocket consolidation) — 🔄 Partially complete (Polygon migrated, NYSE/StockSharp deferred)
5. ~~Phase 4 (metrics injection)~~ ✅
6. ~~Phase 6 (validation pipeline)~~ ✅
7. ~~Phase 5 (desktop deduplication)~~ ✅
8. ~~Phase 7 (cleanup)~~ ✅

> Why this order: it minimizes blast radius by first creating verification rails, then consolidating backend composition and provider internals, and only then moving UI-heavy duplication work.

## Rollback Strategy

- Keep feature flags around discovery/registration until at least one release cycle proves parity.
- Migrate one provider at a time with fixture parity tests.
- Preserve old implementations behind adapters during UI service extraction.
- Do not delete legacy path until integration, replay, and smoke tests pass in CI for two consecutive runs.

## Remaining Work

### Phase 3 — WebSocket Lifecycle (Deferred Items)
- **NYSE migration:** Requires interface refactoring (`DataSourceBase` → `IMarketDataClient`) before `WebSocketProviderBase` can be applied. Track as separate work item.
- **StockSharp migration:** Connector-based architecture (wraps third-party `Connector` class) is fundamentally different from raw WebSocket providers. `WebSocketProviderBase` doesn't apply. Consider a separate `ConnectorProviderBase` if patterns emerge.

---

## Related Documentation

- **Architecture and Planning:**
  - [Repository Cleanup Action Plan](./repository-cleanup-action-plan.md) - Prioritized technical debt reduction
  - [Repository Organization Guide](./repository-organization-guide.md) - Code structure conventions
  - [ADR Index](../adr/README.md) - Architectural decision records

- **Implementation Guides:**
  - [Provider Implementation Guide](./provider-implementation.md) - Adding new data providers
  - [Desktop Platform Improvements](./desktop-platform-improvements-implementation-guide.md) - Desktop development
  - [WPF Implementation Notes](./wpf-implementation-notes.md) - WPF architecture

- **Status and Tracking:**
  - [Project Roadmap](../status/ROADMAP.md) - Overall project timeline
  - [CHANGELOG](../status/CHANGELOG.md) - Version history
