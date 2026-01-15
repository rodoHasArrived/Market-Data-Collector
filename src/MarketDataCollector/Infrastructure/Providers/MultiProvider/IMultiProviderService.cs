using MarketDataCollector.Application.Config;
using MarketDataCollector.Infrastructure.Contracts;

namespace MarketDataCollector.Infrastructure.Providers.MultiProvider;

/// <summary>
/// Service interface for managing simultaneous multi-provider connections.
/// Provides unified access to provider management, metrics, failover, and symbol mapping.
/// </summary>
[ImplementsAdr("ADR-001", "Core multi-provider service contract")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IMultiProviderService : IAsyncDisposable
{
    /// <summary>
    /// Gets the connection manager for direct provider access.
    /// </summary>
    MultiProviderConnectionManager ConnectionManager { get; }

    /// <summary>
    /// Gets the automatic failover manager.
    /// </summary>
    AutomaticFailoverManager FailoverManager { get; }

    /// <summary>
    /// Gets the symbol mapping service.
    /// </summary>
    ProviderSymbolMappingService SymbolMapping { get; }

    /// <summary>
    /// Gets whether the service is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Initializes and starts the multi-provider service.
    /// Connects to all enabled providers and starts health monitoring.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the multi-provider service and disconnects all providers.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new provider configuration and optionally connects immediately.
    /// </summary>
    Task<bool> AddProviderAsync(DataSourceConfig config, bool connectImmediately = true, CancellationToken ct = default);

    /// <summary>
    /// Removes a provider and disconnects if connected.
    /// </summary>
    Task<bool> RemoveProviderAsync(string providerId, CancellationToken ct = default);

    /// <summary>
    /// Gets connection status for all providers.
    /// </summary>
    IReadOnlyDictionary<string, ProviderConnectionStatus> GetConnectionStatus();

    /// <summary>
    /// Gets comparison metrics for all providers.
    /// </summary>
    ProviderComparisonResult GetComparisonMetrics();

    /// <summary>
    /// Gets detailed metrics snapshot for a specific provider.
    /// </summary>
    ProviderMetricsSnapshot? GetProviderMetrics(string providerId);

    /// <summary>
    /// Gets the current failover configuration.
    /// </summary>
    FailoverConfigurationDto GetFailoverConfiguration();

    /// <summary>
    /// Updates the failover configuration.
    /// </summary>
    Task UpdateFailoverConfigurationAsync(FailoverConfigurationDto config, CancellationToken ct = default);

    /// <summary>
    /// Adds a failover rule.
    /// </summary>
    void AddFailoverRule(FailoverRuleDto rule);

    /// <summary>
    /// Removes a failover rule.
    /// </summary>
    bool RemoveFailoverRule(string ruleId);

    /// <summary>
    /// Forces a manual failover for a rule.
    /// </summary>
    Task<bool> ForceFailoverAsync(string ruleId, string targetProviderId);

    /// <summary>
    /// Gets the health state for all monitored providers.
    /// </summary>
    IReadOnlyDictionary<string, ProviderHealthStateDto> GetHealthStates();

    /// <summary>
    /// Subscribes a symbol across specified providers (or all if not specified).
    /// Uses symbol mapping to translate canonical symbols to provider-specific symbols.
    /// </summary>
    Dictionary<string, int> SubscribeSymbol(
        SymbolConfig symbol,
        string[]? providerIds = null,
        bool subscribeTrades = true,
        bool subscribeDepth = true);

    /// <summary>
    /// Unsubscribes a symbol from specified providers (or all if not specified).
    /// </summary>
    void UnsubscribeSymbol(string symbol, string[]? providerIds = null);
}

/// <summary>
/// DTO for failover configuration that can be serialized to/from JSON.
/// </summary>
public sealed record FailoverConfigurationDto(
    /// <summary>
    /// List of failover rules.
    /// </summary>
    FailoverRuleDto[] Rules,

    /// <summary>
    /// Health check interval in seconds.
    /// </summary>
    int HealthCheckIntervalSeconds = 10,

    /// <summary>
    /// Whether to automatically recover to primary when it becomes healthy.
    /// </summary>
    bool AutoRecover = true,

    /// <summary>
    /// Minimum time to wait before attempting recovery (seconds).
    /// </summary>
    int MinRecoveryDelaySeconds = 60
);

/// <summary>
/// DTO for a failover rule.
/// </summary>
public sealed record FailoverRuleDto(
    /// <summary>
    /// Unique identifier for this rule.
    /// </summary>
    string Id,

    /// <summary>
    /// The primary provider ID.
    /// </summary>
    string PrimaryProviderId,

    /// <summary>
    /// Ordered list of backup provider IDs.
    /// </summary>
    string[] BackupProviderIds,

    /// <summary>
    /// Number of consecutive failures before triggering failover.
    /// </summary>
    int FailoverThreshold = 3,

    /// <summary>
    /// Number of consecutive successes required for recovery.
    /// </summary>
    int RecoveryThreshold = 5,

    /// <summary>
    /// Minimum data quality score (0-100). 0 = disabled.
    /// </summary>
    double DataQualityThreshold = 0,

    /// <summary>
    /// Maximum acceptable latency in ms. 0 = disabled.
    /// </summary>
    double MaxLatencyMs = 0,

    /// <summary>
    /// Whether the rule is currently in failover state.
    /// </summary>
    bool IsInFailoverState = false,

    /// <summary>
    /// The currently active provider ID (primary or backup).
    /// </summary>
    string? CurrentActiveProviderId = null
);

/// <summary>
/// DTO for provider health state.
/// </summary>
public sealed record ProviderHealthStateDto(
    string ProviderId,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTimeOffset? LastIssueTime,
    DateTimeOffset? LastSuccessTime,
    HealthIssueDto[] RecentIssues
);

/// <summary>
/// DTO for a health issue.
/// </summary>
public sealed record HealthIssueDto(
    string Type,
    string? Message,
    DateTimeOffset Timestamp
);

/// <summary>
/// Extension methods for converting between domain models and DTOs.
/// </summary>
public static class MultiProviderDtoExtensions
{
    /// <summary>
    /// Converts a FailoverConfiguration to DTO.
    /// </summary>
    public static FailoverConfigurationDto ToDto(this FailoverConfiguration config)
    {
        return new FailoverConfigurationDto(
            Rules: config.Rules.Select(r => r.ToDto()).ToArray(),
            HealthCheckIntervalSeconds: config.HealthCheckIntervalSeconds,
            AutoRecover: config.AutoRecover,
            MinRecoveryDelaySeconds: config.MinRecoveryDelaySeconds
        );
    }

    /// <summary>
    /// Converts a FailoverRule to DTO.
    /// </summary>
    public static FailoverRuleDto ToDto(this FailoverRule rule)
    {
        return new FailoverRuleDto(
            Id: rule.Id,
            PrimaryProviderId: rule.PrimaryProviderId,
            BackupProviderIds: rule.BackupProviderIds.ToArray(),
            FailoverThreshold: rule.FailoverThreshold,
            RecoveryThreshold: rule.RecoveryThreshold,
            DataQualityThreshold: rule.DataQualityThreshold,
            MaxLatencyMs: rule.MaxLatencyMs,
            IsInFailoverState: rule.IsInFailoverState,
            CurrentActiveProviderId: rule.CurrentActiveProviderId
        );
    }

    /// <summary>
    /// Converts a FailoverRuleDto to domain model.
    /// </summary>
    public static FailoverRule ToModel(this FailoverRuleDto dto)
    {
        return new FailoverRule(
            id: dto.Id,
            primaryProviderId: dto.PrimaryProviderId,
            backupProviderIds: dto.BackupProviderIds,
            failoverThreshold: dto.FailoverThreshold,
            recoveryThreshold: dto.RecoveryThreshold,
            dataQualityThreshold: dto.DataQualityThreshold,
            maxLatencyMs: dto.MaxLatencyMs
        );
    }

    /// <summary>
    /// Converts a ProviderHealthState to DTO.
    /// </summary>
    public static ProviderHealthStateDto ToDto(this ProviderHealthState state)
    {
        return new ProviderHealthStateDto(
            ProviderId: state.ProviderId,
            ConsecutiveFailures: state.ConsecutiveFailures,
            ConsecutiveSuccesses: state.ConsecutiveSuccesses,
            LastIssueTime: state.LastIssueTime,
            LastSuccessTime: state.LastSuccessTime,
            RecentIssues: state.RecentIssues.Select(i => new HealthIssueDto(
                Type: i.Type.ToString(),
                Message: i.Message,
                Timestamp: i.Timestamp
            )).ToArray()
        );
    }
}
