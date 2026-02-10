using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Represents a sampling preset for display.
/// </summary>
public sealed class SamplingPresetItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SamplingStrategyType Strategy { get; set; }
    public long? SampleSize { get; set; }
    public double? SamplePercent { get; set; }
    public int? IntervalSeconds { get; set; }
    public string[]? EventTypes { get; set; }
    public int? Seed { get; set; }
    public bool IncludeStatistics { get; set; }
}

/// <summary>
/// Represents a saved sample for display.
/// </summary>
public sealed class SavedSampleItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public long RecordCount { get; set; }
    public long FileSizeBytes { get; set; }
    public List<string>? Symbols { get; set; }

    public string CreatedDisplay => CreatedAt.ToString("MM/dd HH:mm");
    public string RecordCountDisplay => RecordCount switch
    {
        >= 1_000_000 => $"{RecordCount / 1_000_000.0:F1}M",
        >= 1_000 => $"{RecordCount / 1_000.0:F1}K",
        _ => RecordCount.ToString("N0")
    };
    public string FileSizeDisplay => FileSizeBytes switch
    {
        >= 1_073_741_824 => $"{FileSizeBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{FileSizeBytes / 1_024.0:F1} KB",
        _ => $"{FileSizeBytes} B"
    };
    public string SymbolsDisplay => Symbols != null && Symbols.Count > 0
        ? string.Join(", ", Symbols.Take(3)) + (Symbols.Count > 3 ? $" +{Symbols.Count - 3}" : "")
        : "-";
}
