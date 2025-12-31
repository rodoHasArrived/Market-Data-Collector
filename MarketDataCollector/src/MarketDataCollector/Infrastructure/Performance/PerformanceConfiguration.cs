using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace MarketDataCollector.Infrastructure.Performance;

/// <summary>
/// Configures runtime performance settings and provides initialization hooks
/// for high-performance market data collection.
/// </summary>
public static class PerformanceConfiguration
{
    private static bool _initialized;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes performance optimizations. Should be called early in application startup.
    /// </summary>
    /// <param name="options">Performance configuration options</param>
    public static void Initialize(PerformanceOptions? options = null)
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            options ??= PerformanceOptions.Default;

            // Configure GC
            ConfigureGarbageCollection(options);

            // Set process priority
            ConfigureProcessPriority(options);

            // Pre-JIT critical methods
            if (options.WarmUpJit)
            {
                WarmUpCriticalMethods();
            }

            _initialized = true;
        }
    }

    private static void ConfigureGarbageCollection(PerformanceOptions options)
    {
        // Force a collection before starting to clean up startup allocations
        if (options.ForceInitialGc)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        }

        // Set latency mode for reduced GC pauses
        if (options.LowLatencyMode)
        {
            try
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }
            catch
            {
                // May not be supported in all configurations
            }
        }
    }

    private static void ConfigureProcessPriority(PerformanceOptions options)
    {
        if (!options.SetHighProcessPriority) return;

        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;
        }
        catch
        {
            // May require elevated privileges
        }
    }

    /// <summary>
    /// Pre-JITs critical methods to avoid JIT delays during hot paths.
    /// </summary>
    private static void WarmUpCriticalMethods()
    {
        // Force JIT compilation of frequently used methods
        RuntimeHelpers.PrepareMethod(typeof(ThreadingUtilities).GetMethod(nameof(ThreadingUtilities.SetHighPriority))!.MethodHandle);
        RuntimeHelpers.PrepareMethod(typeof(HighResolutionTimestamp).GetMethod(nameof(HighResolutionTimestamp.GetTimestamp))!.MethodHandle);

        // Warm up Stopwatch
        _ = Stopwatch.GetTimestamp();
        _ = Stopwatch.Frequency;

        // Warm up DateTimeOffset
        _ = DateTimeOffset.UtcNow;

        // Warm up Interlocked operations
        long dummy = 0;
        Interlocked.Increment(ref dummy);
        Interlocked.Decrement(ref dummy);
        Interlocked.Exchange(ref dummy, 1);
        Interlocked.CompareExchange(ref dummy, 2, 1);
    }

    /// <summary>
    /// Executes a warm-up routine for the pipeline and collectors.
    /// Call this before market open to minimize initial latency variance.
    /// </summary>
    public static async Task WarmUpPipelineAsync(
        Action<string>? logAction = null,
        CancellationToken ct = default)
    {
        logAction?.Invoke("Starting pipeline warm-up...");

        // Generate dummy data to warm up serialization paths
        var timestamp = DateTimeOffset.UtcNow;

        // Warm up timestamp operations
        for (int i = 0; i < 1000; i++)
        {
            _ = HighResolutionTimestamp.GetTimestamp();
            _ = HighResolutionTimestamp.GetCurrentTime();
        }

        // Small delay to let JIT complete
        await Task.Delay(10, ct).ConfigureAwait(false);

        logAction?.Invoke("Pipeline warm-up complete.");
    }

    /// <summary>
    /// Gets information about the current GC configuration.
    /// </summary>
    public static GcConfigurationInfo GetGcInfo()
    {
        return new GcConfigurationInfo(
            IsServerGc: GCSettings.IsServerGC,
            LatencyMode: GCSettings.LatencyMode,
            LargeObjectHeapCompactionMode: GCSettings.LargeObjectHeapCompactionMode,
            TotalMemoryBytes: GC.GetTotalMemory(false),
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            ProcessorCount: Environment.ProcessorCount
        );
    }

    /// <summary>
    /// Requests a Gen0 garbage collection.
    /// Use sparingly - typically only during known low-activity periods.
    /// </summary>
    public static void RequestGen0Collection()
    {
        GC.Collect(0, GCCollectionMode.Optimized, blocking: false);
    }

    /// <summary>
    /// Requests a full garbage collection with compaction.
    /// Use only during maintenance windows or shutdown.
    /// </summary>
    public static void RequestFullCollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}

/// <summary>
/// Options for performance configuration.
/// </summary>
public sealed record PerformanceOptions
{
    /// <summary>
    /// Default performance options optimized for market data collection.
    /// </summary>
    public static PerformanceOptions Default { get; } = new();

    /// <summary>
    /// Whether to force an initial GC to clean up startup allocations.
    /// </summary>
    public bool ForceInitialGc { get; init; } = true;

    /// <summary>
    /// Whether to enable low-latency GC mode.
    /// </summary>
    public bool LowLatencyMode { get; init; } = true;

    /// <summary>
    /// Whether to set the process to high priority.
    /// </summary>
    public bool SetHighProcessPriority { get; init; } = true;

    /// <summary>
    /// Whether to pre-JIT critical methods.
    /// </summary>
    public bool WarmUpJit { get; init; } = true;

    /// <summary>
    /// CPU core to pin the main processing thread to (-1 for no pinning).
    /// </summary>
    public int MainThreadCpuAffinity { get; init; } = -1;
}

/// <summary>
/// Information about the current GC configuration.
/// </summary>
public readonly record struct GcConfigurationInfo(
    bool IsServerGc,
    GCLatencyMode LatencyMode,
    GCLargeObjectHeapCompactionMode LargeObjectHeapCompactionMode,
    long TotalMemoryBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int ProcessorCount
);
