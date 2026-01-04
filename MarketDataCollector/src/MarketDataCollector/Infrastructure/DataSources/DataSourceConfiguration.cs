using MarketDataCollector.Application.Config;
using MarketDataCollector.Infrastructure.Providers.NYSE;

namespace MarketDataCollector.Infrastructure.DataSources;

/// <summary>
/// Unified configuration for all data sources.
/// Consolidates settings for real-time, historical, and hybrid providers.
/// </summary>
public sealed record UnifiedDataSourcesConfig
{
    /// <summary>
    /// Default settings applied to all sources unless overridden.
    /// </summary>
    public DefaultsConfig Defaults { get; init; } = new();

    /// <summary>
    /// Individual source configurations keyed by source ID.
    /// </summary>
    public Dictionary<string, SourceConfig> Sources { get; init; } = new();

    /// <summary>
    /// Failover configuration.
    /// </summary>
    public FailoverConfig Failover { get; init; } = new();

    /// <summary>
    /// Symbol mapping configuration.
    /// </summary>
    public SymbolMappingConfig SymbolMapping { get; init; } = new();

    /// <summary>
    /// Plugin system configuration.
    /// </summary>
    public PluginSystemConfig Plugins { get; init; } = new();
}

/// <summary>
/// Configuration for the plugin system.
/// </summary>
public sealed record PluginSystemConfig
{
    /// <summary>
    /// Whether the plugin system is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Primary directory for plugins.
    /// </summary>
    public string PluginDirectory { get; init; } = "plugins";

    /// <summary>
    /// Additional directories to scan for plugins.
    /// </summary>
    public string[] AdditionalDirectories { get; init; } = [];

    /// <summary>
    /// Whether to enable hot reload when plugin files change.
    /// </summary>
    public bool EnableHotReload { get; init; } = true;

    /// <summary>
    /// Whether to watch directories for new plugins.
    /// </summary>
    public bool EnableDirectoryWatching { get; init; } = true;

    /// <summary>
    /// Whether to automatically load new plugins added to directories.
    /// </summary>
    public bool AutoLoadNewPlugins { get; init; } = true;

    /// <summary>
    /// Hot reload debounce time in milliseconds.
    /// </summary>
    public int HotReloadDebounceMs { get; init; } = 2000;

    /// <summary>
    /// Default permissions granted to plugins.
    /// </summary>
    public string[] DefaultPermissions { get; init; } = ["Network", "Environment"];

    /// <summary>
    /// Whether plugins must explicitly request permissions.
    /// </summary>
    public bool RequireExplicitPermissions { get; init; } = false;

    /// <summary>
    /// Additional assemblies to share with plugins.
    /// </summary>
    public string[] SharedAssemblies { get; init; } = [];

    /// <summary>
    /// Plugin-specific configurations keyed by plugin ID.
    /// </summary>
    public Dictionary<string, PluginInstanceConfig> PluginConfigs { get; init; } = new();
}

/// <summary>
/// Configuration for an individual plugin instance.
/// </summary>
public sealed record PluginInstanceConfig
{
    /// <summary>
    /// Whether this plugin is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Priority override for this plugin.
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Plugin-specific settings.
    /// </summary>
    public Dictionary<string, object?> Settings { get; init; } = new();
}

/// <summary>
/// Default settings applied to all data sources.
/// </summary>
public sealed record DefaultsConfig
{
    /// <summary>
    /// Default retry policy for all sources.
    /// </summary>
    public RetryPolicyConfig RetryPolicy { get; init; } = new();

    /// <summary>
    /// Default health check settings.
    /// </summary>
    public HealthCheckConfig HealthCheck { get; init; } = new();

    /// <summary>
    /// Default rate limit settings.
    /// </summary>
    public RateLimitConfig RateLimits { get; init; } = new();
}

/// <summary>
/// Retry policy configuration.
/// </summary>
public sealed record RetryPolicyConfig
{
    /// <summary>
    /// Maximum number of retries before failing.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Base delay between retries in milliseconds.
    /// </summary>
    public int BaseDelayMs { get; init; } = 1000;

    /// <summary>
    /// Maximum delay between retries in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; init; } = 30000;

    /// <summary>
    /// Whether to use exponential backoff.
    /// </summary>
    public bool UseExponentialBackoff { get; init; } = true;

    /// <summary>
    /// Converts to internal options format.
    /// </summary>
    public RetryPolicyOptions ToOptions() => new(MaxRetries, BaseDelayMs, MaxDelayMs, UseExponentialBackoff);
}

/// <summary>
/// Health check configuration.
/// </summary>
public sealed record HealthCheckConfig
{
    /// <summary>
    /// Interval between health checks in seconds.
    /// </summary>
    public int IntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Timeout for health check requests in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Number of consecutive failures before marking unhealthy.
    /// </summary>
    public int UnhealthyThreshold { get; init; } = 3;

    /// <summary>
    /// Converts to internal options format.
    /// </summary>
    public HealthCheckOptions ToOptions() => new(IntervalSeconds, TimeoutSeconds, UnhealthyThreshold);
}

/// <summary>
/// Rate limit configuration.
/// </summary>
public sealed record RateLimitConfig
{
    /// <summary>
    /// Maximum concurrent requests to this source.
    /// </summary>
    public int MaxConcurrentRequests { get; init; } = 5;

    /// <summary>
    /// Maximum requests per time window.
    /// </summary>
    public int MaxRequestsPerWindow { get; init; } = 100;

    /// <summary>
    /// Time window in seconds for rate limiting.
    /// </summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// </summary>
    public int MinDelayBetweenRequestsMs { get; init; } = 0;

    /// <summary>
    /// Converts to internal options format.
    /// </summary>
    public RateLimitOptions ToOptions() => new(
        MaxConcurrentRequests,
        MaxRequestsPerWindow,
        TimeSpan.FromSeconds(WindowSeconds),
        MinDelayBetweenRequestsMs
    );
}

/// <summary>
/// Configuration for an individual data source.
/// </summary>
public sealed record SourceConfig
{
    /// <summary>
    /// Whether this source is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Priority for source selection (lower = higher priority).
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// Type of data: Realtime, Historical, Hybrid.
    /// </summary>
    public string Type { get; init; } = "Hybrid";

    /// <summary>
    /// Category: Exchange, Broker, Aggregator, Free, Premium.
    /// </summary>
    public string Category { get; init; } = "Free";

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Connection-specific configuration.
    /// </summary>
    public ConnectionConfig? Connection { get; init; }

    /// <summary>
    /// Capability-specific configuration.
    /// </summary>
    public CapabilityConfig? Capabilities { get; init; }

    /// <summary>
    /// Credential configuration.
    /// </summary>
    public CredentialConfig? Credentials { get; init; }

    /// <summary>
    /// Source-specific rate limits (overrides defaults).
    /// </summary>
    public RateLimitConfig? RateLimits { get; init; }

    /// <summary>
    /// Source-specific retry policy (overrides defaults).
    /// </summary>
    public RetryPolicyConfig? RetryPolicy { get; init; }

    /// <summary>
    /// Source-specific health check settings (overrides defaults).
    /// </summary>
    public HealthCheckConfig? HealthCheck { get; init; }

    /// <summary>
    /// Alpaca-specific options.
    /// </summary>
    public AlpacaOptions? Alpaca { get; init; }

    /// <summary>
    /// Polygon-specific options.
    /// </summary>
    public PolygonOptions? Polygon { get; init; }

    /// <summary>
    /// Interactive Brokers-specific options.
    /// </summary>
    public IBOptions? IB { get; init; }

    /// <summary>
    /// NYSE Direct Connection-specific options.
    /// </summary>
    public NYSEOptions? NYSE { get; init; }
}

/// <summary>
/// Connection configuration for a data source.
/// </summary>
public sealed record ConnectionConfig
{
    /// <summary>
    /// Base URL for HTTP-based sources.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Host for socket-based sources.
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// Port for socket-based sources.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Whether to use paper/sandbox environment.
    /// </summary>
    public bool UsePaper { get; init; }

    /// <summary>
    /// Whether to use WebSocket connection.
    /// </summary>
    public bool EnableWebSocket { get; init; } = true;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Keep-alive interval in seconds.
    /// </summary>
    public int KeepAliveSeconds { get; init; } = 30;
}

/// <summary>
/// Capability configuration for a data source.
/// </summary>
public sealed record CapabilityConfig
{
    /// <summary>
    /// Maximum historical lookback in days.
    /// </summary>
    public int? HistoricalLookbackDays { get; init; }

    /// <summary>
    /// Maximum historical lookback in years.
    /// </summary>
    public int? HistoricalLookbackYears { get; init; }

    /// <summary>
    /// Supported intraday bar intervals.
    /// </summary>
    public string[]? IntradayBarIntervals { get; init; }

    /// <summary>
    /// Maximum depth levels for market depth.
    /// </summary>
    public int? MaxDepthLevels { get; init; }

    /// <summary>
    /// Whether this source supports L2 market depth.
    /// </summary>
    public bool SupportsL2 { get; init; }

    /// <summary>
    /// Whether this source supports adjusted prices.
    /// </summary>
    public bool SupportsAdjustedPrices { get; init; }

    /// <summary>
    /// Whether this source supports dividend data.
    /// </summary>
    public bool SupportsDividends { get; init; }

    /// <summary>
    /// Whether this source supports split data.
    /// </summary>
    public bool SupportsSplits { get; init; }

    /// <summary>
    /// Supported market regions.
    /// </summary>
    public string[]? SupportedMarkets { get; init; }
}

/// <summary>
/// Credential configuration for a data source.
/// </summary>
public sealed record CredentialConfig
{
    /// <summary>
    /// Source of credentials: Environment, File, Vault, Config.
    /// </summary>
    public string Source { get; init; } = "Environment";

    /// <summary>
    /// Environment variable name for API key.
    /// </summary>
    public string? ApiKeyVar { get; init; }

    /// <summary>
    /// Environment variable name for key ID.
    /// </summary>
    public string? KeyIdVar { get; init; }

    /// <summary>
    /// Environment variable name for secret key.
    /// </summary>
    public string? SecretKeyVar { get; init; }

    /// <summary>
    /// Environment variable name for username.
    /// </summary>
    public string? UsernameVar { get; init; }

    /// <summary>
    /// Environment variable name for password.
    /// </summary>
    public string? PasswordVar { get; init; }

    /// <summary>
    /// Path to credentials file (if Source is File).
    /// </summary>
    public string? CredentialsPath { get; init; }

    /// <summary>
    /// Vault path for credentials (if Source is Vault).
    /// </summary>
    public string? VaultPath { get; init; }

    /// <summary>
    /// Resolves the API key from configured source.
    /// </summary>
    public string? ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKeyVar))
            return Environment.GetEnvironmentVariable(ApiKeyVar);
        return null;
    }

    /// <summary>
    /// Resolves the key ID from configured source.
    /// </summary>
    public string? ResolveKeyId()
    {
        if (!string.IsNullOrWhiteSpace(KeyIdVar))
            return Environment.GetEnvironmentVariable(KeyIdVar);
        return null;
    }

    /// <summary>
    /// Resolves the secret key from configured source.
    /// </summary>
    public string? ResolveSecretKey()
    {
        if (!string.IsNullOrWhiteSpace(SecretKeyVar))
            return Environment.GetEnvironmentVariable(SecretKeyVar);
        return null;
    }
}

/// <summary>
/// Failover configuration.
/// </summary>
public sealed record FailoverConfig
{
    /// <summary>
    /// Whether failover is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Failover strategy: Priority, HealthScore, RoundRobin, Random.
    /// </summary>
    public string Strategy { get; init; } = "Priority";

    /// <summary>
    /// Maximum number of failover attempts.
    /// </summary>
    public int MaxFailoverAttempts { get; init; } = 3;

    /// <summary>
    /// Cooldown period in seconds after a source fails.
    /// </summary>
    public int CooldownSeconds { get; init; } = 60;

    /// <summary>
    /// Converts to internal options format.
    /// </summary>
    public FallbackOptions ToOptions()
    {
        var strategy = Strategy.ToLowerInvariant() switch
        {
            "healthscore" => FallbackStrategy.HealthScore,
            "roundrobin" => FallbackStrategy.RoundRobin,
            "random" => FallbackStrategy.Random,
            _ => FallbackStrategy.Priority
        };

        return new FallbackOptions(
            Enabled,
            strategy,
            MaxFailoverAttempts,
            TimeSpan.FromSeconds(CooldownSeconds)
        );
    }
}

#region Configuration Extensions

/// <summary>
/// Extension methods for working with data source configuration.
/// </summary>
public static class DataSourceConfigurationExtensions
{
    /// <summary>
    /// Gets the DataSourceOptions for a specific source, merging with defaults.
    /// </summary>
    public static DataSourceOptions GetOptionsForSource(
        this UnifiedDataSourcesConfig config,
        string sourceId)
    {
        var defaults = config.Defaults;
        var sourceConfig = config.Sources.GetValueOrDefault(sourceId);

        var retryPolicy = sourceConfig?.RetryPolicy?.ToOptions() ?? defaults.RetryPolicy.ToOptions();
        var rateLimits = sourceConfig?.RateLimits?.ToOptions() ?? defaults.RateLimits.ToOptions();
        var healthCheck = sourceConfig?.HealthCheck?.ToOptions() ?? defaults.HealthCheck.ToOptions();
        var priority = sourceConfig?.Priority ?? 100;

        return new DataSourceOptions(priority, retryPolicy, rateLimits, healthCheck);
    }

    /// <summary>
    /// Parses the DataSourceType from string.
    /// </summary>
    public static DataSourceType ParseType(this SourceConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "realtime" => DataSourceType.Realtime,
            "historical" => DataSourceType.Historical,
            "hybrid" or "both" => DataSourceType.Hybrid,
            _ => DataSourceType.Hybrid
        };
    }

    /// <summary>
    /// Parses the DataSourceCategory from string.
    /// </summary>
    public static DataSourceCategory ParseCategory(this SourceConfig config)
    {
        return config.Category.ToLowerInvariant() switch
        {
            "exchange" => DataSourceCategory.Exchange,
            "broker" => DataSourceCategory.Broker,
            "aggregator" => DataSourceCategory.Aggregator,
            "free" => DataSourceCategory.Free,
            "premium" => DataSourceCategory.Premium,
            _ => DataSourceCategory.Free
        };
    }

    /// <summary>
    /// Gets all enabled source IDs.
    /// </summary>
    public static IEnumerable<string> GetEnabledSourceIds(this UnifiedDataSourcesConfig config)
    {
        return config.Sources
            .Where(kvp => kvp.Value.Enabled)
            .Select(kvp => kvp.Key);
    }
}

#endregion
