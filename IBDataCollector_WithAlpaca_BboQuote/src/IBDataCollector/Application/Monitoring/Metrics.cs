using System.Threading;

namespace IBDataCollector.Application.Monitoring;

/// <summary>
/// Minimal hot-path safe counters (no allocations; thread-safe).
/// </summary>
public static class Metrics
{
    private static long _published;
    private static long _dropped;
    private static long _integrity;

    public static long Published => Interlocked.Read(ref _published);
    public static long Dropped => Interlocked.Read(ref _dropped);
    public static long Integrity => Interlocked.Read(ref _integrity);

    public static void IncPublished() => Interlocked.Increment(ref _published);
    public static void IncDropped() => Interlocked.Increment(ref _dropped);
    public static void IncIntegrity() => Interlocked.Increment(ref _integrity);
}
