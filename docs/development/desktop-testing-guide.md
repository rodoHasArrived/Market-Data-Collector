# Desktop Development Testing Guide

This guide helps contributors set up and test desktop applications (WPF and UWP) for Market Data Collector.

## Quick Commands Reference

```bash
# Environment validation
make desktop-dev-bootstrap

# Build desktop applications
make build-wpf                    # Build WPF (recommended)
make build-uwp                    # Build UWP (legacy)

# Run tests
make test-desktop-services        # Run all desktop-focused tests
dotnet test tests/MarketDataCollector.Wpf.Tests        # WPF service tests (Windows only)
dotnet test tests/MarketDataCollector.Ui.Tests         # Shared UI service tests (Windows only)

# Diagnostics
make uwp-xaml-diagnose           # UWP XAML diagnostics (if needed)
```

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
- ✅ .NET 9 SDK installation
- ✅ Windows SDK presence (Windows only)
- ✅ Visual Studio Build Tools
- ✅ XAML tooling support
- ✅ Desktop project restore and smoke build

**Actionable Fix Messages**: The script provides specific instructions for any missing components.

### 2. Run Desktop Tests

```bash
# Run all desktop-focused tests (platform-aware)
make test-desktop-services

# Or run specific test projects:
dotnet test tests/MarketDataCollector.Wpf.Tests  # Windows only
dotnet test tests/MarketDataCollector.Tests --filter "FullyQualifiedName~ConfigurationUnificationTests"
```

## Test Projects

### MarketDataCollector.Ui.Tests (71 tests, Windows only)

Tests for shared UI services used by both WPF and UWP desktop applications.

**Test Suites:**

1. **Collections Tests** (19 tests)
   - `BoundedObservableCollection` (8 tests) - Capacity-limited observable collection
   - `CircularBuffer` (11 tests) - Circular buffer operations and extension methods

2. **Service Tests** (52 tests)
   - `FormValidationRules` (4 tests) - Input validation rules
   - `ApiClientService` (7 tests) - HTTP client configuration and interaction
   - `BackfillService` (9 tests) - Historical data backfill coordination
   - `WatchlistService` (9 tests) - Symbol watchlist management
   - `SystemHealthService` (10 tests) - System health monitoring and metrics
   - `FixtureDataService` (13 tests) - Mock data generation for offline development

**Running Ui.Tests:**

```bash
# Windows only
dotnet test tests/MarketDataCollector.Ui.Tests/MarketDataCollector.Ui.Tests.csproj
```

These tests validate services used by both WPF and UWP, ensuring consistent behavior across desktop platforms.

### MarketDataCollector.Wpf.Tests (58 tests, Windows only)

Tests for WPF singleton services. These tests require Windows as they depend on WPF types (`System.Windows.Controls.Frame`, etc.).

**Test Suites:**

1. **NavigationServiceTests** (14 tests)
   - Singleton pattern validation
   - Frame initialization
   - Page navigation and registration
   - Navigation history and breadcrumbs
   - Event handling

2. **ConfigServiceTests** (13 tests)
   - Singleton pattern validation
   - Configuration initialization
   - Configuration validation
   - Data source management
   - Symbol management
   - Configuration reload

3. **StatusServiceTests** (13 tests)
   - Singleton pattern validation
   - Status updates and events
   - HTTP client interaction (with mocked unreachable endpoints)
   - Cancellation token support
   - Thread safety

4. **ConnectionServiceTests** (18 tests)
   - Singleton pattern validation
   - Connection state management
   - Auto-reconnect logic
   - Connection monitoring
   - Settings management
   - Event handling
   - HTTP client interaction

**Running WPF Tests:**

```bash
# Windows only
dotnet test tests/MarketDataCollector.Wpf.Tests/MarketDataCollector.Wpf.Tests.csproj
```

On non-Windows platforms, these tests will be skipped automatically by the Makefile target.

### Combined Test Coverage Summary

| Project | Tests | Platform | Coverage Areas |
|---------|-------|----------|----------------|
| **MarketDataCollector.Ui.Tests** | 71 | Windows | Shared UI services, collections, form validation |
| **MarketDataCollector.Wpf.Tests** | 58 | Windows | WPF singleton services, navigation, configuration |
| **Total Desktop Tests** | **129** | Windows | Comprehensive desktop service validation |

**Coverage breakdown:**
- Navigation: 14 tests (page routing, history, breadcrumbs)
- Configuration: 13 tests (validation, data source management)
- Status Tracking: 13 tests (real-time updates, HTTP interaction)
- Connection Management: 18 tests (state management, auto-reconnect)
- Collections: 19 tests (bounded/circular buffer operations)
- Business Services: 52 tests (validation, health, backfill, fixtures)

## UI Fixture Mode for Offline Development

The UI fixture mode enables desktop developers to work without a running backend service, significantly improving development velocity.

### Using Fixture Mode

**Enable via environment variable:**

```bash
# Windows PowerShell
$env:MDC_FIXTURE_MODE = "1"
dotnet run --project src/MarketDataCollector.Wpf

# Windows Command Prompt
set MDC_FIXTURE_MODE=1
dotnet run --project src/MarketDataCollector.Wpf
```

**What Fixture Mode Provides:**

- ✅ **Offline development** - No network connectivity required
- ✅ **Deterministic data** - Same mock data every time
- ✅ **Faster iteration** - No backend startup wait time
- ✅ **Demo capabilities** - Show UI features without live data

**Fixture Data Available:**

- Mock status responses (provider health, connection states)
- Sample market data (trades, quotes, order book snapshots)
- Configuration templates
- Historical backfill progress
- Data quality metrics

**Test Coverage for Fixtures:**

The `FixtureDataService` has 13 dedicated tests validating:
- Mock data generation for all major API endpoints
- Consistent data structure matching real API contracts
- Randomized but realistic values (prices, volumes, timestamps)
- Edge cases (empty states, error conditions)

See [UI Fixture Mode Guide](./ui-fixture-mode-guide.md) for complete documentation.

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

### UWP XAML Diagnostics

If you encounter XAML compilation issues with UWP:

```bash
make uwp-xaml-diagnose

# Or directly:
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev/diagnose-uwp-xaml.ps1
```

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
1. Check XAML syntax in the Views/ directory
2. Ensure all referenced resources exist
3. Run `make uwp-xaml-diagnose` for detailed diagnostics

### Tests Not Running on Non-Windows

**Expected Behavior**: WPF tests require Windows and will be skipped on Linux/macOS. This is by design.

**What Runs on Non-Windows**:
- Core tests in `MarketDataCollector.Tests`
- F# tests in `MarketDataCollector.FSharp.Tests`
- Configuration and CLI tests

## Test Coverage

Current test coverage for desktop services:

- **NavigationService**: Page navigation, history tracking, event handling
- **ConfigService**: Configuration validation, data source management
- **StatusService**: Status updates, HTTP interaction, thread safety
- **ConnectionService**: Connection management, auto-reconnect, monitoring

**Areas Not Yet Covered** (future work):
- Integration tests with actual backend service
- UI interaction tests (would require UI automation frameworks)
- Visual regression tests
- Performance tests for singleton access patterns

## Contributing Desktop Tests

When adding new desktop tests:

1. **Follow existing patterns**: Use xUnit, FluentAssertions, Moq/NSubstitute
2. **Test singleton behavior**: Verify instance creation, thread safety
3. **Mock external dependencies**: Use test doubles for HTTP clients, file systems
4. **Test error paths**: Verify exception handling, cancellation support
5. **Keep tests fast**: Avoid actual network calls, use mocked endpoints
6. **Document test purpose**: Clear test names and XML comments

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

## Continuous Integration

Desktop tests run in CI via GitHub Actions:

- **Windows runners**: Run full WPF test suite
- **Linux/macOS runners**: Skip WPF tests, run integration tests

See `.github/workflows/desktop-builds.yml` for CI configuration.

## Additional Resources

- [WPF Implementation Notes](./wpf-implementation-notes.md) - WPF architecture and service patterns
- [UI Fixture Mode Guide](./ui-fixture-mode-guide.md) - Complete offline development setup
- [Desktop Support Policy](./policies/desktop-support-policy.md) - Contribution requirements
- [Desktop Architecture](../architecture/desktop-layers.md) - Layer boundaries and design
- [Desktop Improvements Roadmap](../status/ROADMAP.md#desktop-improvements) - Future plans
- [GitHub Actions Summary](./github-actions-summary.md) - CI/CD workflows

## Related Documentation

- **Desktop Development:**
  - [Desktop Development Workflow](./desktop-dev-workflow.md) - Quick command reference
  - [Desktop Platform Improvements - Implementation Guide](./desktop-platform-improvements-implementation-guide.md) - Complete improvement roadmap
  - [Desktop Improvements - Executive Summary](./desktop-improvements-executive-summary.md) - Impact analysis and priorities
  - [Desktop Improvements - Quick Reference](./desktop-improvements-quick-reference.md) - One-page summary
  
- **Testing and Quality:**
  - [Test Project README](../../tests/MarketDataCollector.Ui.Tests/README.md) - Ui.Tests project details
  - [WPF Test Project README](../../tests/MarketDataCollector.Wpf.Tests/README.md) - WPF-specific tests (if exists)
  
- **Architecture and Policies:**
  - [Repository Organization Guide](./repository-organization-guide.md) - Code structure conventions
  - [Desktop Support Policy](./policies/desktop-support-policy.md) - Required validation checks
