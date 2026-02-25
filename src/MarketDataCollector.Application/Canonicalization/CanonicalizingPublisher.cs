using System.Diagnostics;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Domain.Events;
using Serilog;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Decorator that canonicalizes events before forwarding them to the inner publisher.
/// Wraps any <see cref="IMarketEventPublisher"/> to apply symbol resolution, venue
/// normalization, and condition code mapping transparently.
/// </summary>
/// <remarks>
/// <para><b>Phase 2 (Dual-Write):</b> When <c>DualWriteEnabled</c> is true,
/// the raw event is published first, then the canonicalized version. This allows
/// downstream consumers to validate parity between raw and enriched paths.</para>
/// <para><b>Phase 3 (Canonical-only):</b> When dual-write is off,
/// only the canonicalized event is published.</para>
/// <para>Pilot symbol filtering limits canonicalization to a configurable subset
/// during rollout. Events for non-pilot symbols pass through unchanged.</para>
/// </remarks>
public sealed class CanonicalizingPublisher : IMarketEventPublisher
{
    private readonly IMarketEventPublisher _inner;
    private readonly IEventCanonicalizer _canonicalizer;
    private readonly HashSet<string>? _pilotSymbols;
    private readonly bool _dualWrite;
    private readonly ILogger _log = LoggingSetup.ForContext<CanonicalizingPublisher>();

    // Metrics counters (lock-free via Interlocked)
    private long _canonicalizedCount;
    private long _skippedCount;
    private long _unresolvedCount;
    private long _dualWriteCount;
    private long _totalDurationTicks;

    public CanonicalizingPublisher(
        IMarketEventPublisher inner,
        IEventCanonicalizer canonicalizer,
        IEnumerable<string>? pilotSymbols = null,
        bool dualWrite = true)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));
        _dualWrite = dualWrite;

        if (pilotSymbols is not null)
        {
            var set = new HashSet<string>(pilotSymbols, StringComparer.OrdinalIgnoreCase);
            _pilotSymbols = set.Count > 0 ? set : null;
        }
    }

    /// <summary>Total events successfully canonicalized.</summary>
    public long CanonicalizationCount => Interlocked.Read(ref _canonicalizedCount);

    /// <summary>Events skipped (non-pilot or heartbeat).</summary>
    public long SkippedCount => Interlocked.Read(ref _skippedCount);

    /// <summary>Events with unresolved canonical symbols.</summary>
    public long UnresolvedCount => Interlocked.Read(ref _unresolvedCount);

    /// <summary>Events dual-written (raw + canonical).</summary>
    public long DualWriteCount => Interlocked.Read(ref _dualWriteCount);

    /// <summary>Average canonicalization time in microseconds.</summary>
    public double AverageDurationUs
    {
        get
        {
            var count = Interlocked.Read(ref _canonicalizedCount);
            if (count == 0) return 0;
            var ticks = Interlocked.Read(ref _totalDurationTicks);
            return (double)ticks / Stopwatch.Frequency * 1_000_000 / count;
        }
    }

    public bool TryPublish(in MarketEvent evt)
    {
        // Skip canonicalization for non-pilot symbols
        if (_pilotSymbols is not null && !_pilotSymbols.Contains(evt.Symbol))
        {
            Interlocked.Increment(ref _skippedCount);
            return _inner.TryPublish(in evt);
        }

        // Dual-write: publish raw event first
        if (_dualWrite)
        {
            var rawOk = _inner.TryPublish(in evt);
            if (!rawOk)
            {
                // If raw publish fails (backpressure), don't try canonical either
                return false;
            }
            Interlocked.Increment(ref _dualWriteCount);
        }

        // Canonicalize
        var start = Stopwatch.GetTimestamp();
        var canonical = _canonicalizer.Canonicalize(evt);
        var elapsed = Stopwatch.GetTimestamp() - start;
        Interlocked.Add(ref _totalDurationTicks, elapsed);

        // Track unresolved symbols
        if (canonical.CanonicalSymbol is null && canonical.CanonicalizationVersion > 0)
        {
            Interlocked.Increment(ref _unresolvedCount);
        }

        Interlocked.Increment(ref _canonicalizedCount);

        // Publish the canonical event (or replace the raw if not dual-writing)
        return _inner.TryPublish(in canonical);
    }

    /// <summary>
    /// Gets a snapshot of canonicalization metrics for Prometheus export.
    /// </summary>
    public CanonicalizationMetricsSnapshot GetMetricsSnapshot() => new(
        CanonicalizationCount,
        SkippedCount,
        UnresolvedCount,
        DualWriteCount,
        AverageDurationUs);
}

/// <summary>
/// Snapshot of canonicalization pipeline metrics.
/// </summary>
public readonly record struct CanonicalizationMetricsSnapshot(
    long Canonicalized,
    long Skipped,
    long Unresolved,
    long DualWrites,
    double AverageDurationUs);
