# High-Impact Repository Improvements Brainstorm

> Generated from deep codebase analysis on 2026-03-02.
> Focus: code generalization, program output quality, architectural soundness.
> Scope: ideas ranked purely by impact — effort is intentionally not considered.

---

## 1. Replace Stringly-Typed Identifiers with Strong Domain Types

**Current state:** Symbols, provider IDs, stream IDs, venue codes, and subscription IDs are all bare `string` or `int` throughout the codebase. The `MarketEvent` record uses `string Symbol`, `string Source`, `string? CanonicalSymbol`, `string? CanonicalVenue`. Collectors key state on `string`. The entire pipeline, storage, and API layer pass raw strings around.

**Problem:** Nothing prevents mixing a symbol with a venue code, a provider ID with a stream ID, or passing an un-normalized symbol where a canonical one is expected. The compiler cannot help. Bugs like "passed venue where symbol expected" are silent runtime errors. The `EffectiveSymbol` property on `MarketEvent` is a band-aid for what should be a type-level distinction.

**Improvement:** Introduce value-object wrappers:

```csharp
public readonly record struct Symbol(string Value) : IComparable<Symbol>;
public readonly record struct CanonicalSymbol(string Value);
public readonly record struct ProviderId(string Value);
public readonly record struct Venue(string Value);
public readonly record struct StreamId(string Value);
public readonly record struct SubscriptionId(int Value);
```

These are zero-cost at runtime (single-field readonly structs) but eliminate entire categories of bugs at compile time. The `ConcurrentDictionary<string, SymbolTradeState>` in `TradeDataCollector` becomes `ConcurrentDictionary<Symbol, SymbolTradeState>`, making the key semantics explicit. Storage paths, dedup keys, metrics labels, and quality monitoring all benefit from type-safe symbols vs canonical symbols.

**Impact:** Eliminates a class of subtle runtime bugs. Makes API contracts self-documenting. Enables compile-time enforcement of "canonical vs raw" symbol distinction that currently relies on developer discipline.

---

## 2. Make the Event Pipeline Generic and Composable (Middleware Pipeline)

**Current state:** `EventPipeline` is a monolithic 677-line class that hardcodes: channel-based backpressure, WAL integration, batch consumption, periodic flushing, metrics tracking, and audit trail logging. All concerns are interleaved in `ConsumeAsync()`. Adding a new cross-cutting concern (e.g., deduplication, filtering, transformation, sampling) requires modifying this class directly.

**Problem:** The pipeline is not composable. Every new behavior (canonicalization, validation, filtering, enrichment) must be wired externally or bolted onto the monolith. The `CanonicalizingPublisher` wraps `IMarketEventPublisher` — but this only works at the publish boundary, not within the pipeline. There is no way to express "validate, then canonicalize, then deduplicate, then persist" as a pipeline of independent stages.

**Improvement:** Introduce a middleware-based pipeline architecture:

```csharp
public delegate ValueTask EventPipelineDelegate(MarketEvent evt, CancellationToken ct);

public interface IEventPipelineMiddleware
{
    ValueTask InvokeAsync(MarketEvent evt, EventPipelineDelegate next, CancellationToken ct);
}
```

Each concern becomes a composable middleware:
- `WalMiddleware` — WAL append before forwarding
- `DeduplicationMiddleware` — drop duplicate sequences
- `CanonicalizationMiddleware` — normalize symbols/venues
- `ValidationMiddleware` — run F# validators, emit integrity events
- `MetricsMiddleware` — track throughput and latency
- `FilterMiddleware` — configurable event type filtering
- `StorageSinkMiddleware` — terminal middleware that writes to sink

The pipeline builder composes them:
```csharp
pipeline.Use<MetricsMiddleware>()
        .Use<DeduplicationMiddleware>()
        .Use<CanonicalizationMiddleware>()
        .Use<ValidationMiddleware>()
        .Use<WalMiddleware>()
        .Use<StorageSinkMiddleware>();
```

**Impact:** Transforms the pipeline from a closed system into an open, extensible one. New behaviors are additive, not invasive. Each middleware is independently testable. Pipeline composition becomes a configuration concern rather than a code change.

---

## 3. Unify the Dual Domain Model (C# Records + F# Types)

**Current state:** The domain is split across two type systems:
- C# records in `Contracts/Domain/` and `Domain/Events/` (`Trade`, `BboQuotePayload`, `LOBSnapshot`, `MarketEvent`)
- F# records in `MarketDataCollector.FSharp/Domain/` (`TradeEvent`, `QuoteEvent`, `OrderBookSnapshot`)
- The `Interop.fs` file provides manual wrappers (`TradeEventWrapper`, `QuoteEventWrapper`) to bridge between them.

**Problem:** There are two parallel representations of every core domain concept. A trade is both a C# `Trade` record and an F# `TradeEvent` record. Conversion between them is manual and fragile. The F# validation library (`TradeValidator`, `QuoteValidator`) operates on F# types, so C# code must convert to F# types, validate, then convert back. This dual model increases surface area for bugs and makes it unclear which representation is canonical.

**Improvement:** Choose one canonical representation and derive the other:

**Option A: F# as the canonical domain, generate C# projections.** F# discriminated unions and record types are more expressive for domain modeling. Use the existing `FSharpInteropGenerator` (in `build/dotnet/`) to auto-generate C# wrappers from F# types, eliminating hand-written `Interop.fs`.

**Option B: C# as the canonical domain, use F# computation expressions over C# types.** Since the C# types are already used everywhere, make the F# validators operate directly on C# record types via extension modules. Eliminate the parallel F# domain types entirely.

Either way, the goal is: **one source of truth for each domain concept**, with the other language consuming it directly.

**Impact:** Eliminates an entire layer of conversion code, reduces bug surface for type mismatches, makes the F# validation pipeline zero-friction to use from C#.

---

## 4. Introduce a Proper Event Sourcing / CQRS Backbone

**Current state:** `MarketEvent` is a sealed record with 16+ factory methods that acts as both a domain event and a persistence envelope. The `MarketEventPayload` base class uses polymorphic dispatch (nullable base type) with runtime type checks. The WAL stores serialized `MarketEvent` blobs. Storage sinks receive events one at a time via `AppendAsync`. There is no event store abstraction — only storage sinks.

**Problem:** The system has the shape of event sourcing (immutable events, WAL, replay) but lacks the formal guarantees. There is no event versioning strategy (the `SchemaVersion` field exists but is always `1`). There is no projection/replay capability beyond WAL recovery. Querying historical data requires reading JSONL files. The system cannot answer "replay all trades for SPY from 10:30 to 11:00 through the pipeline" without building ad-hoc infrastructure each time.

**Improvement:** Formalize the event store abstraction:

```csharp
public interface IEventStore
{
    ValueTask AppendAsync(MarketEvent evt, CancellationToken ct);
    IAsyncEnumerable<MarketEvent> ReadForwardAsync(Symbol symbol, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    IAsyncEnumerable<MarketEvent> ReadForwardAsync(EventStreamPosition position, CancellationToken ct);
    Task<EventStreamPosition> GetCurrentPositionAsync(CancellationToken ct);
}
```

Add event schema evolution:
- Each payload type gets an explicit schema version
- Upcasters transform old versions to current
- The WAL and storage layer both use the event store interface

Add projection support:
- Projections are stateful consumers of the event stream
- VWAP, order flow stats, spread monitoring become projections
- Projections can be rebuilt from the event store at any time

**Impact:** Enables time-travel debugging, replay-based backtesting, schema evolution without data migration, and decouples read-side queries from write-side storage.

---

## 5. Extract a Provider Contract Test Suite (Consumer-Driven Contracts)

**Current state:** Each provider (Alpaca, Polygon, IB, StockSharp, NYSE, etc.) is tested in isolation with mocks. There is no shared test suite that verifies all providers satisfy the `IMarketDataClient` and `IHistoricalDataProvider` contracts identically. The `ContractVerificationService` in `Infrastructure/Contracts/` exists but is a runtime service, not a test harness.

**Problem:** Provider implementations can drift. One provider might emit trades with negative sequence numbers (which `TradeDataCollector` rejects), another might emit timestamps in local time instead of UTC, another might not handle cancellation tokens properly. These discrepancies are only discoverable at integration time or in production.

**Improvement:** Create a shared contract test base class:

```csharp
public abstract class MarketDataClientContractTests<T> where T : IMarketDataClient
{
    protected abstract T CreateClient();

    [Fact] public async Task Connect_Then_Disconnect_Should_Not_Throw() { ... }
    [Fact] public async Task Subscribe_Trades_Should_Return_Positive_SubscriptionId() { ... }
    [Fact] public async Task Events_Should_Have_Monotonic_Sequences() { ... }
    [Fact] public async Task Events_Should_Have_UTC_Timestamps() { ... }
    [Fact] public async Task Cancellation_Should_Be_Respected() { ... }
    [Fact] public async Task Dispose_Should_Disconnect_Gracefully() { ... }
}

// Each provider inherits and provides its implementation:
public class AlpacaContractTests : MarketDataClientContractTests<AlpacaMarketDataClient>
{
    protected override AlpacaMarketDataClient CreateClient() => ...;
}
```

Similarly for `IHistoricalDataProvider`:

```csharp
public abstract class HistoricalProviderContractTests<T> where T : IHistoricalDataProvider
{
    [Fact] public async Task GetDailyBars_Should_Return_Sorted_By_Date() { ... }
    [Fact] public async Task GetDailyBars_Should_Have_Positive_OHLC_Values() { ... }
    [Fact] public async Task GetDailyBars_Should_Respect_Date_Range() { ... }
    [Fact] public async Task Rate_Limit_Should_Not_Throw_But_Wait() { ... }
}
```

**Impact:** Guarantees behavioral consistency across all providers. New providers automatically inherit the full contract test suite. Regression detection is immediate.

---

## 6. Implement Structural Typing for MarketEventPayload (Eliminate Polymorphic Null)

**Current state:** `MarketEvent` has a `Payload` property of type `MarketEventPayload?`. This is a nullable base class that the consumer must downcast at runtime. The 16 factory methods on `MarketEvent` create events with different payload types, but the type information is lost in the record's signature. Consumers must pattern-match on `Type` and then cast `Payload`:

```csharp
if (evt.Type == MarketEventType.Trade && evt.Payload is Trade trade) { ... }
```

**Problem:** The compiler cannot prove exhaustiveness. Nothing prevents accessing `evt.Payload` as a `Trade` when it's actually an `LOBSnapshot`. The nullable payload means `Heartbeat` events have `null` payloads — a special case that every consumer must handle. Adding a new event type requires updating every consumer manually.

**Improvement:** Use a discriminated union pattern (C# 13 supports this well):

```csharp
public abstract record MarketEventPayload
{
    public sealed record TradePayload(Trade Trade) : MarketEventPayload;
    public sealed record L2SnapshotPayload(LOBSnapshot Snapshot) : MarketEventPayload;
    public sealed record BboQuotePayload(BboQuote Quote) : MarketEventPayload;
    public sealed record OrderFlowPayload(OrderFlowStatistics Stats) : MarketEventPayload;
    public sealed record IntegrityPayload(IntegrityEvent Integrity) : MarketEventPayload;
    public sealed record HeartbeatPayload() : MarketEventPayload;
    public sealed record HistoricalBarPayload(HistoricalBar Bar) : MarketEventPayload;
    // ... etc
}
```

Now `MarketEvent.Payload` is non-nullable (every event has a payload, even heartbeats). Consumers use exhaustive pattern matching:

```csharp
var result = evt.Payload switch
{
    MarketEventPayload.TradePayload t => HandleTrade(t.Trade),
    MarketEventPayload.BboQuotePayload q => HandleQuote(q.Quote),
    // compiler warns if cases are missing
};
```

**Impact:** Eliminates null-payload special cases, enables compiler-verified exhaustive handling, makes adding new event types a compile-error-driven process.

---

## 7. Implement Backpressure Propagation Across Provider → Collector → Pipeline

**Current state:** The `EventPipeline` has backpressure (bounded channel, drop-oldest). But the `TradeDataCollector.OnTrade()` method is synchronous and void — it calls `TryPublish` and silently drops if the pipeline is full. Providers push data into collectors with no feedback mechanism. The `DroppedEventAuditTrail` records drops, but nothing uses this signal to slow down the source.

**Problem:** In a sustained overload scenario, the system silently drops data while providers continue pushing at full speed. There is no feedback loop. The pipeline drops events, the audit trail logs them, but the providers don't know and don't slow down. This means the system's behavior under load is "lose data silently" rather than "slow down gracefully."

**Improvement:** Introduce backpressure propagation:

1. **Make collectors async-aware:** `OnTrade` returns a `ValueTask<bool>` indicating whether the event was accepted. When the pipeline is full, collectors can signal back to the provider.

2. **Add provider-side flow control:** `IMarketDataClient` gets a `PauseAsync()`/`ResumeAsync()` contract. When backpressure is detected, the subscription orchestrator pauses the provider's data stream.

3. **Implement adaptive rate limiting:** Instead of binary pause/resume, use a token-bucket or leaky-bucket pattern. The pipeline's utilization percentage drives the token refill rate, creating smooth degradation.

4. **Expose backpressure as a metric dimension:** Current metrics track "dropped events" as a count. Instead, expose `pipeline_backpressure_ratio` as a gauge (0.0 = no pressure, 1.0 = fully saturated). This feeds into alerting and auto-scaling decisions.

**Impact:** Transforms the system from "lossy under load" to "gracefully degrading under load." Prevents silent data loss in production. Enables auto-scaling decisions.

---

## 8. Implement a Plugin Architecture for Storage Sinks

**Current state:** Storage sinks are registered at compile time via DI. The `CompositeSink` hardcodes JSONL + optional Parquet. Adding a new sink (e.g., ClickHouse, TimescaleDB, Apache Kafka, S3) requires modifying `ServiceCompositionRoot.cs` and the sink registration logic.

**Problem:** Storage is the most likely extension point for users. Different deployments want different storage backends. But adding a new sink requires rebuilding the application. There is no plugin discovery mechanism for sinks.

**Improvement:** Implement a plugin-based sink architecture:

```csharp
[StorageSink("clickhouse")]
public sealed class ClickHouseSink : IStorageSink { ... }

[StorageSink("kafka")]
public sealed class KafkaSink : IStorageSink { ... }
```

At startup, the composition root scans for `[StorageSink]` attributes (similar to how `[DataSource]` works for providers). Configuration drives which sinks are active:

```json
{
  "Storage": {
    "Sinks": ["jsonl", "parquet", "clickhouse"],
    "ClickHouse": { "ConnectionString": "..." }
  }
}
```

The `CompositeSink` becomes dynamically composed from the configured sink list.

**Impact:** Makes storage extensible without code changes. Users can add new storage backends as plugins. The existing JSONL/Parquet sinks become just two instances of the plugin pattern.

---

## 9. Introduce Deterministic Replay Testing (Golden Master Tests)

**Current state:** Tests mock individual services and assert specific behaviors. The `JsonlReplayer` and `MemoryMappedJsonlReader` exist for replay, but there are no tests that replay a known input sequence and compare the full output against a golden master.

**Problem:** The system transforms input data through many stages (provider → collector → canonicalization → validation → pipeline → storage). End-to-end correctness is only testable in production. A subtle change in trade aggregation, sequence validation, or VWAP calculation could pass all unit tests but produce different output data.

**Improvement:** Create deterministic replay tests:

1. **Capture golden datasets:** Record a sequence of raw provider messages (e.g., 1000 trades + quotes for SPY over 5 minutes from the Alpaca adapter).

2. **Replay through the full pipeline:** Feed the golden input through the real collector → pipeline → storage chain (with in-memory sinks).

3. **Compare output against golden master:** The storage sink's output (JSONL lines) is compared byte-for-byte against a committed golden file.

4. **Detect regressions automatically:** Any change to the pipeline that alters output data causes the golden master test to fail. The developer must explicitly update the golden file, which forces them to review the delta.

```csharp
[Fact]
public async Task Replay_SPY_GoldenDataset_ProducesExpectedOutput()
{
    var input = LoadGoldenInput("testdata/spy-1000-trades.jsonl");
    var sink = new InMemoryStorageSink();
    var pipeline = BuildFullPipeline(sink);

    foreach (var evt in input)
        await pipeline.PublishAsync(evt);
    await pipeline.FlushAsync();

    var output = sink.GetAllEvents();
    await Verify(output); // Verify library for snapshot testing
}
```

**Impact:** Catches subtle behavioral regressions that unit tests miss. Provides confidence that pipeline changes don't silently alter output data. Creates a reproducible baseline for performance benchmarking.

---

## 10. Implement Zero-Allocation Hot Path (Struct-Based Event Pipeline)

**Current state:** `MarketEvent` is a `sealed record` (reference type). Every event allocation goes through the heap. The `ConsumeAsync()` loop creates a `List<MarketEvent>` batch buffer per iteration. The channel stores reference types. In the hot path (high-frequency trade data), this generates significant GC pressure.

**Problem:** For market data at scale (thousands of events per second per symbol, across hundreds of symbols), GC pauses introduce latency spikes. The current architecture allocates on every event: the `MarketEvent` record, the payload record, the `List<T>` buffer resize, and potentially the JSONL serialization string.

**Improvement:** Introduce a struct-based fast path for the highest-volume event types:

1. **Struct event representation for hot path:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public readonly struct RawTradeEvent
{
    public readonly long TimestampTicks;
    public readonly int SymbolHash;  // Pre-computed, lookup in symbol table
    public readonly decimal Price;
    public readonly long Size;
    public readonly byte Aggressor;
    public readonly long Sequence;
}
```

2. **Ring buffer instead of channel:** For the ultra-hot path, use a `SingleProducerSingleConsumer` ring buffer backed by pre-allocated memory. No allocation per event.

3. **Batch serialization:** Instead of serializing events one at a time, batch-serialize to a pre-allocated `Span<byte>` buffer using `Utf8JsonWriter`.

4. **Dual path:** Keep the current `MarketEvent` record-based pipeline for low-volume event types (integrity, heartbeat, historical bars). Use the struct-based path only for trades and quotes.

**Impact:** Eliminates GC pressure on the hot path. Reduces p99 latency. Enables the system to handle 10-100x more events per second before degrading.

---

## 11. Implement a Formal State Machine for Provider Connection Lifecycle

**Current state:** Provider connection state is tracked via `bool IsConnected`, `bool IsReconnecting`, and various `volatile` flags in `WebSocketConnectionManager`. State transitions (disconnected → connecting → connected → reconnecting → disconnected) are implicit in the control flow of `ConnectAsync()`, `ReconnectInternalAsync()`, and event handlers.

**Problem:** Invalid state transitions are possible. For example, calling `SubscribeTrades()` while `IsReconnecting` is true could produce undefined behavior. The reconnection logic uses `SemaphoreSlim` gates to prevent storms, but the allowed transitions are not formally modeled. Race conditions between heartbeat timeout detection and manual disconnect are possible.

**Improvement:** Model the connection lifecycle as a formal state machine:

```csharp
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Subscribing,
    Active,        // Connected + subscriptions active
    Reconnecting,
    Draining,      // Shutting down, processing remaining events
    Disposed
}
```

Use a state machine library or hand-rolled transition table:

```csharp
public sealed class ConnectionStateMachine
{
    private static readonly Dictionary<(ConnectionState, ConnectionTrigger), ConnectionState> Transitions = new()
    {
        { (Disconnected, Connect), Connecting },
        { (Connecting, Connected), Connected },
        { (Connected, Subscribe), Subscribing },
        { (Subscribing, AllSubscribed), Active },
        { (Active, ConnectionLost), Reconnecting },
        { (Reconnecting, Connected), Subscribing },
        { (Active, Disconnect), Draining },
        { (Draining, Drained), Disconnected },
        // ... etc
    };
}
```

**Impact:** Eliminates race conditions in connection management. Makes invalid state transitions compile-time or throw-immediately-time errors. Simplifies reconnection logic. State transitions become observable (logging, metrics, testing).

---

## 12. Compile-Time Architectural Boundary Enforcement

**Current state:** The `BannedReferences.txt` in the Domain project documents that Domain should not reference Application, Infrastructure, Storage, or Core. But this is documentation only — the `.csproj` project reference structure provides the actual enforcement. If someone adds a `<ProjectReference>` to Infrastructure from Domain, it compiles fine.

**Problem:** Architectural boundaries are enforced by convention and code review, not by tooling. As the team grows or AI agents make changes, layer violations can be introduced silently. The current enforcement is project-level (no reference means no access), but within projects, internal coupling is unchecked.

**Improvement:**

1. **Roslyn Analyzer for Layer Boundaries:** Create a custom analyzer that reads `BannedReferences.txt` and emits compiler errors for any `using` statement that references a banned namespace.

2. **ArchUnit-style tests:** Add architectural fitness tests:
```csharp
[Fact]
public void Domain_Should_Not_Reference_Infrastructure()
{
    var result = Types.InAssembly(typeof(MarketEvent).Assembly)
        .Should().NotHaveDependencyOn("MarketDataCollector.Infrastructure")
        .GetResult();
    result.IsSuccessful.Should().BeTrue();
}
```

3. **Module-level access control:** Use `[InternalsVisibleTo]` more deliberately. Currently, internal types in one project are sometimes visible to test projects but also accidentally to other production projects.

**Impact:** Prevents architectural erosion. Makes layer violations impossible to commit. Provides fast feedback during development rather than in code review.

---

## 13. Implement Data Lineage as a First-Class Pipeline Concept

**Current state:** `DataLineageService` exists in Storage but is a separate service that must be called explicitly. Events carry `Source` and `CanonicalizationVersion` fields, but there is no systematic tracking of which transformations an event has passed through.

**Problem:** When debugging data quality issues in production, the question "how did this event get here and what happened to it along the way?" is hard to answer. Was it canonicalized? By which version? Was it validated? Did it pass the bad-tick filter? Was it deduplicated? None of this is recorded on the event itself.

**Improvement:** Add a lineage chain to every event:

```csharp
public sealed record EventLineage(
    ImmutableArray<LineageEntry> Entries
)
{
    public EventLineage Append(string stage, string detail) =>
        new(Entries.Add(new LineageEntry(stage, detail, DateTimeOffset.UtcNow)));
}

public sealed record LineageEntry(
    string Stage,       // "ingestion", "canonicalization", "validation", "dedup", "storage"
    string Detail,      // "alpaca-ws", "v3", "passed", "duplicate-dropped", "jsonl-written"
    DateTimeOffset Timestamp
);
```

Each pipeline middleware appends to the lineage. The storage sink writes the lineage alongside the event data. Query APIs can filter by lineage stage.

**Impact:** Full observability into the event transformation chain. Dramatically simplifies debugging data quality issues. Enables "what-if" analysis (replay with different pipeline configuration).

---

## 14. Replace Runtime Provider Discovery with Source-Generated Registration

**Current state:** `ServiceCompositionRoot` uses reflection (`GetCustomAttribute<DataSourceAttribute>()`, `Activator.CreateInstance()`) to discover and register providers at runtime. This is in `RegisterStreamingFactoriesFromAttributes()`.

**Problem:** Reflection-based discovery is:
- Silent on failure (if a provider's constructor signature changes, `Activator.CreateInstance` throws at runtime, not compile time)
- Not trimming-compatible (breaks with .NET AOT/trimming)
- Not debuggable (hard to tell which providers were actually registered)
- Slow (reflection on startup)

**Improvement:** Use a C# source generator:

```csharp
[GenerateProviderRegistry]
public partial class ProviderRegistry
{
    // Source generator scans for [DataSource] attributes and generates:
    // partial void RegisterAllProviders(IServiceCollection services) { ... }
}
```

The source generator emits explicit registration code at compile time:
```csharp
// Auto-generated
partial void RegisterAllProviders(IServiceCollection services)
{
    services.AddTransient<IMarketDataClient, AlpacaMarketDataClient>();
    services.AddTransient<IMarketDataClient, PolygonMarketDataClient>();
    // ...
}
```

**Impact:** Provider registration becomes compile-time verified. AOT-compatible. Debugging is trivial (generated code is readable). Startup is faster.

---

## 15. Implement Comprehensive Schema Evolution for Stored Data

**Current state:** `MarketEvent` has `SchemaVersion = 1` hardcoded as a default. The `SchemaVersionManager` exists in Storage/Archival but primarily handles versioning at the file level. There is no mechanism to evolve the JSON schema of stored events over time.

**Problem:** The current JSONL storage format will break if any field is renamed, retyped, or restructured. Old data files become unreadable if the C# record changes. There is no upcasting (old schema → new schema) or downcasting capability. This makes the storage format brittle and prevents safe evolution of the domain model.

**Improvement:**

1. **Schema registry:** Register each event type's JSON schema with a version number. Store the schema alongside the data.

2. **Upcasters:** For each schema version transition, define a transformation:
```csharp
public interface IEventUpcaster
{
    int FromVersion { get; }
    int ToVersion { get; }
    JsonElement Upcast(JsonElement oldEvent);
}
```

3. **Read-side adaptation:** When reading old JSONL files, the reader applies upcasters in sequence to bring events to the current schema version.

4. **Write-side stamping:** Every written event includes its schema version, making the data self-describing.

**Impact:** Stored data survives domain model evolution. Old datasets remain queryable forever. Schema changes become safe operations rather than migration nightmares.

---

## Summary: Impact Ranking

| # | Improvement | Impact Area |
|---|-------------|-------------|
| 1 | Strong domain types | Bug prevention, API clarity |
| 2 | Middleware pipeline | Extensibility, testability |
| 3 | Unified domain model | Simplicity, reduced bugs |
| 4 | Event sourcing backbone | Replay, time-travel, querying |
| 5 | Contract test suite | Provider reliability |
| 6 | Discriminated union payloads | Type safety, exhaustiveness |
| 7 | Backpressure propagation | Reliability under load |
| 8 | Plugin storage sinks | Extensibility |
| 9 | Golden master replay tests | Regression detection |
| 10 | Zero-allocation hot path | Performance at scale |
| 11 | Connection state machine | Reliability, debuggability |
| 12 | Compile-time boundary enforcement | Architectural integrity |
| 13 | First-class data lineage | Observability, debugging |
| 14 | Source-generated provider registry | Correctness, AOT compat |
| 15 | Schema evolution for storage | Data longevity |
