# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-02-05T08:06:30.206441+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 8 |
| **Linked to Issues** | 1 |
| **Untracked** | 7 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 5 | Important notes and documentation |
| `TODO` | 3 | General tasks to complete |

### By Directory

| Directory | Count |
|-----------|-------|
| `tests/` | 6 |
| `src/` | 2 |

## All Items

### TODO (3)

- [x] `tests/MarketDataCollector.Tests/Serialization/HighPerformanceJsonTests.cs:108` [#670]
  > Track with issue #670 - Alpaca message parsing tests require dedicated JsonSerializerContext These tests are temporarily skipped due to JSON property name collision with source generator. A dedicated JsonSerializerContext for Alpaca messages is needed to properly support these parsing methods. In the meantime, the production code still works correctly using non-source-generated deserialization.

- [ ] `src/MarketDataCollector.Uwp/Services/StorageOptimizationAdvisorService.cs:976`
  > Using GZip for decompression; in production, integrate ZstdSharp for zstd compression. For now, we'll use GZip with optimal compression as a fallback.

- [ ] `src/MarketDataCollector/Infrastructure/DataSources/DataSourceConfiguration.cs:597`
  > Vault support (AWS Secrets Manager, Azure Key Vault) requires additional implementation.

### NOTE (5)

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:28`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:55`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:84`
  > Using null! dependencies - we only verify that ArgumentOutOfRangeException is not thrown The constructor may throw other exceptions (e.g., NullReferenceException) when accessing null dependencies

- [ ] `tests/MarketDataCollector.Tests/Application/Monitoring/DataQuality/DataFreshnessSlaMonitorTests.cs:523`
  > Actual result depends on current time, so we check the logic is working

- [ ] `tests/MarketDataCollector.Tests/Infrastructure/Resilience/WebSocketResiliencePolicyTests.cs:203`
  > We can't directly invoke the event from outside the class The test validates that the subscription mechanism works

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
