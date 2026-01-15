using System.Collections.Concurrent;
using DataIngestion.Contracts.Messages;
using DataIngestion.Gateway.Configuration;
using Serilog;

namespace DataIngestion.Gateway.Services;

/// <summary>
/// Manages connections to market data providers.
/// </summary>
public interface IProviderManager
{
    /// <summary>Get all provider statuses.</summary>
    IReadOnlyDictionary<string, ProviderConnectionStatus> GetProviderStatuses();

    /// <summary>Connect to a provider.</summary>
    Task<bool> ConnectProviderAsync(string providerName, CancellationToken ct = default);

    /// <summary>Disconnect from a provider.</summary>
    Task DisconnectProviderAsync(string providerName, CancellationToken ct = default);

    /// <summary>Subscribe to market data.</summary>
    Task<int> SubscribeAsync(string providerName, string symbol, SubscriptionType[] types, CancellationToken ct = default);

    /// <summary>Unsubscribe from market data.</summary>
    Task UnsubscribeAsync(string providerName, int subscriptionId, CancellationToken ct = default);
}

/// <summary>
/// Manages market data provider connections and subscriptions.
/// </summary>
public sealed class ProviderManager : IProviderManager, IAsyncDisposable
{
    private readonly GatewayConfig _config;
    private readonly IDataRouter _dataRouter;
    private readonly Serilog.ILogger _log = Log.ForContext<ProviderManager>();
    private readonly ConcurrentDictionary<string, ProviderState> _providers = new();
    private readonly ConcurrentDictionary<int, SubscriptionInfo> _subscriptions = new();
    private int _nextSubscriptionId;

    public ProviderManager(GatewayConfig config, IDataRouter dataRouter)
    {
        _config = config;
        _dataRouter = dataRouter;
        InitializeProviders();
    }

    private void InitializeProviders()
    {
        if (_config.Providers == null) return;

        foreach (var (name, config) in _config.Providers)
        {
            if (!config.Enabled) continue;

            _providers[name] = new ProviderState
            {
                Name = name,
                Config = config,
                Status = new ProviderConnectionStatus(
                    ProviderName: name,
                    IsConnected: false,
                    LastConnectedAt: DateTimeOffset.MinValue,
                    LastDisconnectedAt: null,
                    ReconnectAttempts: 0,
                    LastError: null
                )
            };
        }
    }

    public IReadOnlyDictionary<string, ProviderConnectionStatus> GetProviderStatuses()
    {
        return _providers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Status
        );
    }

    public async Task<bool> ConnectProviderAsync(string providerName, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var state))
        {
            _log.Warning("Provider {Provider} not found", providerName);
            return false;
        }

        if (state.Status.IsConnected)
        {
            _log.Debug("Provider {Provider} already connected", providerName);
            return true;
        }

        try
        {
            _log.Information("Connecting to provider {Provider}", providerName);

            // Simulate connection (actual implementation would connect to provider)
            await Task.Delay(100, ct);

            state.Status = state.Status with
            {
                IsConnected = true,
                LastConnectedAt = DateTimeOffset.UtcNow,
                ReconnectAttempts = 0,
                LastError = null
            };

            _log.Information("Connected to provider {Provider}", providerName);
            return true;
        }
        catch (Exception ex)
        {
            state.Status = state.Status with
            {
                IsConnected = false,
                ReconnectAttempts = state.Status.ReconnectAttempts + 1,
                LastError = ex.Message
            };

            _log.Error(ex, "Failed to connect to provider {Provider}", providerName);
            return false;
        }
    }

    public async Task DisconnectProviderAsync(string providerName, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var state))
        {
            return;
        }

        try
        {
            _log.Information("Disconnecting from provider {Provider}", providerName);

            // Remove all subscriptions for this provider
            var subscriptionsToRemove = _subscriptions
                .Where(s => s.Value.Provider == providerName)
                .Select(s => s.Key)
                .ToList();

            foreach (var subId in subscriptionsToRemove)
            {
                _subscriptions.TryRemove(subId, out _);
            }

            // Simulate disconnect
            await Task.Delay(50, ct);

            state.Status = state.Status with
            {
                IsConnected = false,
                LastDisconnectedAt = DateTimeOffset.UtcNow
            };

            _log.Information("Disconnected from provider {Provider}", providerName);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error disconnecting from provider {Provider}", providerName);
        }
    }

    public async Task<int> SubscribeAsync(
        string providerName,
        string symbol,
        SubscriptionType[] types,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var state))
        {
            throw new InvalidOperationException($"Provider {providerName} not found");
        }

        if (!state.Status.IsConnected)
        {
            await ConnectProviderAsync(providerName, ct);
        }

        var subscriptionId = Interlocked.Increment(ref _nextSubscriptionId);

        var subscription = new SubscriptionInfo
        {
            Id = subscriptionId,
            Provider = providerName,
            Symbol = symbol,
            Types = types,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _subscriptions[subscriptionId] = subscription;
        state.ActiveSubscriptions.Add(subscriptionId);

        _log.Information("Created subscription {Id} for {Symbol} on {Provider}: {Types}",
            subscriptionId, symbol, providerName, string.Join(",", types));

        return subscriptionId;
    }

    public Task UnsubscribeAsync(string providerName, int subscriptionId, CancellationToken ct = default)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            if (_providers.TryGetValue(providerName, out var state))
            {
                state.ActiveSubscriptions.Remove(subscriptionId);
            }

            _log.Information("Removed subscription {Id} for {Symbol} on {Provider}",
                subscriptionId, subscription.Symbol, providerName);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers.Keys)
        {
            await DisconnectProviderAsync(provider);
        }
    }

    private class ProviderState
    {
        public required string Name { get; init; }
        public required ProviderConfig Config { get; init; }
        public ProviderConnectionStatus Status { get; set; } = null!;
        public HashSet<int> ActiveSubscriptions { get; } = [];
    }

    private class SubscriptionInfo
    {
        public int Id { get; init; }
        public required string Provider { get; init; }
        public required string Symbol { get; init; }
        public required SubscriptionType[] Types { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
