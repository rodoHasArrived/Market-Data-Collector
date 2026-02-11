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

- **Collections**: `BoundedObservableCollection`, `CircularBuffer`
- **Services**: Form validation, API clients
- More tests to be added...

## Adding New Tests

1. Create test file in appropriate subdirectory
2. Follow existing test patterns (Arrange-Act-Assert)
3. Use FluentAssertions for readable assertions
4. Run tests on Windows before submitting PR

## Test Structure

```
Collections/
  BoundedObservableCollectionTests.cs
  CircularBufferTests.cs
Services/
  FormValidationServiceTests.cs
  (more to be added)
```
