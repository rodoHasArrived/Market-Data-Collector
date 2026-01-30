namespace MarketDataCollector.Contracts.Api;

/// <summary>
/// Shared UI API routes for the web dashboard and desktop clients.
/// </summary>
public static class UiApiRoutes
{
    // Health and status endpoints (shared with StatusHttpServer)
    public const string Health = "/health";
    public const string HealthDetailed = "/health/detailed";
    public const string Ready = "/ready";
    public const string Live = "/live";
    public const string Metrics = "/metrics";
    public const string Status = "/api/status";
    public const string Errors = "/api/errors";
    public const string Backpressure = "/api/backpressure";
    public const string ProvidersLatency = "/api/providers/latency";
    public const string Connections = "/api/connections";

    // Configuration endpoints
    public const string Config = "/api/config";
    public const string ConfigDataSource = "/api/config/datasource";
    public const string ConfigAlpaca = "/api/config/alpaca";
    public const string ConfigStorage = "/api/config/storage";
    public const string ConfigSymbols = "/api/config/symbols";
    public const string ConfigDataSources = "/api/config/datasources";
    public const string ConfigDataSourcesDefaults = "/api/config/datasources/defaults";
    public const string ConfigDataSourcesToggle = "/api/config/datasources/{id}/toggle";
    public const string ConfigDataSourcesFailover = "/api/config/datasources/failover";

    // Backfill endpoints
    public const string BackfillProviders = "/api/backfill/providers";
    public const string BackfillStatus = "/api/backfill/status";
    public const string BackfillRun = "/api/backfill/run";

    // Provider endpoints
    public const string ProviderComparison = "/api/providers/comparison";
    public const string ProviderStatus = "/api/providers/status";
    public const string ProviderMetrics = "/api/providers/metrics";

    // Failover endpoints
    public const string FailoverConfig = "/api/failover/config";
    public const string FailoverRules = "/api/failover/rules";
    public const string FailoverForce = "/api/failover/force/{ruleId}";
    public const string FailoverHealth = "/api/failover/health";

    // Symbol management endpoints
    public const string SymbolMappings = "/api/symbols/mappings";

    // Storage endpoints
    public const string StorageProfiles = "/api/storage/profiles";
}
