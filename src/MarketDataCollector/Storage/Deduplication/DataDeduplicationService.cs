using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Pipeline;
using Serilog;

namespace MarketDataCollector.Storage.Deduplication;

/// <summary>
/// Implements content-based deduplication for market data from multiple sources.
/// Detects and merges duplicate events with provenance tracking.
/// </summary>
public sealed class DataDeduplicationService : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<DataDeduplicationService>();
    private readonly DeduplicationConfig _config;
    private readonly ConcurrentDictionary<string, EventFingerprint> _recentFingerprints = new();
    private readonly ConcurrentDictionary<string, DeduplicationStats> _symbolStats = new();
    private readonly Timer _cleanupTimer;
    private volatile bool _isDisposed;

    // Global counters
    private long _totalProcessed;
    private long _totalDuplicates;
    private long _totalMerged;
    private long _totalBytesDedup;

    public DataDeduplicationService(DeduplicationConfig? config = null)
    {
        _config = config ?? new DeduplicationConfig();

        _cleanupTimer = new Timer(CleanupOldFingerprints, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _log.Information("DataDeduplicationService initialized with strategy: {Strategy}",
            _config.Strategy);
    }

    /// <summary>
    /// Checks if an event is a duplicate and returns the deduplication result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DeduplicationResult CheckDuplicate(MarketEvent evt)
    {
        if (_isDisposed) return DeduplicationResult.NotDuplicate(evt);

        Interlocked.Increment(ref _totalProcessed);

        var fingerprint = ComputeFingerprint(evt);
        var key = $"{evt.Symbol}:{fingerprint.Hash}";

        // Check for existing fingerprint
        if (_recentFingerprints.TryGetValue(key, out var existing))
        {
            // It's a duplicate
            Interlocked.Increment(ref _totalDuplicates);
            UpdateSymbolStats(evt.Symbol, isDuplicate: true);

            // Determine how to handle based on strategy
            var result = _config.Strategy switch
            {
                DeduplicationStrategy.FirstWins => HandleFirstWins(evt, existing),
                DeduplicationStrategy.BestQuality => HandleBestQuality(evt, existing),
                DeduplicationStrategy.MergeProvenance => HandleMergeProvenance(evt, existing),
                DeduplicationStrategy.KeepAll => DeduplicationResult.NotDuplicate(evt),
                _ => DeduplicationResult.Duplicate(evt, existing.OriginalEvent)
            };

            return result;
        }

        // Not a duplicate - record fingerprint
        var newFingerprint = new EventFingerprint
        {
            Hash = fingerprint.Hash,
            FirstSeenAt = DateTimeOffset.UtcNow,
            OriginalEvent = evt,
            SourceProvider = evt.Source,
            SeenCount = 1
        };

        _recentFingerprints[key] = newFingerprint;
        UpdateSymbolStats(evt.Symbol, isDuplicate: false);

        return DeduplicationResult.NotDuplicate(evt);
    }

    /// <summary>
    /// Runs deduplication analysis on a batch of events.
    /// </summary>
    public DeduplicationReport AnalyzeBatch(IEnumerable<MarketEvent> events)
    {
        var report = new DeduplicationReport
        {
            StartedAt = DateTime.UtcNow
        };

        var duplicateGroups = new Dictionary<string, List<MarketEvent>>();

        foreach (var evt in events)
        {
            var fingerprint = ComputeFingerprint(evt);
            var key = fingerprint.Hash;

            if (!duplicateGroups.TryGetValue(key, out var group))
            {
                group = new List<MarketEvent>();
                duplicateGroups[key] = group;
            }

            group.Add(evt);
            report.TotalEvents++;
        }

        foreach (var (hash, group) in duplicateGroups)
        {
            if (group.Count > 1)
            {
                report.DuplicateGroups.Add(new DuplicateGroup
                {
                    Hash = hash,
                    Count = group.Count,
                    Symbol = group[0].Symbol,
                    EventType = group[0].Type.ToString(),
                    Timestamps = group.Select(e => e.Timestamp).ToList(),
                    Sources = group.Select(e => e.Source ?? "Unknown").Distinct().ToList()
                });

                report.TotalDuplicates += group.Count - 1;
            }
        }

        // Estimate space savings
        var avgEventSize = 200; // bytes
        report.EstimatedSavingsBytes = report.TotalDuplicates * avgEventSize;

        report.CompletedAt = DateTime.UtcNow;
        report.UniqueEvents = report.TotalEvents - report.TotalDuplicates;
        report.DuplicateRatio = report.TotalEvents > 0
            ? (double)report.TotalDuplicates / report.TotalEvents * 100
            : 0;

        return report;
    }

    /// <summary>
    /// Runs deduplication on files in a directory (dry-run by default).
    /// </summary>
    public async Task<FileDeduplicationResult> DeduplicateFilesAsync(
        string directory,
        bool dryRun = true,
        IProgress<DeduplicationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new FileDeduplicationResult
        {
            Directory = directory,
            IsDryRun = dryRun,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var files = Directory.GetFiles(directory, "*.jsonl", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(directory, "*.jsonl.gz", SearchOption.AllDirectories))
                .ToList();

            result.TotalFiles = files.Count;

            // Build fingerprint index from all files
            var globalFingerprints = new Dictionary<string, FileEventInfo>();
            var duplicatesPerFile = new Dictionary<string, List<int>>();
            var processedFiles = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                progress?.Report(new DeduplicationProgress
                {
                    Stage = "Scanning",
                    CurrentFile = file,
                    FilesProcessed = processedFiles,
                    TotalFiles = files.Count
                });

                var lineNumber = 0;
                var fileDuplicates = new List<int>();

                await foreach (var line in ReadFileLinesAsync(file, ct))
                {
                    lineNumber++;

                    try
                    {
                        var hash = ComputeLineHash(line);

                        if (globalFingerprints.TryGetValue(hash, out var existing))
                        {
                            // Duplicate found
                            fileDuplicates.Add(lineNumber);
                            result.TotalDuplicates++;
                            result.SpaceSavingsBytes += Encoding.UTF8.GetByteCount(line);
                        }
                        else
                        {
                            globalFingerprints[hash] = new FileEventInfo
                            {
                                FilePath = file,
                                LineNumber = lineNumber,
                                Hash = hash
                            };
                            result.UniqueEvents++;
                        }
                    }
                    catch { }
                }

                if (fileDuplicates.Count > 0)
                {
                    duplicatesPerFile[file] = fileDuplicates;
                    result.FilesWithDuplicates++;
                }

                processedFiles++;
                result.TotalEvents += lineNumber;
            }

            // If not dry-run, actually remove duplicates
            if (!dryRun && duplicatesPerFile.Count > 0)
            {
                progress?.Report(new DeduplicationProgress
                {
                    Stage = "Removing duplicates",
                    FilesProcessed = 0,
                    TotalFiles = duplicatesPerFile.Count
                });

                var processedDedupFiles = 0;
                foreach (var (file, duplicateLines) in duplicatesPerFile)
                {
                    ct.ThrowIfCancellationRequested();

                    await RemoveDuplicateLinesAsync(file, duplicateLines, ct);
                    processedDedupFiles++;

                    progress?.Report(new DeduplicationProgress
                    {
                        Stage = "Removing duplicates",
                        CurrentFile = file,
                        FilesProcessed = processedDedupFiles,
                        TotalFiles = duplicatesPerFile.Count
                    });
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "File deduplication failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Gets the current deduplication statistics.
    /// </summary>
    public DeduplicationStatistics GetStatistics()
    {
        return new DeduplicationStatistics
        {
            TotalProcessed = Interlocked.Read(ref _totalProcessed),
            TotalDuplicates = Interlocked.Read(ref _totalDuplicates),
            TotalMerged = Interlocked.Read(ref _totalMerged),
            EstimatedBytesDedup = Interlocked.Read(ref _totalBytesDedup),
            ActiveFingerprints = _recentFingerprints.Count,
            SymbolStats = _symbolStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
            DuplicateRatio = Interlocked.Read(ref _totalProcessed) > 0
                ? (double)Interlocked.Read(ref _totalDuplicates) / Interlocked.Read(ref _totalProcessed) * 100
                : 0
        };
    }

    /// <summary>
    /// Clears the fingerprint cache.
    /// </summary>
    public void ClearCache()
    {
        _recentFingerprints.Clear();
        _log.Information("Deduplication cache cleared");
    }

    private (string Hash, string NormalizedData) ComputeFingerprint(MarketEvent evt)
    {
        // Create a normalized representation for fingerprinting
        var normalized = new StringBuilder();

        // Core identity fields (order matters for consistency)
        normalized.Append(evt.Symbol);
        normalized.Append('|');
        normalized.Append(evt.Timestamp.ToUnixTimeMilliseconds());
        normalized.Append('|');
        normalized.Append((int)evt.Type);

        // Type-specific content
        switch (evt.Payload)
        {
            case Trade trade:
                normalized.Append('|');
                normalized.Append(trade.Price.ToString("F8"));
                normalized.Append('|');
                normalized.Append(trade.Size);
                normalized.Append('|');
                normalized.Append(trade.SequenceNumber);
                break;

            // Add cases for other payload types as needed
            default:
                // Fallback: use JSON serialization
                normalized.Append('|');
                normalized.Append(JsonSerializer.Serialize(evt.Payload));
                break;
        }

        var data = normalized.ToString();
        var hash = ComputeHash(data);

        return (hash, data);
    }

    private static string ComputeLineHash(string line)
    {
        return ComputeHash(line.Trim());
    }

    private static string ComputeHash(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..16]).ToLowerInvariant(); // Use first 16 bytes
    }

    private DeduplicationResult HandleFirstWins(MarketEvent newEvent, EventFingerprint existing)
    {
        existing.SeenCount++;
        existing.LastSeenAt = DateTimeOffset.UtcNow;

        return DeduplicationResult.Duplicate(newEvent, existing.OriginalEvent);
    }

    private DeduplicationResult HandleBestQuality(MarketEvent newEvent, EventFingerprint existing)
    {
        // Determine which event has better quality
        var existingQuality = ComputeEventQuality(existing.OriginalEvent);
        var newQuality = ComputeEventQuality(newEvent);

        if (newQuality > existingQuality)
        {
            // Replace with higher quality event
            existing.OriginalEvent = newEvent;
            existing.SourceProvider = newEvent.Source;
            Interlocked.Increment(ref _totalMerged);

            return DeduplicationResult.Replaced(newEvent, existing.OriginalEvent);
        }

        existing.SeenCount++;
        existing.LastSeenAt = DateTimeOffset.UtcNow;

        return DeduplicationResult.Duplicate(newEvent, existing.OriginalEvent);
    }

    private DeduplicationResult HandleMergeProvenance(MarketEvent newEvent, EventFingerprint existing)
    {
        // Track that we've seen this from multiple sources
        existing.SeenCount++;
        existing.LastSeenAt = DateTimeOffset.UtcNow;

        if (newEvent.Source != null && !string.IsNullOrEmpty(newEvent.Source))
        {
            existing.AlternateSources ??= new List<string>();
            if (!existing.AlternateSources.Contains(newEvent.Source))
            {
                existing.AlternateSources.Add(newEvent.Source);
            }
        }

        Interlocked.Increment(ref _totalMerged);

        // Return merged result with provenance info
        return DeduplicationResult.Merged(existing.OriginalEvent, existing.AlternateSources ?? new List<string>());
    }

    private static int ComputeEventQuality(MarketEvent evt)
    {
        var quality = 0;

        // Higher quality for more complete data
        if (evt.StreamId != null) quality += 10;
        if (evt.Source != null) quality += 5;

        // Payload-specific quality
        switch (evt.Payload)
        {
            case Trade trade:
                if (trade.SequenceNumber > 0) quality += 10;
                if (trade.Exchange != null) quality += 5;
                break;
        }

        return quality;
    }

    private void UpdateSymbolStats(string symbol, bool isDuplicate)
    {
        var stats = _symbolStats.GetOrAdd(symbol, _ => new DeduplicationStats { Symbol = symbol });

        if (isDuplicate)
        {
            Interlocked.Increment(ref stats.Duplicates);
        }
        else
        {
            Interlocked.Increment(ref stats.Unique);
        }
    }

    private void CleanupOldFingerprints(object? state)
    {
        if (_isDisposed) return;

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_config.FingerprintRetentionMinutes);
        var keysToRemove = _recentFingerprints
            .Where(kvp => kvp.Value.FirstSeenAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _recentFingerprints.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _log.Debug("Cleaned up {Count} old fingerprints", keysToRemove.Count);
        }
    }

    private static async IAsyncEnumerable<string> ReadFileLinesAsync(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var stream = File.OpenRead(filePath);
        Stream readStream = stream;

        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            readStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
        }

        using var reader = new StreamReader(readStream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    private static async Task RemoveDuplicateLinesAsync(string filePath, List<int> duplicateLines, CancellationToken ct)
    {
        var duplicateSet = new HashSet<int>(duplicateLines);
        var tempPath = filePath + ".dedup.tmp";

        try
        {
            await using var output = File.CreateText(tempPath);
            var lineNumber = 0;

            await foreach (var line in ReadFileLinesAsync(filePath, ct))
            {
                lineNumber++;
                if (!duplicateSet.Contains(lineNumber))
                {
                    await output.WriteLineAsync(line);
                }
            }

            // Replace original with deduplicated version
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cleanupTimer.Dispose();
        _recentFingerprints.Clear();
    }
}

/// <summary>
/// Configuration for deduplication.
/// </summary>
public class DeduplicationConfig
{
    /// <summary>
    /// Deduplication strategy to use.
    /// </summary>
    public DeduplicationStrategy Strategy { get; set; } = DeduplicationStrategy.FirstWins;

    /// <summary>
    /// How long to retain fingerprints in minutes.
    /// </summary>
    public int FingerprintRetentionMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum fingerprints to keep in memory.
    /// </summary>
    public int MaxFingerprintCache { get; set; } = 1_000_000;
}

/// <summary>
/// Deduplication strategy.
/// </summary>
public enum DeduplicationStrategy
{
    /// <summary>
    /// Keep the first event seen, discard duplicates.
    /// </summary>
    FirstWins,

    /// <summary>
    /// Keep the event with best quality indicators.
    /// </summary>
    BestQuality,

    /// <summary>
    /// Merge provenance information from duplicates.
    /// </summary>
    MergeProvenance,

    /// <summary>
    /// Keep all events (no deduplication, just detection).
    /// </summary>
    KeepAll
}

/// <summary>
/// Result of a deduplication check.
/// </summary>
public class DeduplicationResult
{
    public bool IsDuplicate { get; set; }
    public bool WasReplaced { get; set; }
    public bool WasMerged { get; set; }
    public MarketEvent Event { get; set; } = default!;
    public MarketEvent? OriginalEvent { get; set; }
    public List<string>? ProvenanceSources { get; set; }

    public static DeduplicationResult NotDuplicate(MarketEvent evt) => new()
    {
        IsDuplicate = false,
        Event = evt
    };

    public static DeduplicationResult Duplicate(MarketEvent newEvent, MarketEvent original) => new()
    {
        IsDuplicate = true,
        Event = newEvent,
        OriginalEvent = original
    };

    public static DeduplicationResult Replaced(MarketEvent newEvent, MarketEvent original) => new()
    {
        IsDuplicate = true,
        WasReplaced = true,
        Event = newEvent,
        OriginalEvent = original
    };

    public static DeduplicationResult Merged(MarketEvent evt, List<string> sources) => new()
    {
        IsDuplicate = true,
        WasMerged = true,
        Event = evt,
        ProvenanceSources = sources
    };
}

/// <summary>
/// Fingerprint for a seen event.
/// </summary>
internal class EventFingerprint
{
    public string Hash { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public MarketEvent OriginalEvent { get; set; } = default!;
    public string? SourceProvider { get; set; }
    public int SeenCount { get; set; }
    public List<string>? AlternateSources { get; set; }
}

/// <summary>
/// Per-symbol deduplication statistics.
/// </summary>
public class DeduplicationStats
{
    public string Symbol { get; set; } = string.Empty;
    public long Unique { get; set; }
    public long Duplicates { get; set; }

    public DeduplicationStats Clone() => new()
    {
        Symbol = Symbol,
        Unique = Interlocked.Read(ref Unique),
        Duplicates = Interlocked.Read(ref Duplicates)
    };
}

/// <summary>
/// Overall deduplication statistics.
/// </summary>
public class DeduplicationStatistics
{
    public long TotalProcessed { get; set; }
    public long TotalDuplicates { get; set; }
    public long TotalMerged { get; set; }
    public long EstimatedBytesDedup { get; set; }
    public int ActiveFingerprints { get; set; }
    public double DuplicateRatio { get; set; }
    public Dictionary<string, DeduplicationStats> SymbolStats { get; set; } = new();
}

/// <summary>
/// Report from batch analysis.
/// </summary>
public class DeduplicationReport
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int TotalEvents { get; set; }
    public int UniqueEvents { get; set; }
    public int TotalDuplicates { get; set; }
    public double DuplicateRatio { get; set; }
    public long EstimatedSavingsBytes { get; set; }
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new();
}

/// <summary>
/// A group of duplicate events.
/// </summary>
public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public List<DateTimeOffset> Timestamps { get; set; } = new();
    public List<string> Sources { get; set; } = new();
}

/// <summary>
/// Progress during deduplication.
/// </summary>
public class DeduplicationProgress
{
    public string Stage { get; set; } = string.Empty;
    public string? CurrentFile { get; set; }
    public int FilesProcessed { get; set; }
    public int TotalFiles { get; set; }
}

/// <summary>
/// Result of file deduplication.
/// </summary>
public class FileDeduplicationResult
{
    public bool Success { get; set; }
    public bool IsDryRun { get; set; }
    public string Directory { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int FilesWithDuplicates { get; set; }
    public long TotalEvents { get; set; }
    public long UniqueEvents { get; set; }
    public long TotalDuplicates { get; set; }
    public long SpaceSavingsBytes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Info about an event in a file.
/// </summary>
internal class FileEventInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Hash { get; set; } = string.Empty;
}
