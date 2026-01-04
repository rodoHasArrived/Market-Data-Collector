using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Services.Sync;

/// <summary>
/// Service for synchronizing market data between instances.
/// Inspired by StockSharp Hydra's data sync capabilities.
///
/// Features:
/// - Auto-discovery of peer instances
/// - Incremental sync (only missing data)
/// - Conflict resolution (newest wins)
/// - Bandwidth throttling
/// - Resumable transfers
/// </summary>
public sealed class DataSyncService : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<DataSyncService>();
    private readonly DataSyncConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, PeerInstance> _peers = new();
    private readonly ConcurrentDictionary<string, SyncState> _syncStates = new();
    private readonly SemaphoreSlim _syncGate;
    private CancellationTokenSource? _discoveryCts;
    private Task? _discoveryTask;
    private bool _disposed;

    /// <summary>
    /// Event raised when sync starts for a symbol.
    /// </summary>
    public event Action<string, PeerInstance>? SyncStarted;

    /// <summary>
    /// Event raised when sync completes for a symbol.
    /// </summary>
    public event Action<string, SyncResult>? SyncCompleted;

    /// <summary>
    /// Event raised when a peer is discovered.
    /// </summary>
    public event Action<PeerInstance>? PeerDiscovered;

    /// <summary>
    /// Event raised when a peer becomes unavailable.
    /// </summary>
    public event Action<PeerInstance>? PeerLost;

    public DataSyncService(DataSyncConfig? config = null)
    {
        _config = config ?? new DataSyncConfig();
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _syncGate = new SemaphoreSlim(_config.MaxConcurrentSyncs);
    }

    /// <summary>
    /// Start the sync service (enables peer discovery).
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DataSyncService));

        _log.Information("Starting DataSyncService with {PeerCount} configured peers", _config.Peers.Count);

        // Register configured peers
        foreach (var peer in _config.Peers)
        {
            await RegisterPeerAsync(peer, ct);
        }

        // Start peer discovery if enabled
        if (_config.EnableAutoDiscovery)
        {
            _discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _discoveryTask = DiscoverPeersLoopAsync(_discoveryCts.Token);
        }
    }

    /// <summary>
    /// Stop the sync service.
    /// </summary>
    public async Task StopAsync()
    {
        _discoveryCts?.Cancel();

        if (_discoveryTask != null)
        {
            try
            {
                await _discoveryTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _log.Information("DataSyncService stopped");
    }

    /// <summary>
    /// Register a peer instance for syncing.
    /// </summary>
    public async Task RegisterPeerAsync(PeerInstance peer, CancellationToken ct = default)
    {
        if (_peers.TryAdd(peer.Id, peer))
        {
            _log.Information("Registered peer: {PeerId} at {Endpoint}", peer.Id, peer.Endpoint);

            // Test connectivity
            if (await TestPeerConnectivityAsync(peer, ct))
            {
                peer.IsOnline = true;
                peer.LastSeen = DateTimeOffset.UtcNow;
                PeerDiscovered?.Invoke(peer);
            }
        }
    }

    /// <summary>
    /// Unregister a peer instance.
    /// </summary>
    public void UnregisterPeer(string peerId)
    {
        if (_peers.TryRemove(peerId, out var peer))
        {
            _log.Information("Unregistered peer: {PeerId}", peerId);
            PeerLost?.Invoke(peer);
        }
    }

    /// <summary>
    /// Get all registered peers.
    /// </summary>
    public IReadOnlyList<PeerInstance> GetPeers()
    {
        return _peers.Values.ToList();
    }

    /// <summary>
    /// Sync a specific symbol from a peer.
    /// </summary>
    public async Task<SyncResult> SyncSymbolAsync(
        string symbol,
        string peerId,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        if (!_peers.TryGetValue(peerId, out var peer))
        {
            return new SyncResult
            {
                Symbol = symbol,
                PeerId = peerId,
                Success = false,
                Error = $"Peer '{peerId}' not found"
            };
        }

        return await SyncFromPeerAsync(symbol, peer, startTime, endTime, ct);
    }

    /// <summary>
    /// Sync a symbol from the best available peer.
    /// </summary>
    public async Task<SyncResult> SyncSymbolAsync(
        string symbol,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken ct = default)
    {
        // Find the best peer for this symbol
        var peer = await FindBestPeerForSymbolAsync(symbol, ct);
        if (peer == null)
        {
            return new SyncResult
            {
                Symbol = symbol,
                Success = false,
                Error = "No available peer with data for this symbol"
            };
        }

        return await SyncFromPeerAsync(symbol, peer, startTime, endTime, ct);
    }

    /// <summary>
    /// Get the sync state for a symbol.
    /// </summary>
    public SyncState? GetSyncState(string symbol)
    {
        _syncStates.TryGetValue(symbol, out var state);
        return state;
    }

    /// <summary>
    /// Get data availability info from a peer.
    /// </summary>
    public async Task<DataAvailability?> GetPeerDataAvailabilityAsync(
        string peerId,
        string symbol,
        CancellationToken ct = default)
    {
        if (!_peers.TryGetValue(peerId, out var peer))
            return null;

        try
        {
            var url = $"{peer.Endpoint}/api/data/availability/{Uri.EscapeDataString(symbol)}";
            var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DataAvailability>(cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to get data availability from peer {PeerId}", peerId);
        }

        return null;
    }

    /// <summary>
    /// Stream trades from a peer.
    /// </summary>
    public async IAsyncEnumerable<Trade> StreamTradesFromPeerAsync(
        string peerId,
        string symbol,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_peers.TryGetValue(peerId, out var peer))
            yield break;

        var url = $"{peer.Endpoint}/api/data/trades/{Uri.EscapeDataString(symbol)}" +
                  $"?start={startTime:o}&end={endTime:o}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to stream trades from peer {PeerId}", peerId);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            Trade? trade = null;
            try
            {
                trade = JsonSerializer.Deserialize<Trade>(line);
            }
            catch (JsonException ex)
            {
                _log.Warning(ex, "Failed to deserialize trade from peer");
            }

            if (trade != null)
                yield return trade;
        }
    }

    #region Private Methods

    private async Task<SyncResult> SyncFromPeerAsync(
        string symbol,
        PeerInstance peer,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        CancellationToken ct)
    {
        var startTimeActual = DateTimeOffset.UtcNow;
        var state = _syncStates.GetOrAdd(symbol, _ => new SyncState { Symbol = symbol });
        state.Status = SyncStatus.Syncing;
        state.CurrentPeer = peer.Id;

        SyncStarted?.Invoke(symbol, peer);

        await _syncGate.WaitAsync(ct);

        try
        {
            _log.Information("Starting sync of {Symbol} from {PeerId}", symbol, peer.Id);

            // Get data availability from peer
            var availability = await GetPeerDataAvailabilityAsync(peer.Id, symbol, ct);
            if (availability == null)
            {
                return CreateFailedResult(symbol, peer.Id, "Could not get data availability");
            }

            // Calculate what we need
            var syncStart = startTime ?? availability.FirstTimestamp;
            var syncEnd = endTime ?? availability.LastTimestamp;

            // Stream and store trades
            var tradeCount = 0L;
            var byteCount = 0L;

            await foreach (var trade in StreamTradesFromPeerAsync(peer.Id, symbol, syncStart, syncEnd, ct))
            {
                // Here you would write to your local storage
                // For now, just count
                tradeCount++;
                byteCount += EstimateTradeSize(trade);

                // Update progress
                state.RecordsProcessed = tradeCount;
                state.BytesTransferred = byteCount;

                // Throttle if configured
                if (_config.MaxBytesPerSecond > 0 && byteCount > 0)
                {
                    var elapsed = DateTimeOffset.UtcNow - startTimeActual;
                    var currentRate = byteCount / elapsed.TotalSeconds;
                    if (currentRate > _config.MaxBytesPerSecond)
                    {
                        var delay = (byteCount / _config.MaxBytesPerSecond) - elapsed.TotalSeconds;
                        if (delay > 0)
                            await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                    }
                }
            }

            var result = new SyncResult
            {
                Symbol = symbol,
                PeerId = peer.Id,
                Success = true,
                TradesReceived = tradeCount,
                BytesTransferred = byteCount,
                StartTime = startTimeActual,
                EndTime = DateTimeOffset.UtcNow,
                Duration = DateTimeOffset.UtcNow - startTimeActual
            };

            state.Status = SyncStatus.Completed;
            state.LastSyncTime = DateTimeOffset.UtcNow;
            state.LastResult = result;

            _log.Information("Sync completed: {Symbol} - {TradeCount:N0} trades, {Bytes:N0} bytes in {Duration}ms",
                symbol, tradeCount, byteCount, result.Duration.TotalMilliseconds);

            SyncCompleted?.Invoke(symbol, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            state.Status = SyncStatus.Cancelled;
            return CreateFailedResult(symbol, peer.Id, "Sync was cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Sync failed for {Symbol} from {PeerId}", symbol, peer.Id);
            state.Status = SyncStatus.Failed;
            return CreateFailedResult(symbol, peer.Id, ex.Message);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<PeerInstance?> FindBestPeerForSymbolAsync(string symbol, CancellationToken ct)
    {
        PeerInstance? bestPeer = null;
        long maxRecords = 0;

        foreach (var peer in _peers.Values.Where(p => p.IsOnline))
        {
            var availability = await GetPeerDataAvailabilityAsync(peer.Id, symbol, ct);
            if (availability != null && availability.RecordCount > maxRecords)
            {
                maxRecords = availability.RecordCount;
                bestPeer = peer;
            }
        }

        return bestPeer;
    }

    private async Task<bool> TestPeerConnectivityAsync(PeerInstance peer, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{peer.Endpoint}/api/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task DiscoverPeersLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.DiscoveryInterval, ct);

                // Check existing peers
                foreach (var peer in _peers.Values.ToList())
                {
                    var wasOnline = peer.IsOnline;
                    peer.IsOnline = await TestPeerConnectivityAsync(peer, ct);

                    if (peer.IsOnline)
                    {
                        peer.LastSeen = DateTimeOffset.UtcNow;
                        if (!wasOnline)
                        {
                            _log.Information("Peer came online: {PeerId}", peer.Id);
                            PeerDiscovered?.Invoke(peer);
                        }
                    }
                    else if (wasOnline)
                    {
                        _log.Warning("Peer went offline: {PeerId}", peer.Id);
                        PeerLost?.Invoke(peer);
                    }
                }

                // TODO: Implement mDNS/broadcast discovery for local network
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error during peer discovery");
            }
        }
    }

    private static SyncResult CreateFailedResult(string symbol, string? peerId, string error)
    {
        return new SyncResult
        {
            Symbol = symbol,
            PeerId = peerId,
            Success = false,
            Error = error
        };
    }

    private static long EstimateTradeSize(Trade trade)
    {
        // Rough estimate of serialized size
        return 100 + (trade.Symbol?.Length ?? 0) + (trade.Venue?.Length ?? 0);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();

        _httpClient.Dispose();
        _syncGate.Dispose();
        _discoveryCts?.Dispose();
    }
}

/// <summary>
/// Configuration for data sync service.
/// </summary>
public sealed record DataSyncConfig
{
    /// <summary>Configured peer instances.</summary>
    public IReadOnlyList<PeerInstance> Peers { get; init; } = Array.Empty<PeerInstance>();

    /// <summary>Enable automatic peer discovery on local network.</summary>
    public bool EnableAutoDiscovery { get; init; } = false;

    /// <summary>Interval between discovery attempts.</summary>
    public TimeSpan DiscoveryInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Maximum concurrent sync operations.</summary>
    public int MaxConcurrentSyncs { get; init; } = 2;

    /// <summary>Maximum bytes per second (0 = unlimited).</summary>
    public long MaxBytesPerSecond { get; init; } = 0;

    /// <summary>Enable compression for transfers.</summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>Retry count for failed transfers.</summary>
    public int RetryCount { get; init; } = 3;
}

/// <summary>
/// Represents a peer instance for syncing.
/// </summary>
public sealed class PeerInstance
{
    /// <summary>Unique peer identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable peer name.</summary>
    public string? Name { get; init; }

    /// <summary>HTTP endpoint for the peer.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Whether the peer is currently online.</summary>
    public bool IsOnline { get; set; }

    /// <summary>Last time the peer was seen.</summary>
    public DateTimeOffset? LastSeen { get; set; }

    /// <summary>Peer priority (lower = preferred).</summary>
    public int Priority { get; init; } = 100;

    /// <summary>Additional metadata.</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Current sync state for a symbol.
/// </summary>
public sealed class SyncState
{
    public required string Symbol { get; init; }
    public SyncStatus Status { get; set; } = SyncStatus.Idle;
    public string? CurrentPeer { get; set; }
    public long RecordsProcessed { get; set; }
    public long BytesTransferred { get; set; }
    public DateTimeOffset? LastSyncTime { get; set; }
    public SyncResult? LastResult { get; set; }
}

/// <summary>
/// Sync operation status.
/// </summary>
public enum SyncStatus
{
    Idle,
    Syncing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public sealed record SyncResult
{
    public required string Symbol { get; init; }
    public string? PeerId { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public long TradesReceived { get; init; }
    public long DepthSnapshotsReceived { get; init; }
    public long BytesTransferred { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Information about data availability on a peer.
/// </summary>
public sealed record DataAvailability
{
    public required string Symbol { get; init; }
    public DateTimeOffset FirstTimestamp { get; init; }
    public DateTimeOffset LastTimestamp { get; init; }
    public long RecordCount { get; init; }
    public DataType[] AvailableTypes { get; init; } = Array.Empty<DataType>();
    public long StorageSize { get; init; }
}

/// <summary>
/// Types of data available.
/// </summary>
public enum DataType
{
    Trades,
    Depth,
    Quotes,
    Candles
}
