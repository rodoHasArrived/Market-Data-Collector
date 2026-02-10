using System;
using System.Collections.Generic;

namespace MarketDataCollector.Ui.Services;

// DTO classes for storage API responses
// Shared between WPF and UWP desktop applications.

public class StorageStatsSummary
{
    public long TotalSizeBytes { get; set; }
    public long UsedSizeBytes { get; set; }
    public long FreeSizeBytes { get; set; }
    public double UsedPercentage { get; set; }
    public int TotalFiles { get; set; }
    public int TotalSymbols { get; set; }
    public DateTime OldestData { get; set; }
    public DateTime NewestData { get; set; }
}

public class StorageCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public double Percentage { get; set; }
}

public class SymbolStorageInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string? Exchange { get; set; }
    public string? Provider { get; set; }
    public bool IsSubscribed { get; set; }
    public DateTime? FirstDataPoint { get; set; }
    public DateTime? LastDataPoint { get; set; }
    public List<string> DataTypes { get; set; } = new();
}

public class SymbolStorageStats
{
    public string Symbol { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public long TotalEvents { get; set; }
    public int FileCount { get; set; }
    public double DataQuality { get; set; }
    public int GapCount { get; set; }
    public Dictionary<string, long> SizeByType { get; set; } = new();
    public Dictionary<string, long> EventsByType { get; set; } = new();
}

public class SymbolFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long RecordCount { get; set; }
    public string? CompressionType { get; set; }
}

public class SymbolPathResponse
{
    public string Symbol { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
}

public class StorageHealthReport
{
    public string Status { get; set; } = string.Empty;
    public double HealthScore { get; set; }
    public List<StorageIssue> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class StorageIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AffectedPath { get; set; }
}

public class CleanupCandidate
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastAccessed { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CleanupResult
{
    public bool Success { get; set; }
    public int FilesRemoved { get; set; }
    public long BytesFreed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ArchiveStats
{
    public int TotalArchives { get; set; }
    public long TotalSizeBytes { get; set; }
    public int CompressedFiles { get; set; }
    public double CompressionRatio { get; set; }
    public DateTime OldestArchive { get; set; }
    public DateTime NewestArchive { get; set; }
}
