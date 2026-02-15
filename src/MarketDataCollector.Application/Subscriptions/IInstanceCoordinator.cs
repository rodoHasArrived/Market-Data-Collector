namespace MarketDataCollector.Application.Subscriptions;

/// <summary>
/// Coordinates symbol ownership across multiple collector instances.
/// Prevents duplicate subscriptions when running in multi-instance mode.
/// </summary>
/// <remarks>
/// In single-instance mode, a no-op implementation can be used.
/// In multi-instance mode, implementations may use file locks, distributed locks (Redis/etcd),
/// or other mechanisms to ensure each symbol is owned by exactly one instance.
/// </remarks>
public interface IInstanceCoordinator : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier for this collector instance.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Attempt to claim ownership of a symbol for this instance.
    /// If another instance already owns it, returns false.
    /// </summary>
    /// <param name="symbol">The symbol to claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if this instance now owns the symbol.</returns>
    Task<bool> TryClaimSymbolAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Release ownership of a symbol so other instances can claim it.
    /// </summary>
    /// <param name="symbol">The symbol to release.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ReleaseSymbolAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Refresh the heartbeat for all symbols owned by this instance.
    /// Must be called periodically to prevent other instances from reclaiming stale symbols.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task RefreshHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all symbols currently owned by this instance.
    /// </summary>
    IReadOnlyCollection<string> GetOwnedSymbols();

    /// <summary>
    /// Get all symbols currently claimed across all instances (for monitoring).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Map of symbol to owning instance ID.</returns>
    Task<IReadOnlyDictionary<string, string>> GetAllClaimsAsync(CancellationToken ct = default);

    /// <summary>
    /// Reclaim symbols from instances that have not refreshed their heartbeat
    /// within the configured timeout.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of stale claims reclaimed.</returns>
    Task<int> ReclaimStaleAsync(CancellationToken ct = default);
}
