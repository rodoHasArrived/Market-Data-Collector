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

## Phase 0 — Baseline & Safety Rails

### Step 0.1 — Lock baseline behavior snapshots
- **Changes:**
  - Add/expand integration snapshots for key API responses (`/api/status`, `/api/health`, `/api/config`, `/api/providers/*`, `/api/backfill/*`).
  - Add golden sample tests for provider message parsing (Polygon, NYSE, StockSharp).
- **Files to touch (exact):**
  - `tests/MarketDataCollector.Tests/Integration/EndpointTests/*` (new)
  - `tests/MarketDataCollector.Tests/Infrastructure/Providers/*` (new/expanded)
- **Dependency safety:** Tests only.
- **Risk:** **2/5**
- **Exit criteria:** Failing test reproduces when deliberately changing JSON schema or parser output.

### Step 0.2 — Add temporary observability counters for migration
- **Changes:**
  - Add migration diagnostics counters (provider factory hit counts, reconnect attempts, resubscribe outcomes).
- **Files to touch:**
  - `src/MarketDataCollector.Application/...` (monitoring abstraction)
  - `src/MarketDataCollector.Infrastructure/...` (provider call points)
- **Dependency safety:** Additive telemetry, no behavior change.
- **Risk:** **2/5**
- **Exit criteria:** Counters visible in logs/metrics under load.

---

## Phase 1 — Unify Provider Construction (No Feature Change)

### Step 1.1 — Introduce `ProviderRegistry` abstraction
- **Changes:**
  - Create single registry abstraction for provider construction by `DataSourceKind`.
  - Keep existing factories as wrappers temporarily.
- **Files to touch:**
  - `src/MarketDataCollector.Infrastructure/Providers/ProviderRegistry.cs` (new)
  - `src/MarketDataCollector.Infrastructure/Providers/MarketDataClientFactory.cs`
  - `src/MarketDataCollector.Infrastructure/Providers/Core/ProviderFactory.cs`
- **Dependency safety:** Old entry points call new registry internally.
- **Risk:** **3/5**
- **Exit criteria:** Existing tests pass; runtime chooses same provider types as before.

### Step 1.2 — Wire attribute-based discovery into registry (behind switch)
- **Changes:**
  - Use `[DataSource]` discovery to populate registry.
  - Add config flag: `ProviderRegistry:UseAttributeDiscovery` default false.
- **Files to touch:**
  - `src/MarketDataCollector.Infrastructure/.../DataSourceAttribute.cs`
  - `src/MarketDataCollector.Infrastructure/.../DataSourceRegistry.cs`
  - `src/MarketDataCollector/config/appsettings*.json` (or config docs)
- **Dependency safety:** Feature flag allows immediate rollback to explicit registrations.
- **Risk:** **4/5**
- **Exit criteria:** A/B run (flag off/on) resolves same provider for each `DataSourceKind`.

### Step 1.3 — Remove direct provider instantiation from host startup
- **Changes:**
  - Replace `new` provider creation in host startup with DI/registry resolution.
- **Files to touch:**
  - `src/MarketDataCollector/Program.cs`
  - `src/MarketDataCollector.Application/ServiceCompositionRoot.cs`
  - `src/MarketDataCollector.Application/HostStartup.cs`
- **Dependency safety:** Startup now consumes same provider instances through DI.
- **Risk:** **3/5**
- **Exit criteria:** End-to-end startup + provider selection parity in logs.

---

## Phase 2 — Single Composition Root (DI Everywhere)

### Step 2.1 — Move pipeline construction entirely to DI
- **Changes:**
  - Register and resolve `JsonlStoragePolicy`, `JsonlStorageSink`, `WriteAheadLog`, `EventPipeline` exclusively through DI.
  - Remove host-level manual assembly.
- **Files to touch:**
  - `src/MarketDataCollector/Program.cs`
  - `src/MarketDataCollector.Application/ServiceCompositionRoot.cs`
- **Dependency safety:** Pure construction-path consolidation.
- **Risk:** **3/5**
- **Exit criteria:** Runtime object graph equivalent (validated by startup diagnostic dump).

### Step 2.2 — Single config load path
- **Changes:**
  - Load `AppConfig` once; inject as singleton/options.
  - Remove duplicate/minimal+full config load pattern.
- **Files to touch:**
  - `src/MarketDataCollector/Program.cs`
  - `src/MarketDataCollector.Application/HostStartup.cs`
- **Dependency safety:** Config semantics preserved; fewer load paths.
- **Risk:** **2/5**
- **Exit criteria:** Effective config hash identical before/after for same input file.

---

## Phase 3 — WebSocket Lifecycle Consolidation

### Step 3.1 — Define migration contract on `WebSocketProviderBase`
- **Changes:**
  - Ensure base supports auth handshake hook, resubscribe hook, provider-specific parsing hook.
  - Add deterministic reconnect policy surface.
- **Files to touch:**
  - `src/MarketDataCollector.Infrastructure/Shared/WebSocketProviderBase.cs`
  - `src/MarketDataCollector.Infrastructure/Resilience/WebSocketConnectionManager.cs`
- **Dependency safety:** Base class hardening before child migration.
- **Risk:** **3/5**
- **Exit criteria:** Existing providers using current manager still pass tests.

### Step 3.2 — Migrate Polygon to base class
- **Changes:**
  - Replace local socket loop with base hooks.
  - Preserve message parsing and symbol subscription semantics.
- **Files to touch:**
  - `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs`
- **Dependency safety:** One provider at a time.
- **Risk:** **4/5**
- **Exit criteria:** Replay fixture parity + reconnect/resubscribe parity under chaos test.

### Step 3.3 — Migrate NYSE to base class
- **Files to touch:**
  - `src/MarketDataCollector.Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs`
- **Risk:** **4/5**
- **Exit criteria:** Same as 3.2 for NYSE.

### Step 3.4 — Migrate StockSharp to base class
- **Files to touch:**
  - `src/MarketDataCollector.Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs`
- **Risk:** **4/5**
- **Exit criteria:** Same as 3.2 for StockSharp.

### Step 3.5 — Remove redundant reconnect implementations
- **Changes:**
  - Delete provider-local reconnect loops no longer used.
- **Files to touch:** the three providers above + shared helper cleanups.
- **Dependency safety:** Only after all migrations pass.
- **Risk:** **3/5**
- **Exit criteria:** No dead reconnect paths left (static analysis + tests).

---

## Phase 4 — Metrics Abstraction (Decouple from Statics)

### Step 4.1 — Introduce `IEventMetrics`
- **Changes:**
  - Add interface + default adapter to existing static metrics backend.
- **Files to touch:**
  - `src/MarketDataCollector.Application/Pipeline/EventPipeline.cs`
  - `src/MarketDataCollector.Application/Monitoring/Metrics.cs`
  - `src/MarketDataCollector.Ui.Shared/.../PrometheusMetrics.cs`
- **Dependency safety:** Adapter preserves existing output.
- **Risk:** **2/5**
- **Exit criteria:** Metric names and cardinality unchanged.

### Step 4.2 — Inject metrics into hot pipeline paths
- **Changes:**
  - Constructor injection into pipeline and consumers.
- **Files to touch:**
  - `src/MarketDataCollector.Application/Pipeline/EventPipeline.cs`
  - DI registration in `ServiceCompositionRoot.cs`
- **Dependency safety:** No-op test implementation enabled.
- **Risk:** **2/5**
- **Exit criteria:** Pipeline tests run without static side effects.

---

## Phase 5 — Desktop Service Consolidation (WPF-only)

> **Note:** The UWP desktop application has been fully removed from the codebase. WPF is the sole desktop client.
> This phase is now simplified to focus on promoting shared service interfaces and reducing duplication
> between WPF-specific services and `Ui.Services`.

### Step 5.1 — Promote shared service interfaces into `Ui.Services`
- **Changes:**
  - Ensure `IThemeService`, `IConfigService`, `INotificationService`, `INavigationService`, etc. are in shared project.
- **Files to touch:**
  - `src/MarketDataCollector.Ui.Services/*`
  - `src/MarketDataCollector.Wpf/Services/*` (interface references)
- **Dependency safety:** Interface-only move first.
- **Risk:** **2/5** (simplified with single desktop platform)
- **Exit criteria:** WPF compiles with moved interfaces.

### Step 5.2 — Move shared implementations where possible
- **Changes:**
  - Extract common logic into shared service implementations in `Ui.Services`.
  - Keep WPF-specific adapters for platform-only APIs (e.g., `Frame` navigation).
- **Files to touch:**
  - `src/MarketDataCollector.Ui.Services/*`
  - `src/MarketDataCollector.Wpf/Services/*`
- **Dependency safety:** Side-by-side old/new behind registration toggles.
- **Risk:** **3/5**
- **Exit criteria:** Behavior parity in UI smoke tests; shared LOC increased.

---

## Phase 6 — Validation Pipeline Unification

### Step 6.1 — Introduce `IConfigValidator` pipeline
- **Changes:**
  - Implement ordered validators: field, semantic, connectivity.
- **Files to touch:**
  - `src/MarketDataCollector.Application/Config/ConfigValidationHelper.cs`
  - `src/MarketDataCollector.Application/Config/ConfigValidatorCli.cs`
  - `src/MarketDataCollector.Application/Services/PreflightChecker.cs`
  - new `src/MarketDataCollector.Application/Config/IConfigValidator.cs`
- **Dependency safety:** Keep CLI output format stable via adapter layer.
- **Risk:** **3/5**
- **Exit criteria:** Same validation outcomes for existing sample configs.

---

## Phase 7 — Final Cleanup & Hardening

### Step 7.1 — Remove deprecated code paths and flags
- **Changes:**
  - Delete wrapper factories and temporary toggles once confidence is high.
- **Files to touch:** migration shims introduced in earlier phases.
- **Dependency safety:** Only after two stable release cycles.
- **Risk:** **3/5**
- **Exit criteria:** No references to legacy factories/parsers/duplicate services.

### Step 7.2 — Update architecture docs and ADRs
- **Changes:**
  - Document new composition root, provider registry, and UI sharing model.
- **Files to touch:**
  - `docs/architecture/*`
  - `docs/adr/*` (new ADRs if needed)
- **Dependency safety:** Docs only.
- **Risk:** **1/5**
- **Exit criteria:** Docs match runtime reality.

---

## Suggested Execution Order (Strict)

1. Phase 0 (tests + telemetry)
2. Phase 1 (provider registry)
3. Phase 2 (DI composition root)
4. Phase 3 (WebSocket consolidation)
5. Phase 4 (metrics injection)
6. Phase 6 (validation pipeline)
7. Phase 5 (desktop deduplication)
8. Phase 7 (cleanup)

> Why this order: it minimizes blast radius by first creating verification rails, then consolidating backend composition and provider internals, and only then moving UI-heavy duplication work.

## Rollback Strategy

- Keep feature flags around discovery/registration until at least one release cycle proves parity.
- Migrate one provider at a time with fixture parity tests.
- Preserve old implementations behind adapters during UI service extraction.
- Do not delete legacy path until integration, replay, and smoke tests pass in CI for two consecutive runs.

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
