# Readability refactor roadmap

_Date: 2026-03-20_

## Scope

This phase establishes the refactor scaffolding needed to improve readability without changing runtime behavior. The immediate goal is to make later extraction work safer across startup, desktop UI, provider adapters, and configuration flows.

### In scope
- Startup and host composition seams (`src/Meridian/Program.cs`, `src/Meridian.Application/Composition/HostStartup.cs`, `src/Meridian.Application/Commands/*`).
- Configuration loading and validation seams (`src/Meridian.Application/Config/*`, `src/Meridian.Application/Services/*`).
- WPF data-quality and connection management seams (`src/Meridian.Wpf/ViewModels/DataQualityViewModel.cs`, `src/Meridian.Wpf/Views/DataQualityPage.xaml.cs`, `src/Meridian.Wpf/Services/*`).
- Large provider adapter and resilience seams (`src/Meridian.Infrastructure/Adapters/**`, `src/Meridian.Infrastructure/Resilience/*`).
- Characterization tests and documentation that measure progress without forcing architectural rewrites up front.

### Out of scope for Phase 0
- Renaming public contracts purely for style.
- Rewriting provider protocols or changing wire formats.
- Replacing WPF or web UI frameworks.
- Moving business logic across bounded contexts without tests first.
- Throughput/performance tuning that is not directly required to keep behavior stable during refactors.

## Target modules

### Workstream A — Startup orchestration
- `src/Meridian/Program.cs`
- `src/Meridian.Application/Commands/CommandDispatcher.cs`
- `src/Meridian.Application/Services/CliModeResolver.cs`
- `src/Meridian.Application/Composition/HostStartup.cs`

### Workstream B — Configuration and validation
- `src/Meridian.Application/Config/*`
- `src/Meridian.Application/Services/ConfigurationService.cs`
- `src/Meridian.Application/Services/AutoConfigurationService.cs`

### Workstream C — Desktop UI composition
- `src/Meridian.Wpf/ViewModels/DataQualityViewModel.cs`
- `src/Meridian.Wpf/Views/*.xaml.cs` with direct network or JSON logic
- `src/Meridian.Wpf/Services/*`
- `src/Meridian.Ui.Services/Services/*`

### Workstream D — Provider adapters and lifecycle management
- `src/Meridian.Infrastructure/Adapters/Core/*`
- `src/Meridian.Infrastructure/Adapters/Polygon/*`
- `src/Meridian.Infrastructure/Adapters/InteractiveBrokers/*`
- `src/Meridian.Infrastructure/Adapters/StockSharp/*`
- `src/Meridian.Infrastructure/Resilience/*`

## Proposed sequencing

1. **Freeze behavior with characterization tests.**
   - Strengthen command dispatch, mode selection, config validation, WPF mapping, and connection lifecycle coverage before moving logic.
2. **Document seams and current hotspots.**
   - Keep a living baseline of file size, direct HTTP/JSON usage, and oversized adapters.
3. **Extract startup responsibilities behind focused collaborators.**
   - Separate CLI dispatch, deployment mode handling, validation gates, and runtime pipeline startup.
4. **Extract WPF data loading into services/adapters.**
   - Move page/view-model transport and JSON mapping into UI services while preserving bindings.
5. **Split oversized provider adapters.**
   - Break transport, mapping, retry/backoff, and capability negotiation into separate components.
6. **Enforce module boundaries with tests and lightweight architecture rules.**
   - Prefer additive seams and adapters over large rewrites.

## Anti-goals

- Do not mix behavior changes with readability-only moves in the same PR unless tests explicitly prove equivalence.
- Do not introduce new dependency injection graphs for the sake of abstraction alone.
- Do not move HTTP and JSON logic into shared helpers that obscure provider- or page-specific behavior.
- Do not add temporary dual implementations unless there is a documented cutover plan.
- Do not treat file-count reduction as success if responsibility boundaries become less clear.

## Architecture rules

1. **Single startup coordinator, multiple collaborators.** `Program` should compose and delegate; it should not continue to accumulate policy, IO, validation, hosting, and runtime orchestration in one place.
2. **Command dispatch remains deterministic.** Registration order is the behavioral contract until a different policy is explicitly adopted and tested.
3. **Mode resolution stays centralized.** Legacy flags and unified modes must continue to flow through one translation point.
4. **Validation remains side-effect free.** Config validators can report errors and warnings, but should not mutate runtime state.
5. **WPF pages and view models should not own raw transport details long term.** HTTP calls, JSON parsing, and endpoint selection should migrate toward services with page/view-model-facing DTOs.
6. **Provider adapters should separate concerns.** Connection management, payload parsing, subscription orchestration, and failover logic should become independently testable.
7. **Refactors must preserve desktop and CLI guardrails.** Existing `dotnet build`, application tests, UI tests, and provider tests remain the minimum safety net.

## Migration status by workstream

| Workstream | Current state | Phase 0 status | Exit signal |
| --- | --- | --- | --- |
| Startup orchestration | Large `Program` runtime path with mixed CLI, validation, UI, and pipeline responsibilities | Baseline captured; characterization tests strengthened | Team agrees on extraction seams for dispatch, validation gate, and runtime startup |
| Configuration and validation | Validation already centralized but still broad across providers and symbol rules | Baseline captured; characterization tests strengthened | Error/warning behavior is pinned before refactoring validators |
| Desktop UI composition | Data-quality and several page/view-model types still contain direct HTTP/JSON logic | Baseline captured; WPF mapping characterization added | Transport/mapping extraction plan agreed for first page/view-model slice |
| Provider adapters and lifecycle | Several adapters remain very large and mix transport, retries, mapping, and capability logic | Baseline captured; connection lifecycle characterization strengthened | First adapter split candidate selected and protected by tests |

## Phase 0 completion checklist

- [x] Roadmap document exists.
- [x] Baseline complexity metrics are recorded.
- [x] Characterization tests cover startup dispatch, mode selection, config validation, WPF mapping, and provider connection lifecycle.
- [ ] Module boundaries reviewed with the team.
- [ ] Sequencing approved for Phase 1 extraction work.
