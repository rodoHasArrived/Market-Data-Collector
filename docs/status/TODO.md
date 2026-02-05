# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-02-05T06:57:18.260106+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 29 |
| **Linked to Issues** | 1 |
| **Untracked** | 28 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 28 | Important notes and documentation |
| `TODO` | 1 | General tasks to complete |

### By Directory

| Directory | Count |
|-----------|-------|
| `src/` | 21 |
| `tests/` | 6 |
| `.github/` | 2 |

## High Priority

Items requiring immediate attention:

- **[NOTE]** `src/MarketDataCollector.Uwp/Services/CredentialService.cs:357`
  - We don't throw here to maintain backward compatibility. Callers should subscribe to CredentialError event for critical operations.

## All Items

### TODO (1)

- [x] `tests/MarketDataCollector.Tests/Serialization/HighPerformanceJsonTests.cs:108` [#670]
  > Track with issue #670 - Alpaca message parsing tests require dedicated JsonSerializerContext These tests are temporarily skipped due to JSON property name collision with source generator. A dedicated JsonSerializerContext for Alpaca messages is needed to properly support these parsing methods. In the meantime, the production code still works correctly using non-source-generated deserialization.

### NOTE (28)

- [ ] `.github/workflows/desktop-app.yml:314`
  > MSIX packaging requires additional setup: 1. A valid signing certificate 2. Package.appxmanifest configuration 3. WindowsPackageType set to MSIX in project For now, we build as unpackaged (WindowsPackageType=None)

- [ ] `.github/workflows/test-matrix.yml:3`
  > On PRs, only ubuntu tests run to reduce billing costs. Full matrix runs on pushes to main.

- [ ] `src/MarketDataCollector.Contracts/Domain/Events/MarketEventPayload.cs:10`
  > [JsonPolymorphic] attribute not supported by WinUI 3 XAML compiler (net472-based) When building for UWP, these attributes are excluded via conditional compilation

- [ ] `src/MarketDataCollector.Uwp/Contracts/IStatusService.cs:34`
  > ServiceHealthResult and ApiResponse<T> are now defined in MarketDataCollector.Contracts.Api.ClientModels.cs

- [ ] `src/MarketDataCollector.Uwp/Services/ApiClientService.cs:409`
  > ApiResponse<T> and ServiceHealthResult are now defined in MarketDataCollector.Contracts.Api.ClientModels.cs (imported via SharedModelAliases.cs)

- [ ] `src/MarketDataCollector.Uwp/Services/CredentialService.cs:357`
  > We don't throw here to maintain backward compatibility. Callers should subscribe to CredentialError event for critical operations.

- [ ] `src/MarketDataCollector.Uwp/Services/CredentialService.cs:487`
  > We don't increment counters or raise events for HasCredential as it's often called in tight loops for UI updates

- [ ] `src/MarketDataCollector.Uwp/Services/CredentialService.cs:523`
  > Empty vault also throws E_ELEMENT_NOT_FOUND on some Windows versions

- [ ] `src/MarketDataCollector.Uwp/Services/StatusService.cs:229`
  > Backfill-related models (BackfillRequest, BackfillHealthResponse, BackfillProviderHealth, SymbolResolutionResponse, BackfillExecutionResponse, BackfillPreset, BackfillExecution, BackfillStatistics) are now defined in MarketDataCollector.Contracts.Api.BackfillApiModels.cs and StatusModels.cs. Type aliases at the top of this file maintain backwards compatibility.

- [ ] `src/MarketDataCollector.Uwp/Services/StorageOptimizationAdvisorService.cs:749`
  > Merging may not save space but reduces file count BytesSaved represents the difference if any

- [ ] `src/MarketDataCollector.Uwp/Services/StorageOptimizationAdvisorService.cs:976`
  > Using GZip for decompression; in production, integrate ZstdSharp for zstd compression For now, we'll use GZip with optimal compression as a fallback

- [ ] `src/MarketDataCollector/Application/Services/GracefulShutdownHandler.cs:512`
  > IFlushable interface is defined in GracefulShutdownService.cs

- [ ] `src/MarketDataCollector/Infrastructure/DataSources/DataSourceConfiguration.cs:597`
  > Vault support (AWS Secrets Manager, Azure Key Vault) requires additional implementation

- [ ] `src/MarketDataCollector/Infrastructure/Providers/Historical/BaseHistoricalDataProvider.cs:194`
  > Name is abstract, so derived class must implement it before this runs

- [ ] `src/MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/EnhancedIBConnectionManager.IBApi.cs:707`
  > The IB API has a typo in the method name (histoicalData vs historicalData) Some versions use one or the other

- [ ] `src/MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/EnhancedIBConnectionManager.IBApi.cs:736`
  > The full EWrapper interface is extensive. Add methods as you need them for trades/ticks/orders.

- [ ] `src/MarketDataCollector/Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs:1113`
  > Intermarket Sweep (14) can be buy or sell, but is typically used for aggressive buying. We'll keep it as Unknown for accuracy.

- [ ] `src/MarketDataCollector/Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs:656`
  > StockSharp candle unsubscription is handled via subscription object

- [ ] `src/MarketDataCollector/Infrastructure/Resilience/WebSocketResiliencePolicy.cs:270`
  > ClientWebSocket doesn't expose ping/pong frames directly This is a simplified version - production code might use custom ping messages

- [ ] `src/MarketDataCollector/Program.cs:1103`
  > GetEnvironmentName(), LoadConfigWithEnvironmentOverlay(), and MergeConfigs() have been removed to consolidate configuration logic through ConfigurationService. Use ConfigurationService.LoadAndPrepareConfig() for full configuration processing.

- [ ] `src/MarketDataCollector/Program.cs:1107`
  > PipelinePublisher has been consolidated into ServiceCompositionRoot and is accessed via DI through the composition root.

- [ ] `src/MarketDataCollector/Program.cs:1462`
  > CreateBackfillProviders has been consolidated into ProviderFactory and is accessed via HostStartup.CreateBackfillProviders() through the composition root.

- [ ] `src/MarketDataCollector/Storage/Services/TierMigrationService.cs:327`
  > Verification of compressed files would need decompression

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
