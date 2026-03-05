using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Serialization;
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
    private DateTime _currentFileCreationTime;
    private int _uncommittedRecords;
    private DateTime _lastFlushTime = DateTime.UtcNow;
    private bool _disposed;
    private long _corruptedRecordCount;
    private long _skippedRecordCount;

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
    /// Gets the number of valid events recovered during the last initialization.
    /// </summary>
    public long LastRecoveryEventCount { get; private set; }

    /// <summary>
    /// Gets the duration of the last recovery in milliseconds.
    /// </summary>
    public double LastRecoveryDurationMs { get; private set; }

    /// <summary>
    /// Gets the total number of corrupted records encountered across all reads and recoveries.
    /// </summary>
    public long CorruptedRecordCount => Interlocked.Read(ref _corruptedRecordCount);

    /// <summary>
    /// Gets the total number of records skipped due to corruption across all reads and recoveries.
    /// </summary>
    public long SkippedRecordCount => Interlocked.Read(ref _skippedRecordCount);

    /// <summary>
    /// Initialize the WAL, recovering any uncommitted transactions.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _log.Information("Initializing WAL in {WalDirectory}", _walDirectory);

        var recoveryStopwatch = System.Diagnostics.Stopwatch.StartNew();
        long totalRecoveredEvents = 0;

        // Find and recover any existing WAL files
        var walFiles = Directory.GetFiles(_walDirectory, "*.wal")
            .OrderBy(f => f)
            .ToList();

        if (walFiles.Count > 0)
        {
            _log.Information("Found {Count} existing WAL files, recovering...", walFiles.Count);
            foreach (var walFile in walFiles)
            {
                totalRecoveredEvents += await RecoverWalFileAsync(walFile, ct);
            }
        }

        recoveryStopwatch.Stop();
        LastRecoveryEventCount = totalRecoveredEvents;
        LastRecoveryDurationMs = recoveryStopwatch.Elapsed.TotalMilliseconds;

        if (totalRecoveredEvents > 0)
        {
            _log.Information(
                "WAL recovery complete: {RecoveredCount} events in {DurationMs}ms",
                totalRecoveredEvents,
                LastRecoveryDurationMs);
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

            // Serialize the data using centralized high-performance options
            var payload = JsonSerializer.Serialize(data, MarketDataJsonContext.HighPerformanceOptions);

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

            // Check if we should flush (use internal method since we already hold _writeLock)
            if (_options.SyncMode == WalSyncMode.EveryWrite ||
                (_options.SyncMode == WalSyncMode.BatchedSync && _uncommittedRecords >= _options.SyncBatchSize) ||
                (DateTime.UtcNow - _lastFlushTime) >= _options.MaxFlushDelay)
            {
                await FlushInternalAsync(ct);
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
            await FlushInternalAsync(ct);

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
        await _writeLock.WaitAsync(ct);
        try
        {
            await FlushInternalAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Internal flush that assumes the caller already holds <see cref="_writeLock"/>.
    /// Called from <see cref="AppendAsync{T}"/> and <see cref="CommitAsync"/> which
    /// acquire the lock before invoking flush, avoiding a deadlock on the non-reentrant
    /// <see cref="SemaphoreSlim"/>.
    /// </summary>
    private async Task FlushInternalAsync(CancellationToken ct = default)
    {
        if (_currentWriter == null || _currentWalFile == null) return;

        await _currentWriter.FlushAsync().ConfigureAwait(false);

        if (_options.SyncMode != WalSyncMode.NoSync)
        {
            // Use flushToDisk: true to ensure data reaches physical disk (fsync).
            // FileOptions.WriteThrough is not reliably honoured on Linux, so an
            // explicit flush-to-disk is required for crash-safe durability.
            _currentWalFile.Flush(flushToDisk: true);
        }

        _uncommittedRecords = 0;
        _lastFlushTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Get uncommitted records for replay/recovery using streaming reads.
    /// Processes records in batches to avoid loading entire WAL into memory.
    /// </summary>
    public async IAsyncEnumerable<WalRecord> GetUncommittedRecordsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // First pass: find the last committed sequence across all WAL files
        long lastCommittedSequence = 0;

        var walFiles = Directory.GetFiles(_walDirectory, "*.wal")
            .OrderBy(f => f)
            .ToList();

        // Warn if total uncommitted WAL size is large
        var totalSize = walFiles.Sum(f => new FileInfo(f).Length);
        if (totalSize > _options.UncommittedSizeWarningThreshold)
        {
            _log.Warning(
                "WAL uncommitted data size is {SizeMB:F1}MB (threshold: {ThresholdMB:F1}MB). Recovery may be slow",
                totalSize / (1024.0 * 1024.0),
                _options.UncommittedSizeWarningThreshold / (1024.0 * 1024.0));
        }

        foreach (var walFile in walFiles)
        {
            await foreach (var record in ReadWalFileAsync(walFile, ct))
            {
                if (record.RecordType == "COMMIT" && long.TryParse(record.Payload, out var seq))
                {
                    lastCommittedSequence = Math.Max(lastCommittedSequence, seq);
                }
            }
        }

        // Second pass: stream uncommitted records without loading all into memory
        const int batchSize = 10_000;
        var batch = new List<WalRecord>(batchSize);

        foreach (var walFile in walFiles)
        {
            await foreach (var record in ReadWalFileAsync(walFile, ct))
            {
                if (record.RecordType == "COMMIT") continue;
                if (record.Sequence <= lastCommittedSequence) continue;

                batch.Add(record);

                if (batch.Count >= batchSize)
                {
                    foreach (var r in batch)
                    {
                        yield return r;
                    }
                    batch.Clear();
                }
            }
        }

        // Yield remaining records in the last batch
        foreach (var r in batch)
        {
            yield return r;
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
        var now = DateTime.UtcNow;
        var fileName = $"wal_{now:yyyyMMdd_HHmmss}_{_currentSequence:D12}.wal";
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
        await _currentWriter.WriteLineAsync($"{WalMagic}|{WalVersion}|{now:O}").ConfigureAwait(false);
        await _currentWriter.FlushAsync().ConfigureAwait(false);

        _currentFileSize = _currentWalFile.Length;
        _currentFileCreationTime = now;
        _log.Debug("Started new WAL file: {File}", _currentWalPath);
    }

    private async Task RotateWalFileAsync(CancellationToken ct)
    {
        if (_currentWriter != null)
        {
            await _currentWriter.FlushAsync().ConfigureAwait(false);
            await _currentWriter.DisposeAsync().ConfigureAwait(false);
            // _currentWriter.DisposeAsync() already closes the underlying _currentWalFile stream
            _currentWriter = null;
            _currentWalFile = null;
        }

        await StartNewWalFileAsync(ct);
    }

    private bool ShouldRotate()
    {
        return _currentFileSize >= _options.MaxWalFileSizeBytes ||
               (_options.MaxWalFileAge.HasValue &&
                _currentFileCreationTime + _options.MaxWalFileAge.Value < DateTime.UtcNow);
    }

    private async Task WriteRecordAsync(WalRecord record, CancellationToken ct)
    {
        if (_currentWriter == null)
        {
            throw new InvalidOperationException("WAL not initialized");
        }

        // Write fields directly to avoid allocating a single large interpolated string.
        // The StreamWriter buffers internally, so multiple Write calls are coalesced.
        var writer = _currentWriter;
        writer.Write(record.Sequence);
        writer.Write('|');
        writer.Write(record.Timestamp.ToString("O"));
        writer.Write('|');
        writer.Write(record.RecordType);
        writer.Write('|');
        writer.Write(record.Checksum);
        writer.Write('|');
        await writer.WriteLineAsync(record.Payload).ConfigureAwait(false);

        // Approximate size tracking — avoids expensive UTF-8 measurement on every write.
        // Payload dominates; the fixed-format prefix is typically ~80 ASCII bytes.
        _currentFileSize += 80 + Encoding.UTF8.GetByteCount(record.Payload) + Environment.NewLine.Length;
    }

    private async Task<long> RecoverWalFileAsync(string walFile, CancellationToken ct)
    {
        _log.Information("Recovering WAL file: {File}", walFile);

        // Capture corruption counter before reading so we can determine
        // how many records were corrupted in this specific file.
        var corruptedBefore = Interlocked.Read(ref _corruptedRecordCount);

        long validRecords = 0;

        await foreach (var record in ReadWalFileAsync(walFile, ct))
        {
            // ReadWalFileAsync already validates checksums and only yields valid records.
            // Corrupted records are logged and counted within ReadWalFileAsync.
            validRecords++;
        }

        var corruptedInFile = Interlocked.Read(ref _corruptedRecordCount) - corruptedBefore;

        if (corruptedInFile > 0)
        {
            _log.Warning(
                "WAL recovery found {CorruptedCount} corrupted records in {File}",
                corruptedInFile, walFile);
        }

        _log.Information(
            "Recovered {ValidCount} valid records, {CorruptedCount} corrupted from {File}",
            validRecords, corruptedInFile, walFile);

        return validRecords;
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
            if (parts.Length < 5)
            {
                _log.Warning(
                    "Malformed WAL record skipped in {File}: expected 5 fields but found {FieldCount}",
                    walFile, parts.Length);
                Interlocked.Increment(ref _corruptedRecordCount);
                Interlocked.Increment(ref _skippedRecordCount);
                continue;
            }

            if (!long.TryParse(parts[0], out var sequence))
            {
                _log.Warning("Malformed WAL record skipped in {File}: unable to parse sequence", walFile);
                Interlocked.Increment(ref _corruptedRecordCount);
                Interlocked.Increment(ref _skippedRecordCount);
                continue;
            }

            if (!DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
            {
                _log.Warning(
                    "Malformed WAL record skipped in {File}: unable to parse timestamp for sequence {Sequence}",
                    walFile, sequence);
                Interlocked.Increment(ref _corruptedRecordCount);
                Interlocked.Increment(ref _skippedRecordCount);
                continue;
            }

            var recordType = parts[2];
            var checksum = parts[3];
            var payload = parts[4];

            // Validate checksum
            var expectedChecksum = ComputeChecksum(sequence, timestamp, recordType, payload);
            if (!string.Equals(checksum, expectedChecksum, StringComparison.Ordinal))
            {
                _log.Warning(
                    "Invalid checksum for WAL record with sequence {Sequence} in {File}, skipping",
                    sequence, walFile);
                Interlocked.Increment(ref _corruptedRecordCount);
                Interlocked.Increment(ref _skippedRecordCount);
                continue;
            }

            yield return new WalRecord
            {
                Sequence = sequence,
                Timestamp = timestamp,
                RecordType = recordType,
                Checksum = checksum,
                Payload = payload
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

    /// <summary>
    /// Computes a SHA-256 checksum for a WAL record using incremental hashing
    /// to avoid allocating a single large concatenated string.
    /// </summary>
    private static string ComputeChecksum(long sequence, DateTime timestamp, string recordType, string payload)
    {
        // Use incremental hash to avoid concatenating a potentially large string
        // (the payload can be several KB of serialized JSON).
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Span<byte> smallBuf = stackalloc byte[128];

        // sequence|
        if (Encoding.UTF8.TryGetBytes(sequence.ToString(), smallBuf, out var written))
        {
            hash.AppendData(smallBuf[..written]);
        }
        else
        {
            hash.AppendData(Encoding.UTF8.GetBytes(sequence.ToString()));
        }
        hash.AppendData(PipeSeparator);

        // timestamp:O|
        var tsStr = timestamp.ToString("O");
        if (Encoding.UTF8.TryGetBytes(tsStr, smallBuf, out written))
        {
            hash.AppendData(smallBuf[..written]);
        }
        else
        {
            hash.AppendData(Encoding.UTF8.GetBytes(tsStr));
        }
        hash.AppendData(PipeSeparator);

        // recordType|
        if (Encoding.UTF8.TryGetBytes(recordType, smallBuf, out written))
        {
            hash.AppendData(smallBuf[..written]);
        }
        else
        {
            hash.AppendData(Encoding.UTF8.GetBytes(recordType));
        }
        hash.AppendData(PipeSeparator);

        // payload (may be large — rent from pool when it doesn't fit on the stack)
        var payloadByteCount = Encoding.UTF8.GetByteCount(payload);
        if (payloadByteCount <= 1024)
        {
            Span<byte> payloadBuf = stackalloc byte[payloadByteCount];
            Encoding.UTF8.GetBytes(payload, payloadBuf);
            hash.AppendData(payloadBuf);
        }
        else
        {
            var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(payloadByteCount);
            try
            {
                var count = Encoding.UTF8.GetBytes(payload, 0, payload.Length, rented, 0);
                hash.AppendData(rented.AsSpan(0, count));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }

        Span<byte> hashBytes = stackalloc byte[32]; // SHA-256 = 32 bytes
        hash.GetHashAndReset(hashBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static ReadOnlySpan<byte> PipeSeparator => "|"u8;

    /// <summary>
    /// Repairs all WAL files by scanning every record, validating checksums,
    /// and rewriting only valid records into new WAL files.
    /// Corrupted records are discarded and counted in the result.
    /// </summary>
    public async Task<WalRepairResult> RepairAsync(CancellationToken ct = default)
    {
        _log.Information("Starting WAL repair in {WalDirectory}", _walDirectory);

        var walFiles = Directory.GetFiles(_walDirectory, "*.wal")
            .OrderBy(f => f)
            .ToList();

        int totalRecords = 0;
        int validRecords = 0;
        int corruptedRecords = 0;
        int repairedFiles = 0;

        foreach (var walFile in walFiles)
        {
            ct.ThrowIfCancellationRequested();

            // Skip the currently active WAL file
            if (string.Equals(walFile, _currentWalPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileValidRecords = new List<WalRecord>();
            int fileCorruptedCount = 0;

            // Read the raw file directly to count both valid and corrupted records,
            // rather than going through ReadWalFileAsync which filters corrupted ones out.
            await using var stream = new FileStream(
                walFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            // Read and validate header
            var header = await reader.ReadLineAsync();
            if (header == null || !header.StartsWith(WalMagic))
            {
                _log.Warning("Skipping WAL file with invalid header during repair: {File}", walFile);
                continue;
            }

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                totalRecords++;

                var parts = line.Split('|', 5);
                if (parts.Length < 5 ||
                    !long.TryParse(parts[0], out var sequence) ||
                    !DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    fileCorruptedCount++;
                    corruptedRecords++;
                    continue;
                }

                var recordType = parts[2];
                var checksum = parts[3];
                var payload = parts[4];

                var expectedChecksum = ComputeChecksum(sequence, timestamp, recordType, payload);
                if (!string.Equals(checksum, expectedChecksum, StringComparison.Ordinal))
                {
                    _log.Warning(
                        "Repair: corrupted record with sequence {Sequence} in {File}",
                        sequence, walFile);
                    fileCorruptedCount++;
                    corruptedRecords++;
                    continue;
                }

                validRecords++;
                fileValidRecords.Add(new WalRecord
                {
                    Sequence = sequence,
                    Timestamp = timestamp,
                    RecordType = recordType,
                    Checksum = checksum,
                    Payload = payload
                });
            }

            // Only rewrite the file if corruption was found
            if (fileCorruptedCount > 0)
            {
                var tempPath = walFile + ".repair";

                await using var outStream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 64 * 1024, FileOptions.WriteThrough | FileOptions.Asynchronous);
                await using var writer = new StreamWriter(outStream, Encoding.UTF8, bufferSize: 32 * 1024);

                // Write header
                await writer.WriteLineAsync($"{WalMagic}|{WalVersion}|{DateTime.UtcNow:O}");

                // Write valid records
                foreach (var record in fileValidRecords)
                {
                    ct.ThrowIfCancellationRequested();
                    var recordLine = $"{record.Sequence}|{record.Timestamp:O}|{record.RecordType}|{record.Checksum}|{record.Payload}";
                    await writer.WriteLineAsync(recordLine);
                }

                await writer.FlushAsync();

                // Replace original with repaired file
                File.Delete(walFile);
                File.Move(tempPath, walFile);

                repairedFiles++;

                _log.Information(
                    "Repaired WAL file {File}: kept {ValidCount} records, removed {CorruptedCount} corrupted",
                    walFile, fileValidRecords.Count, fileCorruptedCount);
            }
        }

        var result = new WalRepairResult(totalRecords, validRecords, corruptedRecords, repairedFiles);

        _log.Information(
            "WAL repair complete: {TotalRecords} total, {ValidRecords} valid, {CorruptedRecords} corrupted, {RepairedFiles} files repaired",
            result.TotalRecords, result.ValidRecords, result.CorruptedRecords, result.RepairedFiles);

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _writeLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_currentWriter != null)
            {
                await _currentWriter.FlushAsync();
                await _currentWriter.DisposeAsync();
                // _currentWriter.DisposeAsync() already closes the underlying _currentWalFile stream
                // so we should not attempt to flush or dispose it again
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
public sealed class WalRecord
{
    public long Sequence { get; set; }
    public DateTime Timestamp { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;

    public T? DeserializePayload<T>()
    {
        return JsonSerializer.Deserialize<T>(Payload, MarketDataJsonContext.HighPerformanceOptions);
    }
}

/// <summary>
/// WAL configuration options.
/// </summary>
public sealed class WalOptions
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

    /// <summary>
    /// Size threshold (bytes) at which a warning is logged about uncommitted WAL data.
    /// Default is 50MB.
    /// </summary>
    public long UncommittedSizeWarningThreshold { get; set; } = 50 * 1024 * 1024;
}

/// <summary>
/// WAL synchronization modes.
/// </summary>
public enum WalSyncMode : byte
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

/// <summary>
/// Result of a WAL repair operation.
/// </summary>
public sealed record WalRepairResult(
    int TotalRecords,
    int ValidRecords,
    int CorruptedRecords,
    int RepairedFiles);
