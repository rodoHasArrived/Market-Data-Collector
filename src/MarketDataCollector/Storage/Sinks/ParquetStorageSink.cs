using System.Collections.Concurrent;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Interfaces;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Serilog;

namespace MarketDataCollector.Storage.Sinks;

/// <summary>
/// Apache Parquet storage sink for high-performance columnar storage.
/// Provides 10-20x better compression than JSONL and optimized for analytics.
///
/// Based on: https://github.com/aloneguid/parquet-dotnet (MIT)
/// Reference: docs/open-source-references.md #20
/// </summary>
public sealed class ParquetStorageSink : IStorageSink
{
    private readonly ILogger _log = LoggingSetup.ForContext<ParquetStorageSink>();
    private readonly StorageOptions _options;
    private readonly ParquetStorageOptions _parquetOptions;
    private readonly ConcurrentDictionary<string, ParquetBufferState> _buffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _flushTimer;
    private bool _disposed;

    // Trade event schema
    private static readonly ParquetSchema TradeSchema = new(
        new DataField<DateTimeOffset>("Timestamp"),
        new DataField<string>("Symbol"),
        new DataField<decimal>("Price"),
        new DataField<long>("Size"),
        new DataField<string>("AggressorSide"),
        new DataField<long>("SequenceNumber"),
        new DataField<string>("Venue"),
        new DataField<string>("Source")
    );

    // Quote event schema
    private static readonly ParquetSchema QuoteSchema = new(
        new DataField<DateTimeOffset>("Timestamp"),
        new DataField<string>("Symbol"),
        new DataField<decimal>("BidPrice"),
        new DataField<long>("BidSize"),
        new DataField<decimal>("AskPrice"),
        new DataField<long>("AskSize"),
        new DataField<decimal>("Spread"),
        new DataField<long>("SequenceNumber"),
        new DataField<string>("Source")
    );

    // L2 Snapshot schema
    private static readonly ParquetSchema L2Schema = new(
        new DataField<DateTimeOffset>("Timestamp"),
        new DataField<string>("Symbol"),
        new DataField<int>("BidLevels"),
        new DataField<int>("AskLevels"),
        new DataField<decimal>("BestBid"),
        new DataField<decimal>("BestAsk"),
        new DataField<decimal?>("Spread"),
        new DataField<long>("SequenceNumber"),
        new DataField<string>("Source"),
        new DataField<string>("BidsJson"),
        new DataField<string>("AsksJson")
    );

    // Historical bar schema
    private static readonly ParquetSchema BarSchema = new(
        new DataField<DateTimeOffset>("Timestamp"),
        new DataField<string>("Symbol"),
        new DataField<decimal>("Open"),
        new DataField<decimal>("High"),
        new DataField<decimal>("Low"),
        new DataField<decimal>("Close"),
        new DataField<decimal>("Volume"),
        new DataField<long>("SequenceNumber"),
        new DataField<string>("Source")
    );

    public ParquetStorageSink(StorageOptions options, ParquetStorageOptions? parquetOptions = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _parquetOptions = parquetOptions ?? ParquetStorageOptions.Default;

        // Setup periodic flush timer
        _flushTimer = new Timer(
            async _ => await FlushAllBuffersAsync(),
            null,
            _parquetOptions.FlushInterval,
            _parquetOptions.FlushInterval);

        _log.Information("ParquetStorageSink initialized with buffer size {BufferSize}, flush interval {FlushInterval}s",
            _parquetOptions.BufferSize, _parquetOptions.FlushInterval.TotalSeconds);
    }

    public async ValueTask AppendAsync(MarketEvent evt, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ParquetStorageSink));

        EventSchemaValidator.Validate(evt);

        var bufferKey = GetBufferKey(evt);
        var buffer = _buffers.GetOrAdd(bufferKey, _ => new ParquetBufferState(_parquetOptions.BufferSize));

        buffer.Add(evt);

        // Flush if buffer is full
        if (buffer.Count >= _parquetOptions.BufferSize)
        {
            await FlushBufferAsync(bufferKey, buffer, ct);
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        await FlushAllBuffersAsync(ct);
    }

    private async Task FlushAllBuffersAsync(CancellationToken ct = default)
    {
        foreach (var kvp in _buffers)
        {
            if (kvp.Value.Count > 0)
            {
                await FlushBufferAsync(kvp.Key, kvp.Value, ct);
            }
        }
    }

    private async Task FlushBufferAsync(string bufferKey, ParquetBufferState buffer, CancellationToken ct)
    {
        var events = buffer.DrainAll();
        if (events.Count == 0) return;

        try
        {
            var path = GetFilePath(events[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var eventType = events[0].Type;

            switch (eventType)
            {
                case MarketEventType.Trade:
                    await WriteTradesAsync(path, events, ct);
                    break;
                case MarketEventType.BboQuote:
                    await WriteQuotesAsync(path, events, ct);
                    break;
                case MarketEventType.L2Snapshot:
                    await WriteL2SnapshotsAsync(path, events, ct);
                    break;
                case MarketEventType.HistoricalBar:
                    await WriteBarsAsync(path, events, ct);
                    break;
                default:
                    // Write as generic event
                    await WriteGenericEventsAsync(path, events, ct);
                    break;
            }

            _log.Debug("Flushed {Count} events to Parquet file: {Path}", events.Count, path);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to flush {Count} events to Parquet", events.Count);
            throw;
        }
    }

    private async Task WriteTradesAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var timestamps = new List<DateTimeOffset>();
        var symbols = new List<string>();
        var prices = new List<decimal>();
        var sizes = new List<long>();
        var aggressors = new List<string>();
        var sequences = new List<long>();
        var venues = new List<string>();
        var sources = new List<string>();

        foreach (var evt in events)
        {
            if (evt.Payload is Trade trade)
            {
                timestamps.Add(evt.Timestamp);
                symbols.Add(evt.Symbol);
                prices.Add(trade.Price);
                sizes.Add(trade.Size);
                aggressors.Add(trade.Aggressor.ToString());
                sequences.Add(trade.SequenceNumber);
                venues.Add(trade.Venue ?? "UNKNOWN");
                sources.Add(evt.Source);
            }
        }

        using var groupWriter = await ParquetWriter.CreateAsync(TradeSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[0], timestamps.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[1], symbols.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[2], prices.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[3], sizes.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[4], aggressors.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[5], sequences.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[6], venues.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[7], sources.ToArray()));
    }

    private async Task WriteQuotesAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var timestamps = new List<DateTimeOffset>();
        var symbols = new List<string>();
        var bidPrices = new List<decimal>();
        var bidSizes = new List<long>();
        var askPrices = new List<decimal>();
        var askSizes = new List<long>();
        var spreads = new List<decimal>();
        var sequences = new List<long>();
        var sources = new List<string>();

        foreach (var evt in events)
        {
            if (evt.Payload is BboQuotePayload quote)
            {
                timestamps.Add(evt.Timestamp);
                symbols.Add(evt.Symbol);
                bidPrices.Add(quote.BidPrice);
                bidSizes.Add(quote.BidSize);
                askPrices.Add(quote.AskPrice);
                askSizes.Add(quote.AskSize);
                spreads.Add(quote.Spread ?? 0m);
                sequences.Add(quote.SequenceNumber);
                sources.Add(evt.Source);
            }
        }

        using var groupWriter = await ParquetWriter.CreateAsync(QuoteSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[0], timestamps.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[1], symbols.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[2], bidPrices.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[3], bidSizes.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[4], askPrices.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[5], askSizes.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[6], spreads.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[7], sequences.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[8], sources.ToArray()));
    }

    private async Task WriteL2SnapshotsAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var timestamps = new List<DateTimeOffset>();
        var symbols = new List<string>();
        var bidLevelsCounts = new List<int>();
        var askLevelsCounts = new List<int>();
        var bestBids = new List<decimal>();
        var bestAsks = new List<decimal>();
        var spreads = new List<decimal?>();
        var sequences = new List<long>();
        var sources = new List<string>();
        var bidsJson = new List<string>();
        var asksJson = new List<string>();

        foreach (var evt in events)
        {
            LOBSnapshot? snap = null;
            long seqNum = 0;

            if (evt.Payload is L2SnapshotPayload l2Payload)
            {
                snap = l2Payload.Snapshot;
                seqNum = l2Payload.SequenceNumber;
            }
            else if (evt.Payload is LOBSnapshot lobSnap)
            {
                snap = lobSnap;
                seqNum = lobSnap.SequenceNumber;
            }

            if (snap != null)
            {
                timestamps.Add(evt.Timestamp);
                symbols.Add(evt.Symbol);
                bidLevelsCounts.Add(snap.Bids?.Count ?? 0);
                askLevelsCounts.Add(snap.Asks?.Count ?? 0);
                var bestBid = snap.Bids?.FirstOrDefault()?.Price ?? 0;
                var bestAsk = snap.Asks?.FirstOrDefault()?.Price ?? 0;
                bestBids.Add(bestBid);
                bestAsks.Add(bestAsk);
                spreads.Add(bestBid > 0 && bestAsk > 0 ? bestAsk - bestBid : null);
                sequences.Add(seqNum);
                sources.Add(evt.Source);
                bidsJson.Add(System.Text.Json.JsonSerializer.Serialize(snap.Bids ?? (IReadOnlyList<OrderBookLevel>)Array.Empty<OrderBookLevel>()));
                asksJson.Add(System.Text.Json.JsonSerializer.Serialize(snap.Asks ?? (IReadOnlyList<OrderBookLevel>)Array.Empty<OrderBookLevel>()));
            }
        }

        using var groupWriter = await ParquetWriter.CreateAsync(L2Schema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[0], timestamps.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[1], symbols.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[2], bidLevelsCounts.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[3], askLevelsCounts.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[4], bestBids.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[5], bestAsks.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[6], spreads.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[7], sequences.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[8], sources.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[9], bidsJson.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[10], asksJson.ToArray()));
    }

    private async Task WriteBarsAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var timestamps = new List<DateTimeOffset>();
        var symbols = new List<string>();
        var opens = new List<decimal>();
        var highs = new List<decimal>();
        var lows = new List<decimal>();
        var closes = new List<decimal>();
        var volumes = new List<decimal>();
        var sequences = new List<long>();
        var sources = new List<string>();

        foreach (var evt in events)
        {
            if (evt.Payload is HistoricalBar bar)
            {
                timestamps.Add(evt.Timestamp);
                symbols.Add(evt.Symbol);
                opens.Add(bar.Open);
                highs.Add(bar.High);
                lows.Add(bar.Low);
                closes.Add(bar.Close);
                volumes.Add(bar.Volume);
                sequences.Add(bar.SequenceNumber);
                sources.Add(evt.Source);
            }
        }

        using var groupWriter = await ParquetWriter.CreateAsync(BarSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[0], timestamps.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[1], symbols.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[2], opens.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[3], highs.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[4], lows.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[5], closes.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[6], volumes.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[7], sequences.ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[8], sources.ToArray()));
    }

    private async Task WriteGenericEventsAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        // For generic events, write as JSON strings in a simple schema
        var genericSchema = new ParquetSchema(
            new DataField<DateTimeOffset>("Timestamp"),
            new DataField<string>("Symbol"),
            new DataField<string>("Type"),
            new DataField<string>("PayloadJson"),
            new DataField<long>("Sequence"),
            new DataField<string>("Source")
        );

        var timestamps = events.Select(e => e.Timestamp).ToArray();
        var symbols = events.Select(e => e.Symbol).ToArray();
        var types = events.Select(e => e.Type.ToString()).ToArray();
        var payloads = events.Select(e => System.Text.Json.JsonSerializer.Serialize(e.Payload)).ToArray();
        var sequences = events.Select(e => e.Sequence).ToArray();
        var sources = events.Select(e => e.Source).ToArray();

        using var groupWriter = await ParquetWriter.CreateAsync(genericSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(genericSchema.DataFields[0], timestamps));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(genericSchema.DataFields[1], symbols));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(genericSchema.DataFields[2], types));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(genericSchema.DataFields[3], payloads));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(genericSchema.DataFields[4], sequences));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(genericSchema.DataFields[5], sources));
    }

    private string GetBufferKey(MarketEvent evt)
    {
        return $"{evt.Symbol}_{evt.Type}_{evt.Timestamp.Date:yyyyMMdd}";
    }

    private string GetFilePath(MarketEvent evt)
    {
        var date = evt.Timestamp.Date;
        var typeName = evt.Type.ToString().ToLowerInvariant();
        var fileName = $"{evt.Symbol}_{typeName}_{date:yyyyMMdd}.parquet";

        return _options.NamingConvention switch
        {
            FileNamingConvention.BySymbol => Path.Combine(_options.RootPath, evt.Symbol, fileName),
            FileNamingConvention.ByDate => Path.Combine(_options.RootPath, $"{date:yyyy}", $"{date:MM}", $"{date:dd}", fileName),
            FileNamingConvention.ByType => Path.Combine(_options.RootPath, typeName, fileName),
            _ => Path.Combine(_options.RootPath, fileName)
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _flushTimer.DisposeAsync();
        await FlushAllBuffersAsync();
        _buffers.Clear();

        _log.Information("ParquetStorageSink disposed");
    }
}

/// <summary>
/// Thread-safe buffer for accumulating events before writing to Parquet.
/// </summary>
internal sealed class ParquetBufferState
{
    private readonly List<MarketEvent> _events;
    private readonly object _lock = new();
    private readonly int _capacity;

    public ParquetBufferState(int capacity)
    {
        _capacity = capacity;
        _events = new List<MarketEvent>(capacity);
    }

    public int Count
    {
        get
        {
            lock (_lock) return _events.Count;
        }
    }

    public void Add(MarketEvent evt)
    {
        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    public List<MarketEvent> DrainAll()
    {
        lock (_lock)
        {
            var result = new List<MarketEvent>(_events);
            _events.Clear();
            return result;
        }
    }
}

/// <summary>
/// Configuration options for Parquet storage.
/// </summary>
public sealed class ParquetStorageOptions
{
    /// <summary>
    /// Number of events to buffer before writing to disk.
    /// </summary>
    public int BufferSize { get; init; } = 10000;

    /// <summary>
    /// Maximum time between flushes.
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Compression method for Parquet files.
    /// </summary>
    public CompressionMethod CompressionMethod { get; init; } = CompressionMethod.Snappy;

    /// <summary>
    /// Row group size for Parquet files.
    /// </summary>
    public int RowGroupSize { get; init; } = 50000;

    public static ParquetStorageOptions Default => new();

    public static ParquetStorageOptions HighCompression => new()
    {
        CompressionMethod = CompressionMethod.Gzip,
        BufferSize = 50000
    };

    public static ParquetStorageOptions LowLatency => new()
    {
        BufferSize = 1000,
        FlushInterval = TimeSpan.FromSeconds(5),
        CompressionMethod = CompressionMethod.None
    };
}
