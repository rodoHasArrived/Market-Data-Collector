# Desktop Development Testing Guide

This guide helps contributors set up and test desktop applications (WPF and UWP) for Market Data Collector.

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

- [WPF Implementation Notes](../development/wpf-implementation-notes.md)
- [Desktop Architecture](../architecture/desktop-layers.md)
- [Desktop Improvements Roadmap](../status/ROADMAP.md#desktop-improvements)
- [GitHub Actions Summary](../development/github-actions-summary.md)
