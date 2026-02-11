# Desktop UI Services Tests

This test project validates the shared UI services used by WPF and UWP desktop applications.

## Platform Requirements

**Windows Only**: This test project targets `net9.0-windows` because it tests services that depend on Windows-specific APIs (`MarketDataCollector.Ui.Services`).

On non-Windows platforms, the project compiles as an empty library.

## Running Tests

### On Windows

```bash
# Run all desktop UI service tests
dotnet test tests/MarketDataCollector.Ui.Tests

# Or use Makefile
make test-desktop-services
```

### On Linux/macOS

Tests are automatically skipped (project compiles empty).

## Test Coverage

**Current: 58 tests**

- **Collections**: 
  - `BoundedObservableCollection` (8 tests)
  - `CircularBuffer` (11 tests)
- **Services**: 
  - `FormValidationRules` (4 tests)
  - `ApiClientService` (7 tests)
  - `BackfillService` (9 tests)
  - `WatchlistService` (9 tests)
  - `SystemHealthService` (10 tests)

More tests to be added...

## Adding New Tests

1. Create test file in appropriate subdirectory
2. Follow existing test patterns (Arrange-Act-Assert)
3. Use FluentAssertions for readable assertions
4. Run tests on Windows before submitting PR

## Test Structure

```
Collections/
  BoundedObservableCollectionTests.cs (8 tests)
  CircularBufferTests.cs (11 tests)
Services/
  FormValidationServiceTests.cs (4 tests)
  ApiClientServiceTests.cs (7 tests)
  BackfillServiceTests.cs (9 tests)
  WatchlistServiceTests.cs (9 tests)
  SystemHealthServiceTests.cs (10 tests)
```
