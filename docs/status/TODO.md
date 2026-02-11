# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-02-11T18:34:19.998255+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 15 |
| **Linked to Issues** | 0 |
| **Untracked** | 15 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 10 | Important notes and documentation |
| `TODO` | 5 | General tasks to complete |

### By Directory

| Directory | Count |
|-----------|-------|
| `src/` | 8 |
| `tests/` | 7 |

## All Items

### TODO (5)

- [ ] `src/MarketDataCollector.Ui.Services/Services/OrderBookVisualizationService.cs:37`
  > Implement once LiveDataService supports SubscribeToDepthAsync

- [ ] `src/MarketDataCollector.Ui.Services/Services/OrderBookVisualizationService.cs:49`
  > Implement once LiveDataService supports UnsubscribeFromDepthAsync

- [ ] `src/MarketDataCollector.Ui.Services/Services/PortablePackagerService.cs:479`
  > Implement once SchemaService supports GetJsonSchema var schema = _schemaService.GetJsonSchema(type); if (!string.IsNullOrEmpty(schema)) { await File.WriteAllTextAsync( Path.Combine(schemasDir, $"{type}_schema.json"), schema, ct); }

- [ ] `src/MarketDataCollector.Ui.Services/Services/PortfolioImportService.cs:223`
  > Implement once WatchlistService supports CreateOrUpdateWatchlistAsync var watchlistService = WatchlistService.Instance; await watchlistService.CreateOrUpdateWatchlistAsync(watchlistName, symbols, ct);

- [ ] `src/MarketDataCollector.Ui.Services/Services/SetupWizardService.cs:656`
  > Implement credential storage once CredentialService supports SaveCredentialAsync

### NOTE (10)

- [ ] `src/MarketDataCollector.Ui.Shared/Endpoints/ConfigEndpoints.cs:138`
  > Status endpoint is handled by StatusEndpoints.MapStatusEndpoints() which provides live status via StatusEndpointHandlers rather than loading from file

- [ ] `src/MarketDataCollector.Uwp/GlobalUsings.cs:7`
  > Type aliases and Contracts namespaces are NOT re-defined here because they are already provided by the referenced MarketDataCollector.Ui.Services project (via its GlobalUsings.cs). Re-defining them would cause CS0101 duplicate type definition errors. =============================================================================

- [ ] `src/MarketDataCollector.Wpf/GlobalUsings.cs:7`
  > Type aliases and Contracts namespaces are NOT re-defined here because they are already provided by the referenced MarketDataCollector.Ui.Services project (via its GlobalUsings.cs). Re-defining them would cause CS0101 duplicate type definition errors. =============================================================================

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
