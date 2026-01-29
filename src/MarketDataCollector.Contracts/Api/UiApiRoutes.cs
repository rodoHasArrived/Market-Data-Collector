namespace MarketDataCollector.Contracts.Api;

/// <summary>
/// Shared UI API routes for the web dashboard and desktop clients.
/// </summary>
public static class UiApiRoutes
{
    public const string Status = "/api/status";
    public const string Config = "/api/config";
    public const string ConfigDataSource = "/api/config/datasource";
    public const string ConfigAlpaca = "/api/config/alpaca";
    public const string ConfigStorage = "/api/config/storage";
    public const string ConfigSymbols = "/api/config/symbols";
    public const string ConfigDataSources = "/api/config/datasources";
    public const string ConfigDataSourcesDefaults = "/api/config/datasources/defaults";
    public const string ConfigDataSourcesToggle = "/api/config/datasources/{id}/toggle";
    public const string ConfigDataSourcesFailover = "/api/config/datasources/failover";
    public const string BackfillProviders = "/api/backfill/providers";
    public const string BackfillStatus = "/api/backfill/status";
    public const string BackfillRun = "/api/backfill/run";
    public const string ProviderComparison = "/api/providers/comparison";
    public const string ProviderStatus = "/api/providers/status";
    public const string ProviderMetrics = "/api/providers/metrics";
    public const string FailoverConfig = "/api/failover/config";
    public const string FailoverRules = "/api/failover/rules";
    public const string FailoverForce = "/api/failover/force/{ruleId}";
    public const string FailoverHealth = "/api/failover/health";
    public const string SymbolMappings = "/api/symbols/mappings";
    public const string StorageProfiles = "/api/storage/profiles";
}
