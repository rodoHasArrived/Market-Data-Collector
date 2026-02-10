using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Uwp.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing data storage, file operations, and storage statistics.
/// Provides access to storage paths, file listings, and space usage.
/// </summary>
public sealed class StorageService : IStorageService
{
    private static StorageService? _instance;
    private static readonly object _lock = new();

    public static StorageService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new StorageService();
                }
            }
            return _instance;
        }
    }

    private StorageService() { }

    /// <summary>
    /// Gets storage statistics summary.
    /// </summary>
    public async Task<StorageStatsSummary?> GetStorageStatsAsync(CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<StorageStatsSummary>(UiApiRoutes.StorageStats, ct);
    }

    /// <summary>
    /// Gets detailed storage usage by category.
    /// </summary>
    public async Task<List<StorageCategory>?> GetStorageBreakdownAsync(CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<List<StorageCategory>>(UiApiRoutes.StorageBreakdown, ct);
    }

    /// <summary>
    /// Gets symbol-specific information.
    /// </summary>
    public async Task<SymbolStorageInfo?> GetSymbolInfoAsync(string symbol, CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<SymbolStorageInfo>(
            UiApiRoutes.WithParam(UiApiRoutes.StorageSymbolInfo, "symbol", symbol), ct);
    }

    /// <summary>
    /// Gets storage statistics for a specific symbol.
    /// </summary>
    public async Task<SymbolStorageStats?> GetSymbolStorageStatsAsync(string symbol, CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<SymbolStorageStats>(
            UiApiRoutes.WithParam(UiApiRoutes.StorageSymbolStats, "symbol", symbol), ct);
    }

    /// <summary>
    /// Gets list of data files for a symbol.
    /// </summary>
    public async Task<List<DataFileInfo>> GetSymbolFilesAsync(string symbol, CancellationToken ct = default)
    {
        var response = await ApiClientService.Instance.GetAsync<List<SymbolFileDto>>(
            UiApiRoutes.WithParam(UiApiRoutes.StorageSymbolFiles, "symbol", symbol), ct);

        if (response == null) return new List<DataFileInfo>();

        var files = new List<DataFileInfo>();
        foreach (var dto in response)
        {
            files.Add(new DataFileInfo
            {
                FileName = dto.FileName,
                FileType = dto.DataType,
                FileSize = FormatBytes(dto.SizeBytes),
                ModifiedDate = dto.ModifiedAt.ToString("yyyy-MM-dd HH:mm"),
                EventCount = dto.RecordCount.ToString("N0"),
                FileIcon = GetFileIcon(dto.DataType),
                TypeBackground = GetTypeBackground(dto.DataType)
            });
        }

        return files;
    }

    /// <summary>
    /// Gets the folder path for a symbol's data.
    /// </summary>
    public async Task<string?> GetSymbolFolderPathAsync(string symbol, CancellationToken ct = default)
    {
        var response = await ApiClientService.Instance.GetAsync<SymbolPathResponse>(
            UiApiRoutes.WithParam(UiApiRoutes.StorageSymbolPath, "symbol", symbol), ct);
        return response?.FolderPath;
    }

    /// <summary>
    /// Gets storage health report.
    /// </summary>
    public async Task<StorageHealthReport?> GetStorageHealthAsync(CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<StorageHealthReport>(UiApiRoutes.StorageHealth, ct);
    }

    /// <summary>
    /// Gets files that need cleanup based on retention policies.
    /// </summary>
    public async Task<List<CleanupCandidate>?> GetCleanupCandidatesAsync(CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<List<CleanupCandidate>>(UiApiRoutes.StorageCleanupCandidates, ct);
    }

    /// <summary>
    /// Runs storage cleanup.
    /// </summary>
    public async Task<CleanupResult?> RunCleanupAsync(bool dryRun = true, CancellationToken ct = default)
    {
        return await ApiClientService.Instance.PostAsync<CleanupResult>(
            UiApiRoutes.StorageCleanup,
            new { dryRun },
            ct);
    }

    /// <summary>
    /// Gets archive statistics.
    /// </summary>
    public async Task<ArchiveStats?> GetArchiveStatsAsync(CancellationToken ct = default)
    {
        return await ApiClientService.Instance.GetAsync<ArchiveStats>(UiApiRoutes.StorageArchiveStats, ct);
    }

    private static string GetFileIcon(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "trades" => "\uE8AB",      // Exchange icon
            "quotes" => "\uE8D4",      // Document icon
            "depth" => "\uE8A1",       // List icon
            "bars" => "\uE9D9",        // Chart icon
            "parquet" => "\uE7C3",     // Database icon
            _ => "\uE8A5"              // File icon
        };
    }

    private static SolidColorBrush GetTypeBackground(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "trades" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 63, 185, 80)),    // Green
            "quotes" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 88, 166, 255)),   // Blue
            "depth" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 210, 153, 34)),    // Orange
            "bars" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 163, 113, 247)),    // Purple
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 139, 148, 158))          // Gray
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F1} {sizes[order]}";
    }
}

// DTO classes for storage API responses

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
