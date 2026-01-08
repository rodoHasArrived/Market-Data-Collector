using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace MarketDataCollector.Infrastructure.Performance;

/// <summary>
/// High-performance object pool for reducing GC pressure in hot paths.
/// Uses a lock-free concurrent bag with configurable capacity.
/// </summary>
/// <typeparam name="T">Type of object to pool</typeparam>
public sealed class ObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _reset;
    private readonly int _maxCapacity;
    private long _currentCount;

    public ObjectPool(Func<T> factory, Action<T>? reset = null, int maxCapacity = 256)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _reset = reset;
        _maxCapacity = maxCapacity;
    }

    /// <summary>
    /// Gets an object from the pool or creates a new one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            Interlocked.Decrement(ref _currentCount);
            return item;
        }
        return _factory();
    }

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item is null) return;

        var count = Interlocked.Read(ref _currentCount);
        if (count >= _maxCapacity)
        {
            // Pool is full, let GC collect the object
            return;
        }

        _reset?.Invoke(item);
        _pool.Add(item);
        Interlocked.Increment(ref _currentCount);
    }

    /// <summary>
    /// Gets the current number of items in the pool.
    /// </summary>
    public int Count => (int)Interlocked.Read(ref _currentCount);
}

/// <summary>
/// Provides pooled StringBuilder instances for efficient string building.
/// </summary>
public static class StringBuilderPool
{
    private static readonly ObjectPool<StringBuilder> Pool = new(
        factory: () => new StringBuilder(1024),
        reset: sb => sb.Clear(),
        maxCapacity: 64);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Rent() => Pool.Rent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(StringBuilder sb) => Pool.Return(sb);

    /// <summary>
    /// Rents a StringBuilder, executes the action, and returns it.
    /// </summary>
    public static string Build(Action<StringBuilder> action)
    {
        var sb = Rent();
        try
        {
            action(sb);
            return sb.ToString();
        }
        finally
        {
            Return(sb);
        }
    }
}

/// <summary>
/// Provides pooled byte arrays for efficient buffer management.
/// Uses ArrayPool for optimal memory reuse.
/// </summary>
public static class BufferPool
{
    /// <summary>
    /// Rents a byte array of at least the specified size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] RentBytes(int minimumLength)
        => ArrayPool<byte>.Shared.Rent(minimumLength);

    /// <summary>
    /// Returns a byte array to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnBytes(byte[] array, bool clearArray = false)
        => ArrayPool<byte>.Shared.Return(array, clearArray);

    /// <summary>
    /// Rents a char array of at least the specified size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char[] RentChars(int minimumLength)
        => ArrayPool<char>.Shared.Rent(minimumLength);

    /// <summary>
    /// Returns a char array to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReturnChars(char[] array, bool clearArray = false)
        => ArrayPool<char>.Shared.Return(array, clearArray);
}

/// <summary>
/// A pooled memory stream that returns its buffer to the pool on disposal.
/// </summary>
public sealed class PooledMemoryStream : MemoryStream
{
    private byte[]? _rentedBuffer;
    private bool _disposed;

    public PooledMemoryStream(int initialCapacity = 4096) : base(BufferPool.RentBytes(initialCapacity))
    {
        _rentedBuffer = (byte[])GetBuffer();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && _rentedBuffer is not null)
        {
            BufferPool.ReturnBytes(_rentedBuffer);
            _rentedBuffer = null;
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Manages pooled list instances for reducing allocations.
/// </summary>
/// <typeparam name="T">Element type</typeparam>
public sealed class ListPool<T>
{
    private readonly ObjectPool<List<T>> _pool;
    private readonly int _defaultCapacity;

    public ListPool(int defaultCapacity = 16, int maxPoolSize = 64)
    {
        _defaultCapacity = defaultCapacity;
        _pool = new ObjectPool<List<T>>(
            factory: () => new List<T>(_defaultCapacity),
            reset: list => list.Clear(),
            maxCapacity: maxPoolSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<T> Rent() => _pool.Rent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(List<T> list) => _pool.Return(list);
}

/// <summary>
/// Thread-local cache for frequently allocated objects.
/// Avoids contention by using per-thread caches.
/// </summary>
/// <typeparam name="T">Type of object to cache</typeparam>
public sealed class ThreadLocalCache<T> where T : class
{
    private readonly ThreadLocal<T?> _cache;
    private readonly Func<T> _factory;
    private readonly Action<T>? _reset;

    public ThreadLocalCache(Func<T> factory, Action<T>? reset = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _reset = reset;
        _cache = new ThreadLocal<T?>(() => null);
    }

    /// <summary>
    /// Gets an item from the thread-local cache or creates a new one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetOrCreate()
    {
        var item = _cache.Value;
        if (item is not null)
        {
            _cache.Value = null;
            return item;
        }
        return _factory();
    }

    /// <summary>
    /// Returns an item to the thread-local cache.
    /// Only one item per thread is cached; excess items are discarded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item is null) return;
        _reset?.Invoke(item);
        _cache.Value = item;
    }
}

/// <summary>
/// Pre-sized ring buffer for fixed-capacity collections with O(1) operations.
/// </summary>
/// <typeparam name="T">Element type</typeparam>
public sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly int _mask;
    private long _writePos;
    private long _readPos;

    public RingBuffer(int capacity)
    {
        // Round up to power of 2 for efficient masking
        var size = 1;
        while (size < capacity) size <<= 1;

        _buffer = new T[size];
        _mask = size - 1;
    }

    public int Capacity => _buffer.Length;

    public int Count
    {
        get
        {
            var write = Volatile.Read(ref _writePos);
            var read = Volatile.Read(ref _readPos);
            return (int)(write - read);
        }
    }

    public bool IsFull => Count >= Capacity;
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Attempts to write an item to the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(T item)
    {
        var write = Volatile.Read(ref _writePos);
        var read = Volatile.Read(ref _readPos);

        if (write - read >= Capacity)
            return false;

        _buffer[write & _mask] = item;
        Volatile.Write(ref _writePos, write + 1);
        return true;
    }

    /// <summary>
    /// Attempts to read an item from the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out T item)
    {
        var read = Volatile.Read(ref _readPos);
        var write = Volatile.Read(ref _writePos);

        if (read >= write)
        {
            item = default!;
            return false;
        }

        item = _buffer[read & _mask];
        Volatile.Write(ref _readPos, read + 1);
        return true;
    }

    /// <summary>
    /// Clears all items from the buffer.
    /// </summary>
    public void Clear()
    {
        Volatile.Write(ref _readPos, Volatile.Read(ref _writePos));
        Array.Clear(_buffer, 0, _buffer.Length);
    }
}
