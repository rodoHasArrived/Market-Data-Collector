using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Serialization;
using MarketDataCollector.Domain.Events;
using Serilog;

namespace MarketDataCollector.Storage.Replay;

/// <summary>
/// Reads previously captured JSONL events (optionally gzip compressed) and replays them as <see cref="MarketEvent"/> objects.
/// Supports optional checksum verification to detect bit rot or corruption (set VerifyChecksums = true).
/// </summary>
public sealed class JsonlReplayer
{
    private static readonly ILogger Log = LoggingSetup.ForContext<JsonlReplayer>();
    private readonly string _root;

    /// <summary>
    /// When true, verifies file integrity by computing SHA256 checksums before reading.
    /// Logs a warning on mismatch rather than failing, so consumers can decide how to handle.
    /// </summary>
    public bool VerifyChecksums { get; init; }

    /// <summary>
    /// Known checksums for verification. Keys are full file paths, values are hex-encoded SHA256.
    /// Populated externally (e.g., from a manifest file or StorageChecksumService).
    /// </summary>
    public IReadOnlyDictionary<string, string>? KnownChecksums { get; init; }

    /// <summary>
    /// Count of files that failed checksum verification during the last read operation.
    /// </summary>
    public int ChecksumMismatchCount { get; private set; }

    public JsonlReplayer(string root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public async IAsyncEnumerable<MarketEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!Directory.Exists(_root)) yield break;

        ChecksumMismatchCount = 0;

        var files = Directory.EnumerateFiles(_root, "*.jsonl*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (VerifyChecksums && KnownChecksums is not null)
            {
                await VerifyFileChecksumAsync(file, ct);
            }

            await foreach (var evt in ReadFileAsync(file, ct))
                yield return evt;
        }
    }

    private async Task VerifyFileChecksumAsync(string filePath, CancellationToken ct)
    {
        if (KnownChecksums is null || !KnownChecksums.TryGetValue(filePath, out var expected))
            return;

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            var hash = await SHA256.HashDataAsync(stream, ct);
            var actual = Convert.ToHexString(hash).ToLowerInvariant();

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                ChecksumMismatchCount++;
                Log.Warning("Checksum mismatch on read for {FilePath}. Expected: {Expected}, Actual: {Actual}. File may be corrupted.",
                    filePath, expected, actual);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to verify checksum for {FilePath}", filePath);
        }
    }

    private static async IAsyncEnumerable<MarketEvent> ReadFileAsync(string file, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var fs = File.OpenRead(file);
        Stream stream = fs;
        if (file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".gzip", StringComparison.OrdinalIgnoreCase))
            stream = new GZipStream(fs, CompressionMode.Decompress);

        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            MarketEvent? evt = null;
            try { evt = JsonSerializer.Deserialize<MarketEvent>(line, MarketDataJsonContext.HighPerformanceOptions); }
            catch (JsonException ex) { Log.Debug(ex, "Failed to parse JSONL line in {File}", file); }

            if (evt is not null)
                yield return evt;
        }
    }
}
