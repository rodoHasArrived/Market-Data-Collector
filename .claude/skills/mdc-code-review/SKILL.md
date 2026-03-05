---
name: mdc-code-review
description: >
  Code review and architecture compliance skill for the MarketDataCollector project — a .NET 9 / C# 13
  market data system with WPF desktop app, F# 8.0 domain models, real-time streaming pipelines, and
  tiered JSONL/Parquet storage. Use this skill whenever the user asks to review, audit, refactor, or
  improve C# or F# code from MarketDataCollector, or when they share .cs/.fs files and want feedback.
  Also trigger on: MVVM compliance, ViewModel extraction, code-behind cleanup, real-time performance,
  hot-path optimization, pipeline throughput, provider implementation review, backfill logic, data
  integrity validation, error handling patterns, test code quality, unit test review, ProviderSdk
  compliance, dependency violations, JSON source generator usage, hot config reload, or WPF architecture
  — even without naming the project. If code references MarketDataCollector namespaces, BindableBase,
  EventPipeline, IMarketDataClient, IStorageSink, or ProviderSdk types, use this skill.
---

# MarketDataCollector Code Review

## Bundled Resources

```
mdc-code-review/
├── SKILL.md                      ← you are here
├── agents/
│   └── grader.md                 ← assertions grader for evals; read when grading test runs
├── references/
│   ├── architecture.md           ← deep project context: solution layout (all 10 projects),
│   │                               expanded dependency graph, provider/backfill architecture,
│   │                               F# interop rules, testing conventions, ADRs — read when
│   │                               you need more detail than what's in this SKILL.md
│   └── schemas.md                ← JSON schemas for evals.json, grading.json, benchmark.json
├── evals/
│   └── evals.json                ← eval set (8 test cases with assertions)
├── eval-viewer/
│   ├── generate_review.py        ← launch the eval review viewer
│   └── viewer.html               ← the viewer HTML
└── scripts/
    ├── aggregate_benchmark.py    ← aggregate grading results into benchmark.json
    ├── run_eval.py               ← run a single eval
    ├── package_skill.py          ← package this skill into a .skill file
    └── utils.py                  ← shared utilities
```

**When to read `references/architecture.md`**: When you need the full solution layout (10 projects including ProviderSdk, FSharp, Contracts), the exact dependency rules (what can import what, including C#/F# interop boundaries), detailed core abstractions (`IMarketDataClient`, `IStorageSink`, `EventPipeline`, exception hierarchy), the backfill subsystem rules, data integrity validation rules, benchmark conventions, or the ADR quick-reference table. The sections in this SKILL.md are a sufficient summary for most reviews.

**When to use `evals/evals.json`**: When testing or iterating on this skill. Run evals and use `generate_review.py` to surface results.

---

A unified code review skill that catches architecture violations, performance anti-patterns, error handling gaps, test quality issues, and provider compliance problems in the MarketDataCollector codebase.

## Context: What This Project Is

MarketDataCollector is a high-throughput .NET 9 / C# 13 system (with F# 8.0 domain models) that captures real-time market microstructure data (trades, quotes, L2 order books) from multiple providers (Alpaca, Polygon, Interactive Brokers, StockSharp, NYSE) and persists it via a backpressured pipeline to JSONL/Parquet storage with WAL durability. It also supports historical backfill from 10+ providers (Yahoo Finance, Stooq, Tiingo, Alpha Vantage, Finnhub, etc.) with automatic failover chains. It has a WPF desktop app (recommended), a legacy UWP app (Windows 10+ only, deprecated for new development), and a web dashboard — all sharing services through a layered architecture.

**Key facts for reviewers:**
- **734 source files**: 717 C#, 17 F#, 85 test files
- **WPF is the primary desktop target.** UWP is legacy — flag any new UWP-targeted code or WinRT dependency introduction into shared projects.
- The project already has strong backend patterns — bounded channels, Write-Ahead Logging, batched flushing, backpressure signals. The primary area for improvement is the WPF desktop layer, where business logic has accumulated in XAML code-behind files instead of proper ViewModels.
- There is a dedicated `MarketDataCollector.ProviderSdk` project with clean interfaces for provider implementations.
- F# domain models in `MarketDataCollector.FSharp` require attention at C#/F# interop boundaries.

## How to Review Code

When the user shares code or asks for a review, work through these six lenses in order. Not all lenses apply to every file — use judgment to skip lenses that are irrelevant (e.g., don't apply Lens 1 to a pipeline service class, don't apply Lens 5 to a ViewModel).

### Lens 1: MVVM Architecture Compliance

The goal is separation of concerns: Views should be thin XAML + minimal code-behind; ViewModels should own state, commands, and orchestration; Services handle data access and business logic.

**What to look for in code-behind files (.xaml.cs):**

1. **Business logic in code-behind** — Any computation, data transformation, rate calculation, string formatting of domain data, or conditional logic beyond simple UI toggling belongs in a ViewModel or Service. The DashboardPage.xaml.cs in this project is a canonical example: it calculates event rates, formats numbers, manages timers, tracks state like `_previousPublished` and `_isCollectorPaused` — all of which should live in a ViewModel.

2. **Direct UI element manipulation** — Code like `PublishedCount.Text = FormatNumber(status.Published)` is a red flag. Properties should be data-bound to ViewModel properties. Named element access (`x:Name`) in code-behind beyond `InitializeComponent()` and event wireup is a smell.

3. **Service injection into Pages** — When a Page constructor takes 5+ service dependencies, that's a sign the Page is doing ViewModel work. Services should be injected into ViewModels; Pages should receive their ViewModel.

4. **Event handler bloat** — Click handlers that do more than delegate to a command (e.g., `StartCollector_Click` that contains `try/catch` and calls multiple services) should be replaced with `ICommand` implementations bound in XAML.

5. **Nested model classes in code-behind** — Classes like `DashboardActivityItem`, `SymbolPerformanceItem` defined inside a Page class should be extracted to the Models folder or to the ViewModel file.

6. **Timer management in Views** — `DispatcherTimer` setup and tick handlers in code-behind should move to the ViewModel, ideally using `System.Threading.Timer` or `PeriodicTimer` with dispatcher marshaling only at the binding layer.

**The BindableBase pattern:**
The project has a `BindableBase` class in `Wpf/ViewModels/` with `SetProperty<T>` and `RaisePropertyChanged`. All new ViewModels should inherit from this. The pattern is:

```csharp
public class DashboardViewModel : BindableBase
{
    private string _publishedCount = "0";
    public string PublishedCount
    {
        get => _publishedCount;
        private set => SetProperty(ref _publishedCount, value);
    }
}
```

**Dependency rules to enforce:**
- ✅ WPF host → Ui.Services, Contracts
- ✅ ViewModels → Ui.Services, Contracts, Core.Models
- ✅ Ui.Services → Contracts, Core.Models, Pipeline
- ✅ ProviderSdk → Contracts only
- ✅ FSharp → Contracts only
- ✅ Contracts → nothing (leaf project — no upstream dependencies)
- ✅ Pipeline → Contracts, Infrastructure
- ❌ Ui.Services → WPF host types (no reverse dependency)
- ❌ Ui.Shared → WPF-only APIs (platform leak)
- ❌ Host-to-host (Wpf ↔ Web)
- ❌ Core/Contracts → Infrastructure (dependency inversion violation)
- ❌ Any shared project → UWP-specific or WinRT APIs (UWP is legacy)
- ❌ ProviderSdk → anything except Contracts (keep the SDK thin)

### Lens 2: Real-Time Performance

This project has millisecond-accuracy requirements for market data capture. Performance issues in the UI layer can starve the pipeline or cause dropped events.

**What to look for:**

1. **Blocking calls on the UI thread** — Any synchronous I/O, `Task.Result`, `Task.Wait()`, `.GetAwaiter().GetResult()` on the dispatcher thread. Also watch for `Dispatcher.Invoke` (synchronous) when `Dispatcher.InvokeAsync` (asynchronous) would suffice.

2. **Allocations in hot paths** — In code that runs per-tick or per-event:
   - String interpolation in logging (use structured logging: `_logger.LogInformation("Received {Count} bars", count)`)
   - LINQ queries that allocate (`.ToList()`, `.Select()`, `.Where()`) where a simple loop would work
   - Boxing of value types
   - Creating new `ObservableCollection<T>` or list instances when updating existing ones
   - Repeated `FindResource()` calls — cache brush/resource lookups

3. **Improper async/await patterns:**
   - `async void` methods beyond event handlers (these swallow exceptions)
   - Missing `ConfigureAwait(false)` in library/service code (though in ViewModel code that updates UI, `ConfigureAwait(true)` is correct)
   - Not passing `CancellationToken` through async chains
   - Fire-and-forget tasks without error handling

4. **Data binding inefficiencies:**
   - Raising `PropertyChanged` for properties that haven't actually changed (the `SetProperty` pattern in BindableBase handles this, but manual `RaisePropertyChanged` calls may not)
   - Updating many bound properties individually when a batch update pattern would reduce layout passes
   - `ObservableCollection` modifications in a loop without using batch operations

5. **Channel and pipeline concerns:**
   - Unbounded channels or queues (should use `BoundedChannel` with backpressure policy)
   - Missing `BoundedChannelFullMode` specification (project standard is `DropOldest`)
   - Large batch sizes without configurable limits
   - Missing flush timeouts on shutdown paths

6. **Thread safety:**
   - Shared mutable state without synchronization
   - `volatile` misuse (it doesn't guarantee atomicity for compound operations)
   - Lock contention in paths that should be lock-free
   - Non-thread-safe collection access from multiple threads

7. **JSON serialization compliance (ADR-014):**
   - All serializable types must have `[JsonSerializable(typeof(T))]` on a source-generator context class
   - Flag any use of `JsonSerializer.Serialize<T>()` or `JsonSerializer.Deserialize<T>()` without a `JsonSerializerContext` — this falls back to runtime reflection
   - The correct pattern: `JsonSerializer.Serialize(value, MyJsonContext.Default.MyType)`
   - Flag `[JsonPropertyName]` on types that aren't registered in a `JsonSerializerContext`

8. **Hot configuration reload:**
   - Code that reacts to runtime config changes must use `IOptionsMonitor<T>` (not `IOptions<T>`)
   - Flag code that reads config at startup and caches the value in a field without subscribing to `OnChange()`
   - Exception: truly static config (connection strings, storage root paths) can use `IOptions<T>`
   - Timer intervals, subscription lists, and provider settings must be reloadable

### Lens 3: Error Handling & Resilience

MarketDataCollector must handle provider disconnections, rate limits, data corruption, and shutdown gracefully. Review error handling for correctness and completeness.

**What to look for:**

1. **Exception hierarchy compliance** — All domain exceptions must derive from `MarketDataException`. Flag:
   - `throw new Exception(...)` or `throw new InvalidOperationException(...)` for domain errors
   - `throw new ApplicationException(...)` (never appropriate)
   - Catch blocks that catch `Exception` and don't rethrow or handle specifically
   - Missing exception context (inner exception not passed to constructor)

2. **Provider resilience patterns:**
   - Missing reconnection logic in `IMarketDataClient` implementations — providers must reconnect on transient failures with exponential backoff and jitter
   - Missing `CancellationToken` propagation — every async method in the provider chain must accept and forward `ct`
   - `DisposeAsync` must cancel outstanding operations before disposing resources
   - Rate limit handling: providers must catch `RateLimitException` and respect `RetryAfter`, not self-throttle with `Task.Delay()`

3. **Shutdown path completeness:**
   - Every `IAsyncDisposable` must flush buffers before disposing
   - Pipeline shutdown must drain the bounded channel (with a timeout)
   - WAL must be flushed and closed before application exit
   - Missing `finally` blocks in long-running loops (ingest, flush)

4. **Failover chain correctness (backfill):**
   - Backfill code must catch `ProviderException` and fall through to the next provider in `ProviderPriority`
   - Must not catch `OperationCanceledException` as a provider failure (it means the user cancelled)
   - Failover logging must include which provider failed and which is being tried next

5. **Defensive coding:**
   - Null checks at public API boundaries (especially methods receiving data from providers)
   - Guard clauses for invalid arguments (empty symbol lists, negative counts, zero timeouts)
   - Timeout enforcement on all external calls (provider connections, HTTP requests)

### Lens 4: Test Code Quality

The project has 85 test files. Review test code for correctness, maintainability, and coverage of the patterns that matter most.

**What to look for:**

1. **Test naming convention** — Must follow `MethodName_Scenario_ExpectedResult`:
   - ✅ `ConnectAsync_InvalidApiKey_ThrowsProviderAuthException`
   - ✅ `WriteAsync_ChannelFull_DropsOldestEvent`
   - ❌ `TestConnection` (too vague)
   - ❌ `ShouldWork` (meaningless)

2. **Arrange-Act-Assert structure** — Each test should have clearly separated sections with `// Arrange`, `// Act`, `// Assert` comments. Flag tests that mix all three or have assertions scattered throughout.

3. **Async test patterns:**
   - `async Task` test methods, never `async void` (xUnit/NUnit won't await `async void`)
   - Must pass `CancellationToken` to async methods under test (use `TestContext` or `CancellationTokenSource` with timeout)
   - No `Task.Delay()` for timing — use `TaskCompletionSource`, `SemaphoreSlim`, or test-specific synchronization
   - No `Thread.Sleep()` ever

4. **Mock and fake usage:**
   - Prefer explicit fakes/stubs for core interfaces (`IMarketDataClient`, `IStorageSink`) over heavy mocking frameworks
   - Mocks should verify behavior (method called with correct args), not implementation details (internal method call order)
   - Flag tests that mock the class under test

5. **Channel and pipeline testing:**
   - Tests for bounded channel behavior must verify backpressure (what happens when full)
   - Tests for pipeline flush must verify data integrity (all events written, correct order)
   - Tests for shutdown must verify graceful drain with timeout

6. **Test isolation:**
   - No shared mutable state between tests (static fields, shared collections)
   - Each test must create its own instances — flag `[ClassInitialize]` or `[TestInitialize]` that creates shared state
   - File-based tests must use unique temp directories and clean up

### Lens 5: Provider Implementation Compliance

Provider implementations in `Infrastructure/` must follow the `ProviderSdk` contracts consistently.

**What to look for:**

1. **Interface completeness** — Every provider must implement `IMarketDataClient` fully:
   - `ConnectAsync` with proper auth and `CancellationToken`
   - `SubscribeAsync` with symbol validation
   - `StreamEventsAsync` as a proper `IAsyncEnumerable<MarketEvent>` (not a wrapper around `Task<List<>>`)
   - `DisposeAsync` with cleanup (cancel tokens, close connections, flush)

2. **Rate limit tracking** — Must use `ProviderRateLimitTracker` from ProviderSdk:
   - No `Task.Delay(1000)` or similar self-throttling
   - Must call `tracker.WaitIfNeededAsync(ct)` before each request
   - Rate limit config must come from `IOptionsMonitor<T>`, not hardcoded

3. **Reconnection logic:**
   - Must implement exponential backoff with jitter (not fixed delay)
   - Must emit `ProviderStatus.Reconnecting` status during reconnection
   - Must log reconnection attempts with attempt count and delay
   - Must respect `CancellationToken` during backoff waits

4. **Data mapping correctness:**
   - Provider-specific DTOs must be mapped to `MarketEvent` at the boundary
   - Timestamps must be converted to UTC
   - Symbol normalization must happen at the provider boundary
   - Sequence numbers must be preserved for integrity checking downstream

5. **UWP contamination check:**
   - Flag any `Windows.*` namespace imports in provider code
   - Flag any WinRT interop in shared infrastructure code
   - Flag any conditional compilation for UWP (`#if WINDOWS_UWP`) in shared code — UWP paths should not be expanding

### Lens 6: Cross-Cutting Concerns

These apply to any file in the project.

**What to look for:**

1. **Dependency rules** — See the dependency graph in Lens 1. Additionally:
   - `Contracts` is a leaf project — it must have zero `<ProjectReference>` items
   - `ProviderSdk` must reference only `Contracts`
   - `FSharp` must reference only `Contracts`

2. **C# ↔ F# interop boundaries:**
   - C# code consuming F# discriminated unions must handle all cases (exhaustive matching)
   - C# code receiving F# `option<T>` must convert to nullable at the boundary
   - Nullable reference type annotations must not be assumed to hold across the F# boundary

3. **Benchmark code (when reviewing `benchmarks/` files):**
   - Must have `[MemoryDiagnoser]` attribute on the benchmark class
   - Setup logic in `[GlobalSetup]`, not in `[Benchmark]` methods
   - At least one `[Benchmark(Baseline = true)]` for comparison
   - No I/O or network calls in benchmarked methods
   - Hot-path benchmarks should target 0 bytes allocated

## Review Output Format

Structure your review as a C# file with the refactored/corrected code, preceded by a summary comment block. For pure review (no refactor requested), output as markdown with categorized findings.

**For refactoring requests**, produce a complete, compilable C# file:

```csharp
// =============================================================================
// REVIEW SUMMARY
// =============================================================================
// File: DashboardViewModel.cs (extracted from DashboardPage.xaml.cs)
// 
// MVVM Findings:
//   [M1] Extracted business logic from code-behind to ViewModel
//   [M2] Replaced direct UI manipulation with bindable properties  
//   [M3] Converted click handlers to ICommand (RelayCommand)
//   [M4] Moved timer management to ViewModel with PeriodicTimer
//
// Performance Findings:
//   [P1] Cached FindResource() brush lookups as static fields
//   [P2] Replaced Dispatcher.Invoke with InvokeAsync where possible
//   [P3] Added CancellationToken propagation to async methods
//
// Error Handling Findings:
//   [E1] Replaced bare Exception with ProviderException
//   [E2] Added reconnection logic with exponential backoff
//
// Test Findings:
//   [T1] Renamed test methods to follow naming convention
//   [T2] Added CancellationToken timeout to async tests
//
// Provider/Backfill Findings:
//   [B1] Added rate limit tracking via ProviderRateLimitTracker
//   [B2] Switched from IOptions<T> to IOptionsMonitor<T> for hot reload
//
// Data Integrity Findings:
//   [D1] Added sequence gap detection before storage write
//
// Breaking Changes: None — existing XAML bindings need updating to match
// new property names (see binding migration notes below).
// =============================================================================

namespace MarketDataCollector.Wpf.ViewModels;
// ... refactored code
```

**For review-only requests**, produce categorized findings:

```
## MVVM Compliance
- **[M1] CRITICAL**: Business logic in code-behind (line 42-67) — rate calculation belongs in ViewModel
- **[M2] WARNING**: 5 service dependencies injected into Page constructor

## Real-Time Performance  
- **[P1] CRITICAL**: Dispatcher.Invoke (synchronous) in OnLiveStatusReceived — use InvokeAsync
- **[P2] WARNING**: FindResource() called on every status update — cache brushes

## Error Handling & Resilience
- **[E1] CRITICAL**: Bare `catch (Exception)` swallows pipeline errors — use specific exception types
- **[E2] WARNING**: DisposeAsync missing flush of pending events

## Test Quality
- **[T1] WARNING**: async void test method — xUnit won't await this, test silently passes
- **[T2] INFO**: Test name "TestProcess" — use MethodUnderTest_Scenario_ExpectedBehavior pattern

## Provider & Backfill Compliance
- **[B1] CRITICAL**: No rate limit handling — will get API key banned at scale
- **[B2] WARNING**: IOptions<T> cached at startup — use IOptionsMonitor<T> for hot reload

## Data Integrity
- **[D1] WARNING**: No sequence validation on incoming trades — gaps will go undetected

## Conventions
- **[C1] INFO**: String interpolation in log call (line 89) — use structured logging
- **[C2] INFO**: JsonSerializer.Serialize without source-generated context — violates ADR-014
```

Severity levels:
- **CRITICAL**: Will cause bugs, data loss, or significant performance degradation
- **WARNING**: Architectural violation or performance concern that should be addressed
- **INFO**: Style/convention deviation, minor improvement opportunity

## Project-Specific Conventions to Enforce

These come directly from the project's CLAUDE.md and architecture docs:

**Naming & style:**
- Async methods must end with `Async` suffix
- CancellationToken parameter named `ct` or `cancellationToken`
- Private fields prefixed with `_`
- Interfaces prefixed with `I`
- Structured logging with semantic parameters (never string interpolation)

**Architecture patterns:**
- Use `System.Threading.Channels` for producer-consumer patterns
- Prefer `Span<T>` and `Memory<T>` for buffer operations
- Use custom exception types from `Core/Exceptions/` (not bare `Exception`)
- Follow ADR decisions in `docs/adr/` (especially ADR-004 async streaming, ADR-013 bounded channels, ADR-014 JSON source generators, ADR-017 no business logic in WPF layer, ADR-021 configurable UI refresh via `IOptions<UiSettings>`)

**Serialization (ADR-014):**
- All JSON serialization must use source generators: `[JsonSerializable(typeof(MyType))]` on a `JsonSerializerContext`
- Never call `JsonSerializer.Serialize<T>(obj)` without passing a source-generated context — this uses reflection
- New DTOs must be registered in the project's `JsonSerializerContext` partial class

**Hot config reload:**
- Use `IOptionsMonitor<T>` (not `IOptions<T>`) for any setting that can change at runtime
- Symbol subscriptions, refresh intervals, and provider settings are all hot-reloadable
- Flag any code that reads config once at startup and caches the value for settings that should be live

**UWP deprecation:**
- WPF is the recommended desktop target; UWP is legacy (Windows 10 build 19041+ required)
- Flag any new code targeting UWP, any WinRT dependency introduced into shared projects
- `Ui.Shared` and `Ui.Services` must remain platform-neutral (no WPF or UWP APIs)

**F# interop:**
- F# domain models are in `MarketDataCollector.FSharp` (17 files)
- C# consumers must handle `FSharpOption<T>` properly (not just null-check)
- F# record types are immutable — don't attempt property setters from C#
- Discriminated unions require pattern matching, not type-casting

**BenchmarkDotNet (benchmarks/ directory):**
- `[Benchmark]` methods should be minimal — no setup logic inside benchmarked methods
- Use `[GlobalSetup]` / `[IterationSetup]` for initialization
- Always include a `[Benchmark(Baseline = true)]` for comparison
- Don't allocate in benchmark methods unless measuring allocation

---

## Running Evals (for skill development)

To test or improve this skill using the bundled eval set:

**1. Run a test case manually** (Claude.ai — no subagents):
Read `evals/evals.json`, pick a prompt, follow this skill's instructions to produce the review output, save to a workspace dir.

**2. Grade the output**:
Read `agents/grader.md` and evaluate the assertions from `evals/evals.json` against the output. Save results to `grading.json` alongside the output.

**3. View results**:
```bash
python eval-viewer/generate_review.py \
  --workspace <path-to-workspace>/iteration-1 \
  --skill-name mdc-code-review \
  --static /tmp/mdc_review.html
```
Then open `/tmp/mdc_review.html` in a browser.

**4. Aggregate benchmark**:
```bash
python -m scripts.aggregate_benchmark <workspace>/iteration-1 --skill-name mdc-code-review
```

**5. Package the skill** when done:
```bash
python scripts/package_skill.py /tmp/mdc-code-review
```

See `references/schemas.md` for full JSON schemas.

