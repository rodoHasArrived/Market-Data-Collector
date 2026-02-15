using System.Collections.Concurrent;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Subscriptions;
using Serilog;

namespace MarketDataCollector.Infrastructure.Shared;

/// <summary>
/// File-based implementation of <see cref="IInstanceCoordinator"/>.
/// Uses a shared claims directory with one JSON file per symbol.
/// Each file contains the owning instance ID and last heartbeat timestamp.
/// Stale claims (no heartbeat within timeout) can be reclaimed by other instances.
/// </summary>
/// <remarks>
/// Designed for deployments where instances share a filesystem (NFS, shared volume).
/// For cloud-native deployments, replace with a Redis or etcd implementation.
/// </remarks>
public sealed class FileBasedInstanceCoordinator : IInstanceCoordinator
{
    private readonly string _claimsDirectory;
    private readonly TimeSpan _heartbeatTimeout;
    private readonly ConcurrentDictionary<string, bool> _ownedSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _log;
    private readonly object _fileLock = new();
    private bool _disposed;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Creates a new file-based instance coordinator.
    /// </summary>
    /// <param name="claimsDirectory">Directory to store claim files. Created if it doesn't exist.</param>
    /// <param name="instanceId">Unique identifier for this instance. Defaults to machine-name + PID.</param>
    /// <param name="heartbeatTimeout">How long before a claim is considered stale. Default: 60 seconds.</param>
    /// <param name="log">Logger instance.</param>
    public FileBasedInstanceCoordinator(
        string claimsDirectory,
        string? instanceId = null,
        TimeSpan? heartbeatTimeout = null,
        ILogger? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimsDirectory);

        _claimsDirectory = claimsDirectory;
        InstanceId = instanceId ?? $"{Environment.MachineName}-{Environment.ProcessId}";
        _heartbeatTimeout = heartbeatTimeout ?? TimeSpan.FromSeconds(60);
        _log = log ?? LoggingSetup.ForContext<FileBasedInstanceCoordinator>();

        Directory.CreateDirectory(_claimsDirectory);

        _log.Information("Instance coordinator initialized: {InstanceId}, claims at {ClaimsDir}, timeout {Timeout}s",
            InstanceId, _claimsDirectory, _heartbeatTimeout.TotalSeconds);
    }

    /// <inheritdoc/>
    public string InstanceId { get; }

    /// <inheritdoc/>
    public Task<bool> TryClaimSymbolAsync(string symbol, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ct.ThrowIfCancellationRequested();

        symbol = symbol.Trim().ToUpperInvariant();
        var claimFile = GetClaimFilePath(symbol);

        lock (_fileLock)
        {
            // Check if a valid claim already exists
            var existingClaim = ReadClaim(claimFile);
            if (existingClaim is not null)
            {
                // Already owned by us
                if (string.Equals(existingClaim.InstanceId, InstanceId, StringComparison.Ordinal))
                {
                    existingClaim.LastHeartbeat = DateTimeOffset.UtcNow;
                    WriteClaim(claimFile, existingClaim);
                    _ownedSymbols[symbol] = true;
                    return Task.FromResult(true);
                }

                // Owned by another instance — check if stale
                if (!IsStale(existingClaim))
                {
                    _log.Debug("Symbol {Symbol} already claimed by instance {OtherInstance}",
                        symbol, existingClaim.InstanceId);
                    return Task.FromResult(false);
                }

                _log.Information("Reclaiming stale symbol {Symbol} from instance {OtherInstance} (last heartbeat: {LastHeartbeat})",
                    symbol, existingClaim.InstanceId, existingClaim.LastHeartbeat);
            }

            // Claim the symbol
            var claim = new SymbolClaim
            {
                Symbol = symbol,
                InstanceId = InstanceId,
                ClaimedAt = DateTimeOffset.UtcNow,
                LastHeartbeat = DateTimeOffset.UtcNow,
            };

            WriteClaim(claimFile, claim);
            _ownedSymbols[symbol] = true;

            _log.Information("Claimed symbol {Symbol} for instance {InstanceId}", symbol, InstanceId);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task ReleaseSymbolAsync(string symbol, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        symbol = symbol.Trim().ToUpperInvariant();
        var claimFile = GetClaimFilePath(symbol);

        lock (_fileLock)
        {
            var existingClaim = ReadClaim(claimFile);
            if (existingClaim is not null &&
                string.Equals(existingClaim.InstanceId, InstanceId, StringComparison.Ordinal))
            {
                try
                {
                    File.Delete(claimFile);
                }
                catch (IOException ex)
                {
                    _log.Warning(ex, "Failed to delete claim file for {Symbol}", symbol);
                }

                _log.Information("Released symbol {Symbol} from instance {InstanceId}", symbol, InstanceId);
            }
        }

        _ownedSymbols.TryRemove(symbol, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RefreshHeartbeatAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        lock (_fileLock)
        {
            foreach (var symbol in _ownedSymbols.Keys)
            {
                var claimFile = GetClaimFilePath(symbol);
                var claim = ReadClaim(claimFile);
                if (claim is not null && string.Equals(claim.InstanceId, InstanceId, StringComparison.Ordinal))
                {
                    claim.LastHeartbeat = DateTimeOffset.UtcNow;
                    WriteClaim(claimFile, claim);
                }
                else
                {
                    // Claim was stolen or removed — update local state
                    _ownedSymbols.TryRemove(symbol, out _);
                    _log.Warning("Lost ownership of symbol {Symbol} during heartbeat refresh", symbol);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetOwnedSymbols()
    {
        return _ownedSymbols.Keys.ToList();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> GetAllClaimsAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        lock (_fileLock)
        {
            if (!Directory.Exists(_claimsDirectory))
                return Task.FromResult<IReadOnlyDictionary<string, string>>(result);

            foreach (var file in Directory.EnumerateFiles(_claimsDirectory, "*.claim.json"))
            {
                var claim = ReadClaim(file);
                if (claim is not null && !IsStale(claim))
                {
                    result[claim.Symbol] = claim.InstanceId;
                }
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
    }

    /// <inheritdoc/>
    public Task<int> ReclaimStaleAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        int reclaimed = 0;

        lock (_fileLock)
        {
            if (!Directory.Exists(_claimsDirectory))
                return Task.FromResult(0);

            foreach (var file in Directory.EnumerateFiles(_claimsDirectory, "*.claim.json"))
            {
                var claim = ReadClaim(file);
                if (claim is not null && IsStale(claim))
                {
                    try
                    {
                        File.Delete(file);
                        reclaimed++;
                        _log.Information("Reclaimed stale claim for {Symbol} from instance {Instance} (last heartbeat: {LastHeartbeat})",
                            claim.Symbol, claim.InstanceId, claim.LastHeartbeat);
                    }
                    catch (IOException ex)
                    {
                        _log.Warning(ex, "Failed to reclaim stale claim file for {Symbol}", claim.Symbol);
                    }
                }
            }
        }

        return Task.FromResult(reclaimed);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Release all owned symbols on shutdown
        foreach (var symbol in _ownedSymbols.Keys.ToList())
        {
            await ReleaseSymbolAsync(symbol).ConfigureAwait(false);
        }

        _log.Information("Instance coordinator disposed: {InstanceId}", InstanceId);
    }

    private string GetClaimFilePath(string symbol)
    {
        // Sanitize symbol for filename (replace special chars)
        var safeSymbol = symbol.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
        return Path.Combine(_claimsDirectory, $"{safeSymbol}.claim.json");
    }

    private SymbolClaim? ReadClaim(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SymbolClaim>(json, s_jsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _log.Debug(ex, "Failed to read claim file {File}", filePath);
            return null;
        }
    }

    private void WriteClaim(string filePath, SymbolClaim claim)
    {
        try
        {
            var json = JsonSerializer.Serialize(claim, s_jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (IOException ex)
        {
            _log.Warning(ex, "Failed to write claim file for {Symbol}", claim.Symbol);
        }
    }

    private bool IsStale(SymbolClaim claim)
    {
        return DateTimeOffset.UtcNow - claim.LastHeartbeat > _heartbeatTimeout;
    }

    /// <summary>
    /// Represents a symbol ownership claim in a shared file.
    /// </summary>
    internal sealed class SymbolClaim
    {
        public string Symbol { get; set; } = string.Empty;
        public string InstanceId { get; set; } = string.Empty;
        public DateTimeOffset ClaimedAt { get; set; }
        public DateTimeOffset LastHeartbeat { get; set; }
    }
}
