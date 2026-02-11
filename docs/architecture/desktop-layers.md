# Desktop Application Architecture

## Overview

The Market Data Collector desktop applications (WPF and UWP) follow a layered architecture designed to maximize code sharing while allowing platform-specific customization where necessary.

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Platform UI Layer                        │
│  ┌──────────────────┐              ┌──────────────────┐    │
│  │   WPF Desktop    │              │   UWP Desktop    │    │
│  │   (Primary)      │              │   (Legacy)       │    │
│  │                  │              │                  │    │
│  │ - Views/Pages    │              │ - Views/Pages    │    │
│  │ - App.xaml       │              │ - App.xaml       │    │
│  │ - MainWindow     │              │ - MainWindow     │    │
│  └─────────┬────────┘              └────────┬─────────┘    │
│            │                                 │              │
│            └─────────────┬───────────────────┘              │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│              Platform-Specific Services Layer               │
│  ┌───────────────────────▼──────────────────────────────┐  │
│  │                                                       │  │
│  │  NavigationService  ThemeService  StorageService     │  │
│  │  (Platform-specific implementations)                 │  │
│  │                                                       │  │
│  └───────────────────────┬──────────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│              Shared UI Services Layer                       │
│  ┌───────────────────────▼──────────────────────────────┐  │
│  │    MarketDataCollector.Ui.Services                   │  │
│  │                                                       │  │
│  │  - ApiClientService      - BackfillService           │  │
│  │  - ChartingService       - SystemHealthService       │  │
│  │  - WatchlistService      - FixtureDataService        │  │
│  │  - ConfigService         - CredentialService         │  │
│  │  - NotificationService   - LoggingService            │  │
│  │  - ProviderHealthService - DataQualityService        │  │
│  │  - 50+ shared services                               │  │
│  │                                                       │  │
│  └───────────────────────┬──────────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│              Contracts & Models Layer                       │
│  ┌───────────────────────▼──────────────────────────────┐  │
│  │    MarketDataCollector.Contracts                     │  │
│  │                                                       │  │
│  │  - StatusResponse        - BackfillRequest/Response  │  │
│  │  - ProviderCatalog       - LiveDataModels            │  │
│  │  - BackfillApiModels     - StatusModels              │  │
│  │  - API DTOs and contracts                            │  │
│  │                                                       │  │
│  └───────────────────────┬──────────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────┘
                           │
                           ▼
                    Backend HTTP API
                  (http://localhost:8080)
```

## Project Structure

### WPF Desktop (Primary)
**Location**: `src/MarketDataCollector.Wpf/`

- **Views/**: XAML views and pages
- **ViewModels/**: Data-binding and presentation logic
- **Services/**: Platform-specific service implementations
- **App.xaml**: Application entry point
- **MainWindow.xaml**: Main application window

### UWP Desktop (Legacy)
**Location**: `src/MarketDataCollector.Uwp/`

- **Views/**: XAML views and pages
- **ViewModels/**: Data-binding and presentation logic
- **Services/**: Platform-specific service implementations
- **App.xaml**: Application entry point
- **MainWindow.xaml**: Main application window

### Shared UI Services
**Location**: `src/MarketDataCollector.Ui.Services/`

Platform-agnostic services used by both WPF and UWP:
- API communication
- Data management
- Business logic
- Fixture data for offline development

### Contracts
**Location**: `src/MarketDataCollector.Contracts/`

Shared data models and API contracts:
- Request/Response DTOs
- Domain models
- API routes

## Dependency Rules

### ✅ Allowed Dependencies

1. **Platform UI → Platform Services**: WPF/UWP can use their own services
2. **Platform UI → Shared Services**: Both platforms can use `Ui.Services`
3. **Platform Services → Shared Services**: Platform services can delegate to shared logic
4. **Shared Services → Contracts**: All services use shared contracts
5. **All Layers → Contracts**: Everyone can reference contracts

### ❌ Forbidden Dependencies

1. **Shared Services → Platform UI**: Shared code CANNOT reference WPF/UWP
2. **Shared Services → Platform Services**: Shared code CANNOT reference platform-specific services
3. **WPF → UWP** or **UWP → WPF**: Platforms CANNOT reference each other
4. **Contracts → Any other layer**: Contracts must remain pure POCOs

## Service Patterns

### Current State (Code Duplication)

Both WPF and UWP have nearly identical implementations of 31+ services:
- NavigationService
- ThemeService
- StorageService
- ConfigService
- LoggingService
- NotificationService
- CredentialService
- And 25+ more...

**Duplication**: ~100%

### Target State (Shared Base with Platform Adapters)

Extract common logic to shared services, keep only platform-specific code in platform projects:

```csharp
// Shared base in Ui.Services
public abstract class NavigationServiceBase
{
    protected abstract void NavigateCore(string pageTag);
    
    public void Navigate(string pageTag)
    {
        ValidatePageTag(pageTag);
        NavigateCore(pageTag);
    }
}

// WPF-specific implementation
public sealed class WpfNavigationService : NavigationServiceBase
{
    protected override void NavigateCore(string pageTag)
    {
        // WPF-specific Frame navigation
    }
}

// UWP-specific implementation
public sealed class UwpNavigationService : NavigationServiceBase
{
    protected override void NavigateCore(string pageTag)
    {
        // UWP-specific Frame navigation
    }
}
```

This reduces duplication from 100% to ~20% (only platform-specific parts).

## Communication Flow

### UI → Backend API

```
┌──────────┐     ┌─────────────────┐     ┌──────────────┐     ┌──────────┐
│   View   │────▶│ Platform Service│────▶│ Shared Svc   │────▶│ HTTP API │
│  (XAML)  │     │  (Optional)     │     │ (ApiClient)  │     │ Backend  │
└──────────┘     └─────────────────┘     └──────────────┘     └──────────┘
```

### With Fixture Mode

```
┌──────────┐     ┌─────────────────┐     ┌──────────────┐
│   View   │────▶│ Platform Service│────▶│FixtureData   │
│  (XAML)  │     │  (Optional)     │     │ Service      │
└──────────┘     └─────────────────┘     └──────────────┘
                                               │
                                               ▼
                                        (Mock data, no API)
```

## Key Services by Layer

### Platform-Specific Services (WPF/UWP)

**Must be platform-specific** due to API differences:
- `NavigationService` - Uses platform-specific Frame APIs
- `ThemeService` - Uses platform-specific theme resources
- `StorageService` - Uses platform-specific file system APIs
- `KeyboardShortcutService` - Uses platform-specific input handling

### Shared Services (Ui.Services)

**Can be shared** as they don't depend on platform APIs:
- `ApiClientService` - HTTP communication
- `BackfillService` - Backfill orchestration
- `SystemHealthService` - Health monitoring
- `WatchlistService` - Symbol list management
- `FixtureDataService` - Mock data for offline development
- `FormValidationRules` - Input validation
- `ConfigService` - Configuration management
- `CredentialService` - Credential management
- `NotificationService` - Cross-platform notifications
- `LoggingService` - Structured logging

### Collections (Ui.Services)

Specialized data structures:
- `BoundedObservableCollection<T>` - Observable collection with max size
- `CircularBuffer<T>` - Ring buffer with statistics

## Testing Strategy

### Unit Tests
**Location**: `tests/MarketDataCollector.Ui.Tests/`

- Test shared services in isolation
- Test collections independently
- Use fixture data for deterministic tests
- **Platform**: Windows-only (net9.0-windows)

**Current Coverage**: 71 tests, ~15% coverage

### Integration Tests

Test platform-specific services with mocks:
- Mock shared services
- Verify platform integration
- Test navigation flows
- Validate theme switching

## Design Principles

### 1. **Single Responsibility**
Each service has one clear purpose.

### 2. **Dependency Inversion**
Depend on abstractions (interfaces), not concrete implementations.

### 3. **Don't Repeat Yourself (DRY)**
Share code via `Ui.Services`, avoid duplication between WPF/UWP.

### 4. **Separation of Concerns**
- Views handle presentation
- ViewModels handle binding
- Services handle business logic
- Contracts define data shapes

### 5. **Testability**
All services should be testable in isolation.

## Violation Examples

### ❌ BAD: Shared Service Referencing Platform Code

```csharp
// In Ui.Services (WRONG!)
using MarketDataCollector.Wpf.Services;

public class SharedService
{
    private WpfNavigationService _navigation; // VIOLATION!
}
```

**Why it's wrong**: Shared code cannot reference platform-specific code.

### ✅ GOOD: Platform Service Using Shared Code

```csharp
// In MarketDataCollector.Wpf (CORRECT)
using MarketDataCollector.Ui.Services;

public class WpfDashboardService
{
    private ApiClientService _apiClient; // OK!
}
```

### ❌ BAD: Cross-Platform Reference

```csharp
// In MarketDataCollector.Wpf (WRONG!)
using MarketDataCollector.Uwp.Services;

public class WpfService
{
    private UwpThemeService _theme; // VIOLATION!
}
```

**Why it's wrong**: Platforms should not reference each other.

### ✅ GOOD: Both Platforms Using Shared Service

```csharp
// In MarketDataCollector.Wpf
using MarketDataCollector.Ui.Services;
public class WpfDashboard
{
    private BackfillService _backfill = BackfillService.Instance;
}

// In MarketDataCollector.Uwp
using MarketDataCollector.Ui.Services;
public class UwpDashboard
{
    private BackfillService _backfill = BackfillService.Instance;
}
```

## Migration Path

### Current State → Target State

**Phase 1** (Complete): Create `Ui.Services` project and establish pattern
- ✅ Add test infrastructure (71 tests)
- ✅ Add fixture mode for offline development
- ✅ Document architecture boundaries

**Phase 2** (Weeks 5-8): Extract common services
- [ ] Identify most duplicated services
- [ ] Extract to `Ui.Services` with interfaces
- [ ] Update WPF/UWP to use shared services
- [ ] Add tests for extracted services

**Phase 3** (Weeks 9-12): Reduce remaining duplication
- [ ] Create abstract base classes for platform services
- [ ] Implement platform-specific overrides
- [ ] Consolidate 80% of duplicated code

**Phase 4** (Weeks 13-16): Modernize dependency injection
- [ ] Replace singleton pattern with DI container
- [ ] Improve testability
- [ ] Simplify service lifetime management

## Monitoring Compliance

### Static Analysis

Use ArchUnitNET or similar to enforce rules:

```csharp
[Test]
public void SharedServices_ShouldNotReference_PlatformCode()
{
    var architecture = new ArchitectureContext()
        .WithAssembly(typeof(ApiClientService).Assembly);
        
    architecture
        .GetTypes()
        .Should()
        .NotDependOnAny("MarketDataCollector.Wpf", "MarketDataCollector.Uwp");
}
```

### Code Review Checklist

- [ ] Does this PR introduce cross-platform dependencies?
- [ ] Could this code be shared in `Ui.Services`?
- [ ] Are platform-specific services clearly marked?
- [ ] Do tests cover boundary violations?

## Benefits of This Architecture

✅ **Code Reuse**: 50+ services shared between platforms  
✅ **Testability**: Shared services have 71 unit tests  
✅ **Maintainability**: Fix bugs once, benefits both platforms  
✅ **Clear Boundaries**: Violations are obvious and preventable  
✅ **Offline Development**: Fixture mode enables development without backend  
✅ **Scalability**: Easy to add new platforms (Avalonia, MAUI)  

## Related Documentation

- **Implementation Guide**: `docs/development/desktop-platform-improvements-implementation-guide.md`
- **Fixture Mode**: `docs/development/ui-fixture-mode-guide.md`
- **Executive Summary**: `docs/development/desktop-improvements-executive-summary.md`
- **Test Project**: `tests/MarketDataCollector.Ui.Tests/README.md`

---

**Status**: ✅ Documented  
**Version**: 1.0  
**Last Updated**: 2026-02-11  
**Compliance**: To be enforced via static analysis (Phase 4)
