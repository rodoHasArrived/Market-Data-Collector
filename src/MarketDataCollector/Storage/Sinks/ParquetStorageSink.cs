using System.Collections.Concurrent;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Contracts.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.Interfaces;
using MarketDataCollector.Storage.Services;
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
    private readonly ConcurrentDictionary<string, MarketEventBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
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
        var buffer = _buffers.GetOrAdd(bufferKey, _ => new MarketEventBuffer(_parquetOptions.BufferSize));

        buffer.Add(evt);

        // Flush if buffer is full
        if (buffer.ShouldFlush(_parquetOptions.BufferSize))
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

    private async Task FlushBufferAsync(string bufferKey, MarketEventBuffer buffer, CancellationToken ct)
    {
        var events = buffer.DrainAll().ToList();
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
        var trades = events
            .Where(e => e.Payload is Trade)
            .Select(e => (Event: e, Trade: (Trade)e.Payload))
            .ToList();

        if (trades.Count is 0) return;

        using var groupWriter = await ParquetWriter.CreateAsync(TradeSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[0], trades.Select(t => t.Event.Timestamp).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[1], trades.Select(t => t.Event.Symbol).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[2], trades.Select(t => t.Trade.Price).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[3], trades.Select(t => t.Trade.Size).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[4], trades.Select(t => t.Trade.Aggressor.ToString()).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[5], trades.Select(t => t.Trade.SequenceNumber).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[6], trades.Select(t => t.Trade.Venue ?? "UNKNOWN").ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(TradeSchema.DataFields[7], trades.Select(t => t.Event.Source).ToArray()));
    }

    private async Task WriteQuotesAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var quotes = events
            .Where(e => e.Payload is BboQuotePayload)
            .Select(e => (Event: e, Quote: (BboQuotePayload)e.Payload))
            .ToList();

        if (quotes.Count is 0) return;

        using var groupWriter = await ParquetWriter.CreateAsync(QuoteSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[0], quotes.Select(q => q.Event.Timestamp).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[1], quotes.Select(q => q.Event.Symbol).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[2], quotes.Select(q => q.Quote.BidPrice).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[3], quotes.Select(q => q.Quote.BidSize).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[4], quotes.Select(q => q.Quote.AskPrice).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[5], quotes.Select(q => q.Quote.AskSize).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[6], quotes.Select(q => q.Quote.Spread ?? 0m).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[7], quotes.Select(q => q.Quote.SequenceNumber).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(QuoteSchema.DataFields[8], quotes.Select(q => q.Event.Source).ToArray()));
    }

    private async Task WriteL2SnapshotsAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var snapshots = events
            .Select(e => (Event: e, Data: ExtractL2Data(e)))
            .Where(x => x.Data.Snapshot is not null)
            .ToList();

        if (snapshots.Count is 0) return;

        using var groupWriter = await ParquetWriter.CreateAsync(L2Schema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[0], snapshots.Select(s => s.Event.Timestamp).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[1], snapshots.Select(s => s.Event.Symbol).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[2], snapshots.Select(s => s.Data.Snapshot!.Bids?.Count ?? 0).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[3], snapshots.Select(s => s.Data.Snapshot!.Asks?.Count ?? 0).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[4], snapshots.Select(s => s.Data.Snapshot!.Bids?.FirstOrDefault()?.Price ?? 0m).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[5], snapshots.Select(s => s.Data.Snapshot!.Asks?.FirstOrDefault()?.Price ?? 0m).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[6], snapshots.Select(s => ComputeSpread(s.Data.Snapshot!)).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[7], snapshots.Select(s => s.Data.SequenceNumber).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[8], snapshots.Select(s => s.Event.Source).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[9], snapshots.Select(s => System.Text.Json.JsonSerializer.Serialize(s.Data.Snapshot!.Bids ?? (IReadOnlyList<OrderBookLevel>)[])).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(L2Schema.DataFields[10], snapshots.Select(s => System.Text.Json.JsonSerializer.Serialize(s.Data.Snapshot!.Asks ?? (IReadOnlyList<OrderBookLevel>)[])).ToArray()));
    }

    private static (LOBSnapshot? Snapshot, long SequenceNumber) ExtractL2Data(MarketEvent evt) => evt.Payload switch
    {
        L2SnapshotPayload l2 => (l2.Snapshot, l2.SequenceNumber),
        LOBSnapshot lob => (lob, lob.SequenceNumber),
        _ => (null, 0)
    };

    private static decimal? ComputeSpread(LOBSnapshot snap)
    {
        var bestBid = snap.Bids?.FirstOrDefault()?.Price ?? 0;
        var bestAsk = snap.Asks?.FirstOrDefault()?.Price ?? 0;
        return bestBid > 0 && bestAsk > 0 ? bestAsk - bestBid : null;
    }

    private async Task WriteBarsAsync(string path, List<MarketEvent> events, CancellationToken ct)
    {
        var bars = events
            .Where(e => e.Payload is HistoricalBar)
            .Select(e => (Event: e, Bar: (HistoricalBar)e.Payload))
            .ToList();

        if (bars.Count is 0) return;

        using var groupWriter = await ParquetWriter.CreateAsync(BarSchema, File.Create(path));
        using var rowGroupWriter = groupWriter.CreateRowGroup();

        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[0], bars.Select(b => b.Event.Timestamp).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[1], bars.Select(b => b.Event.Symbol).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[2], bars.Select(b => b.Bar.Open).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[3], bars.Select(b => b.Bar.High).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[4], bars.Select(b => b.Bar.Low).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[5], bars.Select(b => b.Bar.Close).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[6], bars.Select(b => b.Bar.Volume).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[7], bars.Select(b => b.Bar.SequenceNumber).ToArray()));
        await rowGroupWriter.WriteColumnAsync(new DataColumn(BarSchema.DataFields[8], bars.Select(b => b.Event.Source).ToArray()));
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
