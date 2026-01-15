using System.Collections.Concurrent;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Infrastructure.Contracts;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.MultiProvider;

/// <summary>
/// Implementation of the multi-provider service that manages simultaneous connections
/// to multiple market data providers with automatic failover and symbol mapping.
/// </summary>
[ImplementsAdr("ADR-001", "Core multi-provider service implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class MultiProviderService : IMultiProviderService
{
    private readonly ILogger _log = LoggingSetup.ForContext<MultiProviderService>();
    private readonly MultiProviderConnectionManager _connectionManager;
    private readonly AutomaticFailoverManager _failoverManager;
    private readonly ProviderSymbolMappingService _symbolMapping;
    private readonly DataSourcesConfig? _config;
    private readonly ConcurrentDictionary<string, DataSourceConfig> _providerConfigs = new();
    private volatile bool _isRunning;
    private FailoverConfiguration _failoverConfig;

    public MultiProviderService(
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector,
        DataSourcesConfig? config = null,
        string? symbolMappingPath = null)
    {
        _connectionManager = new MultiProviderConnectionManager(tradeCollector, depthCollector, quoteCollector);
        _symbolMapping = new ProviderSymbolMappingService(symbolMappingPath);
        _config = config;

        // Initialize failover configuration from config or defaults
        var failoverRules = new List<FailoverRule>();
        _failoverConfig = new FailoverConfiguration(
            Rules: failoverRules,
            HealthCheckIntervalSeconds: 10,
            AutoRecover: config?.EnableFailover ?? true,
            MinRecoveryDelaySeconds: config?.FailoverTimeoutSeconds ?? 30
        );

        _failoverManager = new AutomaticFailoverManager(_connectionManager, _failoverConfig);

        // Subscribe to failover events
        _failoverManager.FailoverOccurred += OnFailoverOccurred;
        _failoverManager.ProviderRecovered += OnProviderRecovered;
    }

    /// <inheritdoc />
    public MultiProviderConnectionManager ConnectionManager => _connectionManager;

    /// <inheritdoc />
    public AutomaticFailoverManager FailoverManager => _failoverManager;

    /// <inheritdoc />
    public ProviderSymbolMappingService SymbolMapping => _symbolMapping;

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_isRunning) return;

        _log.Information("Starting multi-provider service");

        // Load symbol mappings
        await _symbolMapping.LoadAsync(ct);

        // Connect to all enabled providers
        if (_config?.Sources != null)
        {
            var enabledSources = _config.Sources.Where(s => s.Enabled).ToList();
            _log.Information("Connecting to {Count} enabled data sources", enabledSources.Count);

            var results = await _connectionManager.AddProvidersAsync(enabledSources, ct);

            foreach (var (providerId, success) in results)
            {
                if (success)
                {
                    var source = enabledSources.First(s => s.Id == providerId);
                    _providerConfigs.TryAdd(providerId, source);
                    _log.Information("Connected to provider {ProviderId} ({ProviderType})", providerId, source.Provider);
                }
                else
                {
                    _log.Warning("Failed to connect to provider {ProviderId}", providerId);
                }
            }
        }

        // Start failover monitoring if enabled
        if (_config?.EnableFailover ?? true)
        {
            _failoverManager.Start();
            _log.Information("Automatic failover monitoring started");
        }

        _isRunning = true;
        _log.Information("Multi-provider service started with {Count} active connections",
            _connectionManager.ActiveConnections.Count);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!_isRunning) return;

        _log.Information("Stopping multi-provider service");

        _failoverManager.Stop();

        // Disconnect all providers
        foreach (var providerId in _connectionManager.ActiveConnections.Keys.ToList())
        {
            await _connectionManager.RemoveProviderAsync(providerId, ct);
        }

        // Save symbol mappings
        await _symbolMapping.SaveAsync(ct);

        _isRunning = false;
        _log.Information("Multi-provider service stopped");
    }

    /// <inheritdoc />
    public async Task<bool> AddProviderAsync(DataSourceConfig config, bool connectImmediately = true, CancellationToken ct = default)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));

        _providerConfigs.TryAdd(config.Id, config);

        if (connectImmediately)
        {
            return await _connectionManager.AddProviderAsync(config, ct);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveProviderAsync(string providerId, CancellationToken ct = default)
    {
        _providerConfigs.TryRemove(providerId, out _);
        return await _connectionManager.RemoveProviderAsync(providerId, ct);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ProviderConnectionStatus> GetConnectionStatus()
    {
        return _connectionManager.GetConnectionStatus();
    }

    /// <inheritdoc />
    public ProviderComparisonResult GetComparisonMetrics()
    {
        return _connectionManager.GetComparisonMetrics();
    }

    /// <inheritdoc />
    public ProviderMetricsSnapshot? GetProviderMetrics(string providerId)
    {
        if (_connectionManager.AllMetrics.TryGetValue(providerId, out var metrics))
        {
            return metrics.GetSnapshot();
        }
        return null;
    }

    /// <inheritdoc />
    public FailoverConfigurationDto GetFailoverConfiguration()
    {
        return _failoverConfig.ToDto();
    }

    /// <inheritdoc />
    public async Task UpdateFailoverConfigurationAsync(FailoverConfigurationDto config, CancellationToken ct = default)
    {
        // Stop current failover manager
        _failoverManager.Stop();

        // Create new configuration
        var rules = config.Rules.Select(r => r.ToModel()).ToList();
        _failoverConfig = new FailoverConfiguration(
            Rules: rules,
            HealthCheckIntervalSeconds: config.HealthCheckIntervalSeconds,
            AutoRecover: config.AutoRecover,
            MinRecoveryDelaySeconds: config.MinRecoveryDelaySeconds
        );

        // Restart with new configuration
        if (_isRunning)
        {
            _failoverManager.Start();
        }

        _log.Information("Updated failover configuration with {RuleCount} rules", rules.Count);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AddFailoverRule(FailoverRuleDto rule)
    {
        var domainRule = rule.ToModel();
        _failoverManager.AddRule(domainRule);
        _log.Information("Added failover rule {RuleId}: {Primary} -> [{Backups}]",
            rule.Id, rule.PrimaryProviderId, string.Join(", ", rule.BackupProviderIds));
    }

    /// <inheritdoc />
    public bool RemoveFailoverRule(string ruleId)
    {
        var removed = _failoverManager.RemoveRule(ruleId);
        if (removed)
        {
            _log.Information("Removed failover rule {RuleId}", ruleId);
        }
        return removed;
    }

    /// <inheritdoc />
    public async Task<bool> ForceFailoverAsync(string ruleId, string targetProviderId)
    {
        return await _failoverManager.ForceFailoverAsync(ruleId, targetProviderId);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ProviderHealthStateDto> GetHealthStates()
    {
        return _failoverManager.HealthStates
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDto()
            );
    }

    /// <inheritdoc />
    public Dictionary<string, int> SubscribeSymbol(
        SymbolConfig symbol,
        string[]? providerIds = null,
        bool subscribeTrades = true,
        bool subscribeDepth = true)
    {
        var results = new Dictionary<string, int>();
        var providers = providerIds?.Length > 0
            ? _connectionManager.ActiveConnections
                .Where(c => providerIds.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            : _connectionManager.ActiveConnections;

        foreach (var (id, connection) in providers)
        {
            // Get provider-specific symbol
            var providerSymbol = _symbolMapping.GetProviderSymbol(symbol.Symbol, connection.Config.Provider);
            var mappedConfig = symbol with { Symbol = providerSymbol };

            var subResults = _connectionManager.SubscribeSymbol(mappedConfig, new[] { id }, subscribeTrades, subscribeDepth);
            foreach (var (key, value) in subResults)
            {
                results[key] = value;
            }
        }

        return results;
    }

    /// <inheritdoc />
    public void UnsubscribeSymbol(string symbol, string[]? providerIds = null)
    {
        var providers = providerIds?.Length > 0
            ? _connectionManager.ActiveConnections
                .Where(c => providerIds.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            : _connectionManager.ActiveConnections;

        foreach (var (id, connection) in providers)
        {
            // Get provider-specific symbol
            var providerSymbol = _symbolMapping.GetProviderSymbol(symbol, connection.Config.Provider);
            _connectionManager.UnsubscribeSymbol(providerSymbol, new[] { id });
        }
    }

    private void OnFailoverOccurred(object? sender, FailoverEventArgs e)
    {
        _log.Warning("Failover event: {RuleId} switched from {From} to {To}. Reason: {Reason}",
            e.RuleId, e.FromProviderId, e.ToProviderId, e.Reason);
    }

    private void OnProviderRecovered(object? sender, ProviderRecoveryEventArgs e)
    {
        _log.Information("Recovery event: {RuleId} recovered to {Provider} from {Previous}",
            e.RuleId, e.RecoveredProviderId, e.PreviousActiveProviderId);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _connectionManager.DisposeAsync();
        await _failoverManager.DisposeAsync();
    }
}
