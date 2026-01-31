using MarketDataCollector.Contracts.Domain.Events;

namespace MarketDataCollector.Storage.Services;

/// <summary>
/// Thread-safe generic event buffer to eliminate duplicate buffer implementations
/// across JsonlStorageSink (BatchBuffer) and ParquetStorageSink (ParquetBufferState).
/// Both were implementing identical lock-based buffer patterns.
/// </summary>
/// <typeparam name="T">Type of events to buffer.</typeparam>
public sealed class EventBuffer<T> : IDisposable where T : class
{
    private readonly object _lock = new();
    private readonly List<T> _events;
    private readonly int _capacity;
    private bool _disposed;

    /// <summary>
    /// Creates a new event buffer with optional initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity (default: 1000).</param>
    public EventBuffer(int initialCapacity = 1000)
    {
        _capacity = initialCapacity;
        _events = new List<T>(initialCapacity);
    }

    /// <summary>
    /// Gets the current count of buffered events.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _events.Count;
            }
        }
    }

    /// <summary>
    /// Gets whether the buffer is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Add a single event to the buffer.
    /// </summary>
    public void Add(T evt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));

        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    /// <summary>
    /// Add multiple events to the buffer.
    /// </summary>
    public void AddRange(IEnumerable<T> events)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(events, nameof(events));

        lock (_lock)
        {
            _events.AddRange(events);
        }
    }

    /// <summary>
    /// Drain all events from the buffer and return them.
    /// Buffer is cleared after this call.
    /// </summary>
    public IReadOnlyList<T> DrainAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_events.Count == 0)
                return Array.Empty<T>();

            var result = _events.ToList();
            _events.Clear();
            return result;
        }
    }

    /// <summary>
    /// Drain up to maxCount events from the buffer.
    /// </summary>
    public IReadOnlyList<T> Drain(int maxCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount, nameof(maxCount));

        lock (_lock)
        {
            if (_events.Count == 0)
                return Array.Empty<T>();

            var count = Math.Min(maxCount, _events.Count);
            var result = _events.Take(count).ToList();
            _events.RemoveRange(0, count);
            return result;
        }
    }

    /// <summary>
    /// Peek at all events without removing them.
    /// </summary>
    public IReadOnlyList<T> PeekAll()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }

    /// <summary>
    /// Clear all events from the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }

    /// <summary>
    /// Check if buffer should be flushed based on count threshold.
    /// </summary>
    public bool ShouldFlush(int threshold)
    {
        return Count >= threshold;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _events.Clear();
        }
    }
}

/// <summary>
/// Specialized event buffer for MarketEvent types.
/// </summary>
public sealed class MarketEventBuffer : EventBuffer<MarketEvent>
{
    public MarketEventBuffer(int initialCapacity = 1000) : base(initialCapacity)
    {
    }

    /// <summary>
    /// Drain events filtered by symbol.
    /// </summary>
    public IReadOnlyList<MarketEvent> DrainBySymbol(string symbol)
    {
        var all = DrainAll();
        var matching = all.Where(e => e.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToList();

        // Re-add non-matching events
        var nonMatching = all.Except(matching);
        AddRange(nonMatching);

        return matching;
    }
}
