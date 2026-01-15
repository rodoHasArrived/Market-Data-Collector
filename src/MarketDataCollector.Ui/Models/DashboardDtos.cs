using MarketDataCollector.Application.Config;

namespace MarketDataCollector.Ui.Models;

// ============================================================
// Request DTOs
// ============================================================

/// <summary>Request to update the active data source.</summary>
public record DataSourceRequest(string DataSource);

/// <summary>Request to update storage settings.</summary>
public record StorageSettingsRequest(
    string? DataRoot,
    bool Compress,
    string? NamingConvention,
    string? DatePartition,
    bool IncludeProvider,
    string? FilePrefix);

/// <summary>Request to create or update a data source configuration.</summary>
public record DataSourceConfigRequest(
    string? Id,
    string Name,
    string Provider = "IB",
    bool Enabled = true,
    string Type = "RealTime",
    int Priority = 100,
    AlpacaOptions? Alpaca = null,
    PolygonOptions? Polygon = null,
    IBOptions? IB = null,
    string[]? Symbols = null,
    string? Description = null,
    string[]? Tags = null);

/// <summary>Request to toggle enabled status.</summary>
public record ToggleRequest(bool Enabled);

/// <summary>Request to set default data sources.</summary>
public record DefaultSourcesRequest(string? DefaultRealTimeSourceId, string? DefaultHistoricalSourceId);

/// <summary>Request to update failover settings.</summary>
public record FailoverSettingsRequest(bool EnableFailover, int FailoverTimeoutSeconds);

/// <summary>Request to update full failover configuration.</summary>
public record FailoverConfigRequest(
    bool EnableFailover,
    int HealthCheckIntervalSeconds = 10,
    bool AutoRecover = true,
    int FailoverTimeoutSeconds = 30);

/// <summary>Request to create or update a failover rule.</summary>
public record FailoverRuleRequest(
    string? Id,
    string PrimaryProviderId,
    string[] BackupProviderIds,
    int FailoverThreshold = 3,
    int RecoveryThreshold = 5,
    double DataQualityThreshold = 0,
    double MaxLatencyMs = 0);

/// <summary>Request to force a failover to a specific provider.</summary>
public record ForceFailoverRequest(string TargetProviderId);

/// <summary>Request to run a backfill operation.</summary>
public record BackfillRequestDto(string? Provider, string[] Symbols, DateOnly? From, DateOnly? To);

/// <summary>Request to create or update a symbol mapping.</summary>
public record SymbolMappingRequest(
    string CanonicalSymbol,
    string? IbSymbol = null,
    string? AlpacaSymbol = null,
    string? PolygonSymbol = null,
    string? YahooSymbol = null,
    string? Name = null,
    string? Figi = null);

// ============================================================
// Response DTOs
// ============================================================

/// <summary>Response containing provider comparison data.</summary>
public record ProviderComparisonResponse(
    DateTimeOffset Timestamp,
    ProviderMetricsResponse[] Providers,
    int TotalProviders,
    int HealthyProviders);

/// <summary>Response containing provider connection status.</summary>
public record ProviderStatusResponse(
    string ProviderId,
    string Name,
    string ProviderType,
    bool IsConnected,
    bool IsEnabled,
    int Priority,
    int ActiveSubscriptions,
    DateTimeOffset? LastHeartbeat);

/// <summary>Response containing detailed provider metrics.</summary>
public record ProviderMetricsResponse(
    string ProviderId,
    string ProviderType,
    long TradesReceived,
    long DepthUpdatesReceived,
    long QuotesReceived,
    long ConnectionAttempts,
    long ConnectionFailures,
    long MessagesDropped,
    long ActiveSubscriptions,
    double AverageLatencyMs,
    double MinLatencyMs,
    double MaxLatencyMs,
    double DataQualityScore,
    double ConnectionSuccessRate,
    DateTimeOffset Timestamp);

/// <summary>Response containing failover configuration.</summary>
public record FailoverConfigResponse(
    bool EnableFailover,
    int HealthCheckIntervalSeconds,
    bool AutoRecover,
    int FailoverTimeoutSeconds,
    FailoverRuleResponse[] Rules);

/// <summary>Response containing a failover rule.</summary>
public record FailoverRuleResponse(
    string Id,
    string PrimaryProviderId,
    string[] BackupProviderIds,
    int FailoverThreshold,
    int RecoveryThreshold,
    double DataQualityThreshold,
    double MaxLatencyMs,
    bool IsInFailoverState,
    string? CurrentActiveProviderId);

/// <summary>Response containing provider health information.</summary>
public record ProviderHealthResponse(
    string ProviderId,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTimeOffset? LastIssueTime,
    DateTimeOffset? LastSuccessTime,
    HealthIssueResponse[] RecentIssues);

/// <summary>Response containing a health issue.</summary>
public record HealthIssueResponse(
    string Type,
    string? Message,
    DateTimeOffset Timestamp);

/// <summary>Response containing a symbol mapping.</summary>
public record SymbolMappingResponse(
    string CanonicalSymbol,
    string? IbSymbol,
    string? AlpacaSymbol,
    string? PolygonSymbol,
    string? YahooSymbol,
    string? Name,
    string? Figi);
