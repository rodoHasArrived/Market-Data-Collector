using System.Threading.Channels;

namespace MarketDataCollector.Application.Pipeline;

/// <summary>
/// Shared configuration policy for bounded-channel pipelines.
/// Use the static factory methods (presets) to create policies for common scenarios.
/// </summary>
public sealed record EventPipelinePolicy(
    int Capacity = 100_000,
    BoundedChannelFullMode FullMode = BoundedChannelFullMode.DropOldest,
    bool EnableMetrics = true)
{
    #region Static Presets

    /// <summary>
    /// Default policy for general-purpose event pipelines.
    /// High capacity (100k), drops oldest on overflow, metrics enabled.
    /// </summary>
    public static EventPipelinePolicy Default { get; } = new(100_000, BoundedChannelFullMode.DropOldest, EnableMetrics: true);

    /// <summary>
    /// Policy for high-throughput streaming data pipelines (e.g., market data clients).
    /// Moderate capacity (50k), drops oldest on overflow, metrics enabled for monitoring.
    /// </summary>
    public static EventPipelinePolicy HighThroughput { get; } = new(50_000, BoundedChannelFullMode.DropOldest, EnableMetrics: true);

    /// <summary>
    /// Policy for internal message buffering channels (e.g., StockSharp message buffer).
    /// Moderate capacity (50k), drops oldest on overflow, metrics disabled for performance.
    /// </summary>
    public static EventPipelinePolicy MessageBuffer { get; } = new(50_000, BoundedChannelFullMode.DropOldest, EnableMetrics: false);

    /// <summary>
    /// Policy for background task/maintenance queues where no messages should be dropped.
    /// Low capacity (100), waits when full (backpressure), metrics disabled.
    /// </summary>
    public static EventPipelinePolicy MaintenanceQueue { get; } = new(100, BoundedChannelFullMode.Wait, EnableMetrics: false);

    /// <summary>
    /// Policy for logging channels.
    /// Low capacity (1k), drops oldest on overflow, metrics disabled.
    /// </summary>
    public static EventPipelinePolicy Logging { get; } = new(1_000, BoundedChannelFullMode.DropOldest, EnableMetrics: false);

    #endregion

    /// <summary>
    /// Creates a <see cref="BoundedChannelOptions"/> instance from this policy.
    /// </summary>
    /// <param name="singleReader">Whether there is a single consumer reading from the channel.</param>
    /// <param name="singleWriter">Whether there is a single producer writing to the channel.</param>
    /// <returns>Configured <see cref="BoundedChannelOptions"/>.</returns>
    public BoundedChannelOptions ToBoundedOptions(bool singleReader, bool singleWriter)
    {
        if (Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(Capacity), Capacity, "Capacity must be positive.");

        return new BoundedChannelOptions(Capacity)
        {
            FullMode = FullMode,
            SingleReader = singleReader,
            SingleWriter = singleWriter,
            AllowSynchronousContinuations = false
        };
    }
}
