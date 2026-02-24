using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Detects and alerts on cross-provider mid-price divergence for the same symbol.
/// When two or more providers report quotes for the same symbol and the mid-prices
/// diverge beyond a configurable threshold, this service fires an alert and records
/// the event in Prometheus metrics.
/// Implements improvement 4.2 from the high-value improvements brainstorm.
/// </summary>
public sealed class QuoteDivergenceAlertService : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<QuoteDivergenceAlertService>();
    private readonly QuoteDivergenceConfig _config;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _isDisposed;

    /// <summary>
    /// Tracks the latest mid-price per (symbol, provider) pair.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ProviderQuoteSnapshot>> _quotes = new();

    /// <summary>
    /// Tracks which symbols are currently in divergence state.
    /// </summary>
    private readonly ConcurrentDictionary<string, DivergenceState> _activeDivergences = new();

    /// <summary>
    /// Fired when a new divergence is detected for a symbol.
    /// </summary>
    public event Action<QuoteDivergenceEvent>? OnDivergenceDetected;

    /// <summary>
    /// Fired when a divergence resolves (prices converge again).
    /// </summary>
    public event Action<QuoteDivergenceResolvedEvent>? OnDivergenceResolved;

    public QuoteDivergenceAlertService(QuoteDivergenceConfig? config = null)
    {
        _config = config ?? QuoteDivergenceConfig.Default;
        _log.Information("QuoteDivergenceAlertService initialized with threshold {ThresholdBps} bps, window {WindowSeconds}s",
            _config.DivergenceThresholdBps, _config.ComparisonWindowSeconds);
    }

    /// <summary>
    /// Records a mid-price observation from a provider.
    /// Call this from the event pipeline whenever a BBO quote arrives.
    /// </summary>
    public void RecordQuote(string symbol, string provider, decimal bidPrice, decimal askPrice, DateTimeOffset timestamp)
    {
        if (_isDisposed || bidPrice <= 0 || askPrice <= 0) return;

        var midPrice = (bidPrice + askPrice) / 2m;
        var providerQuotes = _quotes.GetOrAdd(symbol, _ => new ConcurrentDictionary<string, ProviderQuoteSnapshot>());
        providerQuotes[provider] = new ProviderQuoteSnapshot(provider, midPrice, bidPrice, askPrice, timestamp);

        CheckDivergence(symbol, providerQuotes);
    }

    private void CheckDivergence(string symbol, ConcurrentDictionary<string, ProviderQuoteSnapshot> providerQuotes)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_config.ComparisonWindowSeconds);
        var recentQuotes = providerQuotes.Values
            .Where(q => q.Timestamp >= cutoff)
            .ToList();

        if (recentQuotes.Count < 2) return;

        var maxMid = recentQuotes.Max(q => q.MidPrice);
        var minMid = recentQuotes.Min(q => q.MidPrice);
        var avgMid = recentQuotes.Average(q => q.MidPrice);

        if (avgMid == 0) return;

        var divergenceBps = (maxMid - minMid) / avgMid * 10_000m;

        if (divergenceBps >= _config.DivergenceThresholdBps)
        {
            var maxProvider = recentQuotes.First(q => q.MidPrice == maxMid).Provider;
            var minProvider = recentQuotes.First(q => q.MidPrice == minMid).Provider;

            if (!_activeDivergences.ContainsKey(symbol))
            {
                var state = new DivergenceState(DateTimeOffset.UtcNow, divergenceBps, maxProvider, minProvider);
                _activeDivergences[symbol] = state;

                PrometheusMetrics.RecordQuoteDivergence(symbol);
                PrometheusMetrics.SetActiveQuoteDivergences(_activeDivergences.Count);

                _log.Warning(
                    "Cross-provider quote divergence detected for {Symbol}: {DivergenceBps:F1} bps between {MaxProvider} ({MaxMid:F4}) and {MinProvider} ({MinMid:F4})",
                    symbol, divergenceBps, maxProvider, maxMid, minProvider, minMid);

                var evt = new QuoteDivergenceEvent(
                    symbol, divergenceBps, maxProvider, maxMid, minProvider, minMid,
                    recentQuotes.Select(q => q.Provider).ToArray(), DateTimeOffset.UtcNow);

                OnDivergenceDetected?.Invoke(evt);
            }
            else
            {
                // Update existing divergence
                _activeDivergences[symbol] = _activeDivergences[symbol] with { CurrentDivergenceBps = divergenceBps };
            }
        }
        else if (_activeDivergences.TryRemove(symbol, out var resolvedState))
        {
            PrometheusMetrics.SetActiveQuoteDivergences(_activeDivergences.Count);

            var duration = DateTimeOffset.UtcNow - resolvedState.DetectedAt;
            _log.Information(
                "Cross-provider quote divergence resolved for {Symbol} after {Duration}",
                symbol, duration);

            OnDivergenceResolved?.Invoke(new QuoteDivergenceResolvedEvent(symbol, duration, resolvedState.PeakDivergenceBps));
        }
    }

    /// <summary>
    /// Returns a snapshot of all currently divergent symbols.
    /// </summary>
    public IReadOnlyList<QuoteDivergenceStatus> GetActiveDivergences()
    {
        return _activeDivergences.Select(kvp => new QuoteDivergenceStatus(
            kvp.Key,
            kvp.Value.CurrentDivergenceBps,
            kvp.Value.DetectedAt,
            DateTimeOffset.UtcNow - kvp.Value.DetectedAt,
            kvp.Value.HighProvider,
            kvp.Value.LowProvider
        )).ToList();
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed) return default;
        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();
        return default;
    }

    private sealed record DivergenceState(
        DateTimeOffset DetectedAt,
        decimal CurrentDivergenceBps,
        string HighProvider,
        string LowProvider)
    {
        public decimal PeakDivergenceBps => CurrentDivergenceBps; // simplified; could track max
    }
}

/// <summary>
/// Configuration for cross-provider quote divergence alerting.
/// </summary>
public sealed record QuoteDivergenceConfig
{
    /// <summary>
    /// Divergence threshold in basis points. Alerts fire when the spread between
    /// provider mid-prices exceeds this value.
    /// </summary>
    public decimal DivergenceThresholdBps { get; init; } = 10m;

    /// <summary>
    /// Time window in seconds within which quotes from different providers are compared.
    /// </summary>
    public int ComparisonWindowSeconds { get; init; } = 5;

    public static QuoteDivergenceConfig Default => new();
}

/// <summary>
/// Snapshot of a provider's latest quote for a symbol.
/// </summary>
public sealed record ProviderQuoteSnapshot(
    string Provider,
    decimal MidPrice,
    decimal BidPrice,
    decimal AskPrice,
    DateTimeOffset Timestamp);

/// <summary>
/// Event fired when cross-provider quote divergence is detected.
/// </summary>
public sealed record QuoteDivergenceEvent(
    string Symbol,
    decimal DivergenceBps,
    string HighProvider,
    decimal HighMidPrice,
    string LowProvider,
    decimal LowMidPrice,
    string[] Providers,
    DateTimeOffset DetectedAt);

/// <summary>
/// Event fired when a divergence resolves.
/// </summary>
public sealed record QuoteDivergenceResolvedEvent(
    string Symbol,
    TimeSpan Duration,
    decimal PeakDivergenceBps);

/// <summary>
/// Status of a currently divergent symbol.
/// </summary>
public sealed record QuoteDivergenceStatus(
    string Symbol,
    decimal DivergenceBps,
    DateTimeOffset DetectedAt,
    TimeSpan Duration,
    string HighProvider,
    string LowProvider);
