using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Storage.Archival;

/// <summary>
/// Write-Ahead Log (WAL) for durable, crash-safe storage operations.
/// All market events are first written to the WAL before being committed to primary storage.
/// </summary>
public sealed class WriteAheadLog : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<WriteAheadLog>();
    private readonly string _walDirectory;
    private readonly WalOptions _options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private FileStream? _currentWalFile;
    private StreamWriter? _currentWriter;
    private string? _currentWalPath;
    private long _currentSequence;
    private long _currentFileSize;
    private int _uncommittedRecords;
    private DateTime _lastFlushTime = DateTime.UtcNow;

    // WAL file header constants
    private const string WalMagic = "MDCWAL01";
    private const int WalVersion = 1;

    public WriteAheadLog(string walDirectory, WalOptions? options = null)
    {
        _walDirectory = walDirectory;
        _options = options ?? new WalOptions();
        Directory.CreateDirectory(_walDirectory);
    }

    /// <summary>
    /// Initialize the WAL, recovering any uncommitted transactions.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _log.Information("Initializing WAL in {WalDirectory}", _walDirectory);

        // Find and recover any existing WAL files
        var walFiles = Directory.GetFiles(_walDirectory, "*.wal")
            .OrderBy(f => f)
            .ToList();

        if (walFiles.Count > 0)
        {
            _log.Information("Found {Count} existing WAL files, recovering...", walFiles.Count);
            foreach (var walFile in walFiles)
            {
                await RecoverWalFileAsync(walFile, ct);
            }
        }

        // Get the highest sequence number
        _currentSequence = await GetLastSequenceNumberAsync(ct);

        // Start a new WAL file
        await StartNewWalFileAsync(ct);

        _log.Information("WAL initialized, current sequence: {Sequence}", _currentSequence);
    }

    /// <summary>
    /// Append a record to the WAL.
    /// </summary>
    public async Task<WalRecord> AppendAsync<T>(T data, string recordType, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            // Check if we need to rotate the WAL file
            if (ShouldRotate())
            {
                await RotateWalFileAsync(ct);
            }

            var sequence = ++_currentSequence;
            var timestamp = DateTime.UtcNow;

            // Serialize the data
            var payload = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Create record with checksum
            var record = new WalRecord
            {
                Sequence = sequence,
                Timestamp = timestamp,
                RecordType = recordType,
                Payload = payload,
                Checksum = ComputeChecksum(sequence, timestamp, recordType, payload)
            };

            // Write to WAL
            await WriteRecordAsync(record, ct);

            _uncommittedRecords++;

            // Check if we should flush
            if (_options.SyncMode == WalSyncMode.EveryWrite ||
                (_options.SyncMode == WalSyncMode.BatchedSync && _uncommittedRecords >= _options.SyncBatchSize) ||
                (DateTime.UtcNow - _lastFlushTime) >= _options.MaxFlushDelay)
            {
                await FlushAsync(ct);
            }

            return record;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Commit a batch of records, marking them as successfully persisted.
    /// </summary>
    public async Task CommitAsync(long throughSequence, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            // Write a commit marker
            var commitRecord = new WalRecord
            {
                Sequence = ++_currentSequence,
                Timestamp = DateTime.UtcNow,
                RecordType = "COMMIT",
                Payload = throughSequence.ToString(),
                Checksum = string.Empty // Computed below
            };
            commitRecord.Checksum = ComputeChecksum(
                commitRecord.Sequence,
                commitRecord.Timestamp,
                commitRecord.RecordType,
                commitRecord.Payload);

            await WriteRecordAsync(commitRecord, ct);
            await FlushAsync(ct);

            _log.Debug("Committed through sequence {Sequence}", throughSequence);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Flush any buffered writes to disk.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_currentWriter == null || _currentWalFile == null) return;

        await _currentWriter.FlushAsync();

        if (_options.SyncMode != WalSyncMode.NoSync)
        {
            await _currentWalFile.FlushAsync(ct);
        }

        _uncommittedRecords = 0;
        _lastFlushTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Get uncommitted records for replay/recovery.
    /// </summary>
    public async IAsyncEnumerable<WalRecord> GetUncommittedRecordsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        long lastCommittedSequence = 0;
        var uncommittedRecords = new List<WalRecord>();

        // Read all WAL files
        var walFiles = Directory.GetFiles(_walDirectory, "*.wal")
            .OrderBy(f => f)
            .ToList();

        foreach (var walFile in walFiles)
        {
            await foreach (var record in ReadWalFileAsync(walFile, ct))
            {
                if (record.RecordType == "COMMIT")
                {
                    if (long.TryParse(record.Payload, out var seq))
                    {
                        lastCommittedSequence = seq;
                        // Clear uncommitted up to this point
                        uncommittedRecords.RemoveAll(r => r.Sequence <= lastCommittedSequence);
                    }
                }
                else
                {
                    uncommittedRecords.Add(record);
                }
            }
        }

        // Return only uncommitted records
        foreach (var record in uncommittedRecords.Where(r => r.Sequence > lastCommittedSequence))
        {
            yield return record;
        }
    }

    /// <summary>
    /// Truncate WAL files that have been fully committed.
    /// </summary>
    public async Task TruncateAsync(long throughSequence, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var walFiles = Directory.GetFiles(_walDirectory, "*.wal")
                .OrderBy(f => f)
                .ToList();

            foreach (var walFile in walFiles)
            {
                // Check if this file is fully committed
                long maxSequence = 0;
                await foreach (var record in ReadWalFileAsync(walFile, ct))
                {
                    maxSequence = Math.Max(maxSequence, record.Sequence);
                }

                if (maxSequence <= throughSequence && walFile != _currentWalPath)
                {
                    // Archive or delete the WAL file
                    if (_options.ArchiveAfterTruncate)
                    {
                        var archiveDir = Path.Combine(_walDirectory, "archive");
                        Directory.CreateDirectory(archiveDir);
                        var archivePath = Path.Combine(archiveDir, Path.GetFileName(walFile) + ".gz");

                        await using var input = File.OpenRead(walFile);
                        await using var output = File.Create(archivePath);
                        await using var gzip = new GZipStream(output, CompressionLevel.Optimal);
                        await input.CopyToAsync(gzip, ct);
                    }

                    File.Delete(walFile);
                    _log.Information("Truncated WAL file {File}", walFile);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task StartNewWalFileAsync(CancellationToken ct)
    {
        var fileName = $"wal_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{_currentSequence:D12}.wal";
        _currentWalPath = Path.Combine(_walDirectory, fileName);

        _currentWalFile = new FileStream(
            _currentWalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.WriteThrough | FileOptions.Asynchronous);

        _currentWriter = new StreamWriter(_currentWalFile, Encoding.UTF8, bufferSize: 32 * 1024);

        // Write header
        await _currentWriter.WriteLineAsync($"{WalMagic}|{WalVersion}|{DateTime.UtcNow:O}");
        await _currentWriter.FlushAsync();

        _currentFileSize = _currentWalFile.Length;
        _log.Debug("Started new WAL file: {File}", _currentWalPath);
    }

    private async Task RotateWalFileAsync(CancellationToken ct)
    {
        if (_currentWriter != null)
        {
            await _currentWriter.FlushAsync();
            await _currentWriter.DisposeAsync();
        }

        if (_currentWalFile != null)
        {
            await _currentWalFile.FlushAsync(ct);
            await _currentWalFile.DisposeAsync();
        }

        await StartNewWalFileAsync(ct);
    }

    private bool ShouldRotate()
    {
        return _currentFileSize >= _options.MaxWalFileSizeBytes ||
               (_options.MaxWalFileAge.HasValue &&
                File.GetCreationTimeUtc(_currentWalPath!) + _options.MaxWalFileAge.Value < DateTime.UtcNow);
    }

    private async Task WriteRecordAsync(WalRecord record, CancellationToken ct)
    {
        if (_currentWriter == null)
        {
            throw new InvalidOperationException("WAL not initialized");
        }

        var line = $"{record.Sequence}|{record.Timestamp:O}|{record.RecordType}|{record.Checksum}|{record.Payload}";
        await _currentWriter.WriteLineAsync(line);

        _currentFileSize += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
    }

    private async Task RecoverWalFileAsync(string walFile, CancellationToken ct)
    {
        _log.Information("Recovering WAL file: {File}", walFile);

        var validRecords = 0;
        var invalidRecords = 0;

        await foreach (var record in ReadWalFileAsync(walFile, ct))
        {
            // Verify checksum
            var expectedChecksum = ComputeChecksum(
                record.Sequence, record.Timestamp, record.RecordType, record.Payload);

            if (record.Checksum == expectedChecksum)
            {
                validRecords++;
            }
            else
            {
                invalidRecords++;
                _log.Warning("Invalid checksum for record {Sequence} in {File}",
                    record.Sequence, walFile);
            }
        }

        _log.Information("Recovered {Valid} valid records, {Invalid} invalid from {File}",
            validRecords, invalidRecords, walFile);
    }

    private async IAsyncEnumerable<WalRecord> ReadWalFileAsync(
        string walFile,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = new FileStream(
            walFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        // Skip header
        var header = await reader.ReadLineAsync();
        if (header == null || !header.StartsWith(WalMagic))
        {
            _log.Warning("Invalid WAL header in {File}", walFile);
            yield break;
        }

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split('|', 5);
            if (parts.Length < 5) continue;

            if (!long.TryParse(parts[0], out var sequence)) continue;
            if (!DateTime.TryParse(parts[1], out var timestamp)) continue;

            yield return new WalRecord
            {
                Sequence = sequence,
                Timestamp = timestamp,
                RecordType = parts[2],
                Checksum = parts[3],
                Payload = parts[4]
            };
        }
    }

    private async Task<long> GetLastSequenceNumberAsync(CancellationToken ct)
    {
        long maxSequence = 0;

        var walFiles = Directory.GetFiles(_walDirectory, "*.wal");
        foreach (var walFile in walFiles)
        {
            await foreach (var record in ReadWalFileAsync(walFile, ct))
            {
                maxSequence = Math.Max(maxSequence, record.Sequence);
            }
        }

        return maxSequence;
    }

    private static string ComputeChecksum(long sequence, DateTime timestamp, string recordType, string payload)
    {
        var data = $"{sequence}|{timestamp:O}|{recordType}|{payload}";
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            if (_currentWriter != null)
            {
                await _currentWriter.FlushAsync();
                await _currentWriter.DisposeAsync();
            }

            if (_currentWalFile != null)
            {
                await _currentWalFile.FlushAsync();
                await _currentWalFile.DisposeAsync();
            }

            _log.Information("WAL disposed, last sequence: {Sequence}", _currentSequence);
        }
        finally
        {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }
}

/// <summary>
/// A record in the Write-Ahead Log.
/// </summary>
public class WalRecord
{
    public long Sequence { get; set; }
    public DateTime Timestamp { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;

    public T? DeserializePayload<T>()
    {
        return JsonSerializer.Deserialize<T>(Payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// WAL configuration options.
/// </summary>
public class WalOptions
{
    /// <summary>
    /// Maximum WAL file size before rotation.
    /// </summary>
    public long MaxWalFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Maximum WAL file age before rotation.
    /// </summary>
    public TimeSpan? MaxWalFileAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Sync mode for durability.
    /// </summary>
    public WalSyncMode SyncMode { get; set; } = WalSyncMode.BatchedSync;

    /// <summary>
    /// Number of records to batch before syncing.
    /// </summary>
    public int SyncBatchSize { get; set; } = 1000;

    /// <summary>
    /// Maximum delay between flushes.
    /// </summary>
    public TimeSpan MaxFlushDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to archive WAL files after truncation.
    /// </summary>
    public bool ArchiveAfterTruncate { get; set; } = true;
}

/// <summary>
/// WAL synchronization modes.
/// </summary>
public enum WalSyncMode
{
    /// <summary>
    /// No explicit sync - relies on OS buffering (fastest, least durable).
    /// </summary>
    NoSync,

    /// <summary>
    /// Sync after batches of writes (balanced).
    /// </summary>
    BatchedSync,

    /// <summary>
    /// Sync after every write (slowest, most durable).
    /// </summary>
    EveryWrite
}
