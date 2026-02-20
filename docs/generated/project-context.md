# MarketDataCollector Project Context

**Generated:** 2026-02-20 04:36:00 UTC
**Source:** Auto-generated from code annotations

## Key Interfaces

### IAdminMaintenanceService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IAdminMaintenanceService.cs`

Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications.

| Method | Description |
|--------|-------------|
| `Task<MaintenanceScheduleResult> GetMaintenanceScheduleAsync(CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<OperationResult> UpdateMaintenanceScheduleAsync(MaintenanceScheduleConfig schedule, CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `intenance runs
    Task<MaintenanceRunResult> RunMaintenanceNowAsync(MaintenanceRunOptions? options = null, CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<MaintenanceRunResult> GetMaintenanceRunStatusAsync(string runId, CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<MaintenanceHistoryResult> GetMaintenanceHistoryAsync(int limit = 20, CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<TierConfigResult> GetTierConfigurationAsync(CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<OperationResult> UpdateTierConfigurationAsync(List<StorageTierConfig> tiers, bool autoMigrationEnabled, string? migrationSchedule = null, CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<TierMigrationResult> MigrateToTierAsync(string targetTier, TierMigrationOptions? options = null, CancellationToken ct = default)` | Interface for administrative and maintenance operations including archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<TierUsageResult> GetTierUsageAsync(CancellationToken ct = default)` | archive scheduling, tier migration, retention policies, and file cleanup. Shared between WPF desktop applications. |
| `Task<RetentionPoliciesResult> GetRetentionPoliciesAsync(CancellationToken ct = default)` | - |
| `Task<OperationResult> SaveRetentionPolicyAsync(StorageRetentionPolicy policy, CancellationToken ct = default)` | - |
| `Task<OperationResult> DeleteRetentionPolicyAsync(string policyId, CancellationToken ct = default)` | - |
| `Task<RetentionApplyResult> ApplyRetentionPoliciesAsync(bool dryRun = false, CancellationToken ct = default)` | - |
| `Task<CleanupPreviewResult> PreviewCleanupAsync(CleanupOptions options, CancellationToken ct = default)` | - |
| `Task<MaintenanceCleanupResult> ExecuteCleanupAsync(CleanupOptions options, CancellationToken ct = default)` | - |
| `Task<PermissionValidationResult> ValidatePermissionsAsync(CancellationToken ct = default)` | - |
| `Task<SelfTestResult> RunSelfTestAsync(SelfTestOptions? options = null, CancellationToken ct = default)` | - |
| `Task<ErrorCodesResult> GetErrorCodesAsync(CancellationToken ct = default)` | - |
| `Task<ShowConfigResult> ShowConfigAsync(CancellationToken ct = default)` | - |
| `Task<QuickCheckResult> RunQuickCheckAsync(CancellationToken ct = default)` | - |

### IAlertDispatcher

**Location:** `src/MarketDataCollector.Core/Monitoring/Core/IAlertDispatcher.cs`

Centralized alert dispatcher for publishing and subscribing to monitoring alerts.

| Method | Description |
|--------|-------------|
| `void Publish(MonitoringAlert alert)` | Publishes an alert to all subscribers. |
| `Task PublishAsync(MonitoringAlert alert, CancellationToken ct = default)` | Publishes an alert asynchronously. |

### IArchiveHealthService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IArchiveHealthService.cs`

Interface for archive health services used by shared UI services. Implemented by platform-specific archive health services (WPF).

### IArchiveMaintenanceScheduleManager

**Location:** `src/MarketDataCollector.Storage/Maintenance/IArchiveMaintenanceScheduleManager.cs`

Interface for managing archive maintenance schedules.

| Method | Description |
|--------|-------------|
| `intenanceSchedule> GetAllSchedules()` | Get all maintenance schedules. |
| `Task<ArchiveMaintenanceSchedule> CreateScheduleAsync(ArchiveMaintenanceSchedule schedule, CancellationToken ct = default)` | Create a new maintenance schedule. |
| `Task<ArchiveMaintenanceSchedule> CreateFromPresetAsync(string presetName, string name, CancellationToken ct = default)` | Create a schedule from a preset. |
| `Task<ArchiveMaintenanceSchedule> UpdateScheduleAsync(ArchiveMaintenanceSchedule schedule, CancellationToken ct = default)` | Update an existing schedule. |
| `Task<bool> DeleteScheduleAsync(string scheduleId, CancellationToken ct = default)` | Delete a schedule. |
| `Task<bool> SetScheduleEnabledAsync(string scheduleId, bool enabled, CancellationToken ct = default)` | Enable or disable a schedule. |
| `intenanceSchedule> GetDueSchedules(DateTimeOffset asOf)` | Get schedules that are due for execution. |
| `intenanceScheduleSummary GetStatusSummary()` | Get an overview of all schedules. |

### IArchiveMaintenanceService

**Location:** `src/MarketDataCollector.Storage/Maintenance/IArchiveMaintenanceService.cs`

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

### IBackgroundTaskSchedulerService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IBackgroundTaskSchedulerService.cs`

Interface for scheduling and managing background tasks. Shared between WPF and UWP desktop applications. Part of C1 improvement (WPF/UWP service deduplication).

| Method | Description |
|--------|-------------|
| `Task ScheduleTaskAsync(string taskName, Func<CancellationToken, Task> task, TimeSpan interval, CancellationToken cancellationToken = default)` | Interface for scheduling and managing background tasks. Shared between WPF and UWP desktop applications. Part of C1 improvement (WPF/UWP service deduplication). |
| `Task CancelTaskAsync(string taskName)` | Interface for scheduling and managing background tasks. Shared between WPF and UWP desktop applications. Part of C1 improvement (WPF/UWP service deduplication). |
| `bool IsTaskRunning(string taskName)` | Interface for scheduling and managing background tasks. Shared between WPF and UWP desktop applications. Part of C1 improvement (WPF/UWP service deduplication). |

### ICanonicalSymbolRegistry

**Location:** `src/MarketDataCollector.Contracts/Catalog/ICanonicalSymbolRegistry.cs`

Service for canonical symbol naming standardization. Provides a unified interface for resolving symbols across providers using canonical names, aliases, and industry identifiers (ISIN, FIGI, SEDOL, CUSIP).

| Method | Description |
|--------|-------------|
| `Task RegisterAsync(CanonicalSymbolDefinition definition, CancellationToken ct = default)` | Registers a symbol with its canonical entry, updating aliases and identifier indexes. |
| `Task<int> RegisterBatchAsync(IEnumerable<CanonicalSymbolDefinition> definitions, CancellationToken ct = default)` | Registers multiple symbols in batch. |
| `bool IsKnown(string identifier)` | Checks if a given identifier (canonical, alias, ISIN, FIGI, etc.) is known. |
| `Task<bool> RemoveAsync(string canonical, CancellationToken ct = default)` | Removes a symbol from the registry by its canonical name. |

### ICliCommand

**Location:** `src/MarketDataCollector.Application/Commands/ICliCommand.cs`

Interface for CLI command handlers extracted from Program.cs. Each implementation handles one or more related CLI flags.

| Method | Description |
|--------|-------------|
| `bool CanHandle(string[] args)` | Returns true if this command should handle the given args. |
| `Task<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)` | Executes the command. Returns a <see cref="CliResult"/> with a semantic exit code. |

### IConfigService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IConfigService.cs`

Interface for managing application configuration. Enables testability and dependency injection.

| Method | Description |
|--------|-------------|
| `Task SaveConfigAsync(AppConfig config, CancellationToken ct = default)` | Saves the application configuration. |
| `Task SaveDataSourceAsync(string dataSource, CancellationToken ct = default)` | Saves the data source setting. |
| `Task SaveAlpacaOptionsAsync(AlpacaOptions options, CancellationToken ct = default)` | Saves Alpaca provider options. |
| `Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage, CancellationToken ct = default)` | Saves storage configuration. |
| `Task AddOrUpdateSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)` | Adds or updates a symbol in the configuration. |
| `Task AddSymbolAsync(SymbolConfig symbol, CancellationToken ct = default)` | Adds a symbol to the configuration (alias for AddOrUpdateSymbolAsync). |
| `Task DeleteSymbolAsync(string symbol, CancellationToken ct = default)` | Deletes a symbol from the configuration. |
| `Task<DataSourcesConfig> GetDataSourcesConfigAsync(CancellationToken ct = default)` | Gets the data sources configuration. |
| `Task AddOrUpdateDataSourceAsync(DataSourceConfig dataSource, CancellationToken ct = default)` | Adds or updates a data source configuration. |
| `Task DeleteDataSourceAsync(string id, CancellationToken ct = default)` | Deletes a data source by ID. |
| `Task SetDefaultDataSourceAsync(string id, bool isHistorical, CancellationToken ct = default)` | Sets the default data source for real-time or historical data. |
| `Task ToggleDataSourceAsync(string id, bool enabled, CancellationToken ct = default)` | Toggles a data source's enabled state. |
| `Task UpdateFailoverSettingsAsync(bool enableFailover, int failoverTimeoutSeconds, CancellationToken ct = default)` | Updates failover settings for data sources. |
| `Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)` | Gets the app settings including service URL configuration. |
| `Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default)` | Saves app settings including service URL configuration. |
| `Task UpdateServiceUrlAsync(string serviceUrl, int timeoutSeconds = 30, int backfillTimeoutMinutes = 60, CancellationToken ct = default)` | Updates the service URL configuration. |
| `Task InitializeAsync(CancellationToken ct = default)` | Loads configuration and initializes services with configured URLs. Should be called during app startup. |
| `Task<ConfigValidationResult> ValidateConfigAsync(CancellationToken ct = default)` | Validates the current configuration. |

### IConfigValidator

**Location:** `src/MarketDataCollector.Application/Config/IConfigValidator.cs`

Abstraction over configuration validation so that validation logic can be composed as a pipeline: Field → Semantic → Connectivity.

### IConfigurationProvider

**Location:** `src/MarketDataCollector.Core/Config/IConfigurationProvider.cs`

Unified configuration provider interface for consistent access to application configuration across all components.

| Method | Description |
|--------|-------------|
| `void RegisterMetadata(ConfigurationMetadata metadata)` | Registers configuration metadata. |
| `void Reload()` | Reloads configuration from all sources. |

### IConnectionHealthMonitor

**Location:** `src/MarketDataCollector.Core/Monitoring/IConnectionHealthMonitor.cs`

Interface for monitoring connection health of market data providers. Provides events for connection state changes used by failover logic.

| Method | Description |
|--------|-------------|
| `void RegisterConnection(string connectionId, string providerName)` | Registers a new connection for monitoring. |
| `void UnregisterConnection(string connectionId)` | Unregisters a connection from monitoring. |
| `void RecordHeartbeat(string connectionId)` | Records a heartbeat for a connection. |
| `void RecordLatency(string connectionId, double latencyMs)` | Records a latency sample for a connection. |
| `void RecordDataReceived(string connectionId)` | Records that data was received on a connection (resets heartbeat timer). |

### IConnectionService

**Location:** `src/MarketDataCollector.Wpf/Contracts/IConnectionService.cs`

Interface for managing provider connections with auto-reconnection support. Enables testability and dependency injection. Phase 6C.2: Connection types (ConnectionState, ConnectionSettings, event args) are now shared from MarketDataCollector.Ui.Services.Contracts.

| Method | Description |
|--------|-------------|
| `void UpdateSettings(ConnectionSettings settings)` | string ServiceUrl { get; } ConnectionState State { get; } string CurrentProvider { get; } TimeSpan? Uptime { get; } double LastLatencyMs { get; } int TotalReconnects { get; } |
| `void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30)` | ConnectionState State { get; } string CurrentProvider { get; } TimeSpan? Uptime { get; } double LastLatencyMs { get; } int TotalReconnects { get; } void UpdateSettings(ConnectionSettings settings); |
| `void StartMonitoring()` | TimeSpan? Uptime { get; } double LastLatencyMs { get; } int TotalReconnects { get; } void UpdateSettings(ConnectionSettings settings); void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30); ConnectionSettings GetSettings(); |
| `void StopMonitoring()` | double LastLatencyMs { get; } int TotalReconnects { get; } void UpdateSettings(ConnectionSettings settings); void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30); ConnectionSettings GetSettings(); void StartMonitoring(); |
| `Task<bool> ConnectAsync(string provider, CancellationToken ct = default)` | int TotalReconnects { get; } void UpdateSettings(ConnectionSettings settings); void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30); ConnectionSettings GetSettings(); void StartMonitoring(); void StopMonitoring(); |
| `Task DisconnectAsync(CancellationToken ct = default)` | void UpdateSettings(ConnectionSettings settings); void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30); ConnectionSettings GetSettings(); void StartMonitoring(); void StopMonitoring(); Task<bool> ConnectAsync(string provider, CancellationToken ct = default); |
| `void PauseAutoReconnect()` | void ConfigureServiceUrl(string serviceUrl, int timeoutSeconds = 30); ConnectionSettings GetSettings(); void StartMonitoring(); void StopMonitoring(); Task<bool> ConnectAsync(string provider, CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); |
| `void ResumeAutoReconnect()` | ConnectionSettings GetSettings(); void StartMonitoring(); void StopMonitoring(); Task<bool> ConnectAsync(string provider, CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); void PauseAutoReconnect(); |

### ICredentialService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/ICredentialService.cs`

Interface for credential management services used by shared UI services. Implemented by platform-specific credential services (WPF).

| Method | Description |
|--------|-------------|
| `Task<OAuthRefreshResult> RefreshOAuthTokenAsync(string providerId)` | Interface for credential management services used by shared UI services. Implemented by platform-specific credential services (WPF). |
| `Task UpdateMetadataAsync(string resource, Action<CredentialMetadataUpdate> updateAction)` | Interface for credential management services used by shared UI services. Implemented by platform-specific credential services (WPF). |

### ICredentialStore

**Location:** `src/MarketDataCollector.Application/Credentials/ICredentialStore.cs`

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

**Location:** `src/MarketDataCollector.ProviderSdk/IDataSource.cs`

Unified base interface for all data sources (real-time and historical). Provides a common abstraction for provider discovery, health monitoring, and lifecycle management.

| Method | Description |
|--------|-------------|
| `Task InitializeAsync(CancellationToken ct = default)` | Initializes the data source, validates credentials, and tests connectivity. |
| `Task<bool> ValidateCredentialsAsync(CancellationToken ct = default)` | Validates that credentials are properly configured. Returns true if no credentials are required or if they are valid. |
| `Task<bool> TestConnectivityAsync(CancellationToken ct = default)` | Tests connectivity to the data source. |

### IEventMetrics

**Location:** `src/MarketDataCollector.Application/Monitoring/IEventMetrics.cs`

Abstraction over event pipeline counters so that hot-path metrics can be injected rather than accessed through a static class. The default implementation delegates to the existing <see cref="Metrics"/> statics, preserving zero-allocation / thread-safe behavior on the hot path.

| Method | Description |
|--------|-------------|
| `void IncPublished()` | Abstraction over event pipeline counters so that hot-path metrics can be injected rather than accessed through a static class. The default implementation delegates to the existing <see cref="Metrics"/> statics, preserving zero-allocation / thread-safe behavior on the hot path. |
| `void IncDropped()` | Abstraction over event pipeline counters so that hot-path metrics can be injected rather than accessed through a static class. The default implementation delegates to the existing <see cref="Metrics"/> statics, preserving zero-allocation / thread-safe behavior on the hot path. |
| `void IncIntegrity()` | can be injected rather than accessed through a static class. The default implementation delegates to the existing <see cref="Metrics"/> statics, preserving zero-allocation / thread-safe behavior on the hot path. |
| `void IncTrades()` | The default implementation delegates to the existing <see cref="Metrics"/> statics, preserving zero-allocation / thread-safe behavior on the hot path. |
| `void IncDepthUpdates()` | preserving zero-allocation / thread-safe behavior on the hot path. |
| `void IncQuotes()` | - |
| `void IncHistoricalBars()` | - |
| `void RecordLatency(long startTimestamp)` | - |
| `void Reset()` | - |

### IFlushable

**Location:** `src/MarketDataCollector.Core/Services/IFlushable.cs`

Interface for components that can be flushed during shutdown.

| Method | Description |
|--------|-------------|
| `Task FlushAsync(CancellationToken ct = default)` | Flushes any buffered data to persistent storage. |

### IHealthCheckProvider

**Location:** `src/MarketDataCollector.Core/Monitoring/Core/IHealthCheckProvider.cs`

Interface for components that provide health check capabilities. Implementations should be lightweight and complete quickly.

| Method | Description |
|--------|-------------|
| `Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)` | Performs a health check and returns the result. |
| `void Register(IHealthCheckProvider provider)` | Registers a health check provider. |
| `void Unregister(string componentName)` | Unregisters a health check provider. |
| `Task<AggregatedHealthReport> CheckAllAsync(CancellationToken ct = default)` | Runs all registered health checks and returns an aggregated report. |

### IHistoricalBarWriter

**Location:** `src/MarketDataCollector.ProviderSdk/IHistoricalBarWriter.cs`

Abstraction for writing historical bar data to storage. Defined in ProviderSdk to break the circular dependency between Infrastructure and Storage projects. Storage implementations provide concrete writers; Infrastructure consumers inject this interface.

| Method | Description |
|--------|-------------|
| `Task WriteBarsAsync(IReadOnlyList<HistoricalBar> bars, CancellationToken ct = default)` | Persists a batch of historical bars to the configured storage backend. |

### IHistoricalDataProvider

**Location:** `src/MarketDataCollector.Infrastructure/Providers/Historical/IHistoricalDataProvider.cs`

Unified contract for fetching historical data from vendors. Consolidates previous V1, V2, and Extended interfaces into a single contract with optional capabilities indicated by properties.

| Method | Description |
|--------|-------------|
| `Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)` | Fetch daily OHLCV bars for a symbol within the specified date range. |

### IHistoricalDataSource

**Location:** `src/MarketDataCollector.ProviderSdk/IHistoricalDataSource.cs`

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

### ILoggingService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/ILoggingService.cs`

Interface for logging services used by shared UI services. Implemented by platform-specific logging services (WPF).

| Method | Description |
|--------|-------------|
| `void LogWarning(string message, Exception? exception = null)` | Interface for logging services used by shared UI services. Implemented by platform-specific logging services (WPF). |

### IMaintenanceExecutionHistory

**Location:** `src/MarketDataCollector.Storage/Maintenance/IMaintenanceExecutionHistory.cs`

Interface for tracking maintenance execution history.

| Method | Description |
|--------|-------------|
| `void RecordExecution(MaintenanceExecution execution)` | Record a new execution. |
| `void UpdateExecution(MaintenanceExecution execution)` | Update an existing execution record. |
| `intenanceExecution> GetRecentExecutions(int limit = 50)` | Get recent executions. |
| `intenanceExecution> GetExecutionsForSchedule(string scheduleId, int limit = 50)` | Get executions for a specific schedule. |
| `intenanceExecution> GetFailedExecutions(int limit = 50)` | Get failed executions. |
| `intenanceExecution> GetExecutionsByTimeRange(DateTimeOffset from, DateTimeOffset to)` | Get executions within a time range. |
| `intenanceStatistics GetStatistics(TimeSpan? period = null)` | Get overall maintenance statistics. |
| `Task<int> CleanupOldRecordsAsync(int maxAgeDays = 90, CancellationToken ct = default)` | Clean up old execution records. |

### IMarketDataClient

**Location:** `src/MarketDataCollector.ProviderSdk/IMarketDataClient.cs`

Market data client abstraction for provider-agnostic market data ingestion. Implementations must be non-blocking on publish paths.

| Method | Description |
|--------|-------------|
| `Task ConnectAsync(CancellationToken ct = default)` | Market data client abstraction for provider-agnostic market data ingestion. Implementations must be non-blocking on publish paths. |
| `Task DisconnectAsync(CancellationToken ct = default)` | Market data client abstraction for provider-agnostic market data ingestion. Implementations must be non-blocking on publish paths. |
| `int SubscribeMarketDepth(SymbolConfig cfg)` | <remarks> This interface is the core contract for ADR-001 (Provider Abstraction Pattern). All streaming data providers must implement this interface. Implements <see cref="IProviderMetadata"/> for unified provider discovery and capability reporting across all provider types. </remarks> [ImplementsAdr("ADR-001", "Core streaming data provider contract")] [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); |
| `void UnsubscribeMarketDepth(int subscriptionId)` | All streaming data providers must implement this interface. Implements <see cref="IProviderMetadata"/> for unified provider discovery and capability reporting across all provider types. </remarks> [ImplementsAdr("ADR-001", "Core streaming data provider contract")] [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); int SubscribeMarketDepth(SymbolConfig cfg); |
| `int SubscribeTrades(SymbolConfig cfg)` | and capability reporting across all provider types. </remarks> [ImplementsAdr("ADR-001", "Core streaming data provider contract")] [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); int SubscribeMarketDepth(SymbolConfig cfg); void UnsubscribeMarketDepth(int subscriptionId); |
| `void UnsubscribeTrades(int subscriptionId)` | [ImplementsAdr("ADR-004", "All async methods support CancellationToken")] public interface IMarketDataClient : IProviderMetadata, IAsyncDisposable { bool IsEnabled { get; } Task ConnectAsync(CancellationToken ct = default); Task DisconnectAsync(CancellationToken ct = default); int SubscribeMarketDepth(SymbolConfig cfg); void UnsubscribeMarketDepth(int subscriptionId); int SubscribeTrades(SymbolConfig cfg); |

### IMarketEventPayload

**Location:** `src/MarketDataCollector.Contracts/Domain/Events/IMarketEventPayload.cs`

Marker interface for strongly-typed market event payloads.

### IMarketEventPublisher

**Location:** `src/MarketDataCollector.Domain/Events/IMarketEventPublisher.cs`

Minimal publish contract so collectors can emit MarketEvents without knowing transport. Publish must be non-blocking (hot path).

| Method | Description |
|--------|-------------|
| `bool TryPublish(in MarketEvent evt)` | Minimal publish contract so collectors can emit MarketEvents without knowing transport. Publish must be non-blocking (hot path). |

### IMessagingService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IMessagingService.cs`

Interface for in-process pub/sub messaging between UI components. Shared between WPF desktop applications.

### INavigationService

**Location:** `src/MarketDataCollector.Wpf/Contracts/INavigationService.cs`

Interface for managing navigation throughout the application. Enables testability and dependency injection. Phase 6C.2: Navigation types (NavigationEntry, NavigationEventArgs) are now shared from MarketDataCollector.Ui.Services.Contracts.

| Method | Description |
|--------|-------------|
| `void Initialize(Frame frame)` | Interface for managing navigation throughout the application. Enables testability and dependency injection. Phase 6C.2: Navigation types (NavigationEntry, NavigationEventArgs) are now shared from MarketDataCollector.Ui.Services.Contracts. public interface INavigationService { bool CanGoBack { get; } |
| `bool NavigateTo(string pageTag, object? parameter = null)` | Interface for managing navigation throughout the application. Enables testability and dependency injection. Phase 6C.2: Navigation types (NavigationEntry, NavigationEventArgs) are now shared from MarketDataCollector.Ui.Services.Contracts. public interface INavigationService { bool CanGoBack { get; } void Initialize(Frame frame); |
| `bool NavigateTo(Type pageType, object? parameter = null)` | Interface for managing navigation throughout the application. Enables testability and dependency injection. Phase 6C.2: Navigation types (NavigationEntry, NavigationEventArgs) are now shared from MarketDataCollector.Ui.Services.Contracts. public interface INavigationService { bool CanGoBack { get; } void Initialize(Frame frame); bool NavigateTo(string pageTag, object? parameter = null); |
| `void GoBack()` | Phase 6C.2: Navigation types (NavigationEntry, NavigationEventArgs) are now shared from MarketDataCollector.Ui.Services.Contracts. public interface INavigationService { bool CanGoBack { get; } void Initialize(Frame frame); bool NavigateTo(string pageTag, object? parameter = null); bool NavigateTo(Type pageType, object? parameter = null); |
| `string> GetRegisteredPages()` | void Initialize(Frame frame); bool NavigateTo(string pageTag, object? parameter = null); bool NavigateTo(Type pageType, object? parameter = null); void GoBack(); Type? GetPageType(string pageTag); IReadOnlyList<NavigationEntry> GetBreadcrumbs(); |
| `bool IsPageRegistered(string pageTag)` | bool NavigateTo(string pageTag, object? parameter = null); bool NavigateTo(Type pageType, object? parameter = null); void GoBack(); Type? GetPageType(string pageTag); IReadOnlyList<NavigationEntry> GetBreadcrumbs(); IReadOnlyCollection<string> GetRegisteredPages(); |

### INotificationService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/INotificationService.cs`

Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF).

| Method | Description |
|--------|-------------|
| `Task NotifyErrorAsync(string title, string message, Exception? exception = null)` | Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF). |
| `Task NotifyWarningAsync(string title, string message)` | Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF). |
| `Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info)` | Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF). |
| `Task NotifyBackfillCompleteAsync(bool success, int symbolCount, int barsWritten, TimeSpan duration)` | Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF). |
| `Task NotifyScheduledJobAsync(string jobName, bool started, bool success = true)` | Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF). |
| `Task NotifyStorageWarningAsync(double usedPercent, long freeSpaceBytes)` | Interface for notification services used by shared UI services. Implemented by platform-specific notification services (WPF). |

### IOfflineTrackingPersistenceService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IOfflineTrackingPersistenceService.cs`

Interface for persisting offline tracking data. Shared between WPF desktop applications.

| Method | Description |
|--------|-------------|
| `Task DeleteOfflineDataAsync(string key, CancellationToken cancellationToken = default)` | Interface for persisting offline tracking data. Shared between WPF desktop applications. |
| `Task<bool> HasOfflineDataAsync(string key, CancellationToken cancellationToken = default)` | Interface for persisting offline tracking data. Shared between WPF desktop applications. |

### IOperationalScheduler

**Location:** `src/MarketDataCollector.Application/Scheduling/IOperationalScheduler.cs`

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

### IPendingOperationsQueueService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IPendingOperationsQueueService.cs`

Interface for queuing and managing pending operations. Shared between WPF desktop applications.

| Method | Description |
|--------|-------------|
| `Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default)` | Interface for queuing and managing pending operations. Shared between WPF desktop applications. |
| `Task ClearQueueAsync(CancellationToken cancellationToken = default)` | Interface for queuing and managing pending operations. Shared between WPF desktop applications. |

### IProviderMetadata

**Location:** `src/MarketDataCollector.ProviderSdk/IProviderMetadata.cs`

Unified metadata interface that all provider types implement. Enables consistent discovery, routing, and UI presentation across streaming, backfill, and symbol search providers.

### IProviderModule

**Location:** `src/MarketDataCollector.ProviderSdk/IProviderModule.cs`

Defines a provider module that can register provider services and data sources.

| Method | Description |
|--------|-------------|
| `void Register(IServiceCollection services, DataSourceRegistry registry)` | Register provider services into the DI container. |

### IQualityAnalyzer

**Location:** `src/MarketDataCollector.Application/Monitoring/DataQuality/IQualityAnalyzer.cs`

Analyzers can be discovered, registered, and run by the quality analysis engine.

| Method | Description |
|--------|-------------|
| `Task<QualityAnalysisResult> AnalyzeAsync(
        TData data,
        QualityAnalyzerConfig? config = null,
        CancellationToken ct = default)` | Analyzes data and returns quality issues. |
| `string> ValidateConfig(QualityAnalyzerConfig config)` | Validates configuration for this analyzer. |

### IQuoteStateStore

**Location:** `src/MarketDataCollector.Domain/Collectors/IQuoteStateStore.cs`

Provides access to the latest Best-Bid/Offer quote per symbol for downstream inference (e.g., aggressor side).

| Method | Description |
|--------|-------------|
| `bool TryGet(string symbol, out BboQuotePayload? quote)` | Provides access to the latest Best-Bid/Offer quote per symbol for downstream inference (e.g., aggressor side). public interface IQuoteStateStore { |
| `bool TryRemove(string symbol, out BboQuotePayload? removed)` | Remove cached state for a symbol. Returns <c>true</c> if the symbol existed. |
| `string, BboQuotePayload> Snapshot()` | Snapshot the current cache for inspection/monitoring without exposing internal mutability. |

### IRealtimeDataSource

**Location:** `src/MarketDataCollector.ProviderSdk/IRealtimeDataSource.cs`

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

### ISchemaService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/ISchemaService.cs`

Interface for schema services used by shared UI services. Implemented by platform-specific schema services (WPF).

### ISecretProvider

**Location:** `src/MarketDataCollector.Contracts/Credentials/ISecretProvider.cs`

Abstraction for retrieving secrets from external stores such as AWS Secrets Manager, Azure Key Vault, HashiCorp Vault, or environment variables. Implementations are plugged in via DI to replace the environment-variable bridge in <see cref="MarketDataCollector.Infrastructure.DataSources.CredentialConfig"/>.

| Method | Description |
|--------|-------------|
| `Task<bool> IsAvailableAsync(CancellationToken ct = default)` | Checks whether this secret provider is configured and reachable. |

### ISourceRegistry

**Location:** `src/MarketDataCollector.Storage/Interfaces/ISourceRegistry.cs`

Interface for managing data source and symbol registry information.

| Method | Description |
|--------|-------------|
| `void RegisterSource(SourceInfo source)` | Registers or updates a data source. |
| `void RegisterSymbol(SymbolInfo symbol)` | Registers or updates a symbol. |
| `string ResolveSymbolAlias(string alias)` | Resolves a symbol alias to its canonical name. |

### IStatusService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IStatusService.cs`

Interface for retrieving system status from the collector. Enables testability and dependency injection.

| Method | Description |
|--------|-------------|
| `Task<ApiResponse<StatusResponse>> GetStatusWithResponseAsync(CancellationToken ct = default)` | Gets the status with full response details. |
| `Task<ServiceHealthResult> CheckHealthAsync(CancellationToken ct = default)` | Checks if the service is healthy and reachable. |

### IStorageCatalogService

**Location:** `src/MarketDataCollector.Storage/Interfaces/IStorageCatalogService.cs`

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

### IStoragePolicy

**Location:** `src/MarketDataCollector.Storage/Interfaces/IStoragePolicy.cs`

| Method | Description |
|--------|-------------|
| `string GetPath(MarketEvent evt)` | - |

### IStorageSink

**Location:** `src/MarketDataCollector.Storage/Interfaces/IStorageSink.cs`

| Method | Description |
|--------|-------------|
| `Task AppendAsync(MarketEvent evt, CancellationToken ct = default)` | - |
| `Task FlushAsync(CancellationToken ct = default)` | - |

### ISymbolRegistryService

**Location:** `src/MarketDataCollector.Storage/Interfaces/ISymbolRegistryService.cs`

Service for managing the symbol registry.

| Method | Description |
|--------|-------------|
| `Task InitializeAsync(CancellationToken ct = default)` | Initializes the registry from storage. |
| `Task RegisterSymbolAsync(SymbolRegistryEntry entry, CancellationToken ct = default)` | Registers or updates a symbol entry. |
| `string ResolveAlias(string alias)` | Resolves an alias to a canonical symbol. |
| `Task AddAliasAsync(string canonical, SymbolAlias alias, CancellationToken ct = default)` | Adds an alias for a symbol. |
| `Task AddProviderMappingAsync(string canonical, string provider, string providerSymbol, CancellationToken ct = default)` | Adds a provider mapping. |
| `Task SaveRegistryAsync(CancellationToken ct = default)` | Saves the registry to disk. |
| `Task<int> ImportSymbolsAsync(IEnumerable<SymbolRegistryEntry> symbols, bool merge = true, CancellationToken ct = default)` | Imports symbols from an external source. |

### ISymbolResolver

**Location:** `src/MarketDataCollector.Infrastructure/Providers/Historical/SymbolResolution/ISymbolResolver.cs`

Contract for resolving and normalizing symbols across different providers and exchanges.

| Method | Description |
|--------|-------------|
| `Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default)` | Search for symbols matching a query. |

### ISymbolSearchProvider

**Location:** `src/MarketDataCollector.Infrastructure/Providers/SymbolSearch/ISymbolSearchProvider.cs`

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

**Location:** `src/MarketDataCollector.Infrastructure/Shared/ISymbolStateStore.cs`

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

### IThemeService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IThemeService.cs`

Interface for managing application themes. Shared between WPF desktop applications.

| Method | Description |
|--------|-------------|
| `void SetTheme(AppTheme theme)` | Interface for managing application themes. Shared between WPF desktop applications. |

### IWatchlistService

**Location:** `src/MarketDataCollector.Ui.Services/Contracts/IWatchlistService.cs`

Interface for watchlist services used by shared UI services. Implemented by platform-specific watchlist services (WPF).

| Method | Description |
|--------|-------------|
| `Task<WatchlistData> LoadWatchlistAsync()` | Interface for watchlist services used by shared UI services. Implemented by platform-specific watchlist services (WPF). |

## Data Sources

| Name | Type | Category | Location |
|------|------|----------|----------|
| Alpaca Markets | Hybrid | Broker | `src/MarketDataCollector.ProviderSdk/DataSourceAttribute.cs` |

## ADR Implementations

### ADR-001

| Type | Location | Description |
|------|----------|-------------|
| `AlertDispatcher` | `src/MarketDataCollector.Application/Monitoring/Core/AlertDispatcher.cs` | Alert dispatcher implementation |
| `AlpacaHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Alpaca/AlpacaHistoricalDataProvider.cs` | Alpaca historical data provider implementation |
| `AlpacaMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` | Alpaca streaming data provider implementation |
| `AlpacaMarketDataClient` | `src/MarketDataCollector.ProviderSdk/ImplementsAdrAttribute.cs` | Provider Abstraction Pattern |
| `AlpacaSymbolSearchProviderRefactored` | `src/MarketDataCollector.Infrastructure/Providers/SymbolSearch/AlpacaSymbolSearchProviderRefactored.cs` | Alpaca symbol search provider implementation |
| `AlphaVantageHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/AlphaVantage/AlphaVantageHistoricalDataProvider.cs` | Alpha Vantage historical data provider implementation |
| `BackfillCoordinator` | `src/MarketDataCollector.Application/Http/BackfillCoordinator.cs` | Uses ProviderRegistry for unified provider discovery |
| `BackfillCoordinator` | `src/MarketDataCollector.Ui.Shared/Services/BackfillCoordinator.cs` | Uses ProviderRegistry for unified provider discovery |
| `BaseHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/BaseHistoricalDataProvider.cs` | Base historical data provider implementation |
| `BaseSymbolSearchProvider` | `src/MarketDataCollector.Infrastructure/Providers/SymbolSearch/BaseSymbolSearchProvider.cs` | Base symbol search provider implementation |
| `CompositeHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/CompositeHistoricalDataProvider.cs` | Composite historical data provider with failover |
| `ConfigStore` | `src/MarketDataCollector.Application/Http/ConfigStore.cs` | Consolidated configuration store shared by all hosts |
| `ConfigurationSection` | `src/MarketDataCollector.Core/Config/IConfigurationProvider.cs` | Base configuration section |
| `DataQualityScoringService` | `src/MarketDataCollector.Storage/Services/DataQualityScoringService.cs` | Data quality scoring for multi-source environments |
| `ExpiringSymbolStateStore` | `src/MarketDataCollector.Infrastructure/Shared/ISymbolStateStore.cs` | Time-based expiring symbol state store |
| `FailoverAwareMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Failover/FailoverAwareMarketDataClient.cs` | Failover-aware composite streaming client |
| `FinnhubHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Finnhub/FinnhubHistoricalDataProvider.cs` | Finnhub historical data provider implementation |
| `FinnhubSymbolSearchProviderRefactored` | `src/MarketDataCollector.Infrastructure/Providers/SymbolSearch/FinnhubSymbolSearchProviderRefactored.cs` | Finnhub symbol search provider implementation |
| `HealthCheckAggregator` | `src/MarketDataCollector.Application/Monitoring/Core/HealthCheckAggregator.cs` | Health check aggregator implementation |
| `HostStartup` | `src/MarketDataCollector.Application/Composition/HostStartup.cs` | Unified host startup for all deployment modes |
| `IAlertDispatcher` | `src/MarketDataCollector.Core/Monitoring/Core/IAlertDispatcher.cs` | Centralized monitoring alert dispatcher |
| `IBHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` | Interactive Brokers historical data provider implementation |
| `IBHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` | Interactive Brokers historical data provider stub |
| `IBMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | Interactive Brokers streaming data provider implementation |
| `IBMarketDataClientIBApi` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | Interactive Brokers API streaming data provider |
| `IConfigurationProvider` | `src/MarketDataCollector.Core/Config/IConfigurationProvider.cs` | Unified configuration provider |
| `ICredentialStore` | `src/MarketDataCollector.Application/Credentials/ICredentialStore.cs` | Centralized credential management |
| `IHealthCheckAggregator` | `src/MarketDataCollector.Core/Monitoring/Core/IHealthCheckProvider.cs` | Centralized health check aggregation |
| `IHealthCheckProvider` | `src/MarketDataCollector.Core/Monitoring/Core/IHealthCheckProvider.cs` | Unified health check interface for all components |
| `IHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/IHistoricalDataProvider.cs` | Core historical data provider contract |
| `IHostAdapter` | `src/MarketDataCollector.Application/Composition/HostAdapters.cs` | Host adapters for mode-specific behavior |
| `IMarketDataClient` | `src/MarketDataCollector.ProviderSdk/IMarketDataClient.cs` | Core streaming data provider contract |
| `IOperationalScheduler` | `src/MarketDataCollector.Application/Scheduling/IOperationalScheduler.cs` | Centralized operational scheduling |
| `IProviderMetadata` | `src/MarketDataCollector.ProviderSdk/IProviderMetadata.cs` | Unified provider metadata contract for all provider types |
| `IQualityAnalysisEngine` | `src/MarketDataCollector.Application/Monitoring/DataQuality/IQualityAnalyzer.cs` | Quality analysis engine |
| `IQualityAnalyzer` | `src/MarketDataCollector.Application/Monitoring/DataQuality/IQualityAnalyzer.cs` | Quality analyzer plugin interface |
| `IQualityAnalyzerMetadata` | `src/MarketDataCollector.Application/Monitoring/DataQuality/IQualityAnalyzer.cs` | Quality analyzer discovery interface |
| `IQualityAnalyzerRegistry` | `src/MarketDataCollector.Application/Monitoring/DataQuality/IQualityAnalyzer.cs` | Quality analyzer registry |
| `ISymbolStateStore` | `src/MarketDataCollector.Infrastructure/Shared/ISymbolStateStore.cs` | Centralized symbol state management |
| `ITradingCalendarProvider` | `src/MarketDataCollector.Application/Scheduling/IOperationalScheduler.cs` | Trading calendar provider |
| `NYSEDataSource` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs` | NYSE streaming and historical data provider implementation |
| `NasdaqDataLinkHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/NasdaqDataLink/NasdaqDataLinkHistoricalDataProvider.cs` | Nasdaq Data Link historical data provider implementation |
| `NoOpMarketDataClient` | `src/MarketDataCollector.Infrastructure/NoOpMarketDataClient.cs` | No-op data provider for disabled/unconfigured scenarios |
| `OperationalScheduler` | `src/MarketDataCollector.Application/Scheduling/OperationalScheduler.cs` | Trading-hours-aware operational scheduling |
| `PolygonHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Polygon/PolygonHistoricalDataProvider.cs` | Polygon.io historical data provider implementation |
| `PolygonMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` | Polygon.io streaming data provider implementation |
| `ProviderFactory` | `src/MarketDataCollector.Infrastructure/Providers/Core/ProviderFactory.cs` | Unified provider factory for capability-driven registration |
| `ProviderOptionsBase` | `src/MarketDataCollector.Core/Config/IConfigurationProvider.cs` | Base provider options |
| `ProviderRegistry` | `src/MarketDataCollector.Infrastructure/Providers/Core/ProviderRegistry.cs` | Centralized provider registry for plugin-style management |
| `ProviderServiceExtensions` | `src/MarketDataCollector.Infrastructure/Providers/Core/ProviderServiceExtensions.cs` | Unified DI registration for all provider types |
| `ServiceCompositionRoot` | `src/MarketDataCollector.Application/Composition/ServiceCompositionRoot.cs` | Centralized composition root for service configuration |
| `ServiceRegistry` | `src/MarketDataCollector.Application/Services/ServiceRegistry.cs` | Centralized service registry for organization and discovery |
| `StockSharpHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/StockSharp/StockSharpHistoricalDataProvider.cs` | StockSharp historical data provider implementation |
| `StockSharpMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs` | StockSharp streaming data provider implementation |
| `StockSharpSymbolSearchProvider` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/StockSharp/StockSharpSymbolSearchProvider.cs` | StockSharp symbol search provider implementation |
| `StooqHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Stooq/StooqHistoricalDataProvider.cs` | Stooq historical data provider implementation |
| `StreamingFailoverService` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Failover/StreamingFailoverService.cs` | Runtime streaming provider failover orchestration |
| `SubscriptionManager` | `src/MarketDataCollector.Infrastructure/Shared/SubscriptionManager.cs` | Centralized subscription management for providers |
| `SymbolStateStore` | `src/MarketDataCollector.Infrastructure/Shared/ISymbolStateStore.cs` | ConcurrentDictionary-based symbol state store |
| `TiingoHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Tiingo/TiingoHistoricalDataProvider.cs` | Tiingo historical data provider implementation |
| `UiServer` | `src/MarketDataCollector/UiServer.cs` | UiServer uses centralized composition root |
| `YahooFinanceHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/YahooFinance/YahooFinanceHistoricalDataProvider.cs` | Yahoo Finance historical data provider implementation |

### ADR-002

| Type | Location | Description |
|------|----------|-------------|
| `DataLineageService` | `src/MarketDataCollector.Storage/Services/DataLineageService.cs` | Data lineage tracking for storage operations |
| `LifecyclePolicyEngine` | `src/MarketDataCollector.Storage/Services/LifecyclePolicyEngine.cs` | Tiered storage lifecycle enforcement |
| `PortableDataPackager` | `src/MarketDataCollector.Storage/Packaging/PortableDataPackager.cs` | Portable packaging for tiered storage export |
| `QuotaEnforcementService` | `src/MarketDataCollector.Storage/Services/QuotaEnforcementService.cs` | Capacity management with quota enforcement |

### ADR-004

| Type | Location | Description |
|------|----------|-------------|
| `AlpacaHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Alpaca/AlpacaHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `AlpacaMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` | All async methods support CancellationToken |
| `AlphaVantageHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/AlphaVantage/AlphaVantageHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `BaseHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/BaseHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `BaseSymbolSearchProvider` | `src/MarketDataCollector.Infrastructure/Providers/SymbolSearch/BaseSymbolSearchProvider.cs` | All async methods support CancellationToken |
| `CompositeHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/CompositeHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `FailoverAwareMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Failover/FailoverAwareMarketDataClient.cs` | All async methods support CancellationToken |
| `FinnhubHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Finnhub/FinnhubHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `IBHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/InteractiveBrokers/IBHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `IBMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | All async methods support CancellationToken |
| `IBMarketDataClientIBApi` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | All async methods support CancellationToken |
| `IHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/IHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `IMarketDataClient` | `src/MarketDataCollector.ProviderSdk/IMarketDataClient.cs` | All async methods support CancellationToken |
| `NYSEDataSource` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/NYSE/NYSEDataSource.cs` | All async methods support CancellationToken |
| `NasdaqDataLinkHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/NasdaqDataLink/NasdaqDataLinkHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `NoOpMarketDataClient` | `src/MarketDataCollector.Infrastructure/NoOpMarketDataClient.cs` | All async methods support CancellationToken |
| `PolygonHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Polygon/PolygonHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `PolygonMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` | All async methods support CancellationToken |
| `StockSharpHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/StockSharp/StockSharpHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `StockSharpMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs` | All async methods support CancellationToken |
| `StockSharpSymbolSearchProvider` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/StockSharp/StockSharpSymbolSearchProvider.cs` | All async methods support CancellationToken |
| `StooqHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Stooq/StooqHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `StreamingFailoverService` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Failover/StreamingFailoverService.cs` | All async methods support CancellationToken |
| `TiingoHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/Tiingo/TiingoHistoricalDataProvider.cs` | All async methods support CancellationToken |
| `UiServer` | `src/MarketDataCollector/UiServer.cs` | Large file decomposition - endpoints extracted to dedicated modules |
| `WebSocketReconnectionHelper` | `src/MarketDataCollector.Infrastructure/Shared/WebSocketReconnectionHelper.cs` | All async methods support CancellationToken |
| `YahooFinanceHistoricalDataProvider` | `src/MarketDataCollector.Infrastructure/Providers/Historical/YahooFinance/YahooFinanceHistoricalDataProvider.cs` | All async methods support CancellationToken |

### ADR-005

| Type | Location | Description |
|------|----------|-------------|
| `AlpacaMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Alpaca/AlpacaMarketDataClient.cs` | Attribute-based provider discovery |
| `DataSourceAttribute` | `src/MarketDataCollector.ProviderSdk/DataSourceAttribute.cs` | Core attribute for provider discovery |
| `IBMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/InteractiveBrokers/IBMarketDataClient.cs` | Attribute-based provider discovery |
| `PolygonMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/Polygon/PolygonMarketDataClient.cs` | Attribute-based provider discovery |
| `StockSharpMarketDataClient` | `src/MarketDataCollector.Infrastructure/Providers/Streaming/StockSharp/StockSharpMarketDataClient.cs` | Attribute-based provider discovery |

### ADR-010

| Type | Location | Description |
|------|----------|-------------|
| `HttpClientConfiguration` | `src/MarketDataCollector.Infrastructure/Http/HttpClientConfiguration.cs` | HttpClientFactory for proper HTTP client lifecycle management |

### ADR-012

| Type | Location | Description |
|------|----------|-------------|
| `TracedEventMetrics` | `src/MarketDataCollector.Application/Tracing/TracedEventMetrics.cs` | OpenTelemetry metrics instrumentation for pipeline observability |

