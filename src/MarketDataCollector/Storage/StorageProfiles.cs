using System.IO;

namespace MarketDataCollector.Storage;

/// <summary>
/// Storage profile presets for simplifying configuration.
/// </summary>
public enum StorageProfile
{
    Research,
    LowLatency,
    Archival
}

public sealed record StorageProfilePreset(
    string Id,
    string Label,
    string Description,
    Func<StorageOptions, StorageOptions> Apply);

public static class StorageProfilePresets
{
    private static readonly IReadOnlyList<StorageProfilePreset> Presets = new[]
    {
        new StorageProfilePreset(
            Id: "Research",
            Label: "Research",
            Description: "Balanced defaults for analysis workflows (manifests + compression).",
            Apply: options =>
            {
                var updated = Clone(options);
                updated.Compress = true;
                updated.CompressionCodec = CompressionCodec.Gzip;
                updated.GenerateManifests = true;
                updated.PartitionStrategy ??= new PartitionStrategy(PartitionDimension.Date, PartitionDimension.Symbol, DatePartition.Daily);
                return updated;
            }),
        new StorageProfilePreset(
            Id: "LowLatency",
            Label: "Low Latency",
            Description: "Prioritizes ingest speed with minimal processing.",
            Apply: options =>
            {
                var updated = Clone(options);
                updated.Compress = false;
                updated.CompressionCodec = CompressionCodec.None;
                updated.GenerateManifests = false;
                updated.PartitionStrategy ??= new PartitionStrategy(PartitionDimension.Symbol, PartitionDimension.EventType, DatePartition.Hourly);
                return updated;
            }),
        new StorageProfilePreset(
            Id: "Archival",
            Label: "Archival",
            Description: "Long-term retention with tiering-friendly defaults.",
            Apply: options =>
            {
                var updated = Clone(options);
                updated.Compress = true;
                updated.CompressionCodec = CompressionCodec.Zstd;
                updated.GenerateManifests = true;
                updated.EmbedChecksum = true;
                updated.RetentionDays ??= 3650;
                updated.MaxTotalBytes ??= 2L * 1024L * 1024L * 1024L * 1024L;
                updated.PartitionStrategy ??= new PartitionStrategy(PartitionDimension.Date, PartitionDimension.Source, DatePartition.Monthly);

                updated.Tiering ??= new TieringOptions
                {
                    Enabled = true,
                    Tiers = new List<TierConfig>
                    {
                        new() { Name = "hot", Path = Path.Combine(updated.RootPath, "hot"), MaxAgeDays = 7, Format = "jsonl", Compression = CompressionCodec.None },
                        new() { Name = "warm", Path = Path.Combine(updated.RootPath, "warm"), MaxAgeDays = 30, Format = "jsonl", Compression = CompressionCodec.Gzip },
                        new() { Name = "cold", Path = Path.Combine(updated.RootPath, "cold"), MaxAgeDays = 180, Format = "parquet", Compression = CompressionCodec.Zstd },
                        new() { Name = "archive", Path = Path.Combine(updated.RootPath, "archive"), Format = "parquet", Compression = CompressionCodec.Zstd }
                    }
                };

                return updated;
            })
    };

    public static StorageOptions ApplyProfile(string? profile, StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return options;
        }

        var preset = Presets.FirstOrDefault(p => string.Equals(p.Id, profile, StringComparison.OrdinalIgnoreCase));
        return preset?.Apply(options) ?? options;
    }

    public static IReadOnlyList<StorageProfilePreset> GetPresets() => Presets;

    private static StorageOptions Clone(StorageOptions options)
    {
        return new StorageOptions
        {
            RootPath = options.RootPath,
            Compress = options.Compress,
            CompressionCodec = options.CompressionCodec,
            NamingConvention = options.NamingConvention,
            DatePartition = options.DatePartition,
            IncludeProvider = options.IncludeProvider,
            FilePrefix = options.FilePrefix,
            RetentionDays = options.RetentionDays,
            MaxTotalBytes = options.MaxTotalBytes,
            Tiering = options.Tiering,
            Quotas = options.Quotas,
            Policies = options.Policies,
            GenerateManifests = options.GenerateManifests,
            EmbedChecksum = options.EmbedChecksum,
            PartitionStrategy = options.PartitionStrategy
        };
    }
}
