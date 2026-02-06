# MarketDataCollector Project Context

**Generated:** 2026-02-06 05:15:12 UTC
**Source:** Auto-generated from code annotations

## Key Interfaces

### IAlertDispatcher

**Location:** `MarketDataCollector/Application/Monitoring/Core/IAlertDispatcher.cs`

Centralized alert dispatcher for publishing and subscribing to monitoring alerts.

| Method | Description |
|--------|-------------|
| `void Publish(MonitoringAlert alert)` | Publishes an alert to all subscribers. |
| `Task PublishAsync(MonitoringAlert alert, CancellationToken ct = default)` | Publishes an alert asynchronously. |

### IArchiveMaintenanceService

**Location:** `MarketDataCollector/Storage/Maintenance/IArchiveMaintenanceService.cs`

Interface for archive maintenance service that orchestrates scheduled and on-demand maintenance operations.

| Method | Description |
|--------|-------------|
| `Task<MaintenanceExecution> ExecuteMaintenanceAsync(
        MaintenanceTaskType taskType,
        MaintenanceTaskOptions? options = null,
        string[]? targetPaths = null,
        CancellationToken ct = default)` | Execute a maintenance task immediately. |
| `Task<MaintenanceExecution> TriggerScheduleAsync(string scheduleId, CancellationToken ct = default)` | Trigger a scheduled maintenance to run immediately. |
| `Task<bool> CancelExecutionAsync(string executionId)` | Cancel a running or queued maintenance execution. |
| `intenanceServiceStatus GetStatus()` | Get the current status of the maintenance service. |
| `intenanceSchedule> GetAllSchedules()` | Get all maintenance schedules. |
| `Task<ArchiveMaintenanceSchedule> CreateScheduleAsync(ArchiveMaintenanceSchedule schedule, CancellationToken ct = default)` | Create a new maintenance schedule. |
| `Task<ArchiveMaintenanceSchedule> CreateFromPresetAsync(string presetName, string name, CancellationToken ct = default)` | Create a schedule from a preset. |
| `Task<ArchiveMaintenanceSchedule> UpdateScheduleAsync(ArchiveMaintenanceSchedule schedule, CancellationToken ct = default)` | Update an existing schedule. |
| `Task<bool> DeleteScheduleAsync(string scheduleId, CancellationToken ct = default)` | Delete a schedule. |
| `Task<bool> SetScheduleEnabledAsync(string scheduleId, bool enabled, CancellationToken ct = default)` | Enable or disable a schedule. |
| `intenanceSchedule> GetDueSchedules(DateTimeOffset asOf)` | Get schedules that are due for execution. |
| `intenanceScheduleSummary GetStatusSummary()` | Get an overview of all schedules. |
| `void RecordExecution(MaintenanceExecution execution)` | Record a new execution. |
| `void UpdateExecution(MaintenanceExecution execution)` | Update an existing execution record. |
| `intenanceExecution> GetRecentExecutions(int limit = 50)` | Get recent executions. |
| `intenanceExecution> GetExecutionsForSchedule(string scheduleId, int limit = 50)` | Get executions for a specific schedule. |
| `intenanceExecution> GetFailedExecutions(int limit = 50)` | Get failed executions. |
| `intenanceExecution> GetExecutionsByTimeRange(DateTimeOffset from, DateTimeOffset to)` | Get executions within a time range. |
| `intenanceStatistics GetStatistics(TimeSpan? period = null)` | Get overall maintenance statistics. |
| `Task<int> CleanupOldRecordsAsync(int maxAgeDays = 90, CancellationToken ct = default)` | Clean up old execution records. |

### ICliCommand

**Location:** `MarketDataCollector/Application/Commands/ICliCommand.cs`

Interface for CLI command handlers extracted from Program.cs. Each implementation handles one or more related CLI flags.

| Method | Description |
|--------|-------------|
| `bool CanHandle(string[] args)` | Returns true if this command should handle the given args. |
| `Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)` | Executes the command. Returns the exit code (0 = success). |

### IConfigurationProvider

**Location:** `MarketDataCollector/Application/Config/IConfigurationProvider.cs`

Unified configuration provider interface for consistent access to application configuration across all components.

| Method | Description |
|--------|-------------|
| `void RegisterMetadata(ConfigurationMetadata metadata)` | Registers configuration metadata. |
| `void Reload()` | Reloads configuration from all sources. |

### ICredentialStore

**Location:** `MarketDataCollector/Application/Credentials/ICredentialStore.cs`

Centralized credential store providing unified access to API credentials across all providers with caching, refresh, and validation capabilities.

| Method | Description |
|--------|-------------|
| `Task<CredentialResult> GetCredentialAsync(string provider, string key, CancellationToken ct = default)` | Gets a credential value for a provider. |
| `Task SetCredentialAsync(string provider, string key, string value, CancellationToken ct = default)` | Sets a credential value (for runtime configuration). |
| `Task<bool> HasValidCredentialAsync(string provider, string key, CancellationToken ct = default)` | Checks if a credential exists and is valid. |
| `Task<CredentialResult> RefreshCredentialAsync(string provider, string key, CancellationToken ct = default)` | Refreshes a credential (for OAuth tokens). |
| `Task<CredentialValidationResult> ValidateProviderCredentialsAsync(string provider, CancellationToken ct = default)` | Validates all credentials for a provider. |
| `void RegisterCredential(CredentialMetadata metadata)` | Registers credential metadata for a provider. |
| `void ClearCache(string? provider = null)` | Clears cached credentials (forces reload on next access). |

### IDataSource

**Location:** `MarketDataCollector/Infrastructure/DataSources/IDataSource.cs`

Unified base interface for all data sources (real-time and historical). Provides a common abstraction for provider discovery, health monitoring, and lifecycle management.

| Method | Description |
|--------|-------------|
| `Task InitializeAsync(CancellationToken ct = default)` | Initializes the data source, validates credentials, and tests connectivity. |
| `Task<bool> ValidateCredentialsAsync(CancellationToken ct = default)` | Validates that credentials are properly configured. Returns true if no credentials are required or if they are valid. |
| `Task<bool> TestConnectivityAsync(CancellationToken ct = default)` | Tests connectivity to the data source. |

### IHealthCheckProvider

**Location:** `MarketDataCollector/Application/Monitoring/Core/IHealthCheckProvider.cs`

Interface for components that provide health check capabilities. Implementations should be lightweight and complete quickly.

| Method | Description |
|--------|-------------|
| `Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)` | Performs a health check and returns the result. |
| `void Register(IHealthCheckProvider provider)` | Registers a health check provider. |
| `void Unregister(string componentName)` | Unregisters a health check provider. |
| `Task<AggregatedHealthReport> CheckAllAsync(CancellationToken ct = default)` | Runs all registered health checks and returns an aggregated report. |

### IHistoricalDataProvider

**Location:** `MarketDataCollector/Infrastructure/Providers/Historical/IHistoricalDataProvider.cs`

Unified contract for fetching historical data from vendors. Consolidates previous V1, V2, and Extended interfaces into a single contract with optional capabilities indicated by properties.

| Method | Description |
|--------|-------------|
| `Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)` | Fetch daily OHLCV bars for a symbol within the specified date range. |

### IHistoricalDataSource

**Location:** `MarketDataCollector/Infrastructure/DataSources/IHistoricalDataSource.cs`

Interface for historical data sources providing bar data, dividends, splits, and other historical market information.

| Method | Description |
|--------|-------------|
| `Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets historical daily OHLCV bars for a symbol. |
| `Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets historical daily bars with adjustment information. |
| `Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(
        string symbol,
        string interval,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)` | Gets historical intraday bars for a symbol. |
| `Task<IReadOnlyList<DividendInfo>> GetDividendsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets dividend history for a symbol. |
| `Task<IReadOnlyList<SplitInfo>> GetSplitsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets stock split history for a symbol. |
| `Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets historical daily OHLCV bars for a symbol. |
| `Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(
        string symbol,
        string interval,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)` | Gets historical intraday bars for a symbol. |
| `Task<IReadOnlyList<DividendInfo>> GetDividendsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets dividend history for a symbol. |
| `Task<IReadOnlyList<SplitInfo>> GetSplitsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)` | Gets stock split history for a symbol. |

### IMarketDataClient

**Location:** `MarketDataCollector/Infrastructure/IMarketDataClient.cs`

Market data client abstraction for provider-agnostic market data ingestion. Implementations must be non-blocking on publish paths.

| Method | Description |
|--------|-------------|
| `Task ConnectAsync(CancellationToken ct = default)` | Market data client abstraction for provider-agnostic market data ingestion. Implementations must be non-blocking on publish paths. |
| `Task DisconnectAsync(CancellationToken ct = default)` | Market data client abstraction for provider-agnostic market data ingestion. Implementations must be non-blocking on publish paths. |
| `int SubscribeMarketDepth(SymbolConfig cfg)` | <remarks> This interface is the core contract for ADR-001 (Provider Abstraction Pattern). All streaming data providers must implement this interface. Implements <see cref="IProviderMetadata"/> for unified provider discovery and capability reporting across all provider types. </remarks> [ImplementsAdr("ADR-001", "Core streaming data provider contract")] [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); |
| `void UnsubscribeMarketDepth(int subscriptionId)` | All streaming data providers must implement this interface. Implements <see cref="IProviderMetadata"/> for unified provider discovery and capability reporting across all provider types. </remarks> [ImplementsAdr("ADR-001", "Core streaming data provider contract")] [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); int SubscribeMarketDepth(SymbolConfig cfg); |
| `int SubscribeTrades(SymbolConfig cfg)` | and capability reporting across all provider types. </remarks> [ImplementsAdr("ADR-001", "Core streaming data provider contract")] [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); int SubscribeMarketDepth(SymbolConfig cfg); void UnsubscribeMarketDepth(int subscriptionId); |
| `void UnsubscribeTrades(int subscriptionId)` | [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); int SubscribeMarketDepth(SymbolConfig cfg); void UnsubscribeMarketDepth(int subscriptionId); int SubscribeTrades(SymbolConfig cfg); |

### IMarketEventPublisher

**Location:** `MarketDataCollector/Domain/Events/IMarketEventPublisher.cs`

Minimal publish contract so collectors can emit MarketEvents without knowing transport. Publish must be non-blocking (hot path).

| Method | Description |
|--------|-------------|
| `bool TryPublish(in MarketEvent evt)` | Minimal publish contract so collectors can emit MarketEvents without knowing transport. Publish must be non-blocking (hot path). |

### IOperationalScheduler

**Location:** `MarketDataCollector/Application/Scheduling/IOperationalScheduler.cs`

Centralized operational scheduler for coordinating maintenance tasks, backfill operations, and other scheduled activities with trading hours.

| Method | Description |
|--------|-------------|
| `Task<ScheduleDecision> CanExecuteAsync(
        OperationType operationType,
        ResourceRequirements? requirements = null,
        CancellationToken ct = default)` | Checks if an operation can execute now based on current conditions. |
| `void RegisterMaintenanceWindow(MaintenanceWindow window)` | Registers a maintenance window. |
| `void RemoveMaintenanceWindow(string windowName)` | Removes a maintenance window. |
| `bool IsTradingDay(DateOnly date, string market = "US")` | Checks if a specific date is a trading day. |

### IProviderMetadata

**Location:** `MarketDataCollector/Infrastructure/Providers/Core/IProviderMetadata.cs`

Unified metadata interface that all provider types implement. Enables consistent discovery, routing, and UI presentation across streaming, backfill, and symbol search providers.

### IQualityAnalyzer

**Location:** `MarketDataCollector/Application/Monitoring/DataQuality/IQualityAnalyzer.cs`

Analyzers can be discovered, registered, and run by the quality analysis engine.

| Method | Description |
|--------|-------------|
| `Task<QualityAnalysisResult> AnalyzeAsync(
        TData data,
        QualityAnalyzerConfig? config = null,
        CancellationToken ct = default)` | Analyzes data and returns quality issues. |
| `string> ValidateConfig(QualityAnalyzerConfig config)` | Validates configuration for this analyzer. |

### IQuoteStateStore

**Location:** `MarketDataCollector/Domain/Collectors/IQuoteStateStore.cs`

Provides access to the latest Best-Bid/Offer quote per symbol for downstream inference (e.g., aggressor side).

| Method | Description |
|--------|-------------|
| `bool TryGet(string symbol, out BboQuotePayload? quote)` | Provides access to the latest Best-Bid/Offer quote per symbol for downstream inference (e.g., aggressor side). public interface IQuoteStateStore { |
| `bool TryRemove(string symbol, out BboQuotePayload? removed)` | Remove cached state for a symbol. Returns <c>true</c> if the symbol existed. |
| `string, BboQuotePayload> Snapshot()` | Snapshot the current cache for inspection/monitoring without exposing internal mutability. |

### IRealtimeDataSource

**Location:** `MarketDataCollector/Infrastructure/DataSources/IRealtimeDataSource.cs`

Interface for real-time data sources providing streaming market data. Extends IDataSource with real-time specific functionality for trades, quotes, and market depth.

| Method | Description |
|--------|-------------|
| `Task ConnectAsync(CancellationToken ct = default)` | Connects to the real-time data stream. |
| `Task DisconnectAsync(CancellationToken ct = default)` | Disconnects from the real-time data stream. |
| `int SubscribeTrades(SymbolConfig config)` | Subscribes to real-time trade prints for the specified symbol. |
| `void UnsubscribeTrades(int subscriptionId)` | Unsubscribes from a trade subscription. |
| `int SubscribeQuotes(SymbolConfig config)` | Subscribes to real-time BBO quotes for the specified symbol. |
| `void UnsubscribeQuotes(int subscriptionId)` | Unsubscribes from a quote subscription. |
| `int SubscribeMarketDepth(SymbolConfig config)` | Subscribes to market depth for the specified symbol. |
| `void UnsubscribeMarketDepth(int subscriptionId)` | Unsubscribes from a depth subscription. |
| `void UnsubscribeAll()` | Unsubscribes from all active subscriptions. |
| `int SubscribeTrades(SymbolConfig config)` | Subscribes to trade prints for the given symbol. |
| `void UnsubscribeTrades(int subscriptionId)` | Unsubscribes from a trade subscription. |
| `int SubscribeQuotes(SymbolConfig config)` | Subscribes to BBO quotes for the given symbol. |
| `void UnsubscribeQuotes(int subscriptionId)` | Unsubscribes from a quote subscription. |
| `int SubscribeMarketDepth(SymbolConfig config)` | Subscribes to market depth for the given symbol. |
| `void UnsubscribeMarketDepth(int subscriptionId)` | Unsubscribes from a depth subscription. |

### ISourceRegistry

**Location:** `MarketDataCollector/Storage/Interfaces/ISourceRegistry.cs`

Interface for managing data source and symbol registry information.

| Method | Description |
|--------|-------------|
| `void RegisterSource(SourceInfo source)` | Registers or updates a data source. |
| `void RegisterSymbol(SymbolInfo symbol)` | Registers or updates a symbol. |
| `string ResolveSymbolAlias(string alias)` | Resolves a symbol alias to its canonical name. |

### IStorageCatalogService

**Location:** `MarketDataCollector/Storage/Interfaces/IStorageCatalogService.cs`

Service for managing the storage catalog and manifest system. Provides comprehensive indexing, integrity verification, and metadata management.

| Method | Description |
|--------|-------------|
| `Task InitializeAsync(CancellationToken ct = default)` | Initializes or loads the catalog from the storage root. |
| `Task<CatalogRebuildResult> RebuildCatalogAsync(
        CatalogRebuildOptions? options = null,
        IProgress<CatalogRebuildProgress>? progress = null,
        CancellationToken ct = default)` | Rebuilds the catalog by scanning all storage directories. |
| `Task UpdateFileEntryAsync(IndexedFileEntry entry, CancellationToken ct = default)` | Updates the catalog with a new or modified file entry. |
| `Task RemoveFileEntryAsync(string relativePath, CancellationToken ct = default)` | Removes a file entry from the catalog. |
| `Task UpdateDirectoryIndexAsync(DirectoryIndex index, CancellationToken ct = default)` | Updates or creates a directory index. |
| `Task<DirectoryScanResult> ScanDirectoryAsync(
        string path,
        bool recursive = false,
        CancellationToken ct = default)` | Scans a directory and creates/updates its index. |
| `Task<CatalogVerificationResult> VerifyIntegrityAsync(
        CatalogVerificationOptions? options = null,
        IProgress<CatalogVerificationProgress>? progress = null,
        CancellationToken ct = default)` | Verifies catalog integrity by checking all file checksums. |
| `Task SaveCatalogAsync(CancellationToken ct = default)` | Saves the catalog to disk. |
| `Task ExportCatalogAsync(string outputPath, CatalogExportFormat format = CatalogExportFormat.Json, CancellationToken ct = default)` | Exports the catalog to a portable format. |
| `Task InitializeAsync(CancellationToken ct = default)` | Initializes the registry from storage. |
| `Task RegisterSymbolAsync(SymbolRegistryEntry entry, CancellationToken ct = default)` | Registers or updates a symbol entry. |
| `string ResolveAlias(string alias)` | Resolves an alias to a canonical symbol. |
| `Task AddAliasAsync(string canonical, SymbolAlias alias, CancellationToken ct = default)` | Adds an alias for a symbol. |
| `Task AddProviderMappingAsync(string canonical, string provider, string providerSymbol, CancellationToken ct = default)` | Adds a provider mapping. |
| `Task SaveRegistryAsync(CancellationToken ct = default)` | Saves the registry to disk. |
| `Task<int> ImportSymbolsAsync(IEnumerable<SymbolRegistryEntry> symbols, bool merge = true, CancellationToken ct = default)` | Imports symbols from an external source. |

### IStoragePolicy

**Location:** `MarketDataCollector/Storage/Interfaces/IStoragePolicy.cs`

| Method | Description |
|--------|-------------|
| `string GetPath(MarketEvent evt)` | - |

### IStorageSink

**Location:** `MarketDataCollector/Storage/Interfaces/IStorageSink.cs`

| Method | Description |
|--------|-------------|
| `Task AppendAsync(MarketEvent evt, CancellationToken ct = default)` | - |
| `Task FlushAsync(CancellationToken ct = default)` | - |

### ISymbolResolver

**Location:** `MarketDataCollector/Infrastructure/Providers/Historical/SymbolResolution/ISymbolResolver.cs`

Contract for resolving and normalizing symbols across different providers and exchanges.

| Method | Description |
|--------|-------------|
| `Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default)` | Search for symbols matching a query. |

### ISymbolSearchProvider

**Location:** `MarketDataCollector/Infrastructure/Providers/SymbolSearch/ISymbolSearchProvider.cs`

Interface for symbol search and autocomplete providers.

| Method | Description |
|--------|-------------|
| `Task<bool> IsAvailableAsync(CancellationToken ct = default)` | Check if the provider is available/configured. |
| `Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken ct = default)` | Search for symbols matching the query. |
| `Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        string? assetType = null,
        string? exchange = null,
        CancellationToken ct = default)` | Search for symbols with filtering options. |

### ISymbolStateStore

**Location:** `MarketDataCollector/Infrastructure/Shared/ISymbolStateStore.cs`

Generic interface for thread-safe symbol-keyed state storage. Abstracts the 35+ ConcurrentDictionary usages across the codebase into a reusable pattern with consistent semantics.

| Method | Description |
|--------|-------------|
| `bool TryGet(string symbol, out T? state)` | Gets state for a symbol if it exists. |
| `void Set(string symbol, T state)` | Updates state for a symbol. |
| `bool Remove(string symbol)` | Removes state for a symbol. |
| `bool TryRemove(string symbol, out T? state)` | Removes state for a symbol and returns it. |
| `bool Contains(string symbol)` | Checks if state exists for a symbol. |
| `string> GetSymbols()` | Gets all symbols with state. |
| `string, T> GetSnapshot()` | Gets a snapshot of all state (thread-safe copy). |
| `void Clear()` | Clears all state. |
| `void ForEach(Action<string, T> action)` | Applies an action to all states. |
| `int RemoveStale(Func<string, T, bool> isStale)` | Removes stale entries based on a predicate. |

## Data Sources

| Name | Type | Category | Location |
|------|------|----------|----------|
| Alpaca Markets | Hybrid | Broker | `MarketDataCollector/Infrastructure/DataSources/DataSourceAttribute.cs` |

## ADR Implementations

### ADR-001

| Type | Location | Description |
|------|----------|-------------|
| `AlertDispatcher` | `MarketDataCollector/Application/Monitoring/Core/AlertDispatcher.cs` | Alert dispatcher implementation |
| `AlpacaHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Alpaca/AlpacaHistoricalDataProvider.cs` | Alpaca historical data provider implementation |
| `AlpacaMarketDataClient` | `MarketDataCollector/Infrastructure/Contracts/ImplementsAdrAttribute.cs` | Provider Abstraction Pattern |
| `AlpacaMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` | Alpaca streaming data provider implementation |
| `AlpacaSymbolSearchProviderRefactored` | `MarketDataCollector/Infrastructure/Providers/SymbolSearch/AlpacaSymbolSearchProviderRefactored.cs` | Alpaca symbol search provider implementation |
| `AlphaVantageHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/AlphaVantage/AlphaVantageHistoricalDataProvider.cs` | Alpha Vantage historical data provider implementation |
| `BackfillCoordinator` | `MarketDataCollector/Application/Http/BackfillCoordinator.cs` | Uses ProviderRegistry for unified provider discovery |
| `BaseHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/BaseHistoricalDataProvider.cs` | Base historical data provider implementation |
| `BaseSymbolSearchProvider` | `MarketDataCollector/Infrastructure/Providers/SymbolSearch/BaseSymbolSearchProvider.cs` | Base symbol search provider implementation |
| `CompositeHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/CompositeHistoricalDataProvider.cs` | Composite historical data provider with failover |
| `ConfigStore` | `MarketDataCollector/Application/Http/ConfigStore.cs` | Consolidated configuration store shared by all hosts |
| `ConfigurationProviderExtensions` | `MarketDataCollector/Application/Config/IConfigurationProvider.cs` | Unified configuration provider |
| `CredentialStoreExtensions` | `MarketDataCollector/Application/Credentials/ICredentialStore.cs` | Centralized credential management |
| `ExpiringSymbolStateStore` | `MarketDataCollector/Infrastructure/Shared/ISymbolStateStore.cs` | Time-based expiring symbol state store |
| `FinnhubHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Finnhub/FinnhubHistoricalDataProvider.cs` | Finnhub historical data provider implementation |
| `FinnhubSymbolSearchProviderRefactored` | `MarketDataCollector/Infrastructure/Providers/SymbolSearch/FinnhubSymbolSearchProviderRefactored.cs` | Finnhub symbol search provider implementation |
| `for` | `MarketDataCollector/Application/Config/IConfigurationProvider.cs` | Base configuration section |
| `HealthCheckAggregator` | `MarketDataCollector/Application/Monitoring/Core/HealthCheckAggregator.cs` | Health check aggregator implementation |
| `HostStartup` | `MarketDataCollector/Application/Composition/HostStartup.cs` | Unified host startup for all deployment modes |
| `IBHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` | Interactive Brokers historical data provider implementation |
| `IBHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` | Interactive Brokers historical data provider stub |
| `IBMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | Interactive Brokers streaming data provider implementation |
| `IBMarketDataClientIBApi` | `MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | Interactive Brokers API streaming data provider |
| `MarketDataClientFactory` | `MarketDataCollector/Infrastructure/Providers/MarketDataClientFactory.cs` | Factory-based provider creation for runtime switching |
| `MarketDataClientFactory` | `MarketDataCollector/Infrastructure/Providers/MarketDataClientFactory.cs` | Unified factory replacing scattered provider creation |
| `NasdaqDataLinkHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/NasdaqDataLink/NasdaqDataLinkHistoricalDataProvider.cs` | Nasdaq Data Link historical data provider implementation |
| `NoOpMarketDataClient` | `MarketDataCollector/Infrastructure/NoOpMarketDataClient.cs` | No-op data provider for disabled/unconfigured scenarios |
| `NYSEDataSource` | `MarketDataCollector/Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs` | NYSE streaming and historical data provider implementation |
| `PolygonHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Polygon/PolygonHistoricalDataProvider.cs` | Polygon.io historical data provider implementation |
| `PolygonMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` | Polygon.io streaming data provider implementation |
| `ProviderFactory` | `MarketDataCollector/Infrastructure/Providers/Core/ProviderFactory.cs` | Unified provider factory for capability-driven registration |
| `ProviderRegistry` | `MarketDataCollector/Infrastructure/Providers/Core/ProviderRegistry.cs` | Centralized provider registry for plugin-style management |
| `ProviderServiceExtensions` | `MarketDataCollector/Infrastructure/Providers/Core/ProviderServiceExtensions.cs` | Unified DI registration for all provider types |
| `ServiceCompositionRoot` | `MarketDataCollector/Application/Composition/ServiceCompositionRoot.cs` | Centralized composition root for service configuration |
| `ServiceRegistry` | `MarketDataCollector/Application/Services/ServiceRegistry.cs` | Centralized service registry for organization and discovery |
| `StockSharpHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/StockSharp/StockSharpHistoricalDataProvider.cs` | StockSharp historical data provider implementation |
| `StockSharpMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs` | StockSharp streaming data provider implementation |
| `StockSharpSymbolSearchProvider` | `MarketDataCollector/Infrastructure/Providers/Streaming/StockSharp/StockSharpSymbolSearchProvider.cs` | StockSharp symbol search provider implementation |
| `StooqHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Stooq/StooqHistoricalDataProvider.cs` | Stooq historical data provider implementation |
| `SubscriptionManager` | `MarketDataCollector/Infrastructure/Shared/SubscriptionManager.cs` | Centralized subscription management for providers |
| `SymbolStateStore` | `MarketDataCollector/Infrastructure/Shared/ISymbolStateStore.cs` | Centralized symbol state management |
| `SymbolStateStore` | `MarketDataCollector/Infrastructure/Shared/ISymbolStateStore.cs` | ConcurrentDictionary-based symbol state store |
| `TiingoHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Tiingo/TiingoHistoricalDataProvider.cs` | Tiingo historical data provider implementation |
| `UiServer` | `MarketDataCollector/Application/Http/UiServer.cs` | UiServer uses centralized composition root |
| `WebHostAdapter` | `MarketDataCollector/Application/Composition/HostAdapters.cs` | Host adapters for mode-specific behavior |
| `WebSocketProviderBase` | `MarketDataCollector/Infrastructure/Shared/WebSocketProviderBase.cs` | Base WebSocket streaming data provider implementation |
| `YahooFinanceHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/YahooFinance/YahooFinanceHistoricalDataProvider.cs` | Yahoo Finance historical data provider implementation |

### ADR-002

| Type | Location | Description |
|------|----------|-------------|
| `PortableDataPackager` | `MarketDataCollector/Storage/Packaging/PortableDataPackager.cs` | Portable packaging for tiered storage export |

### ADR-004

| Type | Location | Description |
|------|----------|-------------|
| `AlpacaHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Alpaca/AlpacaHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `AlpacaMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` | All async methods support CancellationToken |
| `AlphaVantageHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/AlphaVantage/AlphaVantageHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `BaseHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/BaseHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `BaseSymbolSearchProvider` | `MarketDataCollector/Infrastructure/Providers/SymbolSearch/BaseSymbolSearchProvider.cs` | All async methods support CancellationToken |
| `CompositeHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/CompositeHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `FinnhubHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Finnhub/FinnhubHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `IBHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `IBMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | All async methods support CancellationToken |
| `IBMarketDataClientIBApi` | `MarketDataCollector/Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | All async methods support CancellationToken |
| `NasdaqDataLinkHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/NasdaqDataLink/NasdaqDataLinkHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `NoOpMarketDataClient` | `MarketDataCollector/Infrastructure/NoOpMarketDataClient.cs` | All async methods support CancellationToken |
| `NYSEDataSource` | `MarketDataCollector/Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs` | All async methods support CancellationToken |
| `PolygonHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Polygon/PolygonHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `PolygonMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` | All async methods support CancellationToken |
| `StockSharpHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/StockSharp/StockSharpHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `StockSharpMarketDataClient` | `MarketDataCollector/Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs` | All async methods support CancellationToken |
| `StockSharpSymbolSearchProvider` | `MarketDataCollector/Infrastructure/Providers/Streaming/StockSharp/StockSharpSymbolSearchProvider.cs` | All async methods support CancellationToken |
| `StooqHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Stooq/StooqHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `TiingoHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/Tiingo/TiingoHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `WebSocketProviderBase` | `MarketDataCollector/Infrastructure/Shared/WebSocketProviderBase.cs` | All async methods support CancellationToken |
| `YahooFinanceHistoricalDataProvider` | `MarketDataCollector/Infrastructure/Providers/Historical/YahooFinance/YahooFinanceHistoricalDataProvider.cs` | All async methods support CancellationToken |

### ADR-005

| Type | Location | Description |
|------|----------|-------------|
| `DataSourceAttribute` | `MarketDataCollector/Infrastructure/DataSources/DataSourceAttribute.cs` | Core attribute for provider discovery |

### ADR-010

| Type | Location | Description |
|------|----------|-------------|
| `HttpClientConfiguration` | `MarketDataCollector/Infrastructure/Http/HttpClientConfiguration.cs` | HttpClientFactory for proper HTTP client lifecycle management |

