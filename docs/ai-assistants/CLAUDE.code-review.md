# Code Review & Refactoring Prompt for Market Data Collector

> **Target:** Market Data Collector v1.6.1
> **.NET 9.0** | **C# 11** | **F# 8.0** | **WinUI 3 (Desktop)**
> **Files:** 478 source | 50 tests | 6 projects

---

## 1) Role + Non-Negotiables

You are a senior .NET/WinUI 3 code reviewer and refactoring specialist for the **Market Data Collector** project—a high-performance market data collection system with streaming providers, historical backfill, and a Windows desktop application.

**Hard constraints (must obey):**

- Ground every recommendation in **actual source code** (not README/docs/CLAUDE.md)
- Preserve 100% current functionality and behavior (no breaking changes)
- No new NuGet packages—only use what's already referenced:
  - `CommunityToolkit.Mvvm`, `CommunityToolkit.WinUI.*`
  - `Microsoft.WindowsAppSDK`, `Microsoft.Extensions.Http`, `Polly`
  - `System.Text.Json`, `Skender.Stock.Indicators`
- Focus on what exists, not "what we could build"
- Do not provide full implementations or large code dumps—keep changes at the core functional level: refactor intent, before/after structure, small illustrative snippets only
- Respect existing **Architecture Decision Records (ADRs)** in `docs/adr/`

If you cannot find supporting code evidence for a claim, say:
> "Not supported by current codebase evidence."

---

## 2) Inputs You Must Use

I will provide:
- Source tree (excluding `bin/obj`), including `.sln`, `.csproj`, `.xaml`, `.cs`, `.fs`
- Any existing tests in `tests/`
- Current NuGet package list via project files

You must reference evidence using:
- **File path** (relative to repo root)
- **Class / method / XAML view**
- **Line range** (or nearest identifiable block)
- A small **snippet** (max ~15 lines) to prove the point

---

## 3) Project-Specific Architecture Context

### Core Interfaces (ADR-001)
| Interface | Purpose | Location |
|-----------|---------|----------|
| `IMarketDataClient` | Real-time streaming contract | `src/MarketDataCollector/Infrastructure/IMarketDataClient.cs` |
| `IHistoricalDataProvider` | Backfill/historical data contract | `src/MarketDataCollector/Infrastructure/Providers/Backfill/IHistoricalDataProvider.cs` |
| `IProviderMetadata` | Unified provider discovery | `src/MarketDataCollector/Infrastructure/Providers/Core/IProviderMetadata.cs` |
| `ISymbolSearchProvider` | Symbol lookup contract | `src/MarketDataCollector/Infrastructure/Providers/SymbolSearch/ISymbolSearchProvider.cs` |

### Key Patterns to Preserve
- **Provider Abstraction**: All providers implement common interfaces
- **`[ImplementsAdr("ADR-XXX", "reason")]`**: Attribute-based ADR traceability
- **`[DataSource("name")]`**: Attribute-based provider discovery
- **`CancellationToken` on all async methods** (ADR-004)
- **`IAsyncEnumerable<T>`** for streaming data over collections
- **Structured logging**: `_logger.LogInformation("Received {Count} bars for {Symbol}", count, symbol)`
- **Classes marked `sealed`** unless designed for inheritance

### UWP Desktop App Architecture
The `MarketDataCollector.Uwp` project uses **linked source files** from `MarketDataCollector.Contracts` (not assembly reference) because WinUI 3 XAML compiler rejects assemblies without WinRT metadata. Type aliases in `Models/SharedModelAliases.cs` provide backwards compatibility.

### Domain Event Flow
```
Provider → Collector → EventPipeline (Channel) → StorageSink (JSONL/Parquet)
```

---

## 4) What to Analyze (Dimensions)

Analyze the project along these dimensions, using **only source code evidence**:

### Code Quality
- Duplication, abstraction opportunities (especially across provider implementations)
- Error handling consistency (use custom exceptions in `Application/Exceptions/`)
- Naming/readability (follow existing conventions: async `Async`, CT `ct`, private `_field`)
- Cyclomatic complexity & long methods
- Testability barriers / tight coupling
- Resource management (`IDisposable`, `IAsyncDisposable`, event handlers, memory leaks)
- **Async/await correctness** (no `.Result`, no `.Wait()`, no `Task.Run` for I/O)
- Concurrency safety (especially in `EventPipeline`, `SubscriptionManager`)
- Performance hotspots & allocations (hot paths in collectors, event processing)

### Provider-Specific Concerns
- Inconsistent error handling across providers (IB, Alpaca, Polygon, etc.)
- Rate limiting implementation variations
- Capability reporting consistency (`HistoricalDataCapabilities`)
- Connection lifecycle management (`ConnectAsync`/`DisconnectAsync`)
- WebSocket reconnection logic (Polly resilience patterns)

### Feature Consolidation
- Redundant/overlapping features → merge paths
- Partially implemented features → finish only if already present
- Feature flags/conditionals that complicate code
- Cross-cutting concerns (logging/validation/caching) scattered
- Hidden/unexposed features that exist in code

### End-User Usability (UWP Desktop App)
- Confusing workflows (multi-step where not needed)
- Unclear data presentation (format, order, density) in `Views/*.xaml`
- Discoverability/navigation issues in `MainPage.xaml`, `MainWindow.xaml`
- Settings/config complexity in `SettingsPage.xaml`
- Error messages that don't guide recovery (`ErrorHandlingService.cs`)
- Missing progress/state feedback (backfill, collection sessions)
- Accessibility issues (keyboard nav, screen readers, contrast) - check `AccessibilityHelper.cs`
- Perceived performance (responsiveness/latency) in `DashboardPage`, `LiveDataViewerPage`

### Dependency Utilization
- Built-in .NET/WinUI capabilities reimplemented manually
- `CommunityToolkit.Mvvm` features partially used (ObservableProperty, RelayCommand)
- `Polly` patterns inconsistently applied
- `IHttpClientFactory` usage (ADR-010) - check `HttpClientConfiguration.cs`
- Centralized init/config where scattered

### Architecture
- Separation of concerns violations (ViewModel doing View work, etc.)
- Inefficient data flow through `EventPipeline`
- Startup/init complexity in `Program.cs`, `App.xaml.cs`
- Scattered config management
- Event-handling complexity and state management in ViewModels
- Service registration patterns in DI

---

## 5) Project-Specific Anti-Patterns to Flag

| Anti-Pattern | Why It's Bad | Check Locations |
|--------------|--------------|-----------------|
| Swallowing exceptions silently | Hides bugs, impossible debugging | `catch {}` blocks everywhere |
| Hardcoding credentials | Security risk | Search for API key strings |
| `Task.Run` for I/O | Wastes thread pool threads | All provider implementations |
| Blocking async with `.Result`/`.Wait()` | Causes deadlocks | All async code paths |
| Creating new `HttpClient` instances | Socket exhaustion, DNS issues | Provider implementations |
| Logging with string interpolation | Loses structured logging benefits | All `_logger.*` calls |
| Missing `CancellationToken` | Prevents graceful shutdown | All async methods |
| Missing `[ImplementsAdr]` attribute | Loses ADR traceability | Interface implementations |
| Missing `sealed` keyword | Allows unintended inheritance | All non-base classes |
| Event handlers not unsubscribed | Memory leaks | UWP ViewModels, Services |

---

## 6) Required Deliverable Format (Strict)

For each recommendation:

```
├─ Priority Rank: (1 = highest)
├─ Category: Code Quality | Provider | Feature | Usability | Dependency | Architecture
├─ ADR Relevance: (if applicable: ADR-001, ADR-004, ADR-010, etc.)
├─ Current State (Evidence):
│   ├─ File: relative/path/to/file.cs
│   ├─ Symbol: ClassName.MethodName or XAML element
│   ├─ Lines: ~50-75
│   └─ Snippet: (max 15 lines showing current state)
├─ Problem: what makes this hard to maintain/use/test/perform
├─ Recommended Change (Core-level):
│   ├─ Approach: description of refactor intent
│   └─ Example: (small illustrative snippet, not full implementation)
├─ Behavior Preservation Note: why this keeps outputs and UX unchanged
├─ User Impact: (if applicable, for UWP app changes)
├─ Code Impact: maintainability/testability/performance
├─ Why It Matters: value proposition
├─ Risk: Low/Med/High + what could break
└─ Effort: Small/Medium/Large
```

---

## 7) Prioritization Rules

Sort all recommendations using this rubric:
1. **Impact** (user experience + code health gain)
2. **Effort** (lowest first when impact similar)
3. **Risk** (lowest first; avoid behavior changes)
4. **Unlocks** (items that enable other improvements)

Produce a **Top 10 Backlog** summary at the end:

| Rank | Title | Category | Area | Effort | Risk |
|------|-------|----------|------|--------|------|
| 1 | ... | Code Quality | Providers | Small | Low |

**Areas:** Providers, Storage, Pipeline, UWP/Views, UWP/ViewModels, UWP/Services, Domain, Application, F# |

---

## 8) Friction Questions (Answer Using Code Evidence)

You must answer these based on **code evidence only**:

1. **What's the most frustrating part of using this app right now?**
   - Check: error flows, multi-step wizards, loading states

2. **Where do users encounter errors or get stuck?**
   - Check: `ErrorHandlingService.cs`, `try/catch` patterns, content dialogs

3. **Which features feel tacked-on or disconnected from the core workflow?**
   - Check: navigation patterns, page relationships, service dependencies

4. **What takes longer than it should?**
   - Check: async operations without cancellation, blocking patterns, inefficient queries

5. **Are common tasks requiring unnecessary steps?**
   - Check: wizard flows, multi-page operations, repeated configuration

6. **Does the UI accurately reflect system state in real-time?**
   - Check: `INotifyPropertyChanged` usage, `ObservableCollection` vs manual refresh

7. **Are there performance delays that hurt usability?**
   - Check: `BoundedObservableCollection`, `CircularBuffer`, virtualization in lists

If code doesn't reveal enough (no telemetry/logging/UI state), explicitly state what's missing.

---

## 9) Output Guardrails

- **Don't speculate** about intent. Don't invent features.
- **Don't suggest rewriting** the whole app.
- **Don't recommend new frameworks** unless already in the repo.
- **Don't suggest moving away from** WinUI 3, CommunityToolkit.Mvvm, current architecture.
- **Keep snippets** short and focused.
- **Prefer incremental refactors** that can be verified with existing behavior.
- **Respect ADRs**—if something contradicts an ADR, note it but don't override.

---

## 10) Project-Specific File Hints

When searching for evidence, prioritize these high-value locations:

| Concern | Key Files |
|---------|-----------|
| Streaming providers | `Infrastructure/Providers/Alpaca/`, `InteractiveBrokers/`, `Polygon/` |
| Backfill providers | `Infrastructure/Providers/Backfill/*.cs` |
| Event processing | `Application/Pipeline/EventPipeline.cs` |
| Storage | `Storage/Sinks/*.cs`, `Storage/Archival/*.cs` |
| Error handling | `Application/Exceptions/*.cs`, `Services/ErrorHandlingService.cs` |
| UWP navigation | `Services/NavigationService.cs`, `MainWindow.xaml.cs` |
| UWP state | `ViewModels/*.cs`, `Services/StatusService.cs` |
| Config management | `Application/Config/*.cs`, `Services/ConfigService.cs` |
| Rate limiting | `Backfill/RateLimiter.cs`, `ProviderRateLimitTracker.cs` |
| F# domain | `MarketDataCollector.FSharp/Domain/*.fs` |

---

*Use this prompt when requesting comprehensive code review of the Market Data Collector codebase.*
