# Desktop & UI Layer Architecture

## Overview

Market Data Collector now uses a **dual UI surface**:

1. **WPF Desktop (`MarketDataCollector.Wpf`)** for rich Windows-first operator workflows.
2. **Web Dashboard (`MarketDataCollector.Ui`)** for browser-based monitoring/configuration.

Both surfaces share contracts and application logic through shared libraries, with clear boundaries between platform host code and reusable UI functionality.

## Layer Diagram

```
┌────────────────────────────────────────────────────────────────────────────┐
│                          UI Host Layer                                    │
│  ┌────────────────────────────┐     ┌──────────────────────────────────┐  │
│  │ MarketDataCollector.Wpf    │     │ MarketDataCollector.Ui           │  │
│  │ (Windows desktop host)     │     │ (ASP.NET Core web host)          │  │
│  │ - XAML views/viewmodels    │     │ - Thin Program.cs host           │  │
│  │ - WPF-only services        │     │ - Serves dashboard/static assets │  │
│  └──────────────┬─────────────┘     └──────────────────┬───────────────┘  │
└─────────────────┼────────────────────────────────────────┼──────────────────┘
                  │                                        │
                  │                                        ▼
                  │                    ┌──────────────────────────────────┐
                  │                    │ MarketDataCollector.Ui.Shared    │
                  │                    │ - Endpoint mapping               │
                  │                    │ - Shared web UI services         │
                  │                    │ - Host composition helpers       │
                  │                    └──────────────────┬───────────────┘
                  │                                        │
                  ▼                                        ▼
┌────────────────────────────────────────────────────────────────────────────┐
│                      Shared UI Services Layer                             │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ MarketDataCollector.Ui.Services                                     │  │
│  │ - API/client orchestration                                          │  │
│  │ - Validation, fixture mode, notifications, config helpers           │  │
│  │ - Shared collections/contracts for desktop-facing features          │  │
│  └──────────────────────────────────────┬───────────────────────────────┘  │
└─────────────────────────────────────────┼──────────────────────────────────┘
                                          │
                                          ▼
┌────────────────────────────────────────────────────────────────────────────┐
│               Contracts + Backend Application Layers                      │
│  MarketDataCollector.Contracts  +  Application/Core/Domain/...            │
│  (DTOs, API contracts, orchestration, pipelines, providers, storage)      │
└────────────────────────────────────────────────────────────────────────────┘
```

## Project Responsibilities

### `src/MarketDataCollector.Wpf/` (Desktop host)

- Owns XAML views, viewmodels, and WPF shell/navigation.
- Registers DI container and composes page/service graph.
- Contains truly platform-specific implementations (theme, keyboard shortcuts, windowing, etc.).
- References `MarketDataCollector.Ui.Services` for shared UI/domain helpers.

### `src/MarketDataCollector.Ui/` (Web host)

- Intentionally thin host (`Program.cs`) that delegates setup to shared endpoint composition.
- Serves browser dashboard and static assets.
- References `MarketDataCollector.Ui.Shared`.

### `src/MarketDataCollector.Ui.Shared/` (Web shared module)

- Contains endpoint mapping and reusable web-host/service glue.
- Bridges the web host to application/contract layers without duplicating wiring in each host.
- References `MarketDataCollector.Application` and `MarketDataCollector.Contracts`.

### `src/MarketDataCollector.Ui.Services/` (Cross-feature shared UI services)

- Shared service logic used by desktop workflows (API, fixture data, validation/utilities, etc.).
- Includes linked contract source files for desktop compatibility scenarios.
- Keeps platform-neutral behavior out of WPF-specific code.

### `src/MarketDataCollector.Contracts/` (Canonical contracts)

- Request/response DTOs, domain event models, enums, config models, API routes.
- Pure contract layer with no UI framework dependencies.

## Dependency Rules

### ✅ Allowed

1. **WPF host → Ui.Services**
2. **Web host (`Ui`) → Ui.Shared**
3. **Ui.Shared → Application + Contracts**
4. **Ui.Services → Contracts models (linked/shared consumption pattern)**
5. **All UI-facing layers → Contracts**

### ❌ Forbidden

1. **Ui.Services → WPF host types** (no dependency back into desktop UI shell)
2. **Ui.Shared → WPF-only APIs** (must stay host-agnostic)
3. **Host-to-host references** (`Wpf` ↔ `Ui`)
4. **Contracts → UI or application hosts**

## Communication Flow

### WPF path

```
View/Page (WPF)
   → WPF platform service (optional)
   → Ui.Services shared logic
   → Backend API / Application service endpoints
```

### Web path

```
HTTP Request
   → Ui host (Program.cs)
   → Ui.Shared endpoint/service mapping
   → Application services
   → Contracts DTO response
```

## Why this layering

- Keeps each host thin and focused on platform concerns.
- Avoids duplicating endpoint/configuration wiring between web surfaces.
- Preserves reusable business-facing UI logic in shared libraries.
- Supports future host additions (another desktop/web shell) with minimal coupling.
