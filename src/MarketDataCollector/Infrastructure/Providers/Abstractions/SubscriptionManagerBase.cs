using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MarketDataCollector.Infrastructure.Providers.Abstractions;

/// <summary>
/// Base class providing thread-safe subscription management for market data providers.
/// Reduces code duplication across provider implementations by centralizing:
/// - Subscription ID generation
/// - Symbol tracking for trades and quotes
/// - Thread-safe subscription state management
/// </summary>
public abstract class SubscriptionManagerBase
{
    /// <summary>
    /// Synchronization object for thread-safe subscription operations.
    /// </summary>
    protected readonly object SubscriptionGate = new();

    /// <summary>
    /// Maps subscription IDs to their symbol and type (e.g., "trades", "quotes", "depth").
    /// </summary>
    protected readonly Dictionary<int, (string Symbol, string Kind)> Subscriptions = new();

    /// <summary>
    /// Set of symbols currently subscribed for trade data.
    /// </summary>
    protected readonly HashSet<string> TradeSymbols = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set of symbols currently subscribed for quote/BBO data.
    /// </summary>
    protected readonly HashSet<string> QuoteSymbols = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set of symbols currently subscribed for market depth (L2) data.
    /// </summary>
    protected readonly HashSet<string> DepthSymbols = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Counter for generating unique subscription IDs.
    /// Starts at 100,000 to avoid conflicts with other ID ranges.
    /// </summary>
    private int _nextSubscriptionId = 100_000;

    /// <summary>
    /// Adds a new subscription and returns the subscription ID.
    /// Thread-safe.
    /// </summary>
    /// <param name="symbol">The symbol to subscribe to.</param>
    /// <param name="kind">The subscription type ("trades", "quotes", "depth").</param>
    /// <returns>A unique subscription ID.</returns>
    protected int AddSubscription(string symbol, string kind)
    {
        var id = Interlocked.Increment(ref _nextSubscriptionId);

        lock (SubscriptionGate)
        {
            Subscriptions[id] = (symbol, kind);

            switch (kind.ToLowerInvariant())
            {
                case "trades":
                    TradeSymbols.Add(symbol);
                    break;
                case "quotes":
                    QuoteSymbols.Add(symbol);
                    break;
                case "depth":
                    DepthSymbols.Add(symbol);
                    break;
            }
        }

        return id;
    }

    /// <summary>
    /// Removes a subscription by ID.
    /// Thread-safe.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID to remove.</param>
    /// <returns>True if the subscription was removed; false if it didn't exist.</returns>
    protected bool RemoveSubscription(int subscriptionId)
    {
        lock (SubscriptionGate)
        {
            if (!Subscriptions.TryGetValue(subscriptionId, out var sub))
                return false;

            Subscriptions.Remove(subscriptionId);

            // Only remove from symbol set if no other subscriptions exist for this symbol/kind
            if (!IsSymbolStillSubscribedForKind(sub.Symbol, sub.Kind))
            {
                switch (sub.Kind.ToLowerInvariant())
                {
                    case "trades":
                        TradeSymbols.Remove(sub.Symbol);
                        break;
                    case "quotes":
                        QuoteSymbols.Remove(sub.Symbol);
                        break;
                    case "depth":
                        DepthSymbols.Remove(sub.Symbol);
                        break;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Checks if a symbol still has active subscriptions for a given kind.
    /// Must be called within a lock on SubscriptionGate.
    /// </summary>
    private bool IsSymbolStillSubscribedForKind(string symbol, string kind)
    {
        return Subscriptions.Values.Any(v =>
            v.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase) &&
            v.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets information about a subscription by ID.
    /// Thread-safe.
    /// </summary>
    /// <param name="subscriptionId">The subscription ID.</param>
    /// <param name="subscription">The subscription info if found.</param>
    /// <returns>True if the subscription exists; false otherwise.</returns>
    protected bool TryGetSubscription(int subscriptionId, out (string Symbol, string Kind) subscription)
    {
        lock (SubscriptionGate)
        {
            return Subscriptions.TryGetValue(subscriptionId, out subscription);
        }
    }

    /// <summary>
    /// Gets the count of active subscriptions.
    /// Thread-safe.
    /// </summary>
    protected int SubscriptionCount
    {
        get
        {
            lock (SubscriptionGate)
            {
                return Subscriptions.Count;
            }
        }
    }

    /// <summary>
    /// Clears all subscriptions.
    /// Thread-safe.
    /// </summary>
    protected void ClearAllSubscriptions()
    {
        lock (SubscriptionGate)
        {
            Subscriptions.Clear();
            TradeSymbols.Clear();
            QuoteSymbols.Clear();
            DepthSymbols.Clear();
        }
    }

    /// <summary>
    /// Gets a snapshot of all currently subscribed trade symbols.
    /// Thread-safe.
    /// </summary>
    protected IReadOnlySet<string> GetTradeSymbolsSnapshot()
    {
        lock (SubscriptionGate)
        {
            return new HashSet<string>(TradeSymbols, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets a snapshot of all currently subscribed quote symbols.
    /// Thread-safe.
    /// </summary>
    protected IReadOnlySet<string> GetQuoteSymbolsSnapshot()
    {
        lock (SubscriptionGate)
        {
            return new HashSet<string>(QuoteSymbols, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets a snapshot of all currently subscribed depth symbols.
    /// Thread-safe.
    /// </summary>
    protected IReadOnlySet<string> GetDepthSymbolsSnapshot()
    {
        lock (SubscriptionGate)
        {
            return new HashSet<string>(DepthSymbols, StringComparer.OrdinalIgnoreCase);
        }
    }
}
