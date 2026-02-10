# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-02-10T04:39:27.381011+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 8 |
| **Linked to Issues** | 0 |
| **Untracked** | 8 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 7 | Important notes and documentation |
| `TODO` | 1 | General tasks to complete |

### By Directory

| Directory | Count |
|-----------|-------|
| `tests/` | 7 |
| `src/` | 1 |

## All Items

### TODO (1)

- [ ] `src/MarketDataCollector.Infrastructure/Providers/Historical/GapAnalysis/DataGapRepair.cs:412`
  > Implement via dependency injection - Infrastructure cannot reference Storage directly (circular dependency). Inject an IStorageSink or similar abstraction instead.

### NOTE (7)

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
