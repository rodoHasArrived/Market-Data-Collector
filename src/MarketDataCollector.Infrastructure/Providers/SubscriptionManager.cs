using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers;

/// <summary>
/// Thread-safe subscription manager to eliminate duplicate subscription tracking code
/// across streaming providers (Alpaca, Polygon, NYSE, StockSharp, IB).
/// All were implementing identical Interlocked.Increment and lock-based patterns.
/// </summary>
public sealed class SubscriptionManager : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<int, SubscriptionInfo> _subscriptions = new();
    private readonly HashSet<string> _tradeSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _depthSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _quoteSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _log;
    private int _nextSubId;
    private bool _disposed;

    public SubscriptionManager(ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<SubscriptionManager>();
    }

    /// <summary>
    /// Gets all currently subscribed trade symbols.
    /// </summary>
    public IReadOnlySet<string> TradeSymbols
    {
        get
        {
            lock (_gate)
            {
                return _tradeSymbols.ToHashSet();
            }
        }
    }

    /// <summary>
    /// Gets all currently subscribed depth symbols.
    /// </summary>
    public IReadOnlySet<string> DepthSymbols
    {
        get
        {
            lock (_gate)
            {
                return _depthSymbols.ToHashSet();
            }
        }
    }

    /// <summary>
    /// Gets all currently subscribed quote symbols.
    /// </summary>
    public IReadOnlySet<string> QuoteSymbols
    {
        get
        {
            lock (_gate)
            {
                return _quoteSymbols.ToHashSet();
            }
        }
    }

    /// <summary>
    /// Gets all active subscription IDs.
    /// </summary>
    public IReadOnlyList<int> ActiveSubscriptionIds
    {
        get
        {
            lock (_gate)
            {
                return _subscriptions.Keys.ToList();
            }
        }
    }

    /// <summary>
    /// Gets count of active subscriptions.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _subscriptions.Count;
            }
        }
    }

    /// <summary>
    /// Subscribe to trades for a symbol.
    /// </summary>
    public int SubscribeTrades(string symbol)
    {
        return Subscribe(symbol, SubscriptionKind.Trades);
    }

    /// <summary>
    /// Subscribe to market depth for a symbol.
    /// </summary>
    public int SubscribeMarketDepth(string symbol)
    {
        return Subscribe(symbol, SubscriptionKind.Depth);
    }

    /// <summary>
    /// Subscribe to quotes for a symbol.
    /// </summary>
    public int SubscribeQuotes(string symbol)
    {
        return Subscribe(symbol, SubscriptionKind.Quotes);
    }

    /// <summary>
    /// Subscribe to a symbol with specified subscription kind.
    /// </summary>
    public int Subscribe(string symbol, SubscriptionKind kind)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));

        var normalizedSymbol = symbol.ToUpperInvariant().Trim();
        var id = Interlocked.Increment(ref _nextSubId);

        lock (_gate)
        {
            _subscriptions[id] = new SubscriptionInfo(normalizedSymbol, kind, DateTimeOffset.UtcNow);

            var symbolSet = GetSymbolSet(kind);
            symbolSet.Add(normalizedSymbol);
        }

        _log.Debug("Subscribed {Kind} for {Symbol} with ID {SubscriptionId}", kind, normalizedSymbol, id);
        return id;
    }

    /// <summary>
    /// Unsubscribe by subscription ID.
    /// </summary>
    /// <returns>True if subscription was found and removed.</returns>
    public bool Unsubscribe(int subscriptionId)
    {
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscriptionId, out var sub))
            {
                _log.Debug("Subscription {SubscriptionId} not found", subscriptionId);
                return false;
            }

            _subscriptions.Remove(subscriptionId);

            // Only remove from symbol set if no other subscriptions exist for this symbol/kind
            var symbolSet = GetSymbolSet(sub.Kind);
            var hasOtherSubs = _subscriptions.Values.Any(s =>
                s.Kind == sub.Kind &&
                s.Symbol.Equals(sub.Symbol, StringComparison.OrdinalIgnoreCase));

            if (!hasOtherSubs)
            {
                symbolSet.Remove(sub.Symbol);
            }

            _log.Debug("Unsubscribed {Kind} for {Symbol} (ID: {SubscriptionId})", sub.Kind, sub.Symbol, subscriptionId);
            return true;
        }
    }

    /// <summary>
    /// Unsubscribe all subscriptions for a symbol.
    /// </summary>
    public int UnsubscribeSymbol(string symbol)
    {
        var normalizedSymbol = symbol.ToUpperInvariant().Trim();
        var removed = 0;

        lock (_gate)
        {
            var toRemove = _subscriptions
                .Where(kvp => kvp.Value.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in toRemove)
            {
                _subscriptions.Remove(id);
                removed++;
            }

            _tradeSymbols.Remove(normalizedSymbol);
            _depthSymbols.Remove(normalizedSymbol);
            _quoteSymbols.Remove(normalizedSymbol);
        }

        _log.Debug("Unsubscribed all ({Count}) subscriptions for {Symbol}", removed, normalizedSymbol);
        return removed;
    }

    /// <summary>
    /// Clear all subscriptions.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _subscriptions.Clear();
            _tradeSymbols.Clear();
            _depthSymbols.Clear();
            _quoteSymbols.Clear();
        }

        _log.Debug("Cleared all subscriptions");
    }

    /// <summary>
    /// Get subscription info by ID.
    /// </summary>
    public SubscriptionInfo? GetSubscription(int subscriptionId)
    {
        lock (_gate)
        {
            return _subscriptions.TryGetValue(subscriptionId, out var sub) ? sub : null;
        }
    }

    /// <summary>
    /// Check if a symbol is subscribed to a specific kind.
    /// </summary>
    public bool IsSubscribed(string symbol, SubscriptionKind kind)
    {
        var normalizedSymbol = symbol.ToUpperInvariant().Trim();

        lock (_gate)
        {
            var symbolSet = GetSymbolSet(kind);
            return symbolSet.Contains(normalizedSymbol);
        }
    }

    /// <summary>
    /// Get all subscriptions of a specific kind.
    /// </summary>
    public IReadOnlyList<SubscriptionInfo> GetSubscriptionsByKind(SubscriptionKind kind)
    {
        lock (_gate)
        {
            return _subscriptions.Values.Where(s => s.Kind == kind).ToList();
        }
    }

    private HashSet<string> GetSymbolSet(SubscriptionKind kind)
    {
        return kind switch
        {
            SubscriptionKind.Trades => _tradeSymbols,
            SubscriptionKind.Depth => _depthSymbols,
            SubscriptionKind.Quotes => _quoteSymbols,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}

/// <summary>
/// Types of market data subscriptions.
/// </summary>
public enum SubscriptionKind
{
    Trades,
    Depth,
    Quotes
}

/// <summary>
/// Information about an active subscription.
/// </summary>
public sealed record SubscriptionInfo(
    string Symbol,
    SubscriptionKind Kind,
    DateTimeOffset SubscribedAt
);
