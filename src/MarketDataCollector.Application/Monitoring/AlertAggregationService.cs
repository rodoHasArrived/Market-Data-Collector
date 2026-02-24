using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Reduces alert noise by grouping, deduplicating, and batching related alerts.
/// Instead of firing individual alerts for each event, this service collects alerts
/// within a configurable window and emits a single grouped notification.
/// Implements improvement 9.9 from the high-value improvements brainstorm.
/// </summary>
public sealed class AlertAggregationService : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<AlertAggregationService>();
    private readonly AlertAggregationConfig _config;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _flushTask;
    private volatile bool _isDisposed;

    /// <summary>
    /// Pending alerts grouped by category key.
    /// </summary>
    private readonly ConcurrentDictionary<string, AlertGroup> _pendingGroups = new();

    /// <summary>
    /// Tracks recently sent alert fingerprints for deduplication.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentFingerprints = new();

    /// <summary>
    /// Fired when a grouped alert batch is ready to send.
    /// </summary>
    public event Action<AlertBatch>? OnAlertBatchReady;

    public AlertAggregationService(AlertAggregationConfig? config = null)
    {
        _config = config ?? AlertAggregationConfig.Default;
        _flushTask = FlushLoopAsync();
        _log.Information("AlertAggregationService initialized with window {WindowSeconds}s, max batch {MaxBatchSize}",
            _config.AggregationWindowSeconds, _config.MaxBatchSize);
    }

    /// <summary>
    /// Submits an alert for aggregation. The alert may be grouped with similar alerts
    /// and delivered as a batch, or suppressed if a duplicate was recently sent.
    /// </summary>
    /// <param name="category">Alert category (e.g., "connection_lost", "sla_violation", "high_latency").</param>
    /// <param name="severity">Alert severity level.</param>
    /// <param name="title">Short alert title.</param>
    /// <param name="message">Detailed alert message.</param>
    /// <param name="source">Source of the alert (e.g., provider name, symbol).</param>
    /// <param name="fingerprint">Optional dedup key. Alerts with the same fingerprint within
    /// the cooldown window are suppressed.</param>
    public void Submit(string category, AlertSeverity severity, string title, string message,
        string? source = null, string? fingerprint = null)
    {
        if (_isDisposed) return;

        // Deduplication check
        var dedupKey = fingerprint ?? $"{category}:{title}:{source}";
        if (_recentFingerprints.TryGetValue(dedupKey, out var lastSent))
        {
            if ((DateTimeOffset.UtcNow - lastSent).TotalSeconds < _config.DeduplicationCooldownSeconds)
            {
                PrometheusMetrics.RecordAlertSuppressed();
                return;
            }
        }

        var alert = new AlertItem(
            category, severity, title, message, source,
            dedupKey, DateTimeOffset.UtcNow);

        var groupKey = $"{category}:{severity}";
        var group = _pendingGroups.GetOrAdd(groupKey, _ => new AlertGroup(category, severity));
        group.Add(alert);

        // If we've hit max batch size, flush immediately
        if (group.Count >= _config.MaxBatchSize)
        {
            FlushGroup(groupKey, group);
        }
    }

    private async Task FlushLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_config.AggregationWindowSeconds));
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                FlushAll();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — flush remaining
            FlushAll();
        }
    }

    private void FlushAll()
    {
        foreach (var kvp in _pendingGroups)
        {
            if (kvp.Value.Count > 0)
            {
                FlushGroup(kvp.Key, kvp.Value);
            }
        }

        // Clean up stale fingerprints
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_config.DeduplicationCooldownSeconds * 2);
        foreach (var kvp in _recentFingerprints.Where(f => f.Value < cutoff).ToList())
        {
            _recentFingerprints.TryRemove(kvp.Key, out _);
        }
    }

    private void FlushGroup(string groupKey, AlertGroup group)
    {
        var alerts = group.DrainAll();
        if (alerts.Count == 0) return;

        // Record fingerprints for deduplication
        foreach (var alert in alerts)
        {
            _recentFingerprints[alert.Fingerprint] = alert.Timestamp;
        }

        // Build the batch summary
        var highestSeverity = alerts.Max(a => a.Severity);
        var sourceSummary = alerts
            .Where(a => a.Source != null)
            .GroupBy(a => a.Source!)
            .Select(g => $"{g.Key} ({g.Count()}x)")
            .ToList();

        var batch = new AlertBatch(
            Category: group.Category,
            Severity: highestSeverity,
            AlertCount: alerts.Count,
            SuppressedCount: 0, // tracked globally via Prometheus
            Summary: alerts.Count == 1
                ? alerts[0].Title
                : $"{alerts.Count} {group.Category} alerts",
            Details: alerts.Count == 1
                ? alerts[0].Message
                : string.Join("\n", alerts.Select(a => $"- [{a.Source ?? "unknown"}] {a.Title}")),
            Sources: sourceSummary,
            Alerts: alerts,
            CreatedAt: DateTimeOffset.UtcNow,
            WindowStart: alerts.Min(a => a.Timestamp),
            WindowEnd: alerts.Max(a => a.Timestamp));

        PrometheusMetrics.RecordAlertBatchSent();

        _log.Information("Emitting alert batch: {Category} with {Count} alerts (severity: {Severity})",
            batch.Category, batch.AlertCount, batch.Severity);

        OnAlertBatchReady?.Invoke(batch);

        _pendingGroups.TryRemove(groupKey, out _);
    }

    /// <summary>
    /// Returns the current count of pending (unbatched) alerts.
    /// </summary>
    public int PendingAlertCount => _pendingGroups.Values.Sum(g => g.Count);

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();
        try { await _flushTask; } catch { /* ignore */ }
        _cts.Dispose();
    }
}

/// <summary>
/// Configuration for alert aggregation behavior.
/// </summary>
public sealed record AlertAggregationConfig
{
    /// <summary>
    /// Time window in seconds to accumulate alerts before flushing a batch.
    /// </summary>
    public int AggregationWindowSeconds { get; init; } = 30;

    /// <summary>
    /// Maximum number of alerts in a single batch before forcing a flush.
    /// </summary>
    public int MaxBatchSize { get; init; } = 50;

    /// <summary>
    /// Cooldown in seconds before a duplicate alert (same fingerprint) is allowed again.
    /// </summary>
    public int DeduplicationCooldownSeconds { get; init; } = 300;

    public static AlertAggregationConfig Default => new();
}

/// <summary>
/// Severity levels for alerts.
/// </summary>
public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

/// <summary>
/// A single alert item before aggregation.
/// </summary>
public sealed record AlertItem(
    string Category,
    AlertSeverity Severity,
    string Title,
    string Message,
    string? Source,
    string Fingerprint,
    DateTimeOffset Timestamp);

/// <summary>
/// Thread-safe container for accumulating alerts of the same category/severity.
/// </summary>
internal sealed class AlertGroup
{
    private readonly object _lock = new();
    private readonly List<AlertItem> _alerts = new();

    public string Category { get; }
    public AlertSeverity Severity { get; }

    public AlertGroup(string category, AlertSeverity severity)
    {
        Category = category;
        Severity = severity;
    }

    public int Count
    {
        get { lock (_lock) { return _alerts.Count; } }
    }

    public void Add(AlertItem alert)
    {
        lock (_lock) { _alerts.Add(alert); }
    }

    public List<AlertItem> DrainAll()
    {
        lock (_lock)
        {
            var copy = new List<AlertItem>(_alerts);
            _alerts.Clear();
            return copy;
        }
    }
}

/// <summary>
/// A batch of grouped alerts ready for delivery.
/// </summary>
public sealed record AlertBatch(
    string Category,
    AlertSeverity Severity,
    int AlertCount,
    int SuppressedCount,
    string Summary,
    string Details,
    IReadOnlyList<string> Sources,
    IReadOnlyList<AlertItem> Alerts,
    DateTimeOffset CreatedAt,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);
