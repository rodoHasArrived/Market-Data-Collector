using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using Serilog;

namespace MarketDataCollector.Application.Pipeline;

/// <summary>
/// Persistent deduplication ledger that survives restarts.
/// Uses a JSONL-backed rolling log with in-memory bloom-filter-like cache.
/// Keyed by (provider, symbol, eventIdentity) with configurable TTL.
/// </summary>
public sealed class PersistentDedupLedger : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<PersistentDedupLedger>();
    private readonly string _ledgerPath;
    private readonly TimeSpan _entryTtl;
    private readonly int _maxInMemoryEntries;

    // In-memory cache: composite key → expiry timestamp
    private readonly ConcurrentDictionary<string, long> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private StreamWriter? _writer;
    private long _totalChecked;
    private long _totalDuplicates;

    /// <summary>
    /// Total events checked for duplicates.
    /// </summary>
    public long TotalChecked => Interlocked.Read(ref _totalChecked);

    /// <summary>
    /// Total duplicates detected.
    /// </summary>
    public long TotalDuplicates => Interlocked.Read(ref _totalDuplicates);

    public PersistentDedupLedger(
        string ledgerDirectory,
        TimeSpan? entryTtl = null,
        int maxInMemoryEntries = 500_000)
    {
        _ledgerPath = Path.Combine(ledgerDirectory, "dedup_ledger.jsonl");
        _entryTtl = entryTtl ?? TimeSpan.FromHours(24);
        _maxInMemoryEntries = maxInMemoryEntries;
        Directory.CreateDirectory(ledgerDirectory);
    }

    /// <summary>
    /// Loads persisted dedup state from disk on startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Ticks - _entryTtl.Ticks;

        if (File.Exists(_ledgerPath))
        {
            var loaded = 0;
            var expired = 0;
            try
            {
                using var reader = new StreamReader(_ledgerPath);
                string? line;
                while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var key = root.GetProperty("k").GetString();
                        var ticks = root.GetProperty("t").GetInt64();

                        if (key != null && ticks > cutoff)
                        {
                            _cache[key] = ticks;
                            loaded++;
                        }
                        else
                        {
                            expired++;
                        }
                    }
                    catch
                    {
                        // Skip malformed lines
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error loading dedup ledger from {Path}, starting fresh", _ledgerPath);
            }

            _log.Information("Loaded {LoadedCount} dedup entries from disk ({ExpiredCount} expired)", loaded, expired);
        }

        // Open writer for appending new entries
        var fs = new FileStream(_ledgerPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        _writer = new StreamWriter(fs, Encoding.UTF8, 4096, leaveOpen: false) { AutoFlush = false };
    }

    /// <summary>
    /// Checks whether an event is a duplicate and records it if new.
    /// Returns true if the event is a DUPLICATE (should be skipped).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<bool> IsDuplicateAsync(MarketEvent evt, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _totalChecked);

        var key = ComputeEventKey(evt);
        var nowTicks = DateTimeOffset.UtcNow.Ticks;

        // Check cache first
        if (_cache.TryGetValue(key, out var existingTicks))
        {
            // Entry exists and is not expired
            if (nowTicks - existingTicks < _entryTtl.Ticks)
            {
                Interlocked.Increment(ref _totalDuplicates);
                return true;
            }
        }

        // Not a duplicate — record it
        _cache[key] = nowTicks;

        // Persist to disk (fire-and-forget the write, but serialize access)
        if (_writer != null)
        {
            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync($"{{\"k\":\"{EscapeJson(key)}\",\"t\":{nowTicks}}}").ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // Evict expired entries periodically
        if (_cache.Count > _maxInMemoryEntries)
        {
            EvictExpired(nowTicks);
        }

        return false;
    }

    /// <summary>
    /// Computes a deterministic identity key for an event based on its type.
    /// Uses provider-specific trade IDs when available, otherwise hashes
    /// the semantic identity fields per event type.
    /// </summary>
    private static string ComputeEventKey(MarketEvent evt)
    {
        // Key structure: {Source}:{EffectiveSymbol}:{Type}:{identity}
        // Uses EffectiveSymbol (CanonicalSymbol ?? Symbol) for consistent dedup across symbol mappings
        var prefix = $"{evt.Source}:{evt.EffectiveSymbol}:{evt.Type}:";

        return evt.Payload switch
        {
            Contracts.Domain.Models.Trade trade =>
                prefix + HashIdentity($"{trade.Timestamp.Ticks}|{trade.Price}|{trade.Size}|{trade.Aggressor}|{trade.Venue}"),

            Contracts.Domain.Models.BboQuotePayload quote =>
                // Quotes: identity is timestamp + price levels (don't dedup by content,
                // treat as time series, but filter exact duplicates)
                prefix + HashIdentity($"{quote.Timestamp.Ticks}|{quote.BidPrice}|{quote.AskPrice}|{quote.BidSize}|{quote.AskSize}"),

            Contracts.Domain.Models.LOBSnapshot snap =>
                // L2: use sequence + timestamp
                prefix + $"seq:{snap.SequenceNumber}",

            _ =>
                // Fallback: use sequence number
                prefix + $"seq:{evt.Sequence}"
        };
    }

    private static string HashIdentity(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Use first 16 bytes (128 bits) for compact key
        return Convert.ToHexString(bytes, 0, 16);
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void EvictExpired(long nowTicks)
    {
        var cutoff = nowTicks - _entryTtl.Ticks;
        var evicted = 0;
        foreach (var kvp in _cache)
        {
            if (kvp.Value < cutoff)
            {
                _cache.TryRemove(kvp.Key, out _);
                evicted++;
            }
        }

        if (evicted > 0)
        {
            _log.Debug("Evicted {EvictedCount} expired dedup entries, {RemainingCount} remaining", evicted, _cache.Count);
        }
    }

    /// <summary>
    /// Flushes the ledger to disk.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_writer == null) return;
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Compacts the ledger file by rewriting only non-expired entries.
    /// Call periodically (e.g., daily) to prevent unbounded file growth.
    /// </summary>
    public async Task CompactAsync(CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_writer != null)
            {
                await _writer.FlushAsync(ct).ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
                _writer = null;
            }

            var tempPath = _ledgerPath + ".tmp";
            var nowTicks = DateTimeOffset.UtcNow.Ticks;
            var cutoff = nowTicks - _entryTtl.Ticks;
            var kept = 0;

            await using (var writer = new StreamWriter(tempPath, false, Encoding.UTF8))
            {
                foreach (var (key, ticks) in _cache)
                {
                    if (ticks > cutoff)
                    {
                        await writer.WriteLineAsync($"{{\"k\":\"{EscapeJson(key)}\",\"t\":{ticks}}}").ConfigureAwait(false);
                        kept++;
                    }
                }
            }

            File.Move(tempPath, _ledgerPath, overwrite: true);

            // Reopen writer
            var fs = new FileStream(_ledgerPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            _writer = new StreamWriter(fs, Encoding.UTF8, 4096, leaveOpen: false) { AutoFlush = false };

            _log.Information("Compacted dedup ledger: {KeptCount} entries retained", kept);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writer != null)
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
                _writer = null;
            }
        }
        finally
        {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }
}
