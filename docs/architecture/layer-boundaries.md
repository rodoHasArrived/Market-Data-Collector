# Layer Boundary Rules

This document defines the allowed dependency directions between layer assemblies.
These boundaries are enforced by **project references** (compile-time) and
**Roslyn analyzer rules** (IDE warnings).

## Dependency Graph

```
MarketDataCollector.Contracts          (no project dependencies)
       ↑
MarketDataCollector.ProviderSdk  →  Contracts
       ↑
MarketDataCollector.Domain       →  Contracts, ProviderSdk
       ↑
MarketDataCollector.Core         →  Domain, Contracts, ProviderSdk
       ↑                              (cross-cutting: logging, serialization, exceptions)
       ├─────────────────────────┐
       ↑                         ↑
MarketDataCollector.Infrastructure   MarketDataCollector.Storage
  →  Core, Domain,                     →  Core, Domain,
     Contracts, ProviderSdk               Contracts, ProviderSdk
       ↑                         ↑
       └────────┬─────────────────┘
                ↑
MarketDataCollector.Application  →  Infrastructure, Storage, Core,
                                    Domain, Contracts, ProviderSdk
                ↑
MarketDataCollector (Host/Exe)   →  Application (+ transitive)
```

## Forbidden Dependencies

| From Assembly        | Must NOT Reference          | Reason                                    |
|---------------------|-----------------------------|-------------------------------------------|
| **Domain**          | Application, Infrastructure, Storage, Core | Pure business logic, no external deps |
| **Core**            | Application, Infrastructure, Storage       | Shared utilities only                 |
| **Infrastructure**  | Application, Storage                       | No upward deps, no peer deps         |
| **Storage**         | Application, Infrastructure                | No upward deps, no peer deps         |
| **Application**     | (none forbidden)                           | Top-level orchestrator                |

## Enforcement Mechanisms

1. **Project References**: Each `.csproj` only lists allowed `<ProjectReference>` entries.
   MSBuild will fail if a type from an unreferenced assembly is used.

2. **Roslyn Analyzer Rules**: `Directory.Build.targets` injects per-project
   `RS0037` (Banned Symbols) rules that flag `using` statements importing
   forbidden namespaces.

3. **CI Gate**: The `pr-checks.yml` workflow runs `dotnet build` which catches
   any project reference violations at compile time.

## Adding a New Layer Dependency

If a new cross-layer dependency is needed:

1. Check this document to see if it is allowed.
2. If it creates a **circular dependency**, extract the shared type to `Core`.
3. Update the `.csproj` `<ProjectReference>` entries.
4. Update this document.
5. Verify with `dotnet build -c Release`.
