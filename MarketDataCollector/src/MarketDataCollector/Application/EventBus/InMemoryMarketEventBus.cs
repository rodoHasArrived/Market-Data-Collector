using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarketDataCollector.Application.Filters;
using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Application.EventBus;

/// <summary>
/// Per-symbol fanout event bus.
/// Publish is O(1): append to symbol topic (+ wildcard topic).
/// Subscribers read independently via cursors. Bounded memory via ring buffers.
/// </summary>
public sealed class InMemoryMarketEventBus : IMarketEventBus
{
    private readonly ConcurrentDictionary<string, Topic> _topics = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _capacityPerTopic;

    public InMemoryMarketEventBus(int capacityPerTopic = 50_000)
    {
        _capacityPerTopic = capacityPerTopic <= 0 ? 50_000 : capacityPerTopic;
    }

    public bool TryPublish(in MarketEvent evt)
    {
        GetTopic(evt.Symbol).Append(evt);
        GetTopic("*").Append(evt);
        return true;
    }

    /// <summary>
    /// Implements IMarketEventPublisher.TryPublish (non-ref overload).
    /// </summary>
    public bool TryPublish(MarketEvent evt) => TryPublish(in evt);

    public ValueTask PublishAsync(MarketEvent evt, CancellationToken ct = default)
    {
        TryPublish(evt);
        return ValueTask.CompletedTask;
    }

    public void Complete()
    {
        // No-op for in-memory ring buffer topics.
    }

    public IMarketEventSubscription Subscribe(MarketEventFilter filter, CancellationToken ct = default)
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));
        var key = string.IsNullOrWhiteSpace(filter.Symbol) ? "*" : filter.Symbol!;
        return new Subscription(GetTopic(key), filter, ct);
    }

    private Topic GetTopic(string key) => _topics.GetOrAdd(key, _ => new Topic(_capacityPerTopic));

    private sealed class Topic
    {
        private readonly MarketEvent[] _buf;
        private readonly object _gate = new();
        private int _next;
        private long _seq;
        private readonly ManualResetEventSlim _signal = new(false);

        public Topic(int capacity)
        {
            _buf = new MarketEvent[capacity];
        }

        public long Append(in MarketEvent evt)
        {
            lock (_gate)
            {
                _buf[_next] = evt;
                _next = (_next + 1) % _buf.Length;
                _seq++;
                _signal.Set();
                return _seq;
            }
        }

        public bool TryReadBatch(ref long cursor, out List<MarketEvent> batch, int max = 1024)
        {
            batch = new List<MarketEvent>(Math.Min(max, 1024));

            lock (_gate)
            {
                var end = _seq;
                var start = cursor;

                // If the reader fell behind past the ring capacity, fast-forward (drops old).
                var minSeq = Math.Max(0, end - _buf.Length);
                if (start < minSeq) start = minSeq;

                for (long s = start; s < end && batch.Count < max; s++)
                {
                    batch.Add(_buf[(int)(s % _buf.Length)]);
                }

                cursor = end;

                // Reset signal if caught up
                if (cursor >= _seq) _signal.Reset();
            }

            return batch.Count > 0;
        }

        public void WaitForData(CancellationToken ct)
        {
            _signal.Wait(5, ct); // short wait; subscriber loop also checks cancellation
        }

        public void Dispose() => _signal.Dispose();
    }

    private sealed class Subscription : IMarketEventSubscription
    {
        private readonly Topic _topic;
        private readonly MarketEventFilter _filter;
        private readonly CancellationToken _ct;
        private bool _disposed;
        private long _cursor;

        public Subscription(Topic topic, MarketEventFilter filter, CancellationToken ct)
        {
            _topic = topic;
            _filter = filter;
            _ct = ct;
        }

        public IAsyncEnumerable<MarketEvent> Stream => StreamImpl();

        private async IAsyncEnumerable<MarketEvent> StreamImpl([EnumeratorCancellation] CancellationToken enumerationCt = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_ct, enumerationCt);
            var ct = linked.Token;

            while (!_disposed && !ct.IsCancellationRequested)
            {
                if (_topic.TryReadBatch(ref _cursor, out var batch))
                {
                    foreach (var evt in batch)
                    {
                        if (_filter.Matches(evt))
                            yield return evt;
                    }

                    // Yield control occasionally under heavy load
                    await Task.Yield();
                    continue;
                }

                _topic.WaitForData(ct);
            }
        }

        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
