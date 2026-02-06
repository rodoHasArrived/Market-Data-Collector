using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Service for analyzing storage usage and providing analytics.
/// </summary>
public sealed class StorageAnalyticsService
{
    private static StorageAnalyticsService? _instance;
    private static readonly object _lock = new();

    private readonly ConfigService _configService;
    private readonly NotificationService _notificationService;
    private StorageAnalytics? _cachedAnalytics;
    private DateTime _lastAnalysisTime;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public static StorageAnalyticsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new StorageAnalyticsService();
                }
            }
            return _instance;
        }
    }

    private StorageAnalyticsService()
    {
        _configService = new ConfigService();
        _notificationService = NotificationService.Instance;
    }

    /// <summary>
    /// Gets storage analytics, using cache if available.
    /// </summary>
    public async Task<StorageAnalytics> GetAnalyticsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedAnalytics != null &&
            DateTime.UtcNow - _lastAnalysisTime < _cacheExpiration)
        {
            return _cachedAnalytics;
        }

        var analytics = await CalculateAnalyticsAsync();
        _cachedAnalytics = analytics;
        _lastAnalysisTime = DateTime.UtcNow;

        AnalyticsUpdated?.Invoke(this, new StorageAnalyticsEventArgs { Analytics = analytics });

        // Check for storage warnings
        await CheckStorageWarningsAsync(analytics);

        return analytics;
    }

    private async Task<StorageAnalytics> CalculateAnalyticsAsync()
    {
        var config = await _configService.LoadConfigAsync();
        var dataRoot = config?.DataRoot ?? "data";

        var analytics = new StorageAnalytics
        {
            LastUpdated = DateTime.UtcNow
        };

        // Resolve data root path
        var basePath = Path.IsPathRooted(dataRoot)
            ? dataRoot
            : Path.Combine(AppContext.BaseDirectory, dataRoot);

        if (!Directory.Exists(basePath))
        {
            return analytics;
        }

        var symbolStats = new Dictionary<string, SymbolStorageInfo>();

        try
        {
            await Task.Run(() =>
            {
                // Analyze all files in the data directory
                var files = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var fileName = Path.GetFileName(file);
                    var dirName = Path.GetDirectoryName(file) ?? "";
                    var relativePath = Path.GetRelativePath(basePath, file);

                    // Try to extract symbol from path or filename
                    var symbol = ExtractSymbolFromPath(relativePath);

                    // Categorize by data type
                    var isTradeData = relativePath.Contains("Trade", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.Contains("trade", StringComparison.OrdinalIgnoreCase);
                    var isDepthData = relativePath.Contains("Depth", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.Contains("depth", StringComparison.OrdinalIgnoreCase);
                    var isHistorical = relativePath.Contains("Historical", StringComparison.OrdinalIgnoreCase) ||
                                       relativePath.Contains("Bar", StringComparison.OrdinalIgnoreCase);

                    var size = fileInfo.Length;

                    // Update totals
                    analytics.TotalSizeBytes += size;
                    analytics.TotalFileCount++;

                    if (isTradeData)
                    {
                        analytics.TradeSizeBytes += size;
                        analytics.TradeFileCount++;
                    }
                    else if (isDepthData)
                    {
                        analytics.DepthSizeBytes += size;
                        analytics.DepthFileCount++;
                    }
                    else if (isHistorical)
                    {
                        analytics.HistoricalSizeBytes += size;
                        analytics.HistoricalFileCount++;
                    }
                    else
                    {
                        // Default to trade data if unknown
                        analytics.TradeSizeBytes += size;
                        analytics.TradeFileCount++;
                    }

                    // Update per-symbol stats
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        if (!symbolStats.ContainsKey(symbol))
                        {
                            symbolStats[symbol] = new SymbolStorageInfo
                            {
                                Symbol = symbol,
                                OldestData = fileInfo.CreationTimeUtc,
                                NewestData = fileInfo.LastWriteTimeUtc
                            };
                        }

                        var stats = symbolStats[symbol];
                        stats.SizeBytes += size;
                        stats.FileCount++;

                        if (fileInfo.CreationTimeUtc < stats.OldestData)
                            stats.OldestData = fileInfo.CreationTimeUtc;
                        if (fileInfo.LastWriteTimeUtc > stats.NewestData)
                            stats.NewestData = fileInfo.LastWriteTimeUtc;
                    }
                }

                // Calculate percentages
                if (analytics.TotalSizeBytes > 0)
                {
                    foreach (var stats in symbolStats.Values)
                    {
                        stats.PercentOfTotal = (double)stats.SizeBytes / analytics.TotalSizeBytes * 100;
                    }
                }

                // Sort by size and take top symbols
                analytics.SymbolBreakdown = symbolStats.Values
                    .OrderByDescending(s => s.SizeBytes)
                    .ToArray();

                // Estimate daily growth (based on last 7 days of data)
                analytics.DailyGrowthBytes = EstimateDailyGrowth(basePath);

                // Project days until disk is full
                analytics.ProjectedDaysUntilFull = CalculateDaysUntilFull(basePath, analytics.DailyGrowthBytes);
            });
        }
        catch (Exception ex)
        {
            // Log error but return what we have
            System.Diagnostics.Debug.WriteLine($"Storage analysis error: {ex.Message}");
        }

        return analytics;
    }

    private static string ExtractSymbolFromPath(string relativePath)
    {
        // Try to extract symbol from common path patterns
        // Pattern 1: symbol/type/date.ext (BySymbol)
        // Pattern 2: date/symbol/type.ext (ByDate)
        // Pattern 3: type/symbol/date.ext (ByType)
        // Pattern 4: symbol_type_date.ext (Flat)

        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Check if first part looks like a symbol (typically 1-5 uppercase letters)
        if (parts.Length >= 1 && IsLikelySymbol(parts[0]))
        {
            return parts[0].ToUpper();
        }

        // Check if second part looks like a symbol
        if (parts.Length >= 2 && IsLikelySymbol(parts[1]))
        {
            return parts[1].ToUpper();
        }

        // Try to extract from filename
        var fileName = Path.GetFileNameWithoutExtension(parts[^1]);
        var fileNameParts = fileName.Split('_', '-');
        foreach (var part in fileNameParts)
        {
            if (IsLikelySymbol(part))
            {
                return part.ToUpper();
            }
        }

        return string.Empty;
    }

    private static bool IsLikelySymbol(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 1 || value.Length > 6)
            return false;

        // Check if it's all uppercase letters (or numbers for indices like SPY500)
        return value.All(c => char.IsLetterOrDigit(c)) &&
               value.Any(char.IsLetter) &&
               !new[] { "Trade", "Depth", "Historical", "Bar", "Data" }.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static long EstimateDailyGrowth(string basePath)
    {
        try
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var recentFiles = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTimeUtc >= sevenDaysAgo)
                .ToList();

            var totalSize = recentFiles.Sum(f => f.Length);
            return totalSize / 7; // Average per day
        }
        catch
        {
            return 0;
        }
    }

    private static int? CalculateDaysUntilFull(string basePath, long dailyGrowth)
    {
        if (dailyGrowth <= 0) return null;

        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(basePath) ?? "C:");
            var freeSpace = driveInfo.AvailableFreeSpace;

            // Leave 10% buffer
            var usableSpace = (long)(freeSpace * 0.9);
            return (int)(usableSpace / dailyGrowth);
        }
        catch
        {
            return null;
        }
    }

    private async Task CheckStorageWarningsAsync(StorageAnalytics analytics)
    {
        try
        {
            var config = await _configService.LoadConfigAsync();
            var dataRoot = config?.DataRoot ?? "data";
            var basePath = Path.IsPathRooted(dataRoot)
                ? dataRoot
                : Path.Combine(AppContext.BaseDirectory, dataRoot);

            var driveInfo = new DriveInfo(Path.GetPathRoot(basePath) ?? "C:");
            var usedPercent = 100.0 - (driveInfo.AvailableFreeSpace * 100.0 / driveInfo.TotalSize);

            if (usedPercent >= 90)
            {
                await _notificationService.NotifyStorageWarningAsync(usedPercent, driveInfo.AvailableFreeSpace);
            }
        }
        catch
        {
            // Ignore drive info errors
        }
    }

    /// <summary>
    /// Gets storage usage for the data drive.
    /// </summary>
    public async Task<DriveStorageInfo?> GetDriveInfoAsync()
    {
        try
        {
            var config = await _configService.LoadConfigAsync();
            var dataRoot = config?.DataRoot ?? "data";
            var basePath = Path.IsPathRooted(dataRoot)
                ? dataRoot
                : Path.Combine(AppContext.BaseDirectory, dataRoot);

            var driveInfo = new DriveInfo(Path.GetPathRoot(basePath) ?? "C:");

            return new DriveStorageInfo
            {
                DriveName = driveInfo.Name,
                TotalBytes = driveInfo.TotalSize,
                FreeBytes = driveInfo.AvailableFreeSpace,
                UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace,
                UsedPercent = 100.0 - (driveInfo.AvailableFreeSpace * 100.0 / driveInfo.TotalSize),
                DriveType = driveInfo.DriveType.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats bytes into human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
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

    /// <summary>
    /// Event raised when analytics are updated.
    /// </summary>
    public event EventHandler<StorageAnalyticsEventArgs>? AnalyticsUpdated;
}

/// <summary>
/// Storage analytics event args.
/// </summary>
public class StorageAnalyticsEventArgs : EventArgs
{
    public StorageAnalytics? Analytics { get; set; }
}

/// <summary>
/// Drive storage information.
/// </summary>
public class DriveStorageInfo
{
    public string DriveName { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsedPercent { get; set; }
    public string DriveType { get; set; } = string.Empty;
}
