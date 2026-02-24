using System.Collections.Concurrent;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Thread-safe in-memory counters for canonicalization events.
/// Integrates with <see cref="Monitoring.PrometheusMetrics"/> for metric export.
/// </summary>
public static class CanonicalizationMetrics
{
    private static long _successTotal;
    private static long _softFailTotal;
    private static long _hardFailTotal;
    private static readonly ConcurrentDictionary<(string Provider, string Field), long> _unresolvedCounts = new();
    private static int _activeVersion;

    /// <summary>Records a successful canonicalization.</summary>
    public static void RecordSuccess(string provider, string eventType)
    {
        Interlocked.Increment(ref _successTotal);
    }

    /// <summary>Records a soft failure (partial canonicalization).</summary>
    public static void RecordSoftFail(string provider, string eventType)
    {
        Interlocked.Increment(ref _softFailTotal);
    }

    /// <summary>Records a hard failure (event dropped or missing required fields).</summary>
    public static void RecordHardFail(string provider, string eventType)
    {
        Interlocked.Increment(ref _hardFailTotal);
    }

    /// <summary>Records an unresolved field (symbol, venue, or condition).</summary>
    public static void RecordUnresolved(string provider, string field)
    {
        _unresolvedCounts.AddOrUpdate((provider, field), 1, (_, count) => count + 1);
    }

    /// <summary>Sets the active canonicalization version.</summary>
    public static void SetActiveVersion(int version)
    {
        Interlocked.Exchange(ref _activeVersion, version);
    }

    /// <summary>Gets a snapshot of current metrics.</summary>
    public static CanonicalizationSnapshot GetSnapshot()
    {
        return new CanonicalizationSnapshot(
            SuccessTotal: Interlocked.Read(ref _successTotal),
            SoftFailTotal: Interlocked.Read(ref _softFailTotal),
            HardFailTotal: Interlocked.Read(ref _hardFailTotal),
            ActiveVersion: _activeVersion,
            UnresolvedCounts: new Dictionary<(string Provider, string Field), long>(_unresolvedCounts)
        );
    }

    /// <summary>Resets all counters (for testing).</summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref _successTotal, 0);
        Interlocked.Exchange(ref _softFailTotal, 0);
        Interlocked.Exchange(ref _hardFailTotal, 0);
        _unresolvedCounts.Clear();
        Interlocked.Exchange(ref _activeVersion, 0);
    }
}

/// <summary>
/// Immutable snapshot of canonicalization metrics at a point in time.
/// </summary>
public sealed record CanonicalizationSnapshot(
    long SuccessTotal,
    long SoftFailTotal,
    long HardFailTotal,
    int ActiveVersion,
    Dictionary<(string Provider, string Field), long> UnresolvedCounts
);
