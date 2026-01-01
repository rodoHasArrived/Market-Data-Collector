using System.Collections.Concurrent;
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
        new DataField<int>("Size"),
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
        new DataField<int>("BidSize"),
        new DataField<decimal>("AskPrice"),
        new DataField<int>("AskSize"),
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
        new DataField<decimal>("Spread"),
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
        var sizes = new List<int>();
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

        var table = new Table(
            TradeSchema,
            new DataColumn(TradeSchema.DataFields[0], timestamps.ToArray()),
            new DataColumn(TradeSchema.DataFields[1], symbols.ToArray()),
            new DataColumn(TradeSchema.DataFields[2], prices.ToArray()),
            new DataColumn(TradeSchema.DataFields[3], sizes.ToArray()),
            new DataColumn(TradeSchema.DataFields[4], aggressors.ToArray()),
            new DataColumn(TradeSchema.DataFields[5], sequences.ToArray()),
            new DataColumn(TradeSchema.DataFields[6], venues.ToArray()),
            new DataColumn(TradeSchema.DataFields[7], sources.ToArray())
        );

        await WriteTableAsync(path, table, ct);
    }

    private async Task WriteQuotesAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var timestamps = new List<DateTimeOffset>();
        var symbols = new List<string>();
        var bidPrices = new List<decimal>();
        var bidSizes = new List<int>();
        var askPrices = new List<decimal>();
        var askSizes = new List<int>();
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
                spreads.Add(quote.Spread);
                sequences.Add(quote.SequenceNumber);
                sources.Add(evt.Source);
            }
        }

        var table = new Table(
            QuoteSchema,
            new DataColumn(QuoteSchema.DataFields[0], timestamps.ToArray()),
            new DataColumn(QuoteSchema.DataFields[1], symbols.ToArray()),
            new DataColumn(QuoteSchema.DataFields[2], bidPrices.ToArray()),
            new DataColumn(QuoteSchema.DataFields[3], bidSizes.ToArray()),
            new DataColumn(QuoteSchema.DataFields[4], askPrices.ToArray()),
            new DataColumn(QuoteSchema.DataFields[5], askSizes.ToArray()),
            new DataColumn(QuoteSchema.DataFields[6], spreads.ToArray()),
            new DataColumn(QuoteSchema.DataFields[7], sequences.ToArray()),
            new DataColumn(QuoteSchema.DataFields[8], sources.ToArray())
        );

        await WriteTableAsync(path, table, ct);
    }

    private async Task WriteL2SnapshotsAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var timestamps = new List<DateTimeOffset>();
        var symbols = new List<string>();
        var bidLevelsCounts = new List<int>();
        var askLevelsCounts = new List<int>();
        var bestBids = new List<decimal>();
        var bestAsks = new List<decimal>();
        var spreads = new List<decimal>();
        var sequences = new List<long>();
        var sources = new List<string>();
        var bidsJson = new List<string>();
        var asksJson = new List<string>();

        foreach (var evt in events)
        {
            if (evt.Payload is L2SnapshotPayload snap)
            {
                timestamps.Add(evt.Timestamp);
                symbols.Add(evt.Symbol);
                bidLevelsCounts.Add(snap.Bids?.Count ?? 0);
                askLevelsCounts.Add(snap.Asks?.Count ?? 0);
                bestBids.Add(snap.Bids?.FirstOrDefault()?.Price ?? 0);
                bestAsks.Add(snap.Asks?.FirstOrDefault()?.Price ?? 0);
                spreads.Add(snap.Spread);
                sequences.Add(snap.SequenceNumber);
                sources.Add(evt.Source);
                bidsJson.Add(System.Text.Json.JsonSerializer.Serialize(snap.Bids ?? new List<OrderBookLevel>()));
                asksJson.Add(System.Text.Json.JsonSerializer.Serialize(snap.Asks ?? new List<OrderBookLevel>()));
            }
        }

        var table = new Table(
            L2Schema,
            new DataColumn(L2Schema.DataFields[0], timestamps.ToArray()),
            new DataColumn(L2Schema.DataFields[1], symbols.ToArray()),
            new DataColumn(L2Schema.DataFields[2], bidLevelsCounts.ToArray()),
            new DataColumn(L2Schema.DataFields[3], askLevelsCounts.ToArray()),
            new DataColumn(L2Schema.DataFields[4], bestBids.ToArray()),
            new DataColumn(L2Schema.DataFields[5], bestAsks.ToArray()),
            new DataColumn(L2Schema.DataFields[6], spreads.ToArray()),
            new DataColumn(L2Schema.DataFields[7], sequences.ToArray()),
            new DataColumn(L2Schema.DataFields[8], sources.ToArray()),
            new DataColumn(L2Schema.DataFields[9], bidsJson.ToArray()),
            new DataColumn(L2Schema.DataFields[10], asksJson.ToArray())
        );

        await WriteTableAsync(path, table, ct);
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

        var table = new Table(
            BarSchema,
            new DataColumn(BarSchema.DataFields[0], timestamps.ToArray()),
            new DataColumn(BarSchema.DataFields[1], symbols.ToArray()),
            new DataColumn(BarSchema.DataFields[2], opens.ToArray()),
            new DataColumn(BarSchema.DataFields[3], highs.ToArray()),
            new DataColumn(BarSchema.DataFields[4], lows.ToArray()),
            new DataColumn(BarSchema.DataFields[5], closes.ToArray()),
            new DataColumn(BarSchema.DataFields[6], volumes.ToArray()),
            new DataColumn(BarSchema.DataFields[7], sequences.ToArray()),
            new DataColumn(BarSchema.DataFields[8], sources.ToArray())
        );

        await WriteTableAsync(path, table, ct);
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

        var table = new Table(
            genericSchema,
            new DataColumn(genericSchema.DataFields[0], timestamps),
            new DataColumn(genericSchema.DataFields[1], symbols),
            new DataColumn(genericSchema.DataFields[2], types),
            new DataColumn(genericSchema.DataFields[3], payloads),
            new DataColumn(genericSchema.DataFields[4], sequences),
            new DataColumn(genericSchema.DataFields[5], sources)
        );

        await WriteTableAsync(path, table, ct);
    }

    private async Task WriteTableAsync(string path, Table table, CancellationToken ct)
    {
        var fileExists = File.Exists(path);

        await using var stream = new FileStream(
            path,
            fileExists ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        await ParquetWriter.WriteAsync(table, stream, _parquetOptions.CompressionMethod);
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
            FileNamingConvention.ByEventType => Path.Combine(_options.RootPath, typeName, fileName),
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
