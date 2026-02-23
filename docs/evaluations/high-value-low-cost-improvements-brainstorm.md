# High-Value, Low-Cost Improvements Brainstorm

**Date:** 2026-02-23
**Context:** With 94.3% of core improvements complete (33/35 items), this document identifies the next wave of high-ROI, low-effort improvements across reliability, developer experience, operations, and code quality.

**Scoring criteria:**
- **Value**: Direct impact on reliability, correctness, developer productivity, or operational visibility
- **Cost**: Estimated effort in hours/days, not weeks; no major refactors
- **Risk**: Low regression risk; isolated changes preferred

---

## Category 1: Startup & Configuration Hardening

### 1.1 Startup credential validation with actionable errors

**Problem:** The app loads configuration and connects to providers, but if API credentials are missing or malformed the errors surface deep in provider code with cryptic messages (401 Unauthorized, null reference on key parsing, etc.). The `PreflightChecker` exists but doesn't validate that all *enabled* providers have their required credentials set.

**Improvement:** Add a `ValidateProviderCredentials()` step to `PreflightChecker` that iterates enabled providers via `DataSourceRegistry`, checks their `[DataSource]` attribute metadata, and verifies the corresponding environment variables or config sections are populated. Emit a table of missing credentials at startup with the exact env var names to set.

**Value:** High -- eliminates the #1 "why won't it start?" question for new users.
**Cost:** ~4-8 hours. The registry and attribute metadata already exist.
**Files:** `src/MarketDataCollector.Application/Services/PreflightChecker.cs`, `src/MarketDataCollector.ProviderSdk/CredentialValidator.cs`

---

### 1.2 Deprecation warning for legacy `DataSource` string config

**Problem:** The config supports both `"DataSource": "IB"` (legacy single-provider) and `"DataSources": { "Sources": [...] }` (new multi-provider). When both are present, the precedence is undocumented and confusing.

**Improvement:** At config load time, if both `DataSource` and `DataSources` are populated, log a structured warning: `"Both 'DataSource' and 'DataSources' are set. 'DataSources' takes precedence. Remove 'DataSource' to silence this warning."` Add a note to `appsettings.sample.json`.

**Value:** Medium -- prevents silent misconfiguration.
**Cost:** ~1-2 hours. Single conditional in `ConfigurationPipeline`.
**Files:** `src/MarketDataCollector.Application/Config/ConfigurationPipeline.cs`

---

### 1.3 Config validation for provider-specific symbol fields

**Problem:** Symbol configs accept IB-specific fields (`SecurityType`, `Exchange`, `PrimaryExchange`) even when using Alpaca or Polygon. No warning is emitted, and users waste time debugging why their IB fields have no effect on Alpaca.

**Improvement:** During config validation, check each symbol's provider-specific fields against the active provider. Emit info-level warnings for unused provider-specific fields: `"Symbol SPY has IB-specific field 'Exchange' but active provider is Alpaca -- this field will be ignored."`

**Value:** Medium -- reduces misconfiguration confusion.
**Cost:** ~3-4 hours. Build a small lookup of which fields belong to which provider.
**Files:** `src/MarketDataCollector.Application/Config/ConfigValidationHelper.cs`

---

## Category 2: Operational Visibility

### 2.1 Structured startup summary with health matrix

**Problem:** `StartupSummary` logs configuration details at startup, but it reads like a wall of text. Operators need a quick pass/fail matrix to confirm the system is healthy.

**Improvement:** Enhance `StartupSummary` to emit a concise health matrix at INFO level:

```
╔══════════════════════════════════════╗
║  Market Data Collector v1.6.2       ║
║  Mode: Web | Port: 8080            ║
╠══════════════════════════════════════╣
║  Providers:                         ║
║    Alpaca        ✓ Connected        ║
║    Polygon       ✓ Connected        ║
║    IB            ✗ No credentials   ║
║  Storage:                           ║
║    JSONL sink    ✓ Ready            ║
║    Parquet sink  ✓ Ready            ║
║    WAL           ✓ 0 pending        ║
║  Symbols:        5 active           ║
║  Backfill:       Disabled           ║
╚══════════════════════════════════════╝
```

**Value:** High -- immediate operational confidence at startup; easy to screenshot for support.
**Cost:** ~4-6 hours. The data is already available from existing services.
**Files:** `src/MarketDataCollector.Application/Services/StartupSummary.cs`

---

### 2.2 Add `/api/config/effective` endpoint

**Problem:** With environment variable overrides, config file values, defaults, and presets all layering together, operators can't easily see what configuration is *actually* in effect. The existing `/api/config` endpoint shows the raw config, not the resolved values.

**Improvement:** Add a `/api/config/effective` endpoint that returns the fully-resolved configuration with source annotations:

```json
{
  "dataSource": { "value": "Alpaca", "source": "appsettings.json" },
  "alpaca.keyId": { "value": "PK***4X", "source": "env:ALPACA__KEYID" },
  "storage.namingConvention": { "value": "BySymbol", "source": "default" }
}
```

Credentials should be masked (already have `SensitiveValueMasker`).

**Value:** High -- eliminates "which setting is winning?" debugging.
**Cost:** ~6-8 hours. Build a config source tracker in `ConfigurationPipeline`.
**Files:** `src/MarketDataCollector.Application/Config/ConfigurationPipeline.cs`, new endpoint in `src/MarketDataCollector.Ui.Shared/Endpoints/ConfigEndpoints.cs`

---

### 2.3 WAL recovery metrics at startup

**Problem:** The Write-Ahead Log (`WriteAheadLog`) recovers pending events on startup, but the recovery count and duration aren't surfaced as metrics or in the startup summary.

**Improvement:** After WAL recovery in `WriteAheadLog.RecoverAsync()`, emit:
- A Prometheus counter `wal_recovery_events_total` with the count of recovered events
- A gauge `wal_recovery_duration_seconds` with the recovery duration
- A structured log: `"WAL recovery complete: {RecoveredCount} events in {Duration}ms"`

**Value:** Medium -- critical for understanding restart behavior and data loss risk.
**Cost:** ~2-3 hours. The recovery logic already exists; just add instrumentation.
**Files:** `src/MarketDataCollector.Storage/Archival/WriteAheadLog.cs`, `src/MarketDataCollector.Application/Monitoring/PrometheusMetrics.cs`

---

### 2.4 Provider reconnection event log with backoff visibility

**Problem:** When WebSocket connections drop, providers reconnect with exponential backoff. But the retry attempt number and next retry delay aren't logged, making it hard to tell whether the system is recovering or stuck in a retry loop.

**Improvement:** In each provider's reconnection logic (and `WebSocketReconnectionHelper`), ensure structured logs include `{Attempt}`, `{MaxAttempts}`, and `{NextRetryMs}`:
```
"WebSocket reconnection attempt {Attempt}/{MaxAttempts} for {Provider}, next retry in {NextRetryMs}ms"
```

Also emit a Prometheus counter `provider_reconnection_attempts_total{provider, outcome}` partitioned by success/failure.

**Value:** Medium -- makes reconnection debugging self-service.
**Cost:** ~3-4 hours. Standardize the log format across providers.
**Files:** `src/MarketDataCollector.Infrastructure/Shared/WebSocketReconnectionHelper.cs`, individual provider files

---

## Category 3: Developer Experience

### 3.1 Environment variable reference document

**Problem:** The project uses 30+ environment variables for credentials, configuration overrides, and feature flags. These are scattered across `appsettings.sample.json` comments, `ConfigEnvironmentOverride.cs`, and individual provider code. No canonical list exists.

**Improvement:** Generate (or manually create) a `docs/reference/environment-variables.md` that lists every supported env var with:
- Variable name
- Description
- Required/optional
- Which provider it belongs to
- Example value
- Corresponding config path

**Value:** High -- the most-asked question for any 12-factor app.
**Cost:** ~3-4 hours. Most of the information exists in code; it just needs consolidation.
**Files:** New `docs/reference/environment-variables.md`

---

### 3.2 `--check-config` CLI flag for offline config validation

**Problem:** The `--dry-run` flag performs full validation including connectivity checks. There's no way to validate just the config file syntax and required fields without network access (useful in CI or air-gapped environments).

**Improvement:** Add a `--check-config` flag (or enhance `--dry-run --offline`) that:
1. Parses the config file
2. Validates required fields are present
3. Checks credential env vars are set (not empty)
4. Validates symbol configs against provider requirements
5. Exits with 0 (valid) or non-zero (invalid) + structured error list

The `--dry-run --offline` combination already exists but may not cover all these checks.

**Value:** Medium -- enables CI/CD config validation without live providers.
**Cost:** ~4-6 hours. Most validation logic exists; wire it into a clean CLI path.
**Files:** `src/MarketDataCollector.Application/Commands/DryRunCommand.cs`, `src/MarketDataCollector.Application/Services/DryRunService.cs`

---

### 3.3 JSON Schema generation for `appsettings.json`

**Problem:** `appsettings.sample.json` is 730 lines with no IDE autocomplete or validation. Developers must read comments to understand valid values. VS Code and JetBrains IDEs support JSON Schema for autocomplete.

**Improvement:** Generate a JSON Schema file from the C# configuration classes (`AppConfig`, `BackfillConfig`, `StorageOptions`, etc.) using a build-time tool or source generator. Reference it in the config file:

```json
{
  "$schema": "./config/appsettings.schema.json",
  ...
}
```

**Value:** High -- immediate IDE autocomplete and inline validation for all configuration.
**Cost:** ~6-8 hours. Use `JsonSchemaExporter` (.NET 9) or a Roslyn-based generator.
**Files:** New schema generator tool, `config/appsettings.schema.json`

---

### 3.4 `make quickstart` target for zero-to-running

**Problem:** New contributors must read CLAUDE.md, install the SDK, copy config, set env vars, and run the build. A `make quickstart` target could automate the happy path.

**Improvement:** Add a Makefile target that:
1. Checks .NET 9 SDK is installed
2. Copies `appsettings.sample.json` to `appsettings.json` if not present
3. Runs `dotnet restore`
4. Runs `dotnet build`
5. Runs `dotnet test` (fast subset)
6. Prints next steps (set env vars, run with `--wizard`)

**Value:** Medium -- reduces onboarding friction from ~15 minutes to ~2 minutes.
**Cost:** ~2-3 hours. Shell script wrapped in Makefile target.
**Files:** `Makefile`

---

## Category 4: Data Integrity & Quality

### 4.1 Automatic gap backfill on reconnection

**Problem:** When a streaming provider disconnects and reconnects, there's a data gap for the disconnection period. The system logs an `IntegrityEvent` but doesn't automatically request backfill for the missing window.

**Improvement:** After a successful reconnection, automatically enqueue a targeted backfill request for each subscribed symbol covering `[disconnect_time, reconnect_time]`. Use the existing `BackfillCoordinator` and `HistoricalBackfillService`. Gate this behind a config flag `AutoGapFill: true`.

**Value:** High -- directly improves data completeness, which is the project's core value proposition.
**Cost:** ~6-8 hours. The backfill infrastructure exists; wire it to the reconnection event.
**Files:** `src/MarketDataCollector.Infrastructure/Shared/WebSocketReconnectionHelper.cs`, `src/MarketDataCollector.Application/Backfill/HistoricalBackfillService.cs`

---

### 4.2 Cross-provider quote divergence alerting

**Problem:** The design review memo flags "feed divergence across providers" as a known risk. When multiple providers are active, their quotes for the same symbol can diverge. The `CrossProviderComparisonService` exists but doesn't emit real-time alerts.

**Improvement:** Add a lightweight comparison in the event pipeline that, when 2+ providers are streaming the same symbol, checks if mid-prices diverge by more than a configurable threshold (e.g., 0.5%). Emit a structured warning and increment `provider_quote_divergence_total{symbol}`.

**Value:** Medium -- early warning for stale feeds or provider issues.
**Cost:** ~4-6 hours. The comparison service has the logic; add a real-time check.
**Files:** `src/MarketDataCollector.Application/Monitoring/DataQuality/CrossProviderComparisonService.cs`

---

### 4.3 Storage checksum verification on read

**Problem:** `StorageChecksumService` computes checksums on write. But there's no verification on read to detect bit rot or corruption in stored files. The `DataValidator` tool exists but must be run manually.

**Improvement:** Add an optional `VerifyOnRead: true` config flag to `StorageOptions`. When enabled, `JsonlReplayer` and `MemoryMappedJsonlReader` verify the file checksum before returning data. Log a warning (not error) on mismatch, and increment `storage_checksum_mismatch_total{path}`.

**Value:** Medium -- catches silent data corruption before it reaches downstream consumers.
**Cost:** ~4-6 hours. Checksum computation exists; add verification in read paths.
**Files:** `src/MarketDataCollector.Storage/Replay/JsonlReplayer.cs`, `src/MarketDataCollector.Storage/Services/StorageChecksumService.cs`

---

## Category 5: Testing & CI Improvements

### 5.1 Flaky test detection in CI

**Problem:** With 3,444 tests, occasional flaky tests (timing-dependent, file-system-dependent) can cause spurious CI failures. There's no mechanism to detect or quarantine flaky tests.

**Improvement:** Add a `--retry-failed` step to the test matrix workflow: if any tests fail, re-run only the failed tests once. If they pass on retry, mark them as flaky and emit a GitHub Actions annotation. Track flaky tests in a `tests/flaky-tests.md` file.

**Value:** Medium -- reduces CI noise and developer frustration.
**Cost:** ~3-4 hours. Use `dotnet test --filter` with the failed test names.
**Files:** `.github/workflows/test-matrix.yml`

---

### 5.2 Test execution time tracking

**Problem:** As the test suite grows (3,444 tests), slow tests can silently degrade CI times. There's no visibility into which tests are slow.

**Improvement:** Add `--logger "trx"` to test runs and post-process the TRX file to extract the top 20 slowest tests. Emit them as a GitHub Actions job summary. Optionally set a threshold (e.g., 5 seconds per test) that warns on PR checks.

**Value:** Medium -- prevents death-by-a-thousand-cuts CI slowdown.
**Cost:** ~3-4 hours. TRX parsing is well-documented; integrate into existing workflow.
**Files:** `.github/workflows/test-matrix.yml`, optional post-processing script

---

### 5.3 Benchmark regression detection

**Problem:** The `benchmarks/` project runs BenchmarkDotNet but results aren't compared across runs. A performance regression could ship without detection.

**Improvement:** In the benchmark workflow, export results as JSON (`--exporters json`), store as a workflow artifact, and compare against the previous run's artifact. Flag regressions >10% as warnings, >25% as failures. Use BenchmarkDotNet's built-in `--statisticalTest` flag for significance testing.

**Value:** Medium -- catches performance regressions before they reach production.
**Cost:** ~4-6 hours. BenchmarkDotNet has comparison support; wire to CI.
**Files:** `.github/workflows/benchmark.yml`, `benchmarks/MarketDataCollector.Benchmarks/`

---

### 5.4 Integration test for graceful shutdown data integrity

**Problem:** `GracefulShutdownService` coordinates flushing WAL, closing sinks, and disconnecting providers. But there's no integration test that verifies zero data loss during a shutdown sequence with in-flight events.

**Improvement:** Write an integration test that:
1. Starts the event pipeline with a mock provider producing events
2. Triggers graceful shutdown via `CancellationToken`
3. Verifies all in-flight events were persisted (WAL + sink)
4. Verifies no duplicate events after recovery

**Value:** High -- validates the most critical operational scenario.
**Cost:** ~6-8 hours. Uses existing `InMemoryStorageSink` test infrastructure.
**Files:** `tests/MarketDataCollector.Tests/Integration/`

---

## Category 6: Code Quality Quick Wins

### 6.1 Replace bare catch blocks with typed exceptions

**Problem:** The `FURTHER_SIMPLIFICATION_OPPORTUNITIES.md` audit identified bare `catch` blocks that swallow exceptions silently. These hide bugs in production.

**Improvement:** Find and replace all bare `catch` and `catch (Exception)` blocks that don't re-throw or log. At minimum, add `_logger.LogWarning(ex, "...")` to each. In hot paths, consider `catch (SpecificException)` instead.

**Value:** High -- prevents silent failures in production.
**Cost:** ~2-4 hours. Grep for `catch\s*\{` and `catch\s*\(Exception`.
**Files:** Various across `src/`

---

### 6.2 Add `TimeProvider` abstraction for testability

**Problem:** Code that uses `DateTime.UtcNow` or `DateTimeOffset.UtcNow` directly is hard to test deterministically. .NET 8+ introduced `TimeProvider` as a built-in abstraction.

**Improvement:** Inject `TimeProvider` (or `TimeProvider.System` as default) into time-sensitive services:
- `TradingCalendar` (market hours checks)
- `DataFreshnessSlaMonitor` (SLA window calculations)
- `BackfillScheduleManager` (next-run calculations)
- `LifecyclePolicyEngine` (retention checks)

This enables deterministic time-based tests without `Thread.Sleep` or flaky timing.

**Value:** Medium -- improves test reliability and enables edge-case time testing.
**Cost:** ~4-6 hours. Add `TimeProvider` parameter to constructors with default.
**Files:** Services listed above

---

### 6.3 Consolidate `Lazy<T>` initialization pattern

**Problem:** The audit identified 43 services using manual double-checked locking for lazy initialization. .NET's `Lazy<T>` is thread-safe by default and eliminates this boilerplate.

**Improvement:** Replace manual `lock` + null-check patterns with `Lazy<T>` or `AsyncLazy<T>`. Prioritize the most-used services first (storage sinks, provider factories).

**Value:** Low-Medium -- reduces boilerplate, eliminates potential lock ordering bugs.
**Cost:** ~4-8 hours for the top 10-15 most impactful services.
**Files:** Various across `src/`

---

### 6.4 Endpoint handler helper to reduce try/catch boilerplate

**Problem:** The 35 endpoint files each repeat the same try/catch + JSON response pattern. The `EndpointHelpers` class exists but isn't used everywhere.

**Improvement:** Ensure all endpoint handlers use `EndpointHelpers.HandleAsync()` (or a similar wrapper) that provides:
- Consistent error response format (`ErrorResponse`)
- Automatic `CancellationToken` propagation
- Request logging with correlation ID
- Exception-to-status-code mapping

**Value:** Medium -- consistent API error responses; less boilerplate.
**Cost:** ~6-8 hours for full migration; can be done incrementally.
**Files:** `src/MarketDataCollector.Ui.Shared/Endpoints/*.cs`

---

## Category 7: Security Hardening

### 7.1 Enforce credential-via-environment at validation time

**Problem:** The design review notes that credentials in `appsettings.json` are a security risk. Environment variable support exists but isn't enforced. A developer could accidentally commit credentials.

**Improvement:** Add a validation check: if any credential field in the config file contains a non-empty, non-placeholder value (not `"your-key-here"`), emit a warning:
```
"WARNING: Credential '{FieldName}' appears to be set directly in config file.
 Use environment variable {EnvVarName} instead to avoid accidental commits."
```

Optionally, add a `--strict-credentials` flag that makes this a hard error.

**Value:** High -- prevents the #1 security anti-pattern.
**Cost:** ~3-4 hours. Add check in `ConfigValidationHelper`.
**Files:** `src/MarketDataCollector.Application/Config/ConfigValidationHelper.cs`

---

### 7.2 API key rotation support

**Problem:** The `ApiKeyMiddleware` supports static API keys for the dashboard. If a key is compromised, the only recourse is to restart the service with a new key.

**Improvement:** Support multiple API keys (comma-separated in env var) and add a `POST /api/admin/rotate-key` endpoint that:
1. Accepts a new key
2. Adds it to the active key set
3. Optionally revokes old keys after a grace period
4. Logs the rotation event

**Value:** Medium -- operational security improvement.
**Cost:** ~4-6 hours.
**Files:** `src/MarketDataCollector.Ui.Shared/Endpoints/ApiKeyMiddleware.cs`, `src/MarketDataCollector.Ui.Shared/Endpoints/AdminEndpoints.cs`

---

## Category 8: Performance Quick Wins

### 8.1 Connection warmup with parallel provider initialization

**Problem:** When using failover with 3+ providers, each provider connects sequentially. Connecting all providers in parallel could reduce startup time by the sum of connection latencies minus the maximum.

**Improvement:** In the startup path where `providerMap` is built, connect all enabled providers in parallel using `Task.WhenAll`. The `FailoverAwareMarketDataClient` already handles the case where some providers fail to connect.

**Value:** Medium -- reduces startup time proportional to provider count.
**Cost:** ~2-3 hours. Change sequential loop to parallel.
**Files:** `src/MarketDataCollector/Program.cs` (provider initialization section)

---

### 8.2 Conditional Parquet sink activation

**Problem:** The `CompositeSink` always writes to all registered sinks. If Parquet export isn't needed for real-time collection, the Parquet serialization overhead is wasted.

**Improvement:** Make Parquet sink activation conditional on config (`Storage.EnableParquet: true/false`). Default to disabled for real-time-only deployments. The `CompositeSink` already supports dynamic sink registration.

**Value:** Medium -- reduces CPU and I/O overhead for real-time deployments.
**Cost:** ~2-3 hours. Add config flag, conditional registration in DI.
**Files:** `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`, `config/appsettings.sample.json`

---

### 8.3 Reduce config file double-read at startup

**Problem:** Program.cs reads the config file twice: once for `LoadConfigMinimal` (to get `DataRoot` for logging) and once for the full `LoadAndPrepareConfig`. This is redundant I/O.

**Improvement:** Read the file once into a `JsonDocument`, extract `DataRoot` for early logging setup, then pass the same document to the full config pipeline. Alternatively, make `DataRoot` default to a well-known path and only require the full config load.

**Value:** Low -- saves ~10-50ms of startup I/O.
**Cost:** ~2-3 hours.
**Files:** `src/MarketDataCollector/Program.cs`

---

## Priority Matrix

| ID | Improvement | Value | Cost | Priority |
|----|------------|-------|------|----------|
| 4.1 | Auto gap backfill on reconnection | High | 6-8h | **P1** |
| 2.1 | Startup health matrix | High | 4-6h | **P1** |
| 1.1 | Credential validation at startup | High | 4-8h | **P1** |
| 7.1 | Enforce credentials via env vars | High | 3-4h | **P1** |
| 6.1 | Replace bare catch blocks | High | 2-4h | **P1** |
| 3.1 | Environment variable reference doc | High | 3-4h | **P1** |
| 5.4 | Graceful shutdown integration test | High | 6-8h | **P1** |
| 3.3 | JSON Schema for config | High | 6-8h | **P2** |
| 2.2 | `/api/config/effective` endpoint | High | 6-8h | **P2** |
| 1.2 | Legacy config deprecation warning | Medium | 1-2h | **P2** |
| 1.3 | Provider-specific field validation | Medium | 3-4h | **P2** |
| 2.3 | WAL recovery metrics | Medium | 2-3h | **P2** |
| 2.4 | Reconnection log standardization | Medium | 3-4h | **P2** |
| 4.2 | Cross-provider divergence alerting | Medium | 4-6h | **P2** |
| 4.3 | Checksum verification on read | Medium | 4-6h | **P2** |
| 5.1 | Flaky test detection | Medium | 3-4h | **P2** |
| 5.2 | Test execution time tracking | Medium | 3-4h | **P2** |
| 5.3 | Benchmark regression detection | Medium | 4-6h | **P2** |
| 3.2 | Offline config validation CLI | Medium | 4-6h | **P2** |
| 3.4 | `make quickstart` target | Medium | 2-3h | **P2** |
| 6.2 | `TimeProvider` abstraction | Medium | 4-6h | **P3** |
| 6.4 | Endpoint handler consolidation | Medium | 6-8h | **P3** |
| 7.2 | API key rotation | Medium | 4-6h | **P3** |
| 8.1 | Parallel provider initialization | Medium | 2-3h | **P3** |
| 8.2 | Conditional Parquet sink | Medium | 2-3h | **P3** |
| 6.3 | `Lazy<T>` consolidation | Low-Med | 4-8h | **P3** |
| 8.3 | Config double-read elimination | Low | 2-3h | **P4** |

---

## Implementation Notes

- **P1 items** are independent of each other and can be implemented in any order or in parallel
- Most improvements are additive (new code paths gated by config) rather than modifying hot paths
- All improvements should include corresponding test coverage
- Items in Categories 1-2 (startup/ops) deliver the most immediate user-facing value
- Items in Category 5 (CI) compound in value over time as the test suite grows
- Category 6 (code quality) items can be done opportunistically alongside other work
