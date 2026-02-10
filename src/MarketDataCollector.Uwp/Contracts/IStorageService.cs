using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for managing data storage, file operations, and storage statistics.
/// </summary>
public interface IStorageService
{
    Task<StorageStatsSummary?> GetStorageStatsAsync(CancellationToken ct = default);
    Task<List<StorageCategory>?> GetStorageBreakdownAsync(CancellationToken ct = default);
    Task<SymbolStorageInfo?> GetSymbolInfoAsync(string symbol, CancellationToken ct = default);
    Task<SymbolStorageStats?> GetSymbolStorageStatsAsync(string symbol, CancellationToken ct = default);
    Task<List<DataFileInfo>> GetSymbolFilesAsync(string symbol, CancellationToken ct = default);
    Task<string?> GetSymbolFolderPathAsync(string symbol, CancellationToken ct = default);
    Task<StorageHealthReport?> GetStorageHealthAsync(CancellationToken ct = default);
    Task<List<CleanupCandidate>?> GetCleanupCandidatesAsync(CancellationToken ct = default);
    Task<CleanupResult?> RunCleanupAsync(bool dryRun = true, CancellationToken ct = default);
    Task<ArchiveStats?> GetArchiveStatsAsync(CancellationToken ct = default);
}
