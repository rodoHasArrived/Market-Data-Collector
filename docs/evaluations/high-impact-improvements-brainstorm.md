# High-Impact Repository Improvements Brainstorm

> **Date:** 2026-03-03
> **Scope:** Code generalization and output program quality — effort is not a factor.

---

## Executive Summary

After a deep analysis of the entire codebase — domain modeling, provider infrastructure, event pipeline, storage, testing, CI/CD, and F# integration — the following are the highest-impact improvements ranked by how much they would improve the **generality**, **correctness**, **robustness**, and **output quality** of the system.

---

## 1. Unified Event Schema Evolution & Versioning

**Current state:** `MarketEvent` carries a `SchemaVersion = 1` field, but there is no actual schema evolution mechanism. The `ISchemaUpcaster` interface exists in Contracts but has no implementations. If the event shape changes, all stored JSONL becomes silently incompatible.

**Improvement:** Implement a real schema evolution pipeline:
- Register upcasters per (fromVersion → toVersion) pair.
- On deserialization (replay, WAL recovery, package import), automatically apply the upcaster chain.
- Add a `--migrate-storage` CLI command that rewrites files to the latest schema version.
- Store the schema version in JSONL file headers (first line metadata) and Parquet file metadata.
- Add a CI check that fails if a MarketEventPayload-derived type changes shape without incrementing SchemaVersion and adding an upcaster.

**Why it matters:** Without this, any domain model evolution silently breaks downstream consumers, replay, backfill comparison, and portable data packages. This is the single most dangerous gap for a system whose purpose is long-term data archival.

---

## 2. Replace Stringly-Typed Provider/Symbol/Venue Identifiers with Strong Types

**Current state:** Provider names, symbols, venues, and stream IDs are all `string`. The codebase normalizes these in scattered ad-hoc ways (`ToLowerInvariant()`, `ToUpperInvariant()`, trim). `ProviderId`, `SymbolId`, and `VenueCode` strong types exist in `Contracts/Domain/` but are not used consistently — most code still passes raw strings.

**Improvement:** Enforce strong types throughout the hot path:
- Make `ProviderId`, `SymbolId`, and `VenueCode` the **only** way to reference these concepts.
- These types should enforce normalization at construction (e.g., `SymbolId` always uppercases, `ProviderId` always lowercases).
- Eliminate all `string symbol` parameters in favor of `SymbolId symbol` in provider interfaces, pipeline, storage, and monitoring.
- Add implicit conversions from string for ergonomics but validate on construction.
- Use these types as dictionary keys to eliminate case-sensitivity bugs.

**Why it matters:** String-typed identifiers are the #1 source of subtle bugs in market data systems. A symbol that is "AAPL" in the provider, "aapl" in storage, and "Aapl" in the dashboard creates three separate data streams that look identical to the user. The existing `EffectiveSymbol` property is a band-aid. Strong types make this class of bug structurally impossible.

---

## 3. Make the F# Validation Pipeline a First-Class Citizen in the Event Flow

**Current state:** The F# validation pipeline (`Transforms.fs`, `ValidationPipeline.fs`) is sophisticated — railway-oriented, applicative, composable — but it sits *beside* the C# pipeline rather than *inside* it. Events flow from collectors → EventPipeline → storage without passing through F# validation. The F# layer is used for enrichment and analysis but not for gating.

**Improvement:** Integrate F# validation as an optional, configurable stage in the EventPipeline:
- Add a `IEventValidator` interface that the pipeline calls between receive and persist.
- The default implementation delegates to the F# `ValidationPipeline`.
- Events that fail validation are routed to a dead-letter sink (separate JSONL file) with the full `ValidationError` list, rather than being silently dropped or persisted with bad data.
- Expose validation metrics (pass rate, error distribution by type) via Prometheus.
- Allow per-symbol validation config (e.g., relaxed thresholds for illiquid symbols).

**Why it matters:** The system currently persists all events regardless of validity. A bad tick from a provider (negative price, zero quantity, future timestamp) gets stored and pollutes downstream analysis. The F# validation code is already written and excellent — it just isn't wired into the hot path.

---

## 4. Implement Proper WAL Corruption Recovery

**Current state:** The WAL (`WriteAheadLog`) has append, commit, truncate, and recovery. But there is no handling for partial writes or corrupted records. If the process crashes mid-write, the WAL file may contain a truncated JSON line. The recovery code (`GetUncommittedRecordsAsync`) will throw a deserialization exception and skip the record, logging a warning — but it doesn't know whether the *next* record is also corrupt (shifted bytes).

**Improvement:**
- Add length-prefixed framing to WAL records: `[4-byte length][payload][checksum]`.
- On recovery, if a record fails checksum validation, scan forward to the next valid frame boundary.
- Track and report the number of corrupted vs. recovered records.
- Add a `--wal-repair` CLI command that dumps corrupted records for manual inspection.
- Add a fuzz test that writes partial records and verifies recovery correctness.

**Why it matters:** A WAL that silently loses data on crash is worse than no WAL at all, because it gives a false sense of durability. The current checksum field is populated but never validated during recovery.

---

## 5. Introduce a Query/Replay Abstraction Over Stored Data

**Current state:** Querying stored data is fragmented across `HistoricalDataQueryService`, `MemoryMappedJsonlReader`, `JsonlReplayer`, `StorageSearchService`, and `StorageCatalogService`. Each has different capabilities, different APIs, and different assumptions about file layout. There is no unified way to say "give me all trades for AAPL between 10:00 and 10:05 on 2026-01-15."

**Improvement:** Create a unified `IMarketDataStore` read abstraction:
```csharp
public interface IMarketDataStore
{
    IAsyncEnumerable<MarketEvent> QueryAsync(
        MarketDataQuery query,
        CancellationToken ct = default);
}

public sealed record MarketDataQuery(
    SymbolId? Symbol = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    MarketEventType? EventType = null,
    string? Source = null,
    int? Limit = null);
```
- Implement it for JSONL, Parquet, and composite (tiered) storage.
- Use this single interface for replay, export, the HTTP query endpoints, quality monitoring, and the desktop data browser.
- Add predicate pushdown for Parquet (column pruning, row group filtering).
- This unifies the 5+ current query paths into one tested, optimized implementation.

**Why it matters:** The current fragmentation means each consumer reimplements filtering, date parsing, and file discovery. Bugs fixed in one path don't propagate. The export service, the replay endpoint, the data browser, and the quality monitor all independently enumerate files — and may produce different results for the same query.

---

## 6. End-to-End Contract Testing for the Provider → Storage Round-Trip

**Current state:** Provider tests mock the publisher. Pipeline tests mock the sink. Storage tests mock the events. There is no test that verifies: "If Alpaca sends this WebSocket message, the correct MarketEvent ends up in JSONL with the right fields." The closest tests are `AlpacaQuoteRoutingTests`, which test collector → publisher but skip deserialization and storage.

**Improvement:** Add contract tests that exercise the full pipeline per provider:
- Given: a recorded WebSocket message (or HTTP response) from each provider.
- When: processed through the actual adapter → collector → pipeline → storage sink.
- Then: the stored JSONL line deserializes to a `MarketEvent` with exactly the expected fields.
- Store the recorded messages as test fixtures (golden files).
- Run these tests in CI on every PR.
- This catches: deserialization regressions, field mapping errors, sequence number drift, timestamp timezone bugs, and symbol normalization inconsistencies.

**Why it matters:** The most common class of bugs in market data systems is "the field was there but had the wrong value." Unit tests with mocks can't catch these because they mock away the transformation layer. A golden-file contract test catches the exact bugs that matter most.

---

## 7. Make the Composite Sink Failure Model Explicit and Configurable

**Current state:** `CompositeSink` writes to multiple sinks (JSONL + Parquet). If one sink throws, it logs a warning and continues. There is no tracking of per-sink failure state, no circuit breaker, and no way for callers to know that data is being written to JSONL but not Parquet.

**Improvement:**
- Add a per-sink health state (Healthy, Degraded, Failed) with automatic circuit breaking.
- Expose sink health as a Prometheus metric and health endpoint.
- Add a configurable failure policy: `ContinueOnPartialFailure` (current default), `FailOnAnyFailure`, `RequireQuorum(n)`.
- When a sink recovers from failure, backfill the missed events from the healthy sink(s) if the data window is within the hot tier.
- Log a structured event (`SinkDegradation`) that the monitoring pipeline can alert on.

**Why it matters:** Silent partial failure is the worst failure mode for a data collection system. Users believe they have Parquet files but they're missing hours of data because Parquet conversion hit an edge case. The current behavior optimizes for availability (keep writing to at least one sink) but sacrifices visibility.

---

## 8. Formalize the Provider Capability Model for Smarter Orchestration

**Current state:** `ProviderCapabilities` is a flat record with boolean flags (`SupportsTrades`, `SupportsQuotes`, `SupportsDepth`). The `CompositeHistoricalDataProvider` uses provider priority (an integer) for fallback ordering. There is no structured way to express: "Polygon supports 1-minute bars for US equities but not for crypto" or "Alpaca's free tier is delayed 15 minutes."

**Improvement:** Replace the flat capability model with a structured one:
```csharp
public sealed record ProviderCapabilities(
    IReadOnlySet<AssetClass> SupportedAssetClasses,
    IReadOnlySet<MarketEventType> SupportedEventTypes,
    IReadOnlySet<string> SupportedBarIntervals,
    IReadOnlySet<string> SupportedExchanges,
    DataFreshness Freshness,         // Realtime, Delayed15Min, EndOfDay
    DateOnly? EarliestHistoricalDate,
    RateLimitProfile RateLimit,
    bool RequiresPaidSubscription);
```
- Use this to auto-select the best provider for a given request (symbol + date range + data type).
- Replace the linear priority integer with constraint-based routing.
- Enable the backfill system to automatically split requests across providers: "Use Alpaca for 2024 intraday, Stooq for 2020 daily."
- Surface capability gaps in the UI: "No provider supports L2 data for this exchange."

**Why it matters:** The current priority-based fallback is simple but wasteful. It tries providers in order until one succeeds, even if the first 3 providers are known not to support the requested asset class. Structured capabilities turn O(n) fallback into O(1) routing.

---

## 9. Enforce Structural Consistency via Roslyn Analyzers

**Current state:** The CLAUDE.md documents many rules (sealed classes, CancellationToken, no Task.Run for I/O, structured logging, [ImplementsAdr] attributes). These rules are enforced by convention and AI agent review, not by the compiler. The `ai-repo-updater.py` script detects violations after the fact.

**Improvement:** Write custom Roslyn analyzers for the project's critical invariants:
- **MDC001:** All public classes must be sealed unless they have `[AllowInheritance]`.
- **MDC002:** All async methods must accept `CancellationToken`.
- **MDC003:** Logger calls must not use string interpolation (detect `$"` inside `Log*` calls).
- **MDC004:** Classes implementing `IMarketDataClient` or `IHistoricalDataProvider` must have `[ImplementsAdr]` and `[DataSource]` attributes.
- **MDC005:** `new HttpClient()` is forbidden — use `IHttpClientFactory`.
- **MDC006:** `Task.Run` calls must have a justifying comment.
- **MDC007:** `PackageReference` items must not have `Version` attributes (CPM enforcement).
- Ship these as a local analyzer project referenced by `Directory.Build.props`.
- Set them to `Error` severity so they fail the build.

**Why it matters:** Convention-based rules decay over time. Every new contributor (human or AI) must rediscover them. Analyzer-based rules are permanent, produce IDE squiggles during development, and fail CI before code review. This converts ~687 bare `catch (Exception)` blocks and ~8 `Task.Run` I/O violations from "known technical debt" to "compilation errors."

---

## 10. Build a Deterministic Data Replay System

**Current state:** `JsonlReplayer` can replay stored JSONL files, but it does not replay at the original event rate. There is no way to simulate "what would have happened if I ran my strategy against the 2025-Q3 data at production speed." The replay is batch (as fast as possible), not temporal.

**Improvement:** Build a deterministic replay engine:
- Read stored events and emit them to a `Channel<MarketEvent>` at the original inter-event timing.
- Support speed multipliers (1x real-time, 10x, 100x, max-speed).
- Allow injecting faults (dropped events, delayed events, provider failover) to test resilience.
- Wire this into the existing EventPipeline so that downstream consumers (monitoring, indicators, storage) behave identically to live operation.
- Add a `--replay` CLI mode: `--replay --replay-source ./data/live/alpaca/2025-07-15/ --replay-speed 10x`.
- Enable the QuantConnect Lean integration to consume replay data as if it were live.

**Why it matters:** Market data collection systems are notoriously hard to test in production-like conditions because markets are only open during trading hours. A deterministic replay system lets developers validate changes against real historical data at any time of day, with exactly reproducible results.

---

## 11. Eliminate the Endpoint Stub Problem

**Current state:** The API declares 287 route constants. Approximately 15-20 endpoints return hardcoded stub data (e.g., `MessagingEndpoints.cs` — all 7 endpoints return `queued=0, running=false`). `ReplayEndpoints.cs` returns empty arrays. These stubs are indistinguishable from working endpoints to API consumers.

**Improvement:**
- **Remove or gate stub endpoints.** Endpoints that don't work should not be discoverable. Options:
  - Return `501 Not Implemented` with a structured body: `{ "status": "not_implemented", "planned_version": "1.8.0" }`.
  - Gate them behind a `--enable-experimental-endpoints` flag.
  - Remove them from the route table entirely until implemented.
- **Add an endpoint health matrix** to the `/api/status` response that reports per-endpoint implementation status.
- **Add integration tests** that verify every registered endpoint returns something other than 501 — this prevents regressions where a working endpoint accidentally becomes a stub.

**Why it matters:** Stub endpoints that return `200 OK` with empty data are API contract violations. Downstream consumers (dashboards, scripts, other services) will silently behave incorrectly. A 501 is honest; a 200 with fake data is a lie.

---

## 12. Unify and Harden the Reconnection Model

**Current state:** WebSocket reconnection is handled differently by different providers:
- `AlpacaMarketDataClient` uses `WebSocketConnectionManager` (centralized).
- `PolygonMarketDataClient` uses both `WebSocketConnectionManager` and `WebSocketReconnectionHelper`.
- `StockSharpMarketDataClient` has its own reconnection logic.
- `IBMarketDataClient` delegates to `EnhancedIBConnectionManager` with a different retry model.

**Improvement:**
- All providers should use `WebSocketConnectionManager` (or a higher-level `IConnectionManager` interface).
- Extract the reconnection strategy into a pluggable policy:
  - `ExponentialBackoffReconnectionPolicy` (current default).
  - `MarketHoursAwareReconnectionPolicy` (don't reconnect outside trading hours).
  - `CircuitBreakerReconnectionPolicy` (stop after N consecutive failures).
- Add reconnection event telemetry: time-to-reconnect, subscription re-registration duration, events missed during downtime.
- Implement **subscription state journaling**: on reconnect, automatically re-subscribe to the exact set of symbols that were active, without the provider adapter needing to track this.

**Why it matters:** Reconnection bugs are the #1 operational issue in streaming systems. If each provider handles reconnection differently, each provider has different reconnection bugs. A unified model means one implementation to get right and one set of tests to validate.

---

## 13. Add Property-Based / Fuzz Testing for the Data Path

**Current state:** Tests use hand-crafted fixtures with specific values. This validates known scenarios but misses edge cases that humans don't think of (e.g., `decimal.MaxValue` prices, symbols with unicode characters, timestamps at `DateTimeOffset.MinValue`).

**Improvement:**
- Add [FsCheck](https://github.com/fscheck/FsCheck) or Hedgehog for property-based testing of the core data path:
  - **MarketEvent round-trip:** For any valid MarketEvent, serializing to JSON and deserializing produces an identical event.
  - **WAL durability:** For any sequence of append/commit/crash/recover operations, no committed event is lost.
  - **Validation completeness:** For any TradeEvent, the F# validator either accepts it or returns at least one specific error (never silently passes invalid data).
  - **Storage path determinism:** For any MarketEvent, the storage policy produces the same path regardless of call order.
- Add fuzzing for the WebSocket message parsers (Alpaca JSON, Polygon JSON, IB binary).

**Why it matters:** The F# codebase is already structurally suited for property-based testing. The `ValidationResult<T>` type, the pure calculation functions, and the immutable records are ideal inputs for QuickCheck-style generators. This would catch the class of bugs that hand-crafted tests systematically miss.

---

## 14. Implement Cross-Provider Data Reconciliation

**Current state:** `CrossProviderComparisonService` exists and can compare data across providers, but it's a monitoring tool — it reports differences, it doesn't resolve them. When Alpaca says AAPL traded at 150.25 and Polygon says 150.26, the system stores both without reconciliation.

**Improvement:**
- Add a reconciliation pipeline that runs after collection:
  - For each symbol, compare events from all active providers within a time window.
  - Identify discrepancies (price differences, missing events, timestamp disagreements).
  - Apply a configurable reconciliation strategy:
    - **Majority vote:** If 2 of 3 providers agree, use that value.
    - **Authority source:** Always prefer the exchange-direct feed.
    - **Conservative:** Flag discrepancies for manual review.
  - Produce a reconciled output stream alongside the raw streams.
  - Store reconciliation decisions as metadata (which provider won, what the delta was).

**Why it matters:** Multi-provider collection is only valuable if you can merge the streams into a single source of truth. Without reconciliation, downstream consumers must pick a provider and hope it was correct. This is especially critical for backtesting, where a 1-cent price difference on a high-volume trade can flip a strategy's P&L.

---

## 15. Decouple the HTTP API from the Collection Engine

**Current state:** The HTTP API (endpoints, dashboard) runs in-process with the collection engine. `Program.cs` starts both the collector and the web server in the same process. This means:
- Restarting the API restarts collection (dropping data).
- A buggy endpoint handler can crash the collector.
- Scaling the API independently of the collector is impossible.

**Improvement:**
- Separate the API into a standalone project that reads from the same storage (JSONL/Parquet files, WAL) but does not own the collection process.
- The collector exposes a minimal status/health endpoint but delegates all API functionality to the separate process.
- Communication between the two processes happens via:
  - Shared filesystem (for stored data).
  - A lightweight IPC channel (Unix domain socket or named pipe) for live status.
  - The existing WAL for crash-consistent reads.
- This enables: independent deployment, independent scaling, independent restarts, and cleaner testing.

**Why it matters:** The current architecture couples the most latency-sensitive component (real-time data collection) with the least latency-sensitive component (HTTP API for dashboards). A single slow API request handler can cause backpressure in the event pipeline. Decoupling these is the single highest-impact architectural change for production reliability.

---

## 16. Implement Structured Concurrency for Provider Lifecycle

**Current state:** Provider lifecycle (connect, subscribe, receive, reconnect, disconnect) is managed through individual async methods and event handlers. There is no structured cancellation scope — if `ConnectAsync` succeeds but `SubscribeMarketDepth` throws, the connection may leak.

**Improvement:**
- Adopt a structured concurrency model where each provider's lifecycle is a single cancellation scope:
  ```
  Provider Scope (CancellationToken)
  ├── Connection Task
  │   ├── Authentication
  │   └── Heartbeat Loop
  ├── Subscription Tasks (one per symbol)
  │   ├── Subscribe
  │   └── Receive Loop
  └── Reconnection Supervisor
  ```
- If any child task fails fatally, the entire scope is cancelled and the reconnection supervisor handles restart.
- If the parent scope is cancelled (shutdown), all child tasks are cancelled in reverse order with configurable drain timeouts.
- This prevents resource leaks, orphaned connections, and the subtle bugs where a provider is "connected" but not "subscribed."

**Why it matters:** The current provider lifecycle code is correct but fragile — it depends on developers remembering to handle every failure path. Structured concurrency makes correct cleanup automatic.

---

## Summary: Impact vs. Risk Matrix

| # | Improvement | Impact on Data Quality | Impact on Code Quality | Risk |
|---|------------|----------------------|----------------------|------|
| 1 | Schema evolution | **Critical** | Medium | Low |
| 2 | Strong-typed identifiers | High | **Critical** | Medium |
| 3 | F# validation in pipeline | **Critical** | High | Low |
| 4 | WAL corruption recovery | **Critical** | Medium | Medium |
| 5 | Unified query abstraction | High | **Critical** | Low |
| 6 | End-to-end contract tests | **Critical** | High | Low |
| 7 | Composite sink failure model | High | Medium | Low |
| 8 | Structured capabilities | High | High | Low |
| 9 | Roslyn analyzers | Medium | **Critical** | Low |
| 10 | Deterministic replay | High | Medium | Medium |
| 11 | Eliminate stub endpoints | Medium | High | Low |
| 12 | Unified reconnection | High | High | Medium |
| 13 | Property-based testing | High | High | Low |
| 14 | Cross-provider reconciliation | **Critical** | Medium | High |
| 15 | Decouple API from collector | High | High | High |
| 16 | Structured concurrency | Medium | High | Medium |

**Top 5 by pure output quality improvement:** #1 (schema), #3 (validation), #4 (WAL), #6 (contract tests), #14 (reconciliation)

**Top 5 by code generalization improvement:** #2 (strong types), #5 (query abstraction), #9 (analyzers), #8 (capabilities), #12 (reconnection)
