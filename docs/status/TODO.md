# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-02-20T04:28:54.287299+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 17 |
| **Linked to Issues** | 0 |
| **Untracked** | 17 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 17 | Important notes and documentation |

### By Directory

| Directory | Count |
|-----------|-------|
| `tests/` | 10 |
| `src/` | 5 |
| `.github/` | 2 |

## Unassigned & Untracked

17 items have no assignee and no issue tracking:

Consider assigning ownership or creating tracking issues for these items.

## All Items

### NOTE (17)

- [ ] `.github/workflows/desktop-builds.yml:9`
  > UWP/WinUI 3 application has been removed. WPF is the sole desktop client.

- [ ] `.github/workflows/test-matrix.yml:5`
  > This workflow intentionally does NOT use reusable-dotnet-build.yml because it needs separate C# / F# test runs with per-language arguments, a Category!=Integration filter, platform-conditional jobs, and per-platform Codecov flags. The reusable template targets simpler "build + test entire solution" scenarios.

- [ ] `src/MarketDataCollector.Ui.Services/Services/AdminMaintenanceModels.cs:411`
  > SelfTest*, ErrorCodes*, ShowConfig*, QuickCheck* models are defined in DiagnosticsService.cs to avoid duplication and maintain single source of truth

- [ ] `src/MarketDataCollector.Ui.Services/Services/DataCompletenessService.cs:631`
  > SymbolCompleteness is defined in AdvancedAnalyticsModels.cs to avoid duplication

- [ ] `src/MarketDataCollector.Ui.Services/Services/ProviderHealthService.cs:530`
  > ProviderComparison is defined in AdvancedAnalyticsModels.cs for cross-provider comparison ProviderHealthComparison below is for overall provider ranking

- [ ] `src/MarketDataCollector.Ui.Shared/Endpoints/ConfigEndpoints.cs:154`
  > Status endpoint is handled by StatusEndpoints.MapStatusEndpoints() which provides live status via StatusEndpointHandlers rather than loading from file

- [ ] `src/MarketDataCollector.Wpf/GlobalUsings.cs:7`
  > Type aliases and Contracts namespaces are NOT re-defined here because they are already provided by the referenced MarketDataCollector.Ui.Services project (via its GlobalUsings.cs). Re-defining them would cause CS0101 duplicate type definition errors.

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:28`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:55`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:84`
  > Using null! dependencies - we only verify that ArgumentOutOfRangeException is not thrown The constructor may throw other exceptions (e.g., NullReferenceException) when accessing null dependencies

- [ ] `tests/MarketDataCollector.Tests/Application/Commands/SymbolCommandsTests.cs:19`
  > SymbolCommands requires a SymbolManagementService which needs a ConfigStore. For CanHandle tests we can use a stub since CanHandle doesn't touch the service. For ExecuteAsync tests that require validation (missing value), we need the real command.

- [ ] `tests/MarketDataCollector.Tests/Application/Monitoring/DataQuality/DataFreshnessSlaMonitorTests.cs:523`
  > Actual result depends on current time, so we check the logic is working

- [ ] `tests/MarketDataCollector.Tests/Infrastructure/Resilience/WebSocketResiliencePolicyTests.cs:201`
  > We can't directly invoke the event from outside the class The test validates that the subscription mechanism works

- [ ] `tests/MarketDataCollector.Tests/Storage/StorageChecksumServiceTests.cs:121`
  > File.WriteAllTextAsync uses UTF-8 with BOM by default on some platforms, so we compute expected from the actual file bytes

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/BackfillServiceTests.cs:181`
  > This test verifies the IsRunning property logic In actual usage, CurrentProgress would be set during a backfill operation We're testing the property getter logic here

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/BackfillServiceTests.cs:198`
  > Similar to IsRunning test, this verifies the property logic

- [ ] `tests/MarketDataCollector.Wpf.Tests/Services/NavigationServiceTests.cs:57`
  > This test assumes NavigationService might not be initialized In production, Initialize should be called during app startup

---

## Contributing

When adding TODO comments, please follow these guidelines:

1. **Link to GitHub Issues**: Use `// TODO: Track with issue #123` format
2. **Be Descriptive**: Explain what needs to be done and why
3. **Use Correct Type**:
   - `TODO` - General tasks
   - `FIXME` - Bugs that need fixing
   - `HACK` - Temporary workarounds
   - `NOTE` - Important information

Example:
```csharp
// TODO: Track with issue #123 - Implement retry logic for transient failures
// This is needed because the API occasionally returns 503 errors during peak load.
```
