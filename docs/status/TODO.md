# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-03-11T11:41:56.639533+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 70 |
| **Linked to Issues** | 0 |
| **Untracked** | 70 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `TODO` | 52 | General tasks to complete |
| `NOTE` | 18 | Important notes and documentation |

### By Directory

| Directory | Count |
|-----------|-------|
| `src/` | 57 |
| `tests/` | 11 |
| `.github/` | 2 |

## Unassigned & Untracked

70 items have no assignee and no issue tracking:

Consider assigning ownership or creating tracking issues for these items.

## All Items

### TODO (52)

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateConstants.cs:10`
  > Add your provider's base URL or WebSocket URI. Example for a REST API: public const string BaseUrl = "https://api.example.com/v1"; Example for a WebSocket API: public const string WssUri = "wss://stream.example.com/ws";

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateConstants.cs:23`
  > Set the maximum number of requests allowed per window.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateConstants.cs:26`
  > Set the rate-limit window duration.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateConstants.cs:29`
  > Set the recommended minimum delay between consecutive requests. MinRequestDelay = Window / MaxRequestsPerWindow

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateConstants.cs:41`
  > Add message type constants. Example: public const string Trade = "trade"; public const string Quote = "quote"; public const string Error = "error";

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:29`
  > Replace "template" with the provider ID, display name, type, and category.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:34`
  > Replace with the actual API key environment variable name.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:44`
  > Set to your provider's unique ID (lowercase, e.g., "tiingo").

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:48`
  > Set a human-readable display name.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:52`
  > Describe the provider's capabilities and data coverage.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:56`
  > Set the named HTTP client registered in the DI container. Add a constant to HttpClientNames and register the client in the composition root.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:65`
  > Set an appropriate priority (lower = tried first in failover chains).

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:69`
  > Set based on the provider's rate limit (e.g., 60 req/min → 1 s delay).

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:79`
  > Adjust to match what the provider actually supports.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:103`
  > Add required HTTP headers for this provider. Example: Http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:126`
  > Build the request URL using TemplateEndpoints constants.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:127`
  > Apply rate limiting before each request: await RateLimiter.WaitAsync(ct).ConfigureAwait(false);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:129`
  > Call Http.GetAsync(url, ct) with the resilience pipeline.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:130`
  > Deserialize the response and map to List<HistoricalBar>.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:131`
  > Normalize symbol, convert timestamps to UTC.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateHistoricalDataProvider.cs:132`
  > Log success: Log.Debug("Fetched {Count} bars for {Symbol}", bars.Count, symbol);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:29`
  > Replace "template" with the provider ID, display name, type, and category.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:40`
  > Replace TemplateOptions with the provider's configuration type. private readonly TemplateOptions _options;

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:48`
  > Add the provider-specific options parameter and validate credentials.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:52`
  > TemplateOptions options */)

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:55`
  > Use ProviderSubscriptionRanges.TemplateStart once a range is allocated.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:68`
  > Set to true when credentials are configured; false otherwise (stub mode).

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:85`
  > Set an appropriate priority. Lower = tried first in failover chains.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:89`
  > Adjust capability flags to match what the provider actually supports.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:98`
  > Replace with the actual credential fields and environment variable names.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:113`
  > Send the provider-specific subscribe message. _ = SendAsync(BuildSubscribeMessage(cfg.Symbol), CancellationToken.None);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:124`
  > Send the provider-specific unsubscribe message. _ = SendAsync(BuildUnsubscribeMessage(sub.Symbol), CancellationToken.None);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:130`
  > Implement if the provider supports market depth; otherwise return -1.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:143`
  > Return the provider-specific WebSocket URI. Use constants from TemplateConstants.cs, e.g.: return TemplateEndpoints.WssUri(_options.Feed);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:152`
  > Send the provider-specific authentication message. Example: var auth = JsonSerializer.Serialize(new { action = "auth", key = _options.ApiKey }); await SendAsync(auth, ct).ConfigureAwait(false);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:163`
  > Parse the incoming JSON message and route to the appropriate collector. Example: using var doc = JsonDocument.Parse(message); var type = doc.RootElement.GetProperty("type").GetString(); if (type == TemplateMessageTypes.Trade) HandleTrade(doc.RootElement);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateMarketDataClient.cs:174`
  > Re-subscribe to all active symbols after reconnection. Example: foreach (var sym in Subscriptions.GetActiveSymbols()) await SendAsync(BuildSubscribeMessage(sym), ct).ConfigureAwait(false);

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:28`
  > Replace "template" with the provider ID, display name, type, and category.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:38`
  > Set to your provider's unique ID (lowercase, e.g., "finnhub").

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:42`
  > Set a human-readable display name.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:46`
  > Set the named HTTP client registered in the DI container.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:50`
  > Set the base URL for this provider's REST API.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:54`
  > Set the environment variable name used to load the API key.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:70`
  > If the provider supports asset-type filtering, override SupportedAssetTypes. public override IReadOnlyList<string> SupportedAssetTypes => ["stock", "etf", "crypto"];

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:73`
  > If the provider supports exchange filtering, override SupportedExchanges. public override IReadOnlyList<string> SupportedExchanges => ["NYSE", "NASDAQ"];

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:98`
  > Add provider-specific authentication headers. Example: if (!string.IsNullOrEmpty(ApiKey)) Http.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:123`
  > Build the request URL from BaseUrl and query parameters.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:124`
  > Send the HTTP request and handle errors.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:125`
  > Deserialize the JSON response.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:126`
  > Map provider-specific DTOs to SymbolSearchResult.

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:127`
  > Return the mapped results (up to maxResults).

- [ ] `src/MarketDataCollector.Infrastructure/Adapters/_Template/TemplateSymbolSearchProvider.cs:147`
  > If the provider supports server-side filtering, implement it here. Otherwise, keep this call to the base class which filters client-side.

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
