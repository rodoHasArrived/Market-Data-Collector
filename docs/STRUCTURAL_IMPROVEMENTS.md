# Structural Improvements Analysis

**Date:** 2026-02-07
**Version Analyzed:** 1.6.1
**Focus:** Modularity, abstraction clarity, extensibility, development process, UX

---

## Summary

This analysis identifies 15 high-impact structural improvements organized into four themes. Each item includes the problem with code-level evidence, a concrete remedy with migration sketch, affected files with line numbers, and expected benefit. Items are ranked by impact-to-effort ratio within each theme.

Companion document: `docs/IMPROVEMENTS.md` covers feature-level and reliability improvements.

---

## A. Modularity & Abstraction Clarity

### A1. Unify Provider Creation into a Single Extensible Registry

**Problem:** Three separate provider creation mechanisms exist and compete:

| Mechanism | File | Lines | Used By |
|-----------|------|-------|---------|
| Switch-based factory | `MarketDataClientFactory.cs` | 43-113 | `Program.cs:296` (streaming) |
| Parallel factory | `ProviderFactory.cs` | 24-406 | `HostStartup` (backfill), `ServiceCompositionRoot:268` (symbol search fallback) |
| Direct instantiation | `Program.cs` | 200-443 | Main streaming path |
| Attribute discovery | `DataSourceAttribute.cs`, `DataSourceRegistry.cs` | Full files | **Never invoked** |

Adding a new streaming provider requires changes in multiple locations. The `[DataSource]` attribute discovery system (ADR-005) is fully implemented but never wired into any startup path.

**Evidence — `MarketDataClientFactory` switch (lines 81-92):**

```csharp
return dataSource switch
{
    DataSourceKind.Alpaca => CreateAlpacaClient(config, publisher, tradeCollector, quoteCollector),
    DataSourceKind.Polygon => new PolygonMarketDataClient(publisher),
    DataSourceKind.StockSharp => new StockSharpMarketDataClient(
        config.StockSharp!, depthCollector, tradeCollector, quoteCollector, publisher),
    _ => new IBMarketDataClient(config, publisher, tradeCollector, depthCollector, quoteCollector),
};
```

**Evidence — `ProviderFactory.CreatePrimaryStreamingProviderAsync` (lines 117-129):**

```csharp
return _config.DataSource switch
{
    DataSourceKind.Alpaca => CreateAlpacaStreamingClient(),
    DataSourceKind.Polygon => CreatePolygonStreamingClient(),
    DataSourceKind.StockSharp => CreateStockSharpStreamingClient(),
    DataSourceKind.IB => CreateIBStreamingClient(),
    _ => CreateIBStreamingClient(),
};
```

This second factory passes `null!` for all collectors (lines 144-145, 152-154, 160-161):

```csharp
return new AlpacaMarketDataClient(
    tradeCollector: null!,  // Will be set during initialization
    quoteCollector: null!,  // Will be set during initialization
    opt: _config.Alpaca! with { KeyId = keyId, SecretKey = secretKey });
```

These `null!` values are **never populated** — the streaming path in `ProviderFactory` is dead code.

**Evidence — unused attribute system:**

`DataSourceAttribute.cs` (lines 24-87) defines:
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DataSourceAttribute : Attribute
{
    public string Id { get; }
    public string DisplayName { get; }
    public DataSourceType Type { get; }
    public DataSourceCategory Category { get; }
    public int Priority { get; set; } = 100;
    public bool EnabledByDefault { get; set; } = true;
}
```

`DataSourceRegistry.DiscoverFromAssemblies()` (lines 21-45) uses reflection to find decorated types. `RegisterModules()` (lines 66-91) discovers `IProviderModule` implementations. Neither method is ever called.

**Dead code summary:**

| Type | Registered In | Used By | Status |
|------|--------------|---------|--------|
| `DataSourceRegistry` | Nowhere | Nowhere | Dead |
| `IProviderModule` | Nowhere | Nowhere | Dead |
| `ProviderFactory.CreateStreamingProvidersAsync` | `ServiceCompositionRoot:486` | Never for streaming | Dead (null collectors) |
| `ProviderRegistry` | `ServiceCompositionRoot:478` | Symbol search fallback only | Mostly dead |

**Remedy:**

1. Decorate each provider with `[DataSource]`:
   ```csharp
   [DataSource("alpaca", "Alpaca", DataSourceType.Streaming, DataSourceCategory.MarketData)]
   public sealed class AlpacaMarketDataClient : IMarketDataClient { }
   ```

2. Wire `DataSourceRegistry.DiscoverFromAssemblies()` into `ServiceCompositionRoot` at startup.

3. Replace both switch statements with a registry lookup:
   ```csharp
   public IMarketDataClient Create(DataSourceKind kind)
       => _registry.Resolve<IMarketDataClient>(kind.ToString());
   ```

4. Delete `MarketDataClientFactory` switch logic and `ProviderFactory` streaming methods. Keep `ProviderFactory` only for backfill/symbol-search (or migrate those too).

5. Remove direct `new` calls from `Program.cs:296-362`.

**Files:** `MarketDataClientFactory.cs:43-113`, `ProviderFactory.cs:100-172`, `Program.cs:296-362`, `DataSourceAttribute.cs`, `DataSourceRegistry.cs`, `ServiceCompositionRoot.cs:466-496`

**Benefit:** Adding a new provider becomes: implement interface, add attribute, done. Eliminates ~200 lines of switch/factory duplication and all dead code paths. Three creation mechanisms collapse to one.

---

### A2. Consolidate DI and Direct Instantiation into a Single Composition Path

**Problem:** `ServiceCompositionRoot.cs` registers services into DI, but `Program.cs` bypasses DI for all critical runtime components. The result is two parallel composition paths where DI registrations are dead code.

**Evidence — types created in both locations:**

| Type | `ServiceCompositionRoot` | `Program.cs` | DI Version Used? |
|------|--------------------------|-------------|------------------|
| `QuoteCollector` | Line 553 | Line 279 | No |
| `TradeDataCollector` | Line 560 | Line 280 | No |
| `MarketDepthCollector` | Line 568 | Line 281 | No |
| `JsonlStoragePolicy` | Line 510 | Line 213 | No |
| `JsonlStorageSink` | Line 517 | Line 214 | No |
| `WriteAheadLog` | Line 525 | Line 218 | No |
| `EventPipeline` | Line 525 | Line 224 | No |
| `PipelinePublisher` | Line 532 | Line 238 | No |

Both paths create instances with **identical constructor arguments**. For example, `Program.cs:279-281`:

```csharp
var quoteCollector = new QuoteCollector(publisher);
var tradeCollector = new TradeDataCollector(publisher, quoteCollector);
var depthCollector = new MarketDepthCollector(publisher, requireExplicitSubscription: true);
```

And `ServiceCompositionRoot.cs:553-572`:

```csharp
services.AddSingleton(sp => new QuoteCollector(sp.GetRequiredService<IMarketEventPublisher>()));
services.AddSingleton(sp => new TradeDataCollector(
    sp.GetRequiredService<IMarketEventPublisher>(),
    sp.GetRequiredService<QuoteCollector>()));
services.AddSingleton(sp => new MarketDepthCollector(
    sp.GetRequiredService<IMarketEventPublisher>(), requireExplicitSubscription: true));
```

Additionally, configuration is loaded twice in `Program.cs`: once at line 57 (minimal load for CLI dispatch) and again at line 69 (full load with validation). The `MarketDataClientFactory` is created via `new` at `Program.cs:296` and is **never registered in DI**.

**Remedy:**

1. Make `ServiceCompositionRoot.AddMarketDataServices()` the **single composition entry point** for all runtime services.

2. In `Program.cs`, replace:
   ```csharp
   var quoteCollector = new QuoteCollector(publisher);
   ```
   with:
   ```csharp
   var quoteCollector = host.Services.GetRequiredService<QuoteCollector>();
   ```

3. Register `MarketDataClientFactory` and `HistoricalBackfillService` in DI (currently not registered anywhere).

4. Load `AppConfig` once, register as `IOptions<AppConfig>` singleton.

5. Remove the `CompositionOptions` enable/disable flags that exist only because `Program.cs` bypasses DI and needs to avoid double-registration.

**Files:** `Program.cs:57-69, 208-281, 296-362`, `ServiceCompositionRoot.cs:66-130, 466-575`, `HostStartup.cs`

**Benefit:** One composition path. DI registrations become the source of truth. All services become testable via service replacement. Dead registrations are eliminated.

---

### A3. Extract WebSocket Provider Base Class Usage

**Problem:** `WebSocketProviderBase` (`Infrastructure/Shared/WebSocketProviderBase.cs`, 518 lines) provides a comprehensive template for WebSocket providers with connection lifecycle, heartbeat monitoring, resilience pipeline, and receive loop. However, **none of the major WebSocket providers use it**.

**`WebSocketProviderBase` offers these extension points (unused by any provider):**

| Member | Kind | Line | Purpose |
|--------|------|------|---------|
| `ConnectionUri` | abstract property | 55 | WebSocket endpoint URL |
| `ProviderName` | abstract property | 60 | For logging |
| `AuthenticateAsync()` | abstract method | 304 | Provider auth after connect |
| `HandleMessageAsync()` | abstract method | 311 | Message dispatch |
| `BuildSubscriptionMessage()` | abstract method | 319 | Subscription protocol |
| `MaxConnectionRetries` | virtual property | 74 | Default: 5 |
| `RetryBaseDelay` | virtual property | 79 | Default: 2s |
| `HeartbeatInterval` | virtual property | 99 | Default: 30s |
| `ReceiveBufferSize` | virtual property | 109 | Default: 64 KB |
| `ConfigureWebSocket()` | virtual method | 329 | Pre-connect hook |
| `OnConnectionLostAsync()` | virtual method | 337 | Reconnection hook |

**Evidence — duplicated fields across providers:**

| Field | `WebSocketProviderBase` | `Polygon` | `NYSE` | `StockSharp` |
|-------|------------------------|-----------|--------|--------------|
| WebSocket instance | `_ws` (line 38) | `_ws` (line 60) | `_webSocket` (line 52) | Custom client |
| CancellationTokenSource | `_cts` (line 40) | `_cts` (line 61) | `_connectionCts` (line 53) | `_reconnectCts` (line 57) |
| Receive loop task | `_receiveLoop` (line 39) | `_receiveLoop` (line 62) | `_receiveTask` (line 54) | `_processorTask` |
| Reconnection gate | N/A | `_reconnectGate` (line 72) | N/A | `_reconnectCts` |
| Subscription manager | `Subscriptions` (line 36) | `_subscriptionManager` (line 57) | `ConcurrentDictionary` (line 60) | Internal tracking |

**Evidence — duplicated connection lifecycle:**

Polygon `ConnectAsync` (lines 260-278):
```csharp
_ws = new ClientWebSocket();
_ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
await _ws.ConnectAsync(new Uri(wsUrl), ct);
await WaitForConnectionMessageAsync(ct);
await AuthenticateAsync(ct);
_receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));
```

NYSE `ConnectAsync` (lines 237-251):
```csharp
_webSocket = new ClientWebSocket();
_webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
await _webSocket.ConnectAsync(new Uri(_wsEndpoint), ct);
_receiveTask = Task.Run(() => ReceiveMessagesAsync(_connectionCts.Token));
```

Both replicate what `WebSocketProviderBase.ConnectAsync` (lines 155-192) already provides:
```csharp
_ws = new ClientWebSocket();
ConfigureWebSocket(_ws);
await _ws.ConnectAsync(ConnectionUri, ct);
await AuthenticateAsync(ct);
// ... starts heartbeat + receive loop automatically
```

**Evidence — reconnection algorithm divergence:**

| Aspect | WebSocketProviderBase | Polygon | NYSE |
|--------|----------------------|---------|------|
| Strategy | Resilience pipeline | Exponential + jitter | Linear multiply |
| Max retries | 5 (line 74) | 10 (line 74) | 5 (line 903) |
| Backoff | Via Polly policy | `2^attempt * base` + jitter | `attempt * delay` |
| Circuit breaker | Yes (line 84) | No | No |
| Heartbeat detection | Yes (line 99) | No | No |

The base class provides the most robust reconnection but is unused.

**Remedy:**

Refactor each provider into a thin subclass:

```csharp
[DataSource("polygon", "Polygon.io", DataSourceType.Streaming)]
public sealed class PolygonMarketDataClient : WebSocketProviderBase
{
    protected override Uri ConnectionUri => new($"wss://socket.polygon.io/stocks");
    protected override string ProviderName => "Polygon";
    protected override int MaxConnectionRetries => 10;

    protected override Task AuthenticateAsync(CancellationToken ct) { /* Polygon auth JSON */ }
    protected override Task HandleMessageAsync(string message, CancellationToken ct) { /* Parse T/Q/AM */ }
    protected override string BuildSubscriptionMessage(string symbol, string kind) { /* Polygon format */ }
}
```

Provider-specific message parsing stays in each subclass. Connection management, heartbeat, reconnection, and receive loop come from the base class.

**Migration order:**

1. NYSE (simplest, 1,125 lines → ~400 lines with base class)
2. Polygon (1,263 lines → ~500 lines — custom auth and message parsing)
3. StockSharp (1,325 lines → ~600 lines — most complex message handling)

**Files:** `WebSocketProviderBase.cs`, `PolygonMarketDataClient.cs:60-72, 225-287, 410-484, 791-850`, `NYSEDataSource.cs:52-54, 224-298, 724-758, 901-923`, `StockSharpMarketDataClient.cs`

**Benefit:** Eliminates ~800 lines of duplicated connection management. Bug fixes to reconnection, heartbeat, and receive loop logic apply to all providers simultaneously. Reconnection behavior becomes consistent across providers (exponential backoff + circuit breaker everywhere).

---

### A4. Inject Metrics Instead of Using Static Globals

**Problem:** `EventPipeline` and other components call `Metrics.*` via static methods. The `Metrics` class (345 lines) uses `static long` fields with `Interlocked` operations, making it impossible to substitute in tests or swap metrics backends.

**Evidence — static counters in `Metrics.cs` (lines 15-21):**

```csharp
private static long _published;
private static long _dropped;
private static long _integrity;
private static long _trades;
private static long _depthUpdates;
private static long _quotes;
private static long _historicalBars;
```

**Evidence — static calls in `EventPipeline.cs`:**

| Location | Call | Hot Path? |
|----------|------|-----------|
| Line 282 | `Metrics.IncPublished()` | Yes — every published event |
| Line 312 | `Metrics.IncDropped()` | Yes — every dropped event |
| Line 342 | `Metrics.IncPublished()` | Yes — async publish path |

All calls are guarded by `if (_metricsEnabled)` but the static dependency remains.

**Evidence — latency tracking is also static (`Metrics.cs:86-112`):**

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void RecordLatency(long startTimestamp)
{
    var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
    Interlocked.Add(ref _totalProcessingTicks, elapsed);
    Interlocked.Increment(ref _latencySampleCount);
    // ... CAS loop for min/max
}
```

**Evidence — GC monitoring tied to static class (`Metrics.cs:236-273`):**

```csharp
public static void UpdateGcCounts()
{
    var gc0 = GC.CollectionCount(0);
    // ... delta tracking via Interlocked
}
```

**Remedy:**

1. Define `IEventMetrics`:
   ```csharp
   public interface IEventMetrics
   {
       void IncPublished();
       void IncDropped();
       void IncConsumed();
       void RecordLatency(long startTimestamp);
   }
   ```

2. Create `DefaultEventMetrics : IEventMetrics` that delegates to existing `Metrics` static class (zero behavioral change).

3. Add `IEventMetrics` as optional constructor parameter to `EventPipeline` (default to `DefaultEventMetrics`).

4. In tests, inject `NullEventMetrics` or `CountingEventMetrics` for assertion.

**Migration is backward-compatible:** The static `Metrics` class remains for global access (status endpoints, Prometheus exporter). The interface wraps it for DI injection.

**Files:** `EventPipeline.cs:32-34, 282, 312, 342`, `Metrics.cs:12-77`, `PrometheusMetrics.cs`, new `IEventMetrics.cs`

**Benefit:** Pipeline becomes testable without side effects. Opens the door to alternative metrics backends (OpenTelemetry, StatsD) without touching `EventPipeline` code. `Metrics` class can evolve independently.

---

### A5. Standardize Error Handling Strategy

**Problem:** The codebase uses three concurrent error handling approaches without clear boundaries:

**Approach 1 — Custom exceptions (`Core/Exceptions/`, 9 types):**

```csharp
// ConnectionException.cs (lines 1-28)
public sealed class ConnectionException : MarketDataCollectorException
{
    public string? Provider { get; }
    public string? Host { get; }
    public int? Port { get; }
}
```

Used in providers: `throw new ConnectionException("Polygon auth failed", provider: "Polygon")` (Polygon line 364).

**Approach 2 — `Result<T, TError>` (`Application/Results/Result.cs`, 186 lines):**

```csharp
// Lines 9-76
public readonly struct Result<TValue, TError>
{
    public bool IsSuccess { get; }
    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException(...);
    public TError Error => IsFailure ? _error! : throw new InvalidOperationException(...);

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure);
    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper);
    public Result<TNew, TError> Bind<TNew>(Func<TValue, Result<TNew, TError>> binder);
}
```

Well-designed functional type with `Match`, `Map`, `Bind`, and `TryAsync` helpers. But usage is inconsistent — some services use `Result<T>`, others throw exceptions for the same kind of failure.

**Approach 3 — `Environment.Exit(1)` (`Program.cs:493-502`):**

Hard process termination bypasses all cleanup, logging, and error reporting. Used when configuration loading fails — but the same failure could be communicated via exception to allow `GracefulShutdownService` to run.

**Evidence — exception chaining gap:**

Some exception constructors accept `innerException` but callers pass `null`:
```csharp
throw new ConfigurationException("Failed to load config");  // No inner exception
```

When the root cause is an `UnauthorizedAccessException` from file I/O, it's silently discarded.

**Remedy:**

Adopt a clear two-tier convention:

| Scenario | Mechanism | Example |
|----------|-----------|---------|
| Unexpected/unrecoverable | Throw exception (custom type) | Connection failure, file I/O error |
| Expected domain failure | Return `Result<T, OperationError>` | Validation failure, rate limit, no data found |
| Fatal startup failure | Throw `ConfigurationException` | Missing config file, invalid credentials |

Rules:
1. **Never** call `Environment.Exit()` — throw and let top-level handler log + exit
2. **Always** pass the original exception as `innerException`
3. **Always** use `Result<T>` in query/read paths where failure is expected
4. **Always** throw in command/write paths where failure is exceptional
5. Document the convention in `CLAUDE.md` coding conventions section

**Files:** `Program.cs:493-502`, `Result.cs`, `Core/Exceptions/*.cs`, `ConfigurationService.cs`

**Benefit:** Predictable error propagation. Callers know which mechanism to expect. Exception chains are never broken. Process never terminates without cleanup.

---

## B. Extensibility & Development Process

### B1. Extract CLI Argument Parsing into a Shared Helper

**Problem:** Command files use two incompatible argument parsing styles, with duplicate implementations.

**Evidence — three parsing patterns coexist:**

| Pattern | Used By | Occurrences |
|---------|---------|-------------|
| `CliArguments.HasFlag(args, "flag")` | `SymbolCommands` only | 6 |
| `CliArguments.GetValue(args, "flag")` | `SymbolCommands` only | 4 |
| `args.Any(a => a.Equals("flag", OrdinalIgnoreCase))` | `PackageCommands`, `ConfigCommands`, `DiagnosticsCommands`, `HelpCommand` | ~35 |
| Local `GetArgValue(args, "flag")` | `PackageCommands:297-301`, `ConfigCommands:94-98` | 13 |

**Evidence — identical duplicate implementation in two files:**

`PackageCommands.cs:297-301`:
```csharp
private static string? GetArgValue(string[] args, string flag)
{
    var idx = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
```

`ConfigCommands.cs:94-98`:
```csharp
private static string? GetArgValue(string[] args, string flag)
{
    var idx = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
```

Character-for-character identical.

**Evidence — `SymbolCommands` uses the centralized helper but others don't:**

```csharp
// SymbolCommands.cs:34 — uses abstraction
if (CliArguments.HasFlag(args, "--symbols")) { ... }

// PackageCommands.cs:24 — inline reimplementation
if (args.Any(a => a.Equals("--package", StringComparison.OrdinalIgnoreCase))) { ... }
```

**Command file sizes:**

| File | LOC | Uses `CliArguments`? | Has local `GetArgValue`? |
|------|-----|---------------------|-------------------------|
| `SymbolCommands.cs` | 186 | Yes | No |
| `PackageCommands.cs` | 303 | No | Yes (lines 297-301) |
| `ConfigCommands.cs` | 100 | No | Yes (lines 94-98) |
| `DiagnosticsCommands.cs` | 81 | No | No (no value reads) |
| `HelpCommand.cs` | 252 | No | No |
| **Total** | **922** | | |

**Remedy:**

1. Extend the existing `CliArguments` class with missing helpers:
   ```csharp
   public static class CliArguments
   {
       public static bool HasFlag(string[] args, string flag);         // exists
       public static string? GetValue(string[] args, string flag);     // exists
       public static string[]? GetValues(string[] args, string flag);  // new: comma-split
       public static DateOnly? GetDate(string[] args, string flag);    // new: date parse
       public static int? GetInt(string[] args, string flag);          // new: int parse
   }
   ```

2. Migrate `PackageCommands`, `ConfigCommands`, `DiagnosticsCommands` to use `CliArguments`.

3. Delete local `GetArgValue` implementations from both files.

**Files:** `Application/Commands/CliArguments.cs`, `PackageCommands.cs:24-28, 297-301`, `ConfigCommands.cs:24-28, 94-98`, `DiagnosticsCommands.cs:29-34`

**Benefit:** Eliminates parsing duplication (~92 LOC). Consistent error messages for malformed arguments. Adding a new command requires zero boilerplate for argument parsing.

---

### B2. Add Integration Tests for HTTP Endpoints

**Problem:** The HTTP API layer (66+ mapped endpoints, 180+ stub routes) has no integration tests using `WebApplicationFactory<T>`. This is the single highest-value open item from `docs/IMPROVEMENTS.md` (item #7).

**Evidence — current endpoint test coverage:**

| Test File | What It Tests |
|-----------|--------------|
| `EndpointStubDetectionTests.cs` | Route format validation, discovers unmapped routes |
| `ConfigEndpointTests.cs` | Config endpoint route existence |
| `BackfillEndpointTests.cs` | Backfill endpoint route existence |
| `StatusEndpointTests.cs` | Status endpoint route existence |
| `ProviderEndpointTests.cs` | Provider endpoint route existence |
| `FailoverEndpointTests.cs` | Failover endpoint route existence |

These test that routes **exist** but not that they **work** — no actual HTTP request/response pairs are tested.

**Evidence — fully functional endpoints with zero HTTP tests:**

- `GET /api/status` — returns full system status with metrics, provider health, queue sizes
- `POST /api/config/data-source` — changes the active data source at runtime
- `POST /api/backfill/run` — triggers historical data backfill
- `GET /api/providers/catalog` — returns provider metadata and capabilities
- `POST /api/failover/force/{ruleId}` — forces provider failover

All are critical paths where regressions could break production monitoring.

**Remedy:**

1. Add `Microsoft.AspNetCore.Mvc.Testing` to `Directory.Packages.props`.

2. Create `EndpointIntegrationTestBase`:
   ```csharp
   public abstract class EndpointIntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
   {
       protected HttpClient Client { get; }

       protected EndpointIntegrationTestBase(WebApplicationFactory<Program> factory)
       {
           Client = factory.WithWebHostBuilder(builder =>
           {
               builder.ConfigureServices(services =>
               {
                   // Replace storage, providers with test doubles
               });
           }).CreateClient();
       }
   }
   ```

3. Write tests for each endpoint group:
   ```csharp
   [Fact]
   public async Task GetStatus_ReturnsOkWithExpectedShape()
   {
       var response = await Client.GetAsync("/api/status");
       response.StatusCode.Should().Be(HttpStatusCode.OK);
       var body = await response.Content.ReadFromJsonAsync<StatusResponse>();
       body.Should().NotBeNull();
       body!.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
   }
   ```

4. Include negative cases: invalid JSON body, missing required fields, auth failures (API key middleware).

**Priority test targets (by endpoint criticality):**

| Group | Endpoints | Priority |
|-------|-----------|----------|
| Status & Health | `/api/status`, `/healthz`, `/readyz`, `/livez` | Critical |
| Configuration | `/api/config`, `/api/config/data-source`, `/api/config/symbols` | Critical |
| Backfill | `/api/backfill/run`, `/api/backfill/status`, `/api/backfill/providers` | High |
| Providers | `/api/providers/status`, `/api/providers/catalog` | High |
| Quality | `/api/quality/dashboard`, `/api/quality/metrics` | Medium |
| Auth | API key validation, rate limiting (429 response) | Medium |

**Files:** New `tests/MarketDataCollector.Tests/Integration/EndpointTests/`, `Directory.Packages.props`, existing `EndpointStubDetectionTests.cs`

**Benefit:** Prevents regressions in the growing API surface. Validates endpoint-to-service wiring. Catches serialization issues (response shape changes). Tests API key middleware and rate limiting.

---

### B3. Add Infrastructure Provider Unit Tests

**Problem:** The infrastructure layer has 55 provider implementation files but only 8 test files. The ratio is ~369 LOC per test file — well below the typical 1:1 target.

**Evidence — test coverage by provider category:**

| Category | Implementation Files | Test Files | Gap |
|----------|---------------------|------------|-----|
| Streaming (Alpaca, Polygon, NYSE, StockSharp, IB, Failover) | 22 | 4 | 18 |
| Historical (10 providers + base + composite) | 26 | ~3 | ~23 |
| Symbol Search (Alpaca, Finnhub, Polygon, OpenFIGI, StockSharp) | 7 | 2 | 5 |
| **Total** | **55** | **8-9** | **~46** |

**Evidence — untested code in critical providers:**

- `PolygonMarketDataClient.cs` (1,263 lines): message parsing, reconnection, auth — no dedicated test
- `StockSharpMarketDataClient.cs` (1,325 lines): message conversion, subscription lifecycle — no dedicated test
- `NYSEDataSource.cs` (1,125 lines): REST + WebSocket hybrid, reactive streams — no dedicated test
- `IBMarketDataClient.cs`: TWS/Gateway protocol — no dedicated test

**Remedy:**

1. Create test fixtures with recorded WebSocket messages (JSON samples from each provider API).

2. Test message parsing independently:
   ```csharp
   [Fact]
   public void ParseTradeMessage_ValidPolygonTrade_ReturnsCorrectDomainEvent()
   {
       var json = File.ReadAllText("Fixtures/polygon_trade_message.json");
       var trade = PolygonMessageParser.ParseTrade(json);
       trade.Symbol.Should().Be("AAPL");
       trade.Price.Should().Be(150.25m);
   }
   ```

3. Test subscription lifecycle (subscribe → verify message sent → unsubscribe → verify cleanup).

4. Test error handling (malformed message → logged, not thrown; connection loss → reconnect triggered).

**Priority by code size and risk:**

| Provider | LOC | Risk | Test Priority |
|----------|-----|------|---------------|
| StockSharp | 1,325 | High (90+ adapters) | 1 |
| Polygon | 1,263 | High (rate limits) | 2 |
| NYSE | 1,125 | Medium (hybrid) | 3 |
| CompositeHistoricalDataProvider | ~400 | High (fallback chain) | 4 |

**Files:** New tests in `tests/MarketDataCollector.Tests/Infrastructure/Providers/`

**Benefit:** Catches parsing regressions when provider APIs change. Enables safe refactoring of provider internals (e.g., migrating to `WebSocketProviderBase` per A3). Message parsing tests serve as living documentation of each provider's wire format.

---

### B4. Introduce a Composite Storage Sink

**Problem:** `EventPipeline` accepts a single `IStorageSink` (`EventPipeline.cs:33`). Multi-sink scenarios (e.g., JSONL for fast replay + Parquet for analytics) require external composition before constructing the pipeline.

**Evidence — `IStorageSink` interface (`Storage/Interfaces/IStorageSink.cs`):**

```csharp
public interface IStorageSink : IAsyncDisposable, IFlushable
{
    ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default);
    new Task FlushAsync(CancellationToken ct = default);
}
```

Minimal contract. `ValueTask` return enables zero-allocation on the hot path.

**Evidence — both sinks share identical buffering and disposal patterns:**

| Pattern | `JsonlStorageSink` | `ParquetStorageSink` |
|---------|--------------------|----------------------|
| Buffer type | `ConcurrentDictionary<string, MarketEventBuffer>` | `ConcurrentDictionary<string, MarketEventBuffer>` |
| Flush gate | `SemaphoreSlim(1, 1)` | `SemaphoreSlim(1, 1)` |
| Timer flush | `Timer(_ => FlushAllBuffersSafelyAsync())` | `Timer(_ => FlushAllBuffersSafelyAsync())` |
| Dispose step 1 | `Interlocked.Exchange(ref _disposed, 1)` | `Interlocked.Exchange(ref _disposed, 1)` |
| Dispose step 2 | `_disposalCts.Cancel()` | `_disposalCts.Cancel()` |
| Dispose step 3 | `await _flushTimer.DisposeAsync()` | `await _flushTimer.DisposeAsync()` |
| Dispose step 4 | Gate-protected final flush | Gate-protected final flush |
| Dispose step 5 | Clear + dispose resources | Clear + dispose resources |

The disposal sequence is **step-for-step identical** across both sinks (JSONL lines 297-358, Parquet lines 445-484).

**Remedy:**

1. Create `CompositeSink`:
   ```csharp
   public sealed class CompositeSink : IStorageSink
   {
       private readonly IReadOnlyList<IStorageSink> _sinks;

       public CompositeSink(IEnumerable<IStorageSink> sinks)
           => _sinks = sinks.ToList();

       public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
       {
           foreach (var sink in _sinks)
               await sink.AppendAsync(evt, ct);
       }

       public async Task FlushAsync(CancellationToken ct = default)
           => await Task.WhenAll(_sinks.Select(s => s.FlushAsync(ct)));

       public async ValueTask DisposeAsync()
       {
           foreach (var sink in _sinks)
               await sink.DisposeAsync();
       }
   }
   ```

2. Optionally extract shared buffering into `BufferedSinkBase` (deferred per existing improvement #5 in `docs/IMPROVEMENTS.md`).

3. Register in DI:
   ```csharp
   services.AddSingleton<IStorageSink>(sp =>
   {
       var jsonl = new JsonlStorageSink(/* ... */);
       var parquet = new ParquetStorageSink(/* ... */);
       return new CompositeSink(new IStorageSink[] { jsonl, parquet });
   });
   ```

**Files:** New `Storage/Sinks/CompositeSink.cs`, `ServiceCompositionRoot.cs:517-529`, `EventPipeline.cs:33`

**Benefit:** Multi-format storage without pipeline changes. New sinks (CSV, database, cloud blob, real-time analytics) can be added by registering them in DI. Format migration becomes incremental — run both old and new sinks in parallel, validate, then remove the old one.

---

## C. Functionality & Feature Completeness

### C1. Eliminate WPF/UWP Service Duplication

**Problem:** 25-30 services are nearly identical between `MarketDataCollector.Wpf/Services/` (43 files) and `MarketDataCollector.Uwp/Services/` (29 files). A detailed comparison of four representative service pairs reveals the pattern.

**Evidence — `ThemeService` comparison:**

| Aspect | WPF (195 LOC) | UWP (203 LOC) |
|--------|---------------|---------------|
| Singleton pattern | `Lazy<ThemeService>` (line 12) | Lock-based double-check (lines 23-36) |
| Theme application | `ResourceDictionary` URIs (lines 88-109) | `RequestedTheme` property (lines 107-118) |
| Theme storage | In-memory only | `ApplicationData.Current.LocalSettings` (lines 136-152) |
| System theme | Not supported | `UISettings` detection (lines 120-134) |
| `ThemeChangedEventArgs` | Lines 172-193 | Lines 198-202 — **nearly identical** |
| Toggle logic | Lines 23-59 | Lines 75-81 — **identical algorithm** |

**Evidence — `ConnectionService` comparison:**

| Aspect | WPF (441 LOC) | UWP (501 LOC) |
|--------|---------------|---------------|
| Reconnect strategy | Fixed delay array: `[1, 2, 5, 10, 30]`s (line 39) | Exponential backoff + 10% jitter (lines 340-357) |
| Health check | Direct `_httpClient.GetAsync("/healthz")` (line 185) | Via `_apiClient.CheckHealthAsync()` (line 196) |
| State machine | Events only | Events + `_notificationService.NotifyConnectionStatusAsync()` |
| Async patterns | `void OnMonitoringTimerElapsed()` (line 168) | Proper async callback (lines 128-141) |

WPF uses a simpler but less robust reconnection algorithm. UWP has better async patterns and notification integration. **Neither benefits from the other's improvements.**

**Evidence — `NotificationService` helper methods are character-for-character identical:**

WPF `FormatDuration()` (lines 319-326) and UWP `FormatDuration()` (lines 349-356):
```csharp
private static string FormatDuration(TimeSpan duration) => duration switch
{
    { TotalDays: >= 1 } => $"{duration.TotalDays:F1} days",
    { TotalHours: >= 1 } => $"{duration.TotalHours:F1} hours",
    { TotalMinutes: >= 1 } => $"{duration.TotalMinutes:F0} minutes",
    _ => $"{duration.TotalSeconds:F0} seconds"
};
```

WPF `FormatBytes()` (lines 328-339) and UWP `FormatBytes()` (lines 358-369) — also identical.

Quiet hours calculation logic is also identical in both (WPF lines 302-317, UWP lines 332-347).

**Evidence — `ConfigService` divergence:**

| Method | WPF | UWP |
|--------|-----|-----|
| `InitializeAsync()` | Returns `Task.CompletedTask` (stub, line 85) | Real initialization (lines 311-319) |
| `ValidateConfigAsync()` | Returns stub result (line 94) | 13 real validation checks (lines 324-420) |
| `GetConfigAsync<T>()` | Returns empty `new T()` (line 192) | Real deserialization |
| Symbol CRUD | Not implemented | Full implementation (lines 109-140) |

WPF `ConfigService` is largely a **skeleton** while UWP has a production-ready implementation.

**Estimated shareable code across 4 analyzed pairs:**

| Service | Shareable LOC | Platform-specific LOC |
|---------|---------------|----------------------|
| ThemeService | ~130 | ~70 each |
| ConfigService | ~140 (load/save) | ~120 each (UI, persistence) |
| ConnectionService | ~200 (state machine) | ~100 each (reconnect, health check) |
| NotificationService | ~200 (types, helpers, quiet hours) | ~150 each (display mechanism) |
| **Subtotal (4 services)** | **~670** | **~440 per platform** |
| **Extrapolated (25 services)** | **~4,000** | **~2,500 per platform** |

**Remedy:**

1. Promote WPF's 15 interface definitions (`IThemeService`, `IConfigService`, etc.) to `MarketDataCollector.Ui.Services`.

2. Extract shared logic into platform-agnostic base classes:
   ```csharp
   // In Ui.Services (shared)
   public abstract class ThemeServiceBase : IThemeService
   {
       public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
       public AppTheme CurrentTheme { get; protected set; }

       public void ToggleTheme() { /* shared logic */ }
       protected abstract void ApplyThemePlatform(AppTheme theme);
       protected abstract AppTheme LoadSavedTheme();
   }
   ```

3. Keep thin platform adapters in WPF/UWP:
   ```csharp
   // In Wpf project
   public sealed class WpfThemeService : ThemeServiceBase
   {
       protected override void ApplyThemePlatform(AppTheme theme)
           => Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = ... };
   }
   ```

4. Adopt UWP's superior implementations where they exist (exponential backoff in `ConnectionService`, real validation in `ConfigService`).

**Files:** `Wpf/Services/*.cs` (43 files), `Uwp/Services/*.cs` (29 files), `Ui.Services/` (shared target)

**Benefit:** Eliminates ~4,000+ lines of duplicated code. Bug fixes and improvements propagate to both platforms automatically. WPF gains UWP's superior reconnection, validation, and notification integration. UWP gains WPF's interface definitions.

---

### C2. Consolidate Configuration Validation

**Problem:** Configuration validation is spread across three separate classes with overlapping responsibilities and inconsistent application.

**Evidence — triple validation redundancy:**

| Check | `ConfigValidationHelper` | `ConfigValidatorCli` | `PreflightChecker` |
|-------|:----:|:----:|:----:|
| JSON syntax valid | Implicit (FluentValidation) | Explicit `JsonSerializer.Deserialize` (line 50) | Not checked |
| DataRoot path valid | `BeValidPath()` rule (line 128) | Implicit in validator | `CheckDiskSpace()` (line 125) |
| Alpaca credentials | FluentValidation rules (lines 151-176) | Rules + env var hints (lines 224-237) | `CheckEnvironmentVariables()` (line 455) |
| Storage config | FluentValidation rules (lines 344-404) | Implicit | Not checked |
| Disk space | Not checked | Not checked | `CheckDiskSpace()` (lines 125-188) |
| File write access | Not checked | Not checked | `CheckFilePermissions()` (lines 190-278) |
| Network connectivity | Not checked | Not checked | `CheckNetworkConnectivityAsync()` (lines 280-358) |
| Environment variables | Not checked | Hints only (lines 220-311) | Checks presence (lines 455-518) |

**Evidence — `ConfigValidationHelper` has two validator classes:**

```csharp
// Lines 81-146
public sealed class AppConfigValidator : AbstractValidator<AppConfig> { /* basic rules */ }

// Lines 148-437
public sealed class ExtendedAppConfigValidator : AbstractValidator<AppConfig> { /* comprehensive rules */ }
```

`ValidateAndLog()` uses `AppConfigValidator`; the overload with error collection uses `ExtendedAppConfigValidator`. Which one runs depends on which overload the caller uses.

**Evidence — `ConfigValidatorCli` duplicates validation + adds formatting:**

```csharp
// Lines 27-129
public int Validate(string configPath)
{
    // Step 1: Check file exists
    // Step 2: Parse JSON
    // Step 3: Validate using ExtendedAppConfigValidator (same as ConfigValidationHelper)
    // Step 4: Print errors with ╔══ box drawing and color
    // Step 5: Print suggestions via GetSuggestionForError()
}
```

This reruns the same `ExtendedAppConfigValidator` but adds CLI-specific formatting and contextual suggestions.

**Evidence — `PreflightChecker` validates at the system level:**

```csharp
// Lines 34-85
public async Task<PreflightResult> RunChecksAsync(string dataRoot, CancellationToken ct)
{
    checks.Add(CheckDiskSpace(dataRoot));                    // >= 1 GB free
    checks.Add(CheckFilePermissions(dataRoot));              // write test file
    checks.Add(await CheckNetworkConnectivityAsync(ct));     // TCP to 8.8.8.8:53 + HTTPS
    checks.Add(CheckMemoryAvailability());                   // >= 256 MB
    checks.Add(CheckSystemTime());                           // UTC year >= 2020
    checks.Add(CheckEnvironmentVariables());                 // PATH, API keys
}
```

No overlap with FluentValidation rules — different concern entirely. But environment variable checking overlaps with `ConfigValidatorCli`'s hints.

**Remedy:**

1. Define a validation pipeline interface:
   ```csharp
   public interface IConfigValidator
   {
       string Name { get; }
       Task<IReadOnlyList<ValidationIssue>> ValidateAsync(AppConfig config, CancellationToken ct);
   }

   public record ValidationIssue(
       ValidationSeverity Severity, string Property, string Message, string? Suggestion);
   ```

2. Implement three validators in pipeline order:
   - `SchemaValidator` — wraps `ExtendedAppConfigValidator` (FluentValidation)
   - `EnvironmentValidator` — checks env vars, disk, permissions, memory
   - `ConnectivityValidator` — checks network and provider endpoints

3. `ConfigValidatorCli` becomes a **formatter** that takes `ValidationIssue[]` and renders them with box-drawing, color, and suggestions.

4. `PreflightChecker` becomes a **runner** that executes the pipeline and returns `PreflightResult`.

5. Delete `AppConfigValidator` (basic) and keep only `ExtendedAppConfigValidator` (comprehensive).

**Files:** `Application/Config/ConfigValidationHelper.cs:81-437`, `Application/Config/ConfigValidatorCli.cs:27-335`, `Application/Services/PreflightChecker.cs:34-589`

**Benefit:** Single validation pipeline with clear ordering. Easy to add new validators (e.g., schema compatibility check). CLI and programmatic callers get the same results with different formatting. No more two `AbstractValidator` classes for the same type.

---

### C3. Expose Drop Statistics and Quality Metrics via API

**Problem:** `DroppedEventAuditTrail` collects detailed drop statistics but doesn't expose them via HTTP. The `DataQualityMonitoringService` computes completeness, gap, and anomaly metrics internally but several quality endpoints remain stubs.

**Evidence — drop tracking exists but is not queryable:**

`DroppedEventAuditTrail` tracks per-symbol drop counts via `ConcurrentDictionary` and writes details to `_audit/dropped_events.jsonl`. But no endpoint serves this data.

**Evidence — stub endpoints in `StubEndpoints.cs`:**

The following quality-related routes return `501 Not Implemented`:
- `GET /api/quality/dashboard`
- `GET /api/quality/metrics`
- `GET /api/quality/completeness`
- `GET /api/quality/anomalies`
- `GET /api/quality/latency`
- `GET /api/quality/health`

Yet the services that compute these values exist and are running:
- `CompletenessScoreCalculator` — computes completeness scores
- `AnomalyDetector` — detects data anomalies
- `LatencyHistogram` — tracks latency distribution
- `DataQualityReportGenerator` — generates quality reports

**Remedy:**

1. Add `GET /api/quality/drops` and `GET /api/quality/drops/{symbol}` endpoints.

2. Wire existing quality services into stub endpoints:
   ```csharp
   app.MapGet("/api/quality/dashboard", (DataQualityMonitoringService quality) =>
       Results.Ok(quality.GetDashboardSnapshot()));

   app.MapGet("/api/quality/drops", (DroppedEventAuditTrail audit) =>
       Results.Ok(audit.GetStatistics()));
   ```

3. Include drop rate in the existing `/api/status` response body.

4. Expose quality health in the `/api/health` composite response.

**Files:** `Ui.Shared/Endpoints/StubEndpoints.cs` (replace stubs), `Application/Pipeline/DroppedEventAuditTrail.cs`, `Application/Monitoring/DataQuality/*.cs`

**Benefit:** Completes the observability story. Web dashboard and external monitoring can display data quality metrics in real time. Gap-aware consumers can query drop events programmatically.

---

## D. User Experience

### D1. Consolidate UWP Navigation to Match WPF Workspace Model

**Problem:** WPF has been organized into 5 workspaces (Monitor, Collect, Storage, Quality, Settings) with ~15 navigation items and a command palette (Ctrl+K). UWP still has 40+ pages in a flat navigation list with workspace headers but no actual collapsing — users see all pages simultaneously.

**Remedy:**

1. Group UWP `NavigationViewItem` elements into 5 workspace categories matching WPF.

2. Use `NavigationViewItem.MenuItems` for sub-pages within each workspace:
   ```xml
   <NavigationViewItem Content="Storage" Icon="HardDrive">
       <NavigationViewItem.MenuItems>
           <NavigationViewItem Content="Browser" Tag="DataBrowser" />
           <NavigationViewItem Content="Archive Health" Tag="ArchiveHealth" />
           <NavigationViewItem Content="Optimization" Tag="StorageOptimization" />
       </NavigationViewItem.MenuItems>
   </NavigationViewItem>
   ```

3. Reduce visible top-level navigation items from 40+ to ~15.

4. Port the command palette (Ctrl+K) to UWP using `ContentDialog` or `TeachingTip` with search.

**Files:** `Uwp/Views/MainPage.xaml`, `Uwp/Services/NavigationService.cs`

**Benefit:** Consistent cross-platform UX. Users switching between WPF and UWP find the same structure. Navigation cognitive load drops from 40+ items to 15.

---

### D2. Add a Unified CLI Help System with Examples

**Problem:** `HelpCommand` (252 lines) displays all flags in a single output. Users must read the entire output to find relevant flags. No contextual help, no examples, no sub-topic support.

**Remedy:**

1. Support `--help <topic>` for focused help:
   ```
   $ marketdata --help backfill

   BACKFILL - Historical Data Retrieval

     --backfill                    Run historical data backfill
     --backfill-provider <name>    Provider: stooq, alpaca, polygon, tiingo, yahoo, finnhub
     --backfill-symbols <list>     Comma-separated symbols
     --backfill-from <date>        Start date (YYYY-MM-DD)
     --backfill-to <date>          End date (YYYY-MM-DD)

   Examples:
     marketdata --backfill --backfill-provider stooq --backfill-symbols SPY,AAPL \
       --backfill-from 2024-01-01 --backfill-to 2024-12-31

     marketdata --backfill --backfill-provider alpaca --backfill-symbols MSFT
   ```

2. Default `--help` (no topic) shows a compact summary:
   ```
   Market Data Collector v1.6.1

   Usage: marketdata [options]

   Topics (use --help <topic> for details):
     backfill    Historical data retrieval
     symbols     Symbol management
     config      Configuration and validation
     storage     Storage and packaging
     monitor     Live monitoring and diagnostics
     setup       First-time setup and wizards

   Common:
     --ui                Run with web dashboard
     --dry-run           Validate without starting
     --quick-check       Fast health check
   ```

3. Draw examples from existing `docs/HELP.md` content.

**Files:** `Application/Commands/HelpCommand.cs`, `Application/Commands/CliArguments.cs`

**Benefit:** Users find relevant help in 2 seconds instead of scrolling through 252 lines. Copy-paste examples reduce trial-and-error. Topic-based help scales as new commands are added.

---

## Priority Matrix

| # | Improvement | Impact | Effort | Priority | LOC Impact |
|---|-----------|--------|--------|----------|------------|
| A1 | Unified provider registry | High | Medium | **P1** | -200 (dead code), +50 (registry wire) |
| A2 | Single DI composition path | High | Medium | **P1** | -150 (dead DI registrations) |
| A3 | WebSocket base class adoption | High | High | **P2** | -800 (duplication) |
| A4 | Injectable metrics | Medium | Low | **P1** | +30 (interface + adapter) |
| A5 | Standardized error handling | Medium | Medium | **P2** | Net neutral (convention, not LOC) |
| B1 | CLI argument parser | Low-Med | Low | **P2** | -92 (duplicate parsing) |
| B2 | HTTP endpoint integration tests | High | Medium | **P1** | +500 (new tests) |
| B3 | Infrastructure provider tests | High | High | **P2** | +1,000 (new tests) |
| B4 | Composite storage sink | Medium | Low | **P1** | +60 (CompositeSink) |
| C1 | WPF/UWP service deduplication | High | High | **P2** | -4,000 (shared extraction) |
| C2 | Consolidated config validation | Medium | Low | **P1** | -200 (merge validators) |
| C3 | Quality metrics API endpoints | Medium | Low | **P1** | +150 (wire existing services) |
| D1 | UWP navigation consolidation | Medium | Medium | **P3** | Net neutral (reorganize XAML) |
| D2 | Contextual CLI help | Low-Med | Low | **P2** | +100 (topic system) |

### Recommended Execution Order

**Phase 1 — Quick wins with high structural impact (1-2 weeks):**

1. **A4 — Injectable metrics** — Low effort, immediately unblocks pipeline testability
2. **C2 — Consolidated config validation** — Low effort, cleaner startup with single pipeline
3. **B4 — Composite storage sink** — Low effort, enables multi-format storage
4. **C3 — Quality metrics API** — Low effort, wires existing services to existing stub routes

**Phase 2 — Core architecture alignment (2-4 weeks):**

5. **A1 — Unified provider registry** — Eliminates triple-path creation, activates dormant ADR-005 infrastructure
6. **A2 — Single DI composition path** — Removes dead DI code, makes composition root the source of truth
7. **B2 — HTTP endpoint integration tests** — Regression safety for the growing API surface

**Phase 3 — Larger refactors with high payoff (4-8 weeks):**

8. **A3 — WebSocket base class adoption** — Eliminates ~800 lines of duplication, standardizes reconnection
9. **C1 — WPF/UWP service deduplication** — Eliminates ~4,000 lines, unifies platform behavior
10. **A5 + B1 + B3 + D1 + D2** — Remaining items in parallel as capacity allows

**Total estimated impact:** -5,200 LOC removed, +1,900 LOC added (tests + new interfaces), net -3,300 LOC with significantly improved modularity, testability, and extensibility.
