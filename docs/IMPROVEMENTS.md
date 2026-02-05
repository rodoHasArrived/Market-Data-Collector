# High-Impact Improvements Analysis

**Date:** 2026-02-05
**Version Analyzed:** 1.6.1
**Scope:** Functionality, reliability, and user experience

---

## Executive Summary

After a thorough codebase analysis spanning architecture, resilience, testing, and UX, the following 15 improvements are ranked by impact-to-effort ratio. They target three themes: **closing critical reliability gaps**, **completing the user-facing surface**, and **unlocking operational confidence**.

---

## 1. Implement Automatic Resubscription on WebSocket Reconnect

**Impact: Critical | Area: Streaming Reliability**

When a WebSocket connection drops and `WebSocketConnectionManager` reconnects, subscription state is not restored. After reconnect, providers (Alpaca, Polygon) silently receive zero events until the application is restarted.

**What to do:**
- Have `WebSocketConnectionManager` maintain a subscription registry
- On successful reconnect, replay all active subscriptions via the provider's subscribe methods
- Add a `Resubscribed` event so monitoring can detect recovery

**Files:** `Infrastructure/Resilience/WebSocketConnectionManager.cs`, `AlpacaMarketDataClient.cs`, `PolygonMarketDataClient.cs`

---

## 2. Add Exponential Backoff to Backfill Rate Limit Handling

**Impact: High | Area: Backfill Reliability**

`BackfillWorkerService` uses a fixed 100ms poll interval after receiving HTTP 429 responses. This hammers rate-limited providers, extending lockout periods and potentially triggering IP bans.

**What to do:**
- Parse the `Retry-After` response header (already supported by `HttpResiliencePolicy`)
- Apply exponential backoff (2s, 4s, 8s... capped at 60s) on consecutive 429s
- Add a dead-letter queue for requests that fail after the retry budget

**Files:** `Infrastructure/Providers/Historical/Queue/BackfillWorkerService.cs`

---

## 3. Close the API Route Implementation Gap

**Impact: High | Area: Web Dashboard UX**

220 API routes are declared in `UiApiRoutes.cs`, but only ~35 have handler implementations. Client apps hitting unimplemented routes get 404/405 with no explanation.

**What to do (phased):**
- **Phase 1:** Register all unimplemented routes and return `501 Not Implemented` with a JSON body: `{ "error": "Not yet implemented", "route": "...", "planned": true }`
- **Phase 2:** Implement the highest-value missing groups: provider status/metrics, symbol management, storage analytics, diagnostics
- **Phase 3:** Implement remaining admin/maintenance/scheduling endpoints

**Files:** `Ui.Shared/Endpoints/*.cs`, `Contracts/Api/UiApiRoutes.cs`

---

## 4. Add Real-Time Dashboard Updates via Server-Sent Events

**Impact: High | Area: Web UX**

The web dashboard uses static HTML templates with no live updates. Users must manually refresh to see current status, making the dashboard ineffective for monitoring.

**What to do:**
- Add an SSE endpoint (`/api/events/stream`) pushing status updates every 2 seconds
- Include: event throughput, active subscriptions, provider health, backpressure level, last error
- Add a lightweight JS snippet to the dashboard template that listens and updates DOM elements
- Fall back to polling if SSE connection drops

**Files:** `Ui.Shared/Endpoints/StatusEndpoints.cs`, `wwwroot/templates/`

---

## 5. Fix Storage Sink Disposal Race Condition

**Impact: High | Area: Data Durability**

In `JsonlStorageSink.DisposeAsync()`, the flush timer is disposed *after* the final buffer flush. If the timer fires between these operations, concurrent flushes corrupt buffered data.

**What to do:**
- Dispose `_flushTimer` first, await its completion
- Then execute `FlushAllBuffersAsync()` as the guaranteed final flush
- Apply the same fix to `ParquetStorageSink`
- Extract shared buffering/flushing logic into a common `BufferedSinkBase` class to prevent future divergence

**Files:** `Storage/Sinks/JsonlStorageSink.cs`, `Storage/Sinks/ParquetStorageSink.cs`

---

## 6. Add Provider Factory with Runtime Switching

**Impact: High | Area: Architecture / Flexibility**

`Program.cs` directly instantiates data clients via a switch statement. This prevents runtime provider switching, makes testing difficult, and couples the entry point to all provider implementations.

**What to do:**
- Create `IMarketDataClientFactory` with a `Create(DataSourceKind)` method
- Register all providers in DI with keyed services (`.NET 8+ KeyedService`)
- Replace the Program.cs switch with factory resolution
- Enable runtime provider switching via the existing `/api/config/data-source` endpoint

**Files:** `Program.cs`, new `Infrastructure/Providers/MarketDataClientFactory.cs`

---

## 7. Add Endpoint Integration Tests

**Impact: High | Area: Quality / Regression Prevention**

The entire HTTP API layer (15 endpoint files) has zero dedicated tests. Bugs in route handlers go undetected until runtime.

**What to do:**
- Use `WebApplicationFactory<T>` from `Microsoft.AspNetCore.Mvc.Testing`
- Write tests for all implemented endpoints: status, config, backfill, failover, providers
- Assert response status codes, content types, and response schema shapes
- Include negative cases (invalid input, missing config)

**Files:** New `tests/MarketDataCollector.Tests/Integration/EndpointTests/`

---

## 8. Implement Dropped Event Audit Trail

**Impact: Medium-High | Area: Data Integrity**

When the `EventPipeline` drops events due to backpressure (`DropOldest` mode), the events vanish silently. Downstream consumers and storage have no awareness of gaps.

**What to do:**
- Log dropped events to a separate audit file (`_audit/dropped_events.jsonl`)
- Include timestamp, event type, symbol, and drop reason
- Expose drop statistics via the `/api/status` endpoint
- Add a `/api/quality/drops` endpoint for gap-aware consumers
- Optionally trigger backfill for symbols with significant drops

**Files:** `Application/Pipeline/EventPipeline.cs`, `Application/Monitoring/BackpressureAlertService.cs`

---

## 9. Add Backfill Progress Reporting

**Impact: Medium-High | Area: UX**

Users running backfill operations have no visibility into progress. Long backfills (years of data) appear frozen with no feedback.

**What to do:**
- Track per-symbol progress in `BackfillWorkerService`: total date ranges requested vs. completed
- Expose via `/api/backfill/progress` with per-symbol completion percentage
- Add SSE updates for active backfill progress
- Display progress bar in the web dashboard and CLI output

**Files:** `Infrastructure/Providers/Historical/Queue/BackfillWorkerService.cs`, `Ui.Shared/Endpoints/BackfillEndpoints.cs`

---

## 10. Harden WAL Recovery for Large Files

**Impact: Medium | Area: Durability**

`WriteAheadLog.GetUncommittedRecordsAsync()` loads all uncommitted records into an in-memory list. With a 100MB WAL file containing millions of uncommitted records, this causes out-of-memory failures during recovery.

**What to do:**
- Change recovery to use `IAsyncEnumerable<WalRecord>` with streaming reads
- Process and replay records in batches (e.g., 10,000 at a time)
- Add a WAL size warning when uncommitted data exceeds a configurable threshold (default 50MB)
- Use full SHA256 for checksums instead of the current truncated 8-byte variant

**Files:** `Storage/Archival/WriteAheadLog.cs`

---

## 11. Add OpenAPI/Swagger Documentation

**Impact: Medium | Area: Developer UX / Integration**

There is no machine-readable API documentation. Consumers of the HTTP API must read source code to understand request/response formats.

**What to do:**
- Add `Swashbuckle.AspNetCore` or `NSwag` to the UI project
- Annotate endpoints with `[ProducesResponseType]` and XML docs
- Serve Swagger UI at `/swagger` when running in development mode
- Generate OpenAPI spec file as a build artifact for CI

**Files:** `Ui.Shared/`, endpoint files, `MarketDataCollector.Ui.Shared.csproj`

---

## 12. Fix SubscriptionManager Memory Leak

**Impact: Medium | Area: Reliability**

`SubscriptionManager` never removes entries from its internal dictionary on unsubscribe. In applications that cycle through symbols (e.g., scanning), this creates a steady memory leak.

**What to do:**
- Implement actual cleanup in `UnsubscribeTrades()` and `UnsubscribeMarketDepth()`
- Remove subscription entries from the dictionary
- Add a `ActiveSubscriptionCount` property for monitoring
- Log subscription lifecycle events at Debug level

**Files:** `Infrastructure/Providers/Shared/SubscriptionManager.cs`

---

## 13. Reduce GC Pressure in Hot Message Paths

**Impact: Medium | Area: Performance**

WebSocket message handlers allocate `new Dictionary<>()` and `new List<OrderBookLevel>()` per message at rates up to 100 Hz, triggering GC pauses every 100-200ms.

**What to do:**
- Pool `List<OrderBookLevel>` using `ObjectPool<T>` from `Microsoft.Extensions.ObjectPooling`
- Replace per-message dictionary allocations with reusable `JsonDocument` parsing
- Use `Span<T>`-based JSON readers (`Utf8JsonReader`) in the Polygon message parser
- Benchmark before/after with `MarketDataCollector.Benchmarks`

**Files:** `Infrastructure/Providers/Polygon/PolygonMarketDataClient.cs`, `Infrastructure/Providers/StockSharp/MessageConverter.cs`

---

## 14. Add Authentication to HTTP Endpoints

**Impact: Medium | Area: Security**

The HTTP API has no authentication. Anyone with network access can read configuration, modify symbols, trigger backfills, and access market data.

**What to do:**
- Implement API key authentication via the existing `ApiKeyMiddleware.cs` (currently present but likely not enforced)
- Support key rotation via environment variable (`MDC_API_KEY`)
- Add rate limiting per API key
- Exempt health check endpoints (`/healthz`, `/readyz`, `/livez`) from auth

**Files:** `Ui.Shared/Endpoints/ApiKeyMiddleware.cs`, `Program.cs`

---

## 15. Consolidate Desktop App Navigation

**Impact: Medium | Area: Desktop UX**

Both WPF and UWP apps have 48 pages each, creating a fragmented and overwhelming navigation experience. Users report difficulty finding features.

**What to do:**
- Group pages into 5 workspaces: **Monitor** (dashboard, live data, charts), **Collect** (providers, symbols, backfill), **Storage** (browser, export, packages), **Quality** (data quality, diagnostics, health), **Settings** (config, credentials, maintenance)
- Implement tabbed workspace navigation with sidebar categories
- Add a command palette (Ctrl+K) for quick page access using the existing `SearchService`

**Files:** `MarketDataCollector.Uwp/Views/MainPage.xaml`, `MarketDataCollector.Wpf/Views/MainPage.xaml`, navigation services

---

## Priority Matrix

| # | Improvement | Impact | Effort | Priority |
|---|------------|--------|--------|----------|
| 1 | WebSocket resubscription | Critical | Low | **P0** |
| 2 | Backfill rate limit backoff | High | Low | **P0** |
| 5 | Storage sink disposal fix | High | Low | **P0** |
| 12 | Subscription memory leak fix | Medium | Low | **P0** |
| 3 | API route gap closure | High | Medium | **P1** |
| 4 | Real-time dashboard (SSE) | High | Medium | **P1** |
| 6 | Provider factory pattern | High | Medium | **P1** |
| 8 | Dropped event audit trail | Med-High | Low | **P1** |
| 9 | Backfill progress reporting | Med-High | Medium | **P1** |
| 7 | Endpoint integration tests | High | Medium | **P2** |
| 10 | WAL recovery hardening | Medium | Medium | **P2** |
| 14 | HTTP API authentication | Medium | Medium | **P2** |
| 11 | OpenAPI documentation | Medium | Low | **P2** |
| 13 | GC pressure reduction | Medium | Medium | **P3** |
| 15 | Desktop nav consolidation | Medium | High | **P3** |

---

## Impact Summary

Implementing P0 items alone (items 1, 2, 5, 12) addresses the most critical reliability gaps with minimal effort. Adding P1 items (3, 4, 6, 8, 9) transforms the user experience from "power-user CLI tool" to "monitorable production system." P2/P3 items build long-term maintainability and polish.
