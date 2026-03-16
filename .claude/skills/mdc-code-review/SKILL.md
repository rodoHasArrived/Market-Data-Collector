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

> **GitHub Copilot / Actions equivalent:** [`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md) — same 6-lens framework as a GitHub agent definition.
> **Navigation index:** [`docs/ai/skills/README.md`](../../../docs/ai/skills/README.md)

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

MarketDataCollector is a high-throughput .NET 9 / C# 13 system (with F# 8.0 domain models) that captures real-time market microstructure data (trades, quotes, L2 order books) from multiple providers (Alpaca, Polygon, Interactive Brokers, StockSharp, NYSE) and persists it via a backpressured pipeline to JSONL/Parquet storage with WAL durability. It also supports historical backfill from 10+ providers (Yahoo Finance, Stooq, Tiingo, Alpha Vantage, Finnhub, etc.) with automatic failover chains. It has a WPF desktop app (recommended) and a web dashboard — sharing services through a layered architecture.

**Key facts for reviewers:**
- **704 source files**: 692 C#, 12 F#, 241 test files
- **WPF is the primary desktop target.** UWP was removed — flag any WinRT dependency introduction into shared projects.
- The project has strong backend patterns — bounded channels, Write-Ahead Logging, batched flushing, backpressure signals. The primary area for improvement is the WPF desktop layer, where business logic has accumulated in XAML code-behind files instead of proper ViewModels.
- There is a dedicated `MarketDataCollector.ProviderSdk` project with clean interfaces for provider implementations.
- F# domain models in `MarketDataCollector.FSharp` require attention at C#/F# interop boundaries.

## How to Review Code

**Canonical review framework:** Read [`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md) for the complete 6-lens review methodology. The lenses are:

1. **Lens 1: MVVM Architecture Compliance** — Thin code-behind, BindableBase pattern, dependency rules
2. **Lens 2: Real-Time Performance** — Blocking calls, hot-path allocations, channel policies, JSON source generators, hot config reload
3. **Lens 3: Error Handling & Resilience** — Exception hierarchy, provider resilience, shutdown paths, failover chains
4. **Lens 4: Test Code Quality** — Naming conventions, AAA structure, async patterns, test isolation
5. **Lens 5: Provider Implementation Compliance** — Interface completeness, rate limiting, reconnection, data mapping
6. **Lens 6: Cross-Cutting Concerns** — Dependency rules, C#/F# interop, benchmark conventions

Not all lenses apply to every file — use judgment to skip irrelevant lenses.

For the full details of each lens including specific patterns to flag, code examples, and dependency rules, see the [code-review-agent.md](../../../.github/agents/code-review-agent.md) file.

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

See [`.github/agents/code-review-agent.md`](../../../.github/agents/code-review-agent.md) § "Project-Specific Conventions to Enforce" for the full list. Key conventions:

- **Naming:** Async suffix, `ct`/`cancellationToken`, `_` prefix for fields, `I` prefix for interfaces, structured logging
- **Architecture:** `EventPipelinePolicy.*.CreateChannel<T>()` for channels, `Span<T>`/`Memory<T>` for buffers, custom exceptions from `Core/Exceptions/`, sealed classes by default
- **Serialization (ADR-014):** Source-generated `JsonSerializerContext` — never reflection-based serialization
- **Hot config:** `IOptionsMonitor<T>` for runtime-changeable settings
- **Desktop:** WPF only — UWP was removed; `Ui.Shared` and `Ui.Services` must stay platform-neutral
- **F# interop:** Handle `FSharpOption<T>` properly, pattern-match discriminated unions, no property setters on F# records
- **Benchmarks:** `[MemoryDiagnoser]`, `[GlobalSetup]`, `[Benchmark(Baseline = true)]`, zero-allocation targets

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

