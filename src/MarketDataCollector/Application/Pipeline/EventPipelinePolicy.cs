using System.Threading.Channels;

namespace MarketDataCollector.Application.Pipeline;

/// <summary>
/// Shared configuration policy for bounded-channel pipelines.
/// </summary>
public sealed record EventPipelinePolicy(
    int Capacity = 100_000,
    BoundedChannelFullMode FullMode = BoundedChannelFullMode.DropOldest,
    bool EnableMetrics = true)
{
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
