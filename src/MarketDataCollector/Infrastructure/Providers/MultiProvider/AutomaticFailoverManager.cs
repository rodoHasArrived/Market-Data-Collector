using System.Collections.Concurrent;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.MultiProvider;

/// <summary>
/// Manages automatic failover between providers based on configurable rules.
/// Monitors provider health and switches to backup providers when issues are detected.
/// </summary>
public sealed class AutomaticFailoverManager : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<AutomaticFailoverManager>();
    private readonly MultiProviderConnectionManager _connectionManager;
    private readonly FailoverConfiguration _config;
    private readonly ConcurrentDictionary<string, ProviderHealthState> _healthStates = new();
    private readonly ConcurrentDictionary<string, FailoverRule> _activeRules = new();
    private readonly SemaphoreSlim _failoverLock = new(1, 1);
    private Timer? _healthCheckTimer;
    private CancellationTokenSource? _cts;

    public AutomaticFailoverManager(
        MultiProviderConnectionManager connectionManager,
        FailoverConfiguration config)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Event raised when a failover occurs.
    /// </summary>
    public event EventHandler<FailoverEventArgs>? FailoverOccurred;

    /// <summary>
    /// Event raised when a provider recovers and becomes primary again.
    /// </summary>
    public event EventHandler<ProviderRecoveryEventArgs>? ProviderRecovered;

    /// <summary>
    /// Gets the current failover status for all providers.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderHealthState> HealthStates => _healthStates;

    /// <summary>
    /// Gets the currently active failover rules.
    /// </summary>
    public IReadOnlyDictionary<string, FailoverRule> ActiveRules => _activeRules;

    /// <summary>
    /// Starts the health monitoring and automatic failover system.
    /// </summary>
    public void Start()
    {
        if (_healthCheckTimer != null) return;

        _cts = new CancellationTokenSource();

        // Initialize health states for configured rules
        foreach (var rule in _config.Rules)
        {
            _activeRules.TryAdd(rule.Id, rule);
            _healthStates.TryAdd(rule.PrimaryProviderId, new ProviderHealthState(rule.PrimaryProviderId));

            foreach (var backupId in rule.BackupProviderIds)
            {
                _healthStates.TryAdd(backupId, new ProviderHealthState(backupId));
            }
        }

        // Start health check timer
        _healthCheckTimer = new Timer(
            async _ => await CheckHealthAndFailoverAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds));

        _log.Information("Automatic failover manager started with {RuleCount} rules", _config.Rules.Count);
    }

    /// <summary>
    /// Stops the health monitoring system.
    /// </summary>
    public void Stop()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
        _cts?.Cancel();
        _log.Information("Automatic failover manager stopped");
    }

    /// <summary>
    /// Adds a new failover rule.
    /// </summary>
    public void AddRule(FailoverRule rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));

        _activeRules.TryAdd(rule.Id, rule);
        _healthStates.TryAdd(rule.PrimaryProviderId, new ProviderHealthState(rule.PrimaryProviderId));

        foreach (var backupId in rule.BackupProviderIds)
        {
            _healthStates.TryAdd(backupId, new ProviderHealthState(backupId));
        }

        _log.Information("Added failover rule {RuleId}: {Primary} -> [{Backups}]",
            rule.Id, rule.PrimaryProviderId, string.Join(", ", rule.BackupProviderIds));
    }

    /// <summary>
    /// Removes a failover rule.
    /// </summary>
    public bool RemoveRule(string ruleId)
    {
        return _activeRules.TryRemove(ruleId, out _);
    }

    /// <summary>
    /// Forces a failover for testing or manual intervention.
    /// </summary>
    public async Task<bool> ForceFailoverAsync(string ruleId, string targetProviderId)
    {
        if (!_activeRules.TryGetValue(ruleId, out var rule))
        {
            _log.Warning("Cannot force failover: rule {RuleId} not found", ruleId);
            return false;
        }

        return await ExecuteFailoverAsync(rule, rule.PrimaryProviderId, targetProviderId, "Manual failover requested");
    }

    /// <summary>
    /// Reports a provider health issue (can be called by external monitors).
    /// </summary>
    public void ReportHealthIssue(string providerId, HealthIssueType issueType, string? message = null)
    {
        if (_healthStates.TryGetValue(providerId, out var state))
        {
            state.RecordIssue(issueType, message);
            _log.Warning("Health issue reported for provider {ProviderId}: {IssueType} - {Message}",
                providerId, issueType, message ?? "No details");
        }
    }

    /// <summary>
    /// Reports a successful health check for a provider.
    /// </summary>
    public void ReportHealthSuccess(string providerId)
    {
        if (_healthStates.TryGetValue(providerId, out var state))
        {
            state.RecordSuccess();
        }
    }

    private async Task CheckHealthAndFailoverAsync()
    {
        if (_cts?.IsCancellationRequested == true) return;

        try
        {
            var connectionStatus = _connectionManager.GetConnectionStatus();
            var metrics = _connectionManager.GetComparisonMetrics();

            // Update health states from connection status
            foreach (var (providerId, status) in connectionStatus)
            {
                if (_healthStates.TryGetValue(providerId, out var state))
                {
                    if (status.IsConnected)
                        state.RecordSuccess();
                    else
                        state.RecordIssue(HealthIssueType.Disconnected, "Provider disconnected");
                }
            }

            // Check each rule for failover conditions
            foreach (var (ruleId, rule) in _activeRules)
            {
                await EvaluateRuleAsync(rule, connectionStatus, metrics);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during health check and failover evaluation");
        }
    }

    private async Task EvaluateRuleAsync(
        FailoverRule rule,
        Dictionary<string, ProviderConnectionStatus> connectionStatus,
        ProviderComparisonResult metrics)
    {
        if (!_healthStates.TryGetValue(rule.PrimaryProviderId, out var primaryHealth))
            return;

        var shouldFailover = false;
        var reason = "";

        // Check if primary is connected
        if (!connectionStatus.TryGetValue(rule.PrimaryProviderId, out var primaryStatus) ||
            !primaryStatus.IsConnected)
        {
            shouldFailover = true;
            reason = "Primary provider disconnected";
        }
        // Check consecutive failures threshold
        else if (primaryHealth.ConsecutiveFailures >= rule.FailoverThreshold)
        {
            shouldFailover = true;
            reason = $"Exceeded failure threshold ({primaryHealth.ConsecutiveFailures}/{rule.FailoverThreshold})";
        }
        // Check data quality threshold
        else if (rule.DataQualityThreshold > 0)
        {
            var primaryMetrics = metrics.Providers.FirstOrDefault(p => p.ProviderId == rule.PrimaryProviderId);
            if (primaryMetrics.DataQualityScore < rule.DataQualityThreshold)
            {
                shouldFailover = true;
                reason = $"Data quality below threshold ({primaryMetrics.DataQualityScore:F1}/{rule.DataQualityThreshold})";
            }
        }
        // Check latency threshold
        else if (rule.MaxLatencyMs > 0)
        {
            var primaryMetrics = metrics.Providers.FirstOrDefault(p => p.ProviderId == rule.PrimaryProviderId);
            if (primaryMetrics.AverageLatencyMs > rule.MaxLatencyMs)
            {
                shouldFailover = true;
                reason = $"Latency exceeds threshold ({primaryMetrics.AverageLatencyMs:F1}ms/{rule.MaxLatencyMs}ms)";
            }
        }

        if (shouldFailover && !rule.IsInFailoverState)
        {
            // Find the best available backup
            var targetProvider = FindBestBackup(rule, connectionStatus, metrics);
            if (targetProvider != null)
            {
                await ExecuteFailoverAsync(rule, rule.PrimaryProviderId, targetProvider, reason);
            }
            else
            {
                _log.Warning("Failover triggered for rule {RuleId} but no healthy backup available", rule.Id);
            }
        }
        else if (!shouldFailover && rule.IsInFailoverState && _config.AutoRecover)
        {
            // Check if primary has recovered
            if (primaryHealth.ConsecutiveSuccesses >= rule.RecoveryThreshold)
            {
                await ExecuteRecoveryAsync(rule);
            }
        }
    }

    private string? FindBestBackup(
        FailoverRule rule,
        Dictionary<string, ProviderConnectionStatus> connectionStatus,
        ProviderComparisonResult metrics)
    {
        foreach (var backupId in rule.BackupProviderIds)
        {
            // Check if backup is connected
            if (!connectionStatus.TryGetValue(backupId, out var backupStatus) || !backupStatus.IsConnected)
                continue;

            // Check if backup is healthy
            if (!_healthStates.TryGetValue(backupId, out var backupHealth))
                continue;

            if (backupHealth.ConsecutiveFailures < rule.FailoverThreshold)
            {
                return backupId;
            }
        }

        return null;
    }

    private async Task<bool> ExecuteFailoverAsync(
        FailoverRule rule,
        string fromProvider,
        string toProvider,
        string reason)
    {
        await _failoverLock.WaitAsync();
        try
        {
            _log.Warning("Executing failover for rule {RuleId}: {From} -> {To}. Reason: {Reason}",
                rule.Id, fromProvider, toProvider, reason);

            // Mark rule as in failover state
            rule.SetFailoverState(true, toProvider);

            // Transfer subscriptions from failing provider to backup provider
            // Don't unsubscribe from source in case it recovers and we need to failback
            var transferResult = _connectionManager.TransferSubscriptions(
                fromProviderId: fromProvider,
                toProviderId: toProvider,
                unsubscribeFromSource: false);

            if (!transferResult.Success)
            {
                _log.Warning("Partial subscription transfer during failover: {Message}", transferResult.Message);
            }

            _log.Information(
                "Subscription transfer during failover: {Transferred} transferred, {Failed} failed",
                transferResult.TransferredCount, transferResult.FailedCount);

            var args = new FailoverEventArgs(
                RuleId: rule.Id,
                FromProviderId: fromProvider,
                ToProviderId: toProvider,
                Reason: reason,
                Timestamp: DateTimeOffset.UtcNow
            );

            FailoverOccurred?.Invoke(this, args);

            _log.Information("Failover completed successfully for rule {RuleId}", rule.Id);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to execute failover for rule {RuleId}", rule.Id);
            return false;
        }
        finally
        {
            _failoverLock.Release();
        }
    }

    private async Task ExecuteRecoveryAsync(FailoverRule rule)
    {
        await _failoverLock.WaitAsync();
        try
        {
            _log.Information("Primary provider recovered for rule {RuleId}. Initiating recovery.", rule.Id);

            var previousActiveProvider = rule.CurrentActiveProviderId;

            // Transfer subscriptions back to primary provider
            // Unsubscribe from the backup since primary is now healthy
            if (!string.IsNullOrEmpty(previousActiveProvider))
            {
                var transferResult = _connectionManager.TransferSubscriptions(
                    fromProviderId: previousActiveProvider,
                    toProviderId: rule.PrimaryProviderId,
                    unsubscribeFromSource: true);

                if (!transferResult.Success)
                {
                    _log.Warning("Partial subscription transfer during recovery: {Message}", transferResult.Message);
                }

                _log.Information(
                    "Subscription transfer during recovery: {Transferred} transferred, {Failed} failed",
                    transferResult.TransferredCount, transferResult.FailedCount);
            }

            rule.SetFailoverState(false, null);

            var args = new ProviderRecoveryEventArgs(
                RuleId: rule.Id,
                RecoveredProviderId: rule.PrimaryProviderId,
                PreviousActiveProviderId: previousActiveProvider,
                Timestamp: DateTimeOffset.UtcNow
            );

            ProviderRecovered?.Invoke(this, args);

            _log.Information("Recovery completed for rule {RuleId}. Primary {Primary} is now active.",
                rule.Id, rule.PrimaryProviderId);
        }
        finally
        {
            _failoverLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        _failoverLock.Dispose();
        _cts?.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration for automatic failover behavior.
/// </summary>
public sealed record FailoverConfiguration(
    /// <summary>
    /// List of failover rules.
    /// </summary>
    IReadOnlyList<FailoverRule> Rules,

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
/// Defines a failover rule between providers.
/// </summary>
public sealed class FailoverRule
{
    public FailoverRule(
        string id,
        string primaryProviderId,
        IReadOnlyList<string> backupProviderIds,
        int failoverThreshold = 3,
        int recoveryThreshold = 5,
        double dataQualityThreshold = 0,
        double maxLatencyMs = 0)
    {
        Id = id;
        PrimaryProviderId = primaryProviderId;
        BackupProviderIds = backupProviderIds;
        FailoverThreshold = failoverThreshold;
        RecoveryThreshold = recoveryThreshold;
        DataQualityThreshold = dataQualityThreshold;
        MaxLatencyMs = maxLatencyMs;
    }

    /// <summary>
    /// Unique identifier for this rule.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The primary provider ID.
    /// </summary>
    public string PrimaryProviderId { get; }

    /// <summary>
    /// Ordered list of backup provider IDs.
    /// </summary>
    public IReadOnlyList<string> BackupProviderIds { get; }

    /// <summary>
    /// Number of consecutive failures before triggering failover.
    /// </summary>
    public int FailoverThreshold { get; }

    /// <summary>
    /// Number of consecutive successes required for recovery.
    /// </summary>
    public int RecoveryThreshold { get; }

    /// <summary>
    /// Minimum data quality score (0-100). 0 = disabled.
    /// </summary>
    public double DataQualityThreshold { get; }

    /// <summary>
    /// Maximum acceptable latency in ms. 0 = disabled.
    /// </summary>
    public double MaxLatencyMs { get; }

    /// <summary>
    /// Whether the rule is currently in failover state.
    /// </summary>
    public bool IsInFailoverState { get; private set; }

    /// <summary>
    /// The currently active provider ID (primary or backup).
    /// </summary>
    public string CurrentActiveProviderId { get; private set; }

    public FailoverRule()
    {
        Id = "";
        PrimaryProviderId = "";
        BackupProviderIds = Array.Empty<string>();
        CurrentActiveProviderId = "";
    }

    internal void SetFailoverState(bool inFailover, string? activeProvider)
    {
        IsInFailoverState = inFailover;
        CurrentActiveProviderId = activeProvider ?? PrimaryProviderId;
    }
}

/// <summary>
/// Tracks health state for a provider.
/// </summary>
public sealed class ProviderHealthState
{
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private readonly List<HealthIssue> _recentIssues = new();
    private readonly object _lock = new();

    public ProviderHealthState(string providerId)
    {
        ProviderId = providerId;
    }

    public string ProviderId { get; }
    public int ConsecutiveFailures => _consecutiveFailures;
    public int ConsecutiveSuccesses => _consecutiveSuccesses;
    public DateTimeOffset? LastIssueTime { get; private set; }
    public DateTimeOffset? LastSuccessTime { get; private set; }
    public IReadOnlyList<HealthIssue> RecentIssues
    {
        get
        {
            lock (_lock) return _recentIssues.ToList();
        }
    }

    public void RecordIssue(HealthIssueType type, string? message = null)
    {
        Interlocked.Increment(ref _consecutiveFailures);
        Interlocked.Exchange(ref _consecutiveSuccesses, 0);
        LastIssueTime = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            _recentIssues.Add(new HealthIssue(type, message, DateTimeOffset.UtcNow));
            // Keep only last 20 issues
            while (_recentIssues.Count > 20)
                _recentIssues.RemoveAt(0);
        }
    }

    public void RecordSuccess()
    {
        Interlocked.Increment(ref _consecutiveSuccesses);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        LastSuccessTime = DateTimeOffset.UtcNow;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _consecutiveSuccesses, 0);
        lock (_lock) _recentIssues.Clear();
    }
}

/// <summary>
/// Types of health issues.
/// </summary>
public enum HealthIssueType
{
    Disconnected,
    Timeout,
    RateLimited,
    DataQuality,
    HighLatency,
    AuthenticationFailure,
    Unknown
}

/// <summary>
/// Represents a health issue event.
/// </summary>
public readonly record struct HealthIssue(
    HealthIssueType Type,
    string? Message,
    DateTimeOffset Timestamp
);

/// <summary>
/// Event arguments for failover events.
/// </summary>
public readonly record struct FailoverEventArgs(
    string RuleId,
    string FromProviderId,
    string ToProviderId,
    string Reason,
    DateTimeOffset Timestamp
);

/// <summary>
/// Event arguments for provider recovery events.
/// </summary>
public readonly record struct ProviderRecoveryEventArgs(
    string RuleId,
    string RecoveredProviderId,
    string? PreviousActiveProviderId,
    DateTimeOffset Timestamp
);
