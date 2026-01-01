using System.Runtime.CompilerServices;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Performance;

namespace MarketDataCollector.Domain.Collectors;

/// <summary>
/// High-performance market depth collector using lock-free order books with immutable snapshots.
/// Provides wait-free reads for external consumers while maintaining consistent write performance.
/// </summary>
/// <remarks>
/// Key optimizations:
/// - Lock-free order book updates via atomic snapshot swapping
/// - Immutable snapshots allow safe concurrent reads without synchronization
/// - Inlined hot-path methods for reduced function call overhead
/// - Branch-prediction-friendly dispatch via delegate table
/// </remarks>
public sealed class HighPerformanceMarketDepthCollector : SymbolSubscriptionTracker
{
    private readonly IMarketEventPublisher _publisher;
    private readonly LockFreeOrderBookCollection _books = new();

    // Branch-prediction-friendly dispatch table for operations
    private static readonly Action<LockFreeOrderBook, MarketDepthUpdate, HighPerformanceMarketDepthCollector>[] OperationHandlers =
    {
        HandleInsert,  // DepthOperation.Insert = 0
        HandleUpdate,  // DepthOperation.Update = 1
        HandleDelete   // DepthOperation.Delete = 2
    };

    public HighPerformanceMarketDepthCollector(IMarketEventPublisher publisher, bool requireExplicitSubscription = true)
        : base(requireExplicitSubscription)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    #region Stream Management

    public void ResetSymbolStream(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        if (_books.TryGet(symbol.Trim(), out var book) && book is not null)
            book.Reset();
    }

    public bool IsSymbolStreamStale(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return false;
        return _books.TryGet(symbol.Trim(), out var book) && book is not null && book.IsStale;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current order book snapshot for a symbol (wait-free read).
    /// Returns null if the symbol has no order book.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderBookSnapshot? GetSnapshot(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;
        return _books.TryGet(symbol.Trim(), out var book) && book is not null ? book.GetSnapshot() : null;
    }

    /// <summary>
    /// Gets all tracked symbols.
    /// </summary>
    public IReadOnlyCollection<string> GetTrackedSymbols() => _books.GetSymbols();

    #endregion

    #region Core Processing

    /// <summary>
    /// Apply a single depth delta update using lock-free operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnDepth(MarketDepthUpdate update)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (string.IsNullOrWhiteSpace(update.Symbol)) return;

        var symbol = update.Symbol.Trim();

        if (!ShouldProcessUpdate(symbol))
            return;

        var book = _books.GetOrCreate(symbol);

        // Use dispatch table for branch-prediction-friendly operation handling
        var opIndex = (int)update.Operation;
        if (opIndex >= 0 && opIndex < OperationHandlers.Length)
        {
            OperationHandlers[opIndex](book, update, this);
        }
        else
        {
            // Unknown operation
            HandleUnknownOperation(book, update);
        }
    }

    /// <summary>
    /// Batch process multiple depth updates efficiently.
    /// </summary>
    public void OnDepthBatch(ReadOnlySpan<MarketDepthUpdate> updates)
    {
        foreach (var update in updates)
        {
            OnDepth(update);
        }
    }

    #endregion

    #region Operation Handlers (Static for branch prediction)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandleInsert(LockFreeOrderBook book, MarketDepthUpdate update, HighPerformanceMarketDepthCollector collector)
    {
        ProcessUpdate(book, update, collector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandleUpdate(LockFreeOrderBook book, MarketDepthUpdate update, HighPerformanceMarketDepthCollector collector)
    {
        ProcessUpdate(book, update, collector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandleDelete(LockFreeOrderBook book, MarketDepthUpdate update, HighPerformanceMarketDepthCollector collector)
    {
        ProcessUpdate(book, update, collector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessUpdate(LockFreeOrderBook book, MarketDepthUpdate update, HighPerformanceMarketDepthCollector collector)
    {
        var result = book.ApplyUpdate(update);

        if (!result.Success)
        {
            // Emit integrity event
            var integrityEvent = new DepthIntegrityEvent(
                Timestamp: update.Timestamp,
                Symbol: update.Symbol,
                Kind: result.IntegrityKind,
                Description: result.ErrorDescription ?? $"Depth integrity: {result.IntegrityKind}",
                Position: update.Position,
                Operation: update.Operation,
                Side: update.Side,
                SequenceNumber: update.SequenceNumber,
                StreamId: update.StreamId,
                Venue: update.Venue
            );

            collector._publisher.TryPublish(MarketEvent.DepthIntegrity(update.Timestamp, update.Symbol, integrityEvent));
            return;
        }

        // Emit snapshot
        if (result.NewSnapshot is not null)
        {
            var lobSnapshot = result.NewSnapshot.ToLOBSnapshot(update.StreamId, update.Venue);
            collector._publisher.TryPublish(MarketEvent.L2Snapshot(update.Timestamp, update.Symbol, lobSnapshot));
        }
    }

    private void HandleUnknownOperation(LockFreeOrderBook book, MarketDepthUpdate update)
    {
        book.MarkStale($"Unknown depth operation: {update.Operation}");

        var integrityEvent = new DepthIntegrityEvent(
            Timestamp: update.Timestamp,
            Symbol: update.Symbol,
            Kind: DepthIntegrityKind.Unknown,
            Description: $"Unknown depth operation: {update.Operation}",
            Position: update.Position,
            Operation: update.Operation,
            Side: update.Side,
            SequenceNumber: update.SequenceNumber,
            StreamId: update.StreamId,
            Venue: update.Venue
        );

        _publisher.TryPublish(MarketEvent.DepthIntegrity(update.Timestamp, update.Symbol, integrityEvent));
    }

    #endregion
}
