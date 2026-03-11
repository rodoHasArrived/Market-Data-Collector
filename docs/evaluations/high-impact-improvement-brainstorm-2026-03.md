# High-Impact Improvement Brainstorm — March 2026

**Date:** 2026-03-01
**Status:** Active — Defects and Improvements Identified
**Author:** Architecture Review

> **Scope**: Ideas that meaningfully improve the quality of the codebase and the
> correctness/reliability of the running program. Implementation effort is
> explicitly **not** a filter — only impact matters.

---

## Executive Assessment

The codebase is **architecturally sound at the macro level** — provider
abstraction, tiered storage, data quality monitoring, and domain modelling are
all well-designed. But deep analysis reveals **critical implementation-level
defects** hiding behind that good architecture: race conditions in the event
pipeline flush, WAL durability gaps, silent data corruption in provider parsing,
memory leaks in monitoring, and an under-utilized F# integration. The system
looks production-ready from the outside but has subtle failure modes that would
cause silent data loss, incorrect metrics, or resource exhaustion under real
market conditions.

**Overall rating: 6.5/10 — Architecturally Sound, Operationally Risky**

| Component | Design | Implementation | Robustness |
|-----------|--------|----------------|------------|
| Event Pipeline | 9/10 | 5/10 | 5/10 |
| Write-Ahead Log | 8/10 | 4/10 | 4/10 |
| Alpaca Client | 7/10 | 4/10 | 4/10 |
| WebSocket Resilience | 8/10 | 5/10 | 6/10 |
| Data Quality Monitoring | 9/10 | 6/10 | 6/10 |
| Domain Models | 9/10 | 8/10 | 8/10 |
| Configuration System | 8/10 | 7/10 | 7/10 |
| Test Suite | 8/10 | 6/10 | 6/10 |
| F# Integration | 7/10 | 5/10 | 5/10 |

---

## Category 1: Data Integrity & Correctness

These are bugs that cause **silent data loss or corruption** in the running
program. Fixing any one of them directly improves the trustworthiness of every
byte of data the system collects.

### 1.1 EventPipeline Flush Semantics Are Broken

**File**: `src/MarketDataCollector.Application/Pipeline/EventPipeline.cs`

The `FlushAsync()` completion condition counts dropped events as "accounted
for":

```csharp
// Current (broken):
if (consumed + dropped >= targetPublished) break;
```

In `DropOldest` mode this breaks **immediately** after publishing because
dropped events count toward the target, even though they were never written to
storage. The caller receives a successful flush even though data was silently
discarded.

**Impact**: Any code that calls `FlushAsync()` and then trusts that all data is
persisted is wrong. This affects shutdown, checkpoint operations, and any
user-facing "data saved" confirmation.

**Fix**: Only break when `consumed >= targetPublished`. Dropped events should be
tracked separately and reported, not conflated with successful persistence.

### 1.2 WAL-to-Sink Transaction Gap

**File**: `src/MarketDataCollector.Storage/Archival/WriteAheadLog.cs` and
`EventPipeline.cs`

The consumer loop appends to the WAL, then writes to the sink, then commits the
WAL:

```
WAL.Append(event) → Sink.Append(event) → Sink.Flush() → WAL.Commit()
```

If the sink write fails **after** WAL append succeeds, the WAL still has the
record. On recovery, those events replay and may duplicate into the sink. Worse:
if `Sink.Flush()` succeeds but the process crashes before `WAL.Commit()`, the
WAL recovery replays already-persisted events — creating duplicates with no
detection mechanism.

**Impact**: After any crash or restart, the stored data may contain duplicate
events. For market data research, this corrupts volume statistics, VWAP
calculations, and trade-flow analysis.

**Fix**: Implement idempotent sink writes (dedup by sequence number) or use a
two-phase commit where the WAL commit only happens after verified sink
persistence.

### 1.3 Alpaca Price Precision Loss

**File**: `src/MarketDataCollector.Infrastructure/Adapters/Alpaca/AlpacaMarketDataClient.cs`

Trade prices are parsed via `GetDouble()` and then cast to `decimal`:

```csharp
var price = el.TryGetProperty("p", out var pProp) ? (decimal)pProp.GetDouble() : 0m;
```

`double` has ~15-17 significant digits; `decimal` has 28-29. But the
**conversion path** `JSON string → double → decimal` introduces floating-point
representation errors. A price of `123.455` might become `123.45499999999999`
after the round-trip.

Additionally, trade size uses `GetInt32()` but large block trades can exceed
`int.MaxValue`. Timestamps silently fall back to `DateTimeOffset.UtcNow` on
parse failure — recording the wrong time rather than flagging the error.

**Impact**: Price corruption on high-precision assets (crypto, forex). Volume
truncation on large block trades. Incorrect timestamps on malformed messages.

**Fix**: Parse prices as `GetDecimal()` or parse the raw string. Use `GetInt64()`
for sizes. Reject events with unparseable timestamps rather than substituting.

### 1.4 No Trade Message Deduplication

**File**: `AlpacaMarketDataClient.cs` (and likely other providers)

Alpaca's WebSocket API is known to send duplicate trade messages. The client has
no deduplication logic — identical trades are published to the pipeline twice.
The pipeline's `PersistentDedupLedger` exists but is sequence-based, not
content-based, so if the same trade arrives with different sequence numbers it
passes through.

**Impact**: Inflated volume metrics, broken VWAP, incorrect order-flow
statistics. Every downstream consumer sees phantom trades.

**Fix**: Implement content-based deduplication using a sliding window of
`(symbol, price, size, exchange_timestamp)` tuples. Bloom filter or LRU hash
set for memory efficiency.

### 1.5 Completeness Score Miscalculation

**File**: `src/MarketDataCollector.Application/Monitoring/DataQuality/CompletenessScoreCalculator.cs`

Expected events per hour is determined at the moment of the **first event of
the day**. If no liquidity profile has been registered by then, the default
(e.g., 10,000 events/hour) is used. For a symbol that actually trades 100
events/hour, the score calculates as 1% complete — **and this score is locked
for that date forever**.

**Impact**: Completeness dashboards show grossly incorrect scores for any symbol
without a pre-registered liquidity profile. Operators cannot trust the quality
metrics.

**Fix**: Allow dynamic liquidity profile updates mid-day, or recalculate
expected counts based on observed patterns after an initial calibration window.

---

## Category 2: Resource Management & Stability

These issues cause the program to degrade over time through resource exhaustion,
hangs, or cascading failures.

### 2.1 Memory Leaks in Monitoring Services

**Files**: `SequenceErrorTracker.cs`, `GapAnalyzer.cs`,
`CompletenessScoreCalculator.cs`

All three services use `ConcurrentDictionary` keyed by symbol (or
symbol+eventType). Entries are added on first sight of a symbol but **never
removed**. The cleanup timers remove old *data within entries* but never remove
the *entries themselves*.

For a system monitoring 1,000 symbols for a day, all 1,000 dictionary entries
persist in memory forever. Over weeks of operation with symbol rotation (e.g.,
options chains), memory grows unboundedly.

`GapAnalyzer` is particularly bad: it creates separate state for each `symbol ×
eventType` combination (1,000 symbols × 4 types = 4,000 entries, each holding
timestamps, sequences, and pending gap state).

**Impact**: Long-running instances slowly consume more memory until OOM or GC
pressure causes latency spikes.

**Fix**: Implement LRU eviction or time-based expiry on dictionary entries. When
a symbol hasn't been seen for N hours, remove its entry entirely.

### 2.2 Hot-Path Allocations in Data Quality

**File**: `GapAnalyzer.cs`

`GetEffectiveConfig()` is called for **every single event** and creates a new
`GapAnalyzerConfig` record each time via the `with` keyword:

```csharp
return _config with { GapThresholdSeconds = ..., ExpectedEventsPerHour = ... };
```

At 50,000 events/second, this creates 50,000 short-lived objects per second —
pure GC pressure.

**Impact**: Increased GC pause times, higher tail latency, reduced throughput
under load.

**Fix**: Cache computed configs per symbol in a dictionary. Invalidate only when
liquidity profile changes.

### 2.3 Consumer Blocking on Slow Sinks

**File**: `EventPipeline.cs`

The consumer loop calls `Sink.FlushAsync()` synchronously (awaited) in the
consumer loop. If the sink is slow (network storage, disk I/O burst), the
consumer is blocked and the bounded channel fills up, triggering backpressure
and event dropping.

There is no timeout on `Sink.FlushAsync()`. If the sink hangs (e.g., NFS mount
becomes unresponsive), the entire pipeline stalls permanently.

**Impact**: A single slow storage operation cascades into data loss across all
symbols.

**Fix**: Add a configurable timeout to sink operations. Consider double-buffering
(write to buffer while previous buffer flushes). Alert operators when flush
latency exceeds thresholds.

### 2.4 WebSocket Receive Buffer Unbounded

**File**: `WebSocketConnectionManager.cs`

The receive loop uses a fixed 64KB buffer but appends to a `StringBuilder` with
no size limit:

```csharp
var buffer = new byte[64 * 1024];
var messageBuilder = new StringBuilder(128 * 1024);
// Loops until EndOfMessage, no size check
```

A malicious or misbehaving server could send a message that grows the
StringBuilder to gigabytes.

**Impact**: Denial-of-service via memory exhaustion from a single oversized
WebSocket message.

**Fix**: Add a maximum message size limit (e.g., 10MB). Disconnect and log a
warning if exceeded.

### 2.5 WebSocket Reconnection Race Condition

**File**: `WebSocketConnectionManager.cs`

The `TryReconnectAsync` method checks `_isReconnecting` without acquiring the
semaphore gate first:

```csharp
if (_isReconnecting) return false;        // No lock!
if (!await _reconnectGate.WaitAsync(0, ct)) return false;
```

Two threads can both see `_isReconnecting == false` and proceed past the first
check simultaneously, then race on the semaphore. This can cause duplicate
reconnection attempts or — worse — one thread succeeding while the other
corrupts the connection state.

**Impact**: Duplicate WebSocket connections, wasted subscriptions, inconsistent
state.

**Fix**: Remove the fast-path check. Let the semaphore be the sole gating
mechanism.

### 2.6 No Provider Connection Timeout

**File**: `src/MarketDataCollector/Program.cs`

The startup flow calls `await dataClient.ConnectAsync()` with no timeout. If a
provider hangs (firewall silently dropping packets, DNS resolution stalling),
the application hangs forever.

**Impact**: Application fails to start with no error message, no timeout, no
recovery.

**Fix**: Wrap connection in a timeout (e.g., 30 seconds). On timeout, log a
clear error and either fall back to an alternative provider or exit with a
meaningful error code.

---

## Category 3: Architectural Improvements

These changes improve the system's fundamental design, making it more
maintainable, extensible, and correct by construction.

### 3.1 End-to-End Trace Context Propagation

**Current state**: OpenTelemetry is wired up (`TracedEventMetrics`,
`OpenTelemetrySetup`), but there's no trace context flowing through the event
pipeline. Each component logs independently with no correlation.

**What's missing**: When a trade arrives from Alpaca, gets processed through the
pipeline, validated by data quality, and written to storage — there's no single
trace ID linking all of those operations.

**Impact of improvement**: Operators can trace any latency anomaly from ingestion
to storage in one query. Debugging goes from "search 5 log files and correlate
timestamps manually" to "filter by trace ID."

**Approach**: Wire `System.Diagnostics.Activity` through the pipeline. Tag each
`MarketEvent` with its originating activity context. Propagate to sink
operations.

### 3.2 WebSocket Provider Base Class

**Current state**: Polygon, NYSE, and StockSharp each implement ~200-300 LOC of
duplicate WebSocket lifecycle code (connect, authenticate, receive loop,
reconnect, heartbeat).

**Impact of improvement**: Bug fixes in reconnection logic apply once instead of
3+ times. New WebSocket providers start from a tested base instead of copying
and adapting. Connection resilience becomes uniform across all providers.

**Approach**:

```csharp
public abstract class WebSocketProviderBase : IMarketDataClient
{
    protected abstract Uri BuildConnectionUri();
    protected abstract Task<bool> AuthenticateAsync(CancellationToken ct);
    protected abstract void HandleMessage(JsonElement message);
    // Shared: connect, reconnect, heartbeat, receive loop, state machine
}
```

### 3.3 Decide the F# Strategy: Deepen or Remove

**Current state**: 12 F# files vs. 652 C# files. F# provides discriminated
unions for market events, validation pipelines, and spread/imbalance
calculations. But the C# domain collectors (`QuoteCollector`,
`TradeDataCollector`) are the *real* domain logic, and they're mutable.

The F# validation pipeline exists but is rarely called from C#. The interop
layer adds ceremony (type conversion at boundaries) without clear payoff. Tests
exist but the coverage is thin.

**The honest assessment**: The F# integration is at a **dead middle ground** — it
adds surface area and complexity without being deep enough to deliver its
inherent safety benefits. Either commitment is valid:

**Option A — Deepen**: Move all validation, canonicalization, and calculations
into F#. Make C# collectors thin adapters that call F# functions. The type
safety then truly protects the hot path.

**Option B — Remove**: Port the useful F# logic (spread calculation, validation
rules) to C# sealed records and pattern matching. Eliminate the interop layer
and the dual-language build complexity.

**Impact**: Either direction reduces cognitive load and maintenance surface area.
The current state is the worst of both worlds.

### 3.4 Idempotent Storage Writes

**Current state**: If the same event is written twice (crash recovery, provider
duplication, reconnection replay), the sink stores both copies. There's no
content-based or sequence-based deduplication at the storage layer.

**Impact of improvement**: Crash recovery, provider reconnection, and message
deduplication all become safe by default. The WAL-to-sink transaction gap
(section 1.2) becomes non-critical because duplicate writes are harmlessly
absorbed.

**Approach**: Each storage sink maintains a bloom filter or hash set of recent
`(symbol, sequence, timestamp)` tuples. Events matching an existing entry are
silently deduplicated. The bloom filter is rebuilt from the last N minutes of
stored data on startup.

### 3.5 Configuration Fail-Fast vs. Self-Healing Separation

**Current state**: `ConfigurationPipeline.ApplySelfHealing()` silently fixes
problems like missing symbols, reversed dates, and unavailable providers. This
is helpful for getting started but dangerous in production — operators don't
know their config was modified.

**Impact of improvement**: Clear separation between "fixable cosmetic issues"
(reversed dates → swap them) and "configuration errors that need human
attention" (missing credentials → fail with actionable error). Production
deployments fail fast on real problems; development environments get helpful
auto-fixes.

**Approach**: Introduce severity levels in self-healing: `AutoFix` (apply
silently), `Warn` (apply but log prominently), `Error` (refuse to start). Let
operators configure the threshold via an environment variable
(`MDC_CONFIG_STRICTNESS=production`).

### 3.6 Proper Backpressure Feedback Loop

**Current state**: `EventPipeline.TryPublish()` returns `bool` but provides no
information about *why* it failed or *what to do*. Publishers have no mechanism
to slow down when the pipeline is under pressure.

**Impact of improvement**: Instead of silently dropping events, the system can
signal providers to pause subscriptions, reduce polling frequency, or queue
locally. This turns uncontrolled data loss into managed flow control.

**Approach**: Return a `PublishResult` enum (`Accepted`, `Queued`,
`BackpressureActive`, `Dropped`) and expose a `BackpressureChanged` event that
providers can subscribe to for proactive throttling.

---

## Category 4: Observability & Operational Excellence

### 4.1 Alert-to-Runbook Linkage

**Current state**: Alert rules exist in `deploy/monitoring/alert-rules.yml` but
don't contain runbook URLs or mitigation steps in their annotations. When an
alert fires, the operator has to search documentation manually.

**Impact**: Embed runbook URLs directly in alert annotations so monitoring tools
(Grafana, PagerDuty) display actionable guidance inline.

### 4.2 Backpressure Alerting Is Single-Shot

**Current state**: The pipeline logs one warning at 80% queue utilization, resets
at 50%. During sustained high load (80-100%), hundreds or thousands of events
can be dropped with only that single warning.

**Impact of improvement**: Continuous alerting during backpressure events. Report
drop rate per second, total events dropped in current episode, and estimated
data loss percentage.

### 4.3 WAL Corruption Alerting

**Current state**: During WAL recovery, invalid checksums are logged at Warning
level and the records are silently skipped. There's no operator alert, no
halt-on-corruption option, and no way to know that 1,000 out of 10,000 records
were silently discarded.

**Impact of improvement**: Configurable corruption response: `Skip` (current
behavior), `Alert` (continue but fire alert), `Halt` (refuse to start until
operator reviews). Default to `Alert` in production.

### 4.4 Provider Health Dashboard

**Current state**: Individual provider metrics exist but there's no unified view
of "which providers are healthy, which are degraded, which are failing, and
what's the overall data collection health?"

**Impact**: A single `/api/providers/dashboard` endpoint that returns a traffic-
light summary: green (all providers healthy), yellow (some degraded, failover
active), red (primary providers down, data at risk).

---

## Category 5: Test Infrastructure

### 5.1 Fix Timing-Dependent Tests

**Current state**: 5+ tests are skipped with `[Fact(Skip = "...")]` because they
rely on timing guarantees (`Task.Delay`) that don't hold in CI environments.
Tests like `QueueUtilization_ReflectsQueueFill` fail because the consumer
drains the queue faster than expected.

**Impact of improvement**: Replace `Task.Delay`-based synchronization with
deterministic signaling (`ManualResetEventSlim`, `TaskCompletionSource`,
`SemaphoreSlim`). Every skipped test is a regression that's not being caught.

### 5.2 Error Injection in Mock Sinks

**Current state**: The mock storage sink used in pipeline tests only supports a
configurable `ProcessingDelay`. There's no way to inject exceptions, simulate
partial writes, or test concurrent failure scenarios.

**Impact of improvement**: Tests can verify that the pipeline handles sink
failures correctly: retries, reports errors, doesn't corrupt state. Currently
this is untested.

### 5.3 Provider Resilience Test Suite

**Current state**: Provider tests primarily cover message parsing and
subscription management. There are no tests for: rate limit enforcement across
providers, partial data corruption (malformed JSON), authentication failure
handling, reconnection under message loss, or heartbeat timeout behavior.

**Impact of improvement**: These are the exact failure modes that occur in
production. Testing them prevents the "works in dev, fails at 3 AM on a
holiday" class of incidents.

### 5.4 Property-Based Testing for Domain Models

**Current state**: Domain model tests are primarily example-based (specific
inputs → expected outputs). For types like `MarketEvent`, `Trade`, and
`LOBSnapshot`, property-based testing would be more effective at finding edge
cases.

**Impact of improvement**: Catch edge cases in serialization round-trips, event
ordering, and payload validation that hand-written examples miss. Libraries like
FsCheck or Hedgehog work well with the existing xUnit setup.

---

## Category 6: Code Quality & Maintainability

### 6.1 Eliminate 42-Service Singleton Anti-Pattern

**Files**: All services in `src/MarketDataCollector.Ui.Services/Services/`

42 services use manual `Lazy<T>` singleton patterns:

```csharp
private static readonly Lazy<AlertService> _instance = new(() => new AlertService());
public static AlertService Instance => _instance.Value;
```

This makes services untestable (can't inject mocks), tightly coupled (services
reference each other via static instances), and duplicative (~4,000-5,000 LOC of
boilerplate).

**Impact of improvement**: Proper DI registration. Services become testable,
composable, and have explicit lifetime management. Dependency graphs become
visible and verifiable.

### 6.2 ServiceCompositionRoot Decomposition

**File**: `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs`

This single file registers 50-100+ services with complex dependency wiring. No
validation that the dependency graph is acyclic. No documentation of
registration order (which matters for some services).

**Impact of improvement**: Break into focused registration modules
(`StorageModule`, `MonitoringModule`, `ProviderModule`). Add startup validation
that resolves all registered services eagerly to catch missing registrations at
boot rather than at first use.

### 6.3 Consistent Error Handling Across Providers

**Current state**: Each provider handles errors differently:
- Alpaca: fire-and-forget subscription, no auth verification
- Polygon: circuit breaker with retry
- IB: conditional connection with simulation fallback
- NYSE: hybrid streaming + historical

**Impact of improvement**: A uniform error handling contract where every provider
reports errors through the same mechanism, with the same severity levels, and
the same recovery semantics. Currently, understanding error behavior requires
reading each provider's implementation individually.

---

## Category 7: Correctness by Construction

These improvements make entire classes of bugs impossible rather than catching
them after the fact.

### 7.1 Typed Symbol Keys

**Current state**: Symbols are passed as `string` everywhere. Nothing prevents
passing a ticker where a CUSIP is expected, or vice versa. Canonical vs.
raw symbols are distinguished only by convention.

**Impact of improvement**: Introduce `Symbol` (raw), `CanonicalSymbol`, and
`ProviderSymbol` value types. The compiler prevents mixing them. Mapping between
types is explicit and auditable.

### 7.2 Sequence Number Domain Separation

**Current state**: `MarketEvent.Sequence` is a pipeline-assigned sequence, but
payloads like `Trade.SequenceNumber` carry exchange-assigned sequences. These
two sequence domains are both `long` and easily confused.

**Impact of improvement**: Introduce `PipelineSequence` and `ExchangeSequence`
value types. Code that accidentally compares or conflates them becomes a
compile-time error.

### 7.3 Non-Nullable Event Payloads via Type Specialization

**Current state**: `MarketEvent.Payload` is `MarketEventPayload?` — nullable for
all event types. Some events (like Heartbeat) have null payloads, but most
require non-null payloads. This is enforced by convention, not the type system.

**Impact of improvement**: Generic specialization
(`MarketEvent<TPayload> where TPayload : MarketEventPayload`) eliminates the
nullability for events that always carry payloads. Pattern matching becomes
exhaustive and the compiler catches missing cases.

---

## Priority Matrix

| # | Improvement | Category | Data Impact | Reliability Impact |
|---|------------|----------|-------------|-------------------|
| 1.1 | Fix flush semantics | Correctness | **Critical** | High |
| 1.2 | WAL-sink transaction | Correctness | **Critical** | High |
| 1.3 | Price precision fix | Correctness | **Critical** | Medium |
| 1.4 | Trade deduplication | Correctness | **Critical** | Medium |
| 2.1 | Memory leak fixes | Stability | Low | **Critical** |
| 2.3 | Sink timeout/buffering | Stability | High | **Critical** |
| 2.5 | Reconnection race fix | Stability | Medium | High |
| 3.1 | E2E trace propagation | Architecture | Low | High |
| 3.4 | Idempotent writes | Architecture | **Critical** | **Critical** |
| 3.6 | Backpressure feedback | Architecture | High | High |
| 5.1 | Fix flaky tests | Testing | Low | Medium |
| 5.3 | Provider resilience tests | Testing | Medium | High |
| 6.1 | Eliminate singletons | Maintainability | Low | Medium |
| 7.1 | Typed symbol keys | Type Safety | Medium | Medium |

---

## Implementation Follow-Up (2026-03-10)

The following items from this brainstorm have been implemented:

| Item | Status | Implementation |
|------|--------|----------------|
| 1.3 — Alpaca price precision & timestamp integrity | ✅ Done | `AlpacaMarketDataClient`: trade sizes now parsed with `GetInt64()` to avoid truncation on block trades exceeding `int.MaxValue`. Both trade and quote messages now reject unparseable timestamps with a `Warning` log instead of silently substituting `UtcNow`, preserving time-series integrity. |
| 1.4 — Trade message deduplication | ✅ Done | `AlpacaMarketDataClient`: content-based deduplication added via a bounded sliding window (`HashSet` + `Queue`) of `(symbol, price, size, timestamp)` tuples (capacity 2,048). Duplicate re-deliveries from Alpaca's WebSocket are suppressed at the `Debug` log level. |
| 4.3 — WAL corruption alerting | ✅ Done | `WriteAheadLog`: new `WalCorruptionMode` enum (`Skip` / `Alert` / `Halt`) added. `WalOptions.CorruptionMode` defaults to `Skip` (backwards-compatible). In `Alert` mode the new `CorruptionDetected` event fires with the corrupted record count so monitoring infrastructure can alert operators. In `Halt` mode an `InvalidDataException` is thrown to force operator review before the application can start. |

Test coverage:
- `AlpacaMessageParsingTests` — 12 tests covering size precision, timestamp rejection, deduplication, and window eviction.
- `WriteAheadLogCorruptionModeTests` — 9 tests covering all three modes and the `WalOptions` default.

---

## Implementation Follow-Up (2026-03-11)

| Item | Status | Implementation |
|------|--------|----------------|
| 2.6 — Provider connection timeout | ✅ Done | `Program.cs`: `dataClient.ConnectAsync()` is now wrapped in a 30-second `CancellationTokenSource` timeout. On timeout an `OperationCanceledException` is caught separately and surfaced as a clear `ErrorCode.ConnectionTimeout` exit code with an actionable log message. |
| 2.3 — Periodic sink flush timeout | ✅ Done | `EventPipeline`: new `sinkFlushTimeout` constructor parameter (default 60 s). Each periodic flush call is wrapped in a `CancellationTokenSource.CreateLinkedTokenSource` that adds the per-flush deadline on top of the pipeline shutdown token. A hung sink now times out and logs a `Warning` instead of stalling the pipeline indefinitely. Pipeline-shutdown cancellation is still distinguished from flush-timeout cancellation via a `when` guard. |
| 3.6 — Backpressure feedback loop | ✅ Done | `EventPipeline.TryPublishWithResult()` added returning a new `PublishResult` enum (`Accepted` / `AcceptedUnderPressure` / `Dropped`). `TryPublish()` is unchanged for backward compatibility. `PublishResult` is defined in `MarketDataCollector.Domain.Events` so all provider adapters can reference it without circular dependencies. |
| 4.4 — Provider health dashboard | ✅ Done | New `GET /api/providers/dashboard` endpoint (`UiApiRoutes.ProvidersDashboard`) added to `ProviderExtendedEndpoints`. Returns an `overallTrafficLight` (`green`/`yellow`/`red`), human-readable `summary`, and per-provider detail including latency from stored metrics. |
| 5.1 — Fix timing-dependent skipped tests | ✅ Done | `QueueUtilization_ReflectsQueueFill`: rewritten using `BlockingStorageSink` + `batchSize: 1` so 49 events remain in the channel while the consumer is blocked on the first. `ValidateFileAsync_SupportsCancellation`: fixed by adding `ct.ThrowIfCancellationRequested()` at the top of `ValidateFileAsync` to honour pre-cancelled tokens before the file is opened. Both tests pass deterministically. |

Test coverage added:
- `EventPipelineTests.TryPublishWithResult_WhenAccepted_ReturnsAccepted` — verifies `Accepted` result on normal publish.
- `EventPipelineTests.TryPublishWithResult_WhenQueueFull_ReturnsDropped` — verifies `Dropped` result when pipeline is at capacity (DropWrite mode).
- `EventPipelineTests.QueueUtilization_ReflectsQueueFill` — previously skipped, now enabled and passes deterministically.
- `DataValidatorTests.ValidateFileAsync_SupportsCancellation` — previously skipped, now enabled and passes deterministically.

---

*Generated 2026-03-02 from deep codebase analysis across pipeline, WAL,
providers, monitoring, configuration, tests, and domain model.*
