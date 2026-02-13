# Desktop Development Testing Guide

**Last Updated:** 2026-02-12

This guide helps contributors set up, build, and test desktop applications (WPF primary, UWP legacy) for Market Data Collector.

---

## Quick Commands

Use these commands for fast desktop-focused iteration:

```bash
# Bootstrap — validate environment and smoke-build
make desktop-dev-bootstrap

# Build
make build-wpf            # WPF (recommended)
make build-uwp            # UWP (legacy, Windows only)

# Test
make test-desktop-services # All desktop-related tests

# UWP XAML diagnostics
make uwp-xaml-diagnose
```

### When to Run What

| Change Type | Commands |
|-------------|----------|
| **WPF changes** | `make build-wpf` + `make test-desktop-services` |
| **UWP changes** | `make build-uwp` + `make uwp-xaml-diagnose` + `make test-desktop-services` |
| **Shared services** (`Ui.Services` or Contracts) | Run all of the above on Windows when possible |

See the [Desktop Support Policy](policies/desktop-support-policy.md) for required validation by change type.

---

## Quick Start

### 1. Validate Your Development Environment

Run the desktop development bootstrap script to validate your environment:

```bash
make desktop-dev-bootstrap
```

Or directly with PowerShell:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev/desktop-dev.ps1
```

This script validates:
- .NET 9 SDK installation
- Windows SDK presence (Windows only)
- Visual Studio Build Tools
- XAML tooling support
- Desktop project restore and smoke build

**Actionable Fix Messages**: The script provides specific instructions for any missing components.

### 2. Run Desktop Tests

```bash
# Run all desktop-focused tests (platform-aware)
make test-desktop-services

# Or run specific test projects:
dotnet test tests/MarketDataCollector.Wpf.Tests  # Windows only
dotnet test tests/MarketDataCollector.Ui.Tests   # Cross-platform (shared services)
```

---

## Test Projects

### MarketDataCollector.Wpf.Tests (58 tests, Windows only)

Tests for WPF singleton services. These tests require Windows as they depend on WPF types (`System.Windows.Controls.Frame`, etc.).

**Test Suites:**

1. **NavigationServiceTests** (14 tests) — Frame initialization, page navigation, history, events
2. **ConfigServiceTests** (13 tests) — Configuration loading, validation, data source and symbol management
3. **StatusServiceTests** (13 tests) — Status updates, HTTP client interaction, cancellation, thread safety
4. **ConnectionServiceTests** (18 tests) — Connection state, auto-reconnect, monitoring, settings
5. **WpfDataQualityServiceTests** — Data quality service integration

**Running WPF Tests:**

```bash
dotnet test tests/MarketDataCollector.Wpf.Tests/MarketDataCollector.Wpf.Tests.csproj
```

On non-Windows platforms, these tests will be skipped automatically by the Makefile target.

### MarketDataCollector.Ui.Tests (71 tests, cross-platform)

Tests for shared UI services in `MarketDataCollector.Ui.Services`. These run on all platforms.

**Test Suites:**

1. **ApiClientServiceTests** — API client configuration and HTTP interactions
2. **BackfillServiceTests** — Backfill coordination and scheduling
3. **FixtureDataServiceTests** — Mock data generation for offline development
4. **FormValidationServiceTests** — Form validation rules and helpers
5. **SystemHealthServiceTests** — System health monitoring
6. **WatchlistServiceTests** — Watchlist management
7. **OrderBookVisualizationServiceTests** — Order book rendering logic
8. **SchemaServiceTests** — Schema compatibility checks
9. **BoundedObservableCollectionTests** — Bounded collection behavior
10. **CircularBufferTests** — Circular buffer operations

**Running UI Tests:**

```bash
dotnet test tests/MarketDataCollector.Ui.Tests/MarketDataCollector.Ui.Tests.csproj
```

---

## Building Desktop Applications

### WPF Application (Recommended)

```bash
make build-wpf

# Or directly:
dotnet build src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj -c Release -r win-x64
```

### UWP Application (Legacy)

```bash
make build-uwp

# Or directly:
dotnet build src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj -c Release -r win-x64
```

### Running with Fixture Mode (Offline Development)

```bash
dotnet run --project src/MarketDataCollector.Wpf -- --fixture

# Or set environment variable
export MDC_FIXTURE_MODE=1
dotnet run --project src/MarketDataCollector.Wpf
```

See the [UI Fixture Mode Guide](ui-fixture-mode-guide.md) for details.

### UWP XAML Diagnostics

If you encounter XAML compilation issues with UWP:

```bash
make uwp-xaml-diagnose

# Or directly:
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev/diagnose-uwp-xaml.ps1
```

See [XAML Compiler Errors](desktop-app-xaml-compiler-errors.md) for common UWP XAML issues.

---

## Common Issues and Solutions

### Missing .NET 9 SDK

**Symptom**: Bootstrap script reports .NET SDK not found or wrong version.

**Fix**: Install .NET 9 SDK from https://dotnet.microsoft.com/download/dotnet/9.0

### Missing Windows SDK

**Symptom**: UWP restore/build fails, bootstrap script reports Windows SDK not found.

**Fix**: Install Windows SDK via Visual Studio Installer or from https://developer.microsoft.com/windows/downloads/windows-sdk/

### Missing Visual Studio Build Tools

**Symptom**: XAML compilation fails, build tools not detected.

**Fix**: Install Visual Studio Build Tools with the "Desktop development with C#" workload from https://visualstudio.microsoft.com/downloads/

### XAML Compiler Errors

**Symptom**: WPF or UWP build fails with XAML syntax errors.

**Fix**:
1. Check XAML syntax in the `Views/` directory
2. Ensure all referenced resources exist
3. Run `make uwp-xaml-diagnose` for detailed diagnostics
4. See [XAML Compiler Errors](desktop-app-xaml-compiler-errors.md) for known issues

### Tests Not Running on Non-Windows

**Expected Behavior**: WPF tests require Windows and will be skipped on Linux/macOS. This is by design.

**What Runs on Non-Windows**:
- Shared UI service tests in `MarketDataCollector.Ui.Tests`
- Core tests in `MarketDataCollector.Tests`
- F# tests in `MarketDataCollector.FSharp.Tests`
- Configuration and CLI tests

---

## Test Coverage

Current test coverage for desktop services:

| Service | Covered Areas |
|---------|--------------|
| **NavigationService** | Page navigation, history tracking, event handling |
| **ConfigService** | Configuration validation, data source management |
| **StatusService** | Status updates, HTTP interaction, thread safety |
| **ConnectionService** | Connection management, auto-reconnect, monitoring |
| **ApiClientService** | HTTP client configuration, error handling |
| **FormValidationRules** | Symbol, date, and path validation |
| **FixtureDataService** | Mock data generation, contract compliance |
| **SystemHealthService** | Health monitoring, threshold evaluation |

**Areas Not Yet Covered** (future work):
- Integration tests with actual backend service
- UI interaction tests (would require UI automation frameworks)
- Visual regression tests
- Performance tests for singleton access patterns

---

## Contributing Desktop Tests

When adding new desktop tests:

1. **Follow existing patterns**: Use xUnit, FluentAssertions, Moq/NSubstitute
2. **Test singleton behavior**: Verify instance creation, thread safety
3. **Mock external dependencies**: Use test doubles for HTTP clients, file systems
4. **Test error paths**: Verify exception handling, cancellation support
5. **Keep tests fast**: Avoid actual network calls, use mocked endpoints
6. **Document test purpose**: Clear test names following `ServiceName_Scenario_ExpectedBehavior` convention

Example test structure:

```csharp
[Fact]
public void ServiceName_Scenario_ExpectedBehavior()
{
    // Arrange
    var service = ServiceName.Instance;
    var input = CreateTestInput();

    // Act
    var result = service.MethodUnderTest(input);

    // Assert
    result.Should().NotBeNull();
    result.SomeProperty.Should().Be(expectedValue);
}
```

---

## Continuous Integration

Desktop tests run in CI via GitHub Actions:

- **Windows runners**: Run full WPF + UWP test suite
- **Linux/macOS runners**: Skip WPF tests, run shared UI service tests

See `.github/workflows/desktop-builds.yml` and `.github/workflows/test-matrix.yml` for CI configuration.

---

## Related Documentation

- [WPF Implementation Notes](wpf-implementation-notes.md) — Architecture, services, and patterns
- [Desktop Architecture](../architecture/desktop-layers.md) — Layer boundaries diagram
- [Desktop Platform Improvements](desktop-platform-improvements-implementation-guide.md) — Improvement roadmap
- [Desktop Improvements Executive Summary](desktop-improvements-executive-summary.md) — Phase 1 results
- [UWP-to-WPF Migration](uwp-to-wpf-migration.md) — Migration rationale and progress
- [UI Fixture Mode Guide](ui-fixture-mode-guide.md) — Offline development with mock data
- [GitHub Actions Summary](github-actions-summary.md) — CI/CD workflow inventory
