using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp;

/// <summary>
/// Detects sequence gaps in market data streams (Hydra-inspired pattern).
/// Tracks sequence numbers per symbol/stream and reports gaps for data quality monitoring.
///
/// Features:
/// - Per-symbol sequence tracking
/// - Multi-stream support (different data types per symbol)
/// - Gap event reporting via callback
/// - Statistics tracking for monitoring
/// </summary>
public sealed class SequenceGapDetector
{
    private readonly ILogger _log = LoggingSetup.ForContext<SequenceGapDetector>();
    private readonly ConcurrentDictionary<string, StreamState> _streams = new();
    private readonly Action<IntegrityEvent>? _onGapDetected;

    private long _totalMessages;
    private long _totalGaps;
    private long _totalMissedMessages;

    /// <summary>
    /// Creates a new sequence gap detector.
    /// </summary>
    /// <param name="onGapDetected">Callback invoked when a sequence gap is detected.</param>
    public SequenceGapDetector(Action<IntegrityEvent>? onGapDetected = null)
    {
        _onGapDetected = onGapDetected;
    }

    /// <summary>
    /// Total messages processed.
    /// </summary>
    public long TotalMessages => _totalMessages;

    /// <summary>
    /// Total gaps detected.
    /// </summary>
    public long TotalGaps => _totalGaps;

    /// <summary>
    /// Total estimated missed messages.
    /// </summary>
    public long TotalMissedMessages => _totalMissedMessages;

    /// <summary>
    /// Number of symbols being tracked.
    /// </summary>
    public int TrackedSymbols => _streams.Count;

    /// <summary>
    /// Check a sequence number and detect gaps.
    /// Returns true if no gap was detected, false if a gap or out-of-order was detected.
    /// </summary>
    /// <param name="symbol">Symbol identifier.</param>
    /// <param name="sequenceNumber">Sequence number of the message.</param>
    /// <param name="streamId">Optional stream identifier for multi-stream scenarios.</param>
    /// <param name="venue">Optional venue/exchange identifier.</param>
    /// <param name="timestamp">Timestamp of the message.</param>
    public bool CheckSequence(
        string symbol,
        long sequenceNumber,
        string? streamId = null,
        string? venue = null,
        DateTimeOffset? timestamp = null)
    {
        if (sequenceNumber <= 0)
            return true; // Invalid sequence, skip check

        Interlocked.Increment(ref _totalMessages);
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var key = CreateKey(symbol, streamId);

        var state = _streams.GetOrAdd(key, _ => new StreamState(sequenceNumber - 1));

        var result = state.CheckAndUpdate(sequenceNumber);

        switch (result.Type)
        {
            case CheckResultType.Gap:
                Interlocked.Increment(ref _totalGaps);
                Interlocked.Add(ref _totalMissedMessages, result.MissedCount);

                var gapEvent = IntegrityEvent.SequenceGap(
                    ts, symbol, result.ExpectedSequence, sequenceNumber, streamId, venue);

                _log.Warning(
                    "Sequence gap detected for {Symbol}: expected {Expected}, got {Received} (missed {Missed} messages)",
                    symbol, result.ExpectedSequence, sequenceNumber, result.MissedCount);

                _onGapDetected?.Invoke(gapEvent);
                return false;

            case CheckResultType.OutOfOrder:
                var outOfOrderEvent = IntegrityEvent.OutOfOrder(
                    ts, symbol, result.LastSequence, sequenceNumber, streamId, venue);

                _log.Debug("Out-of-order message for {Symbol}: last {Last}, received {Received}",
                    symbol, result.LastSequence, sequenceNumber);

                _onGapDetected?.Invoke(outOfOrderEvent);
                return false;

            case CheckResultType.Duplicate:
                _log.Debug("Duplicate message for {Symbol}: sequence {Seq}", symbol, sequenceNumber);
                return false;

            default:
                return true;
        }
    }

    /// <summary>
    /// Reset tracking for a specific symbol/stream.
    /// </summary>
    public void Reset(string symbol, string? streamId = null)
    {
        var key = CreateKey(symbol, streamId);
        _streams.TryRemove(key, out _);
        _log.Debug("Reset sequence tracking for {Key}", key);
    }

    /// <summary>
    /// Reset all tracking.
    /// </summary>
    public void ResetAll()
    {
        _streams.Clear();
        Interlocked.Exchange(ref _totalMessages, 0);
        Interlocked.Exchange(ref _totalGaps, 0);
        Interlocked.Exchange(ref _totalMissedMessages, 0);
        _log.Debug("Reset all sequence tracking");
    }

    /// <summary>
    /// Get statistics for a specific symbol/stream.
    /// </summary>
    public StreamStatistics? GetStatistics(string symbol, string? streamId = null)
    {
        var key = CreateKey(symbol, streamId);
        if (_streams.TryGetValue(key, out var state))
        {
            return state.GetStatistics(symbol, streamId);
        }
        return null;
    }

    /// <summary>
    /// Get statistics for all tracked streams.
    /// </summary>
    public IReadOnlyList<StreamStatistics> GetAllStatistics()
    {
        var result = new List<StreamStatistics>();
        foreach (var (key, state) in _streams)
        {
            var parts = key.Split('|');
            var symbol = parts[0];
            var streamId = parts.Length > 1 ? parts[1] : null;
            result.Add(state.GetStatistics(symbol, streamId));
        }
        return result;
    }

    /// <summary>
    /// Get overall detector statistics.
    /// </summary>
    public DetectorStatistics GetOverallStatistics()
    {
        return new DetectorStatistics(
            TrackedSymbols: _streams.Count,
            TotalMessages: _totalMessages,
            TotalGaps: _totalGaps,
            TotalMissedMessages: _totalMissedMessages,
            GapRate: _totalMessages > 0 ? (double)_totalGaps / _totalMessages : 0
        );
    }

    private static string CreateKey(string symbol, string? streamId)
    {
        return string.IsNullOrEmpty(streamId) ? symbol : $"{symbol}|{streamId}";
    }

    /// <summary>
    /// Internal state for tracking a single stream.
    /// </summary>
    private sealed class StreamState
    {
        private long _lastSequence;
        private long _firstSequence;
        private long _messageCount;
        private long _gapCount;
        private long _missedCount;
        private long _outOfOrderCount;
        private long _duplicateCount;
        private readonly object _lock = new();

        public StreamState(long initialSequence)
        {
            _lastSequence = initialSequence;
            _firstSequence = initialSequence + 1;
        }

        public CheckResult CheckAndUpdate(long sequence)
        {
            lock (_lock)
            {
                _messageCount++;

                if (sequence == _lastSequence + 1)
                {
                    // Normal case: sequential
                    _lastSequence = sequence;
                    return new CheckResult(CheckResultType.Ok, 0, 0, _lastSequence);
                }
                else if (sequence > _lastSequence + 1)
                {
                    // Gap detected
                    var expected = _lastSequence + 1;
                    var missed = sequence - expected;
                    _gapCount++;
                    _missedCount += missed;
                    _lastSequence = sequence;
                    return new CheckResult(CheckResultType.Gap, expected, missed, _lastSequence);
                }
                else if (sequence == _lastSequence)
                {
                    // Duplicate
                    _duplicateCount++;
                    return new CheckResult(CheckResultType.Duplicate, 0, 0, _lastSequence);
                }
                else
                {
                    // Out of order (sequence < _lastSequence)
                    _outOfOrderCount++;
                    return new CheckResult(CheckResultType.OutOfOrder, 0, 0, _lastSequence);
                }
            }
        }

        public StreamStatistics GetStatistics(string symbol, string? streamId)
        {
            lock (_lock)
            {
                return new StreamStatistics(
                    Symbol: symbol,
                    StreamId: streamId,
                    FirstSequence: _firstSequence,
                    LastSequence: _lastSequence,
                    MessageCount: _messageCount,
                    GapCount: _gapCount,
                    MissedCount: _missedCount,
                    OutOfOrderCount: _outOfOrderCount,
                    DuplicateCount: _duplicateCount
                );
            }
        }
    }

    private enum CheckResultType { Ok, Gap, OutOfOrder, Duplicate }

    private readonly record struct CheckResult(
        CheckResultType Type,
        long ExpectedSequence,
        long MissedCount,
        long LastSequence);
}

/// <summary>
/// Statistics for a single stream.
/// </summary>
public sealed record StreamStatistics(
    string Symbol,
    string? StreamId,
    long FirstSequence,
    long LastSequence,
    long MessageCount,
    long GapCount,
    long MissedCount,
    long OutOfOrderCount,
    long DuplicateCount)
{
    /// <summary>
    /// Gap rate (gaps per message).
    /// </summary>
    public double GapRate => MessageCount > 0 ? (double)GapCount / MessageCount : 0;

    /// <summary>
    /// Completeness percentage (messages received / expected).
    /// </summary>
    public double Completeness
    {
        get
        {
            var expected = LastSequence - FirstSequence + 1;
            return expected > 0 ? (double)(expected - MissedCount) / expected * 100 : 100;
        }
    }
}

/// <summary>
/// Overall detector statistics.
/// </summary>
public sealed record DetectorStatistics(
    int TrackedSymbols,
    long TotalMessages,
    long TotalGaps,
    long TotalMissedMessages,
    double GapRate);
