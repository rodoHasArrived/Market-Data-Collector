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
    private static long _dualWriteTotal;
    private static readonly ConcurrentDictionary<(string Provider, string Field), long> _unresolvedCounts = new();
    private static readonly ConcurrentDictionary<string, ProviderParityStats> _parityStats = new();
    private static int _activeVersion;

    /// <summary>Records a successful canonicalization.</summary>
    public static void RecordSuccess(string provider, string eventType)
    {
        Interlocked.Increment(ref _successTotal);
        GetOrAddParity(provider).RecordSuccess();
    }

    /// <summary>Records a soft failure (partial canonicalization).</summary>
    public static void RecordSoftFail(string provider, string eventType)
    {
        Interlocked.Increment(ref _softFailTotal);
        GetOrAddParity(provider).RecordSoftFail();
    }

    /// <summary>Records a hard failure (event dropped or missing required fields).</summary>
    public static void RecordHardFail(string provider, string eventType)
    {
        Interlocked.Increment(ref _hardFailTotal);
        GetOrAddParity(provider).RecordHardFail();
    }

    /// <summary>Records an unresolved field (symbol, venue, or condition).</summary>
    public static void RecordUnresolved(string provider, string field)
    {
        _unresolvedCounts.AddOrUpdate((provider, field), 1, (_, count) => count + 1);
        GetOrAddParity(provider).RecordUnresolved(field);
    }

    /// <summary>Records a dual-write event (both raw and enriched persisted).</summary>
    public static void RecordDualWrite()
    {
        Interlocked.Increment(ref _dualWriteTotal);
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
            DualWriteTotal: Interlocked.Read(ref _dualWriteTotal),
            ActiveVersion: _activeVersion,
            UnresolvedCounts: new Dictionary<(string Provider, string Field), long>(_unresolvedCounts),
            ProviderParity: _parityStats.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToSnapshot())
        );
    }

    /// <summary>Resets all counters (for testing).</summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref _successTotal, 0);
        Interlocked.Exchange(ref _softFailTotal, 0);
        Interlocked.Exchange(ref _hardFailTotal, 0);
        Interlocked.Exchange(ref _dualWriteTotal, 0);
        _unresolvedCounts.Clear();
        _parityStats.Clear();
        Interlocked.Exchange(ref _activeVersion, 0);
    }

    private static ProviderParityStats GetOrAddParity(string provider)
    {
        return _parityStats.GetOrAdd(provider, _ => new ProviderParityStats());
    }
}

/// <summary>
/// Thread-safe per-provider parity counters for Phase 2 validation.
/// </summary>
internal sealed class ProviderParityStats
{
    private long _success;
    private long _softFail;
    private long _hardFail;
    private long _unresolvedSymbol;
    private long _unresolvedVenue;
    private long _unresolvedCondition;

    public void RecordSuccess() => Interlocked.Increment(ref _success);
    public void RecordSoftFail() => Interlocked.Increment(ref _softFail);
    public void RecordHardFail() => Interlocked.Increment(ref _hardFail);

    public void RecordUnresolved(string field)
    {
        switch (field)
        {
            case "symbol": Interlocked.Increment(ref _unresolvedSymbol); break;
            case "venue": Interlocked.Increment(ref _unresolvedVenue); break;
            case "condition": Interlocked.Increment(ref _unresolvedCondition); break;
        }
    }

    public ProviderParitySnapshot ToSnapshot()
    {
        var total = Interlocked.Read(ref _success) +
                    Interlocked.Read(ref _softFail) +
                    Interlocked.Read(ref _hardFail);
        var successCount = Interlocked.Read(ref _success);

        return new ProviderParitySnapshot(
            Total: total,
            Success: successCount,
            SoftFail: Interlocked.Read(ref _softFail),
            HardFail: Interlocked.Read(ref _hardFail),
            UnresolvedSymbol: Interlocked.Read(ref _unresolvedSymbol),
            UnresolvedVenue: Interlocked.Read(ref _unresolvedVenue),
            UnresolvedCondition: Interlocked.Read(ref _unresolvedCondition),
            MatchRatePercent: total > 0 ? Math.Round(100.0 * successCount / total, 2) : 0
        );
    }
}

/// <summary>
/// Immutable snapshot of canonicalization metrics at a point in time.
/// </summary>
public sealed record CanonicalizationSnapshot(
    long SuccessTotal,
    long SoftFailTotal,
    long HardFailTotal,
    long DualWriteTotal,
    int ActiveVersion,
    Dictionary<(string Provider, string Field), long> UnresolvedCounts,
    Dictionary<string, ProviderParitySnapshot> ProviderParity
);

/// <summary>
/// Per-provider parity statistics for Phase 2 validation dashboard.
/// </summary>
public sealed record ProviderParitySnapshot(
    long Total,
    long Success,
    long SoftFail,
    long HardFail,
    long UnresolvedSymbol,
    long UnresolvedVenue,
    long UnresolvedCondition,
    double MatchRatePercent
);
