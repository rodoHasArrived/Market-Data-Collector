# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-03-11T11:23:09.300004+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 18 |
| **Linked to Issues** | 0 |
| **Untracked** | 18 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 18 | Important notes and documentation |

### By Directory

| Directory | Count |
|-----------|-------|
| `tests/` | 11 |
| `src/` | 5 |
| `.github/` | 2 |

## Unassigned & Untracked

18 items have no assignee and no issue tracking:

Consider assigning ownership or creating tracking issues for these items.

## All Items

### NOTE (18)

- [ ] `.github/workflows/desktop-builds.yml:9`
  > UWP/WinUI 3 application has been removed. WPF is the sole desktop client.

- [ ] `.github/workflows/test-matrix.yml:5`
  > This workflow intentionally does NOT use reusable-dotnet-build.yml because it needs separate C# / F# test runs with per-language arguments, a Category!=Integration filter, platform-conditional jobs, and per-platform Codecov flags. The reusable template targets simpler "build + test entire solution" scenarios.

- [ ] `src/MarketDataCollector.Ui.Services/Services/AdminMaintenanceModels.cs:411`
  > SelfTest*, ErrorCodes*, ShowConfig*, QuickCheck* models are defined in DiagnosticsService.cs to avoid duplication and maintain single source of truth

- [ ] `src/MarketDataCollector.Ui.Services/Services/DataCompletenessService.cs:633`
  > SymbolCompleteness is defined in AdvancedAnalyticsModels.cs to avoid duplication

- [ ] `src/MarketDataCollector.Ui.Services/Services/ProviderHealthService.cs:516`
  > ProviderComparison is defined in AdvancedAnalyticsModels.cs for cross-provider comparison ProviderHealthComparison below is for overall provider ranking

- [ ] `src/MarketDataCollector.Ui.Shared/Endpoints/ConfigEndpoints.cs:213`
  > Status endpoint is handled by StatusEndpoints.MapStatusEndpoints() which provides live status via StatusEndpointHandlers rather than loading from file

- [ ] `src/MarketDataCollector.Wpf/GlobalUsings.cs:7`
  > Type aliases and Contracts namespaces are NOT re-defined here because they are already provided by the referenced MarketDataCollector.Ui.Services project (via its GlobalUsings.cs). Re-defining them would cause CS0101 duplicate type definition errors.

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:28`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:55`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:84`
  > Using null! dependencies - we only verify that ArgumentOutOfRangeException is not thrown The constructor may throw other exceptions (e.g., NullReferenceException) when accessing null dependencies

- [ ] `tests/MarketDataCollector.Tests/Application/Monitoring/DataQuality/DataFreshnessSlaMonitorTests.cs:525`
  > Actual result depends on current time, so we check the logic is working

- [ ] `tests/MarketDataCollector.Tests/Application/Pipeline/EventPipelineTests.cs:293`
  > the consumer drains the entire batch from the channel before processing, so the channel is empty once the consumer is blocked.

- [ ] `tests/MarketDataCollector.Tests/Application/Pipeline/EventPipelineTests.cs:525`
  > the consumer drains the entire batch from the channel before processing, so the channel is empty once the consumer is blocked.

- [ ] `tests/MarketDataCollector.Tests/Storage/StorageChecksumServiceTests.cs:121`
  > File.WriteAllTextAsync uses UTF-8 with BOM by default on some platforms, so we compute expected from the actual file bytes

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/ScheduledMaintenanceServiceTests.cs:85`
  > since this is a singleton shared across tests, if StartScheduler was previously called, we stop it first to ensure test isolation.

- [ ] `tests/MarketDataCollector.Wpf.Tests/Services/NavigationServiceTests.cs:78`
  > This test assumes NavigationService might not be initialized In production, Initialize should be called during app startup

- [ ] `tests/MarketDataCollector.Wpf.Tests/Services/OfflineTrackingPersistenceServiceTests.cs:27`
  > Singleton state may persist across tests. We explicitly shut down first to verify the default state transition.

- [ ] `tests/MarketDataCollector.Wpf.Tests/Services/PendingOperationsQueueServiceTests.cs:30`
  > This may not be false if other tests have run InitializeAsync. We test the lifecycle explicitly below.

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
