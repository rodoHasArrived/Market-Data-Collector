using MarketDataCollector.Domain.Events;

namespace MarketDataCollector.Storage.Crystallized;

/// <summary>
/// Crystallized storage format - an intuitive, self-documenting data organization system
/// designed for both casual Excel users and advanced ML practitioners.
///
/// Directory Structure:
/// <code>
/// data/
/// ├── {provider}/                    # Data source (alpaca, polygon, yahoo, etc.)
/// │   └── {symbol}/                  # Trading symbol (AAPL, SPY, etc.)
/// │       ├── bars/                  # Price bar data
/// │       │   ├── daily/             # Daily OHLCV bars
/// │       │   ├── 1h/                # Hourly bars
/// │       │   ├── 5m/                # 5-minute bars
/// │       │   └── 1m/                # 1-minute bars
/// │       ├── trades/                # Tick-by-tick trades
/// │       ├── quotes/                # BBO quote data
/// │       ├── orderbook/             # Level 2 order book
/// │       └── _manifest.json         # Symbol metadata
/// └── _catalog.json                  # Root catalog with all symbols/providers
/// </code>
///
/// File Naming:
/// - Self-documenting: {symbol}_{provider}_{category}_{granularity}_{date}.{ext}
/// - Example: AAPL_alpaca_bars_daily_2024-01.csv
/// </summary>
public sealed class CrystallizedStorageFormat
{
    private readonly CrystallizedStorageOptions _options;

    public CrystallizedStorageFormat(CrystallizedStorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the file path for bar data.
    /// </summary>
    public string GetBarPath(
        string provider,
        string symbol,
        TimeGranularity granularity,
        DateOnly date)
    {
        var partition = FormatDatePartition(date, granularity);
        return BuildPath(provider, symbol, CrystallizedDataCategory.Bars, granularity, partition);
    }

    /// <summary>
    /// Gets the file path for trade data.
    /// </summary>
    public string GetTradePath(string provider, string symbol, DateOnly date)
    {
        var partition = FormatDatePartition(date, TimeGranularity.Tick);
        return BuildPath(provider, symbol, CrystallizedDataCategory.Trades, TimeGranularity.Tick, partition);
    }

    /// <summary>
    /// Gets the file path for quote data.
    /// </summary>
    public string GetQuotePath(string provider, string symbol, DateOnly date)
    {
        var partition = FormatDatePartition(date, TimeGranularity.Tick);
        return BuildPath(provider, symbol, CrystallizedDataCategory.Quotes, TimeGranularity.Tick, partition);
    }

    /// <summary>
    /// Gets the file path for order book data.
    /// </summary>
    public string GetOrderBookPath(string provider, string symbol, DateOnly date)
    {
        var partition = FormatDatePartition(date, TimeGranularity.Tick);
        return BuildPath(provider, symbol, CrystallizedDataCategory.OrderBook, TimeGranularity.Tick, partition);
    }

    /// <summary>
    /// Gets the file path for order flow statistics.
    /// </summary>
    public string GetOrderFlowPath(string provider, string symbol, TimeGranularity granularity, DateOnly date)
    {
        var partition = FormatDatePartition(date, granularity);
        return BuildPath(provider, symbol, CrystallizedDataCategory.OrderFlow, granularity, partition);
    }

    /// <summary>
    /// Gets the file path for a MarketEvent based on its type.
    /// </summary>
    public string GetPathForEvent(MarketEvent evt, TimeGranularity granularity = TimeGranularity.Tick)
    {
        var date = DateOnly.FromDateTime(evt.Timestamp.UtcDateTime);
        var provider = Sanitize(evt.Source);
        var symbol = Sanitize(evt.Symbol);

        return evt.Type switch
        {
            MarketEventType.Trade => GetTradePath(provider, symbol, date),
            MarketEventType.BboQuote => GetQuotePath(provider, symbol, date),
            MarketEventType.L2Snapshot => GetOrderBookPath(provider, symbol, date),
            MarketEventType.HistoricalBar => GetBarPath(provider, symbol, TimeGranularity.Daily, date),
            MarketEventType.OrderFlow => GetOrderFlowPath(provider, symbol, granularity, date),
            _ => GetSystemPath(provider, date)
        };
    }

    /// <summary>
    /// Gets the path to the symbol manifest file.
    /// </summary>
    public string GetSymbolManifestPath(string provider, string symbol)
    {
        return Path.Combine(_options.RootPath, Sanitize(provider), Sanitize(symbol), "_manifest.json");
    }

    /// <summary>
    /// Gets the path to the root catalog file.
    /// </summary>
    public string GetCatalogPath()
    {
        return Path.Combine(_options.RootPath, "_catalog.json");
    }

    /// <summary>
    /// Gets the path for system events.
    /// </summary>
    public string GetSystemPath(string provider, DateOnly date)
    {
        var partition = date.ToString("yyyy-MM-dd");
        return Path.Combine(
            _options.RootPath,
            Sanitize(provider),
            "_system",
            $"events_{partition}{GetExtension()}");
    }

    /// <summary>
    /// Generates a self-documenting filename that works when moved outside the directory structure.
    /// </summary>
    public string GetSelfDocumentingFileName(
        string symbol,
        string provider,
        CrystallizedDataCategory category,
        TimeGranularity granularity,
        string datePartition)
    {
        var granularitySuffix = category == CrystallizedDataCategory.Bars
            ? $"_{granularity.ToFileSuffix()}"
            : "";

        return $"{Sanitize(symbol)}_{Sanitize(provider)}_{category.ToFolderName()}{granularitySuffix}_{datePartition}{GetExtension()}";
    }

    /// <summary>
    /// Parses metadata from a self-documenting filename.
    /// </summary>
    public static CrystallizedFileMetadata? ParseFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Remove extension
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (baseName.EndsWith(".jsonl") || baseName.EndsWith(".csv"))
            baseName = Path.GetFileNameWithoutExtension(baseName);

        var parts = baseName.Split('_');
        if (parts.Length < 4)
            return null;

        var symbol = parts[0];
        var provider = parts[1];
        var categoryStr = parts[2];

        // Determine if there's a granularity component
        TimeGranularity? granularity = null;
        string datePartition;

        if (parts.Length >= 5 && TimeGranularityExtensions.ParseFileSuffix(parts[3]) != null)
        {
            granularity = TimeGranularityExtensions.ParseFileSuffix(parts[3]);
            datePartition = string.Join("_", parts.Skip(4));
        }
        else
        {
            datePartition = string.Join("_", parts.Skip(3));
        }

        return new CrystallizedFileMetadata(
            Symbol: symbol,
            Provider: provider,
            Category: categoryStr,
            Granularity: granularity,
            DatePartition: datePartition);
    }

    /// <summary>
    /// Gets a human-readable preview of the path pattern.
    /// </summary>
    public string GetPathPreview(CrystallizedDataCategory category, TimeGranularity? granularity = null)
    {
        var ext = GetExtension();
        var gran = granularity?.ToFileSuffix() ?? "daily";

        return category switch
        {
            CrystallizedDataCategory.Bars =>
                $"{_options.RootPath}/{{provider}}/{{symbol}}/bars/{gran}/{{date}}{ext}",
            CrystallizedDataCategory.Trades =>
                $"{_options.RootPath}/{{provider}}/{{symbol}}/trades/{{date}}{ext}",
            CrystallizedDataCategory.Quotes =>
                $"{_options.RootPath}/{{provider}}/{{symbol}}/quotes/{{date}}{ext}",
            CrystallizedDataCategory.OrderBook =>
                $"{_options.RootPath}/{{provider}}/{{symbol}}/orderbook/{{date}}{ext}",
            CrystallizedDataCategory.OrderFlow =>
                $"{_options.RootPath}/{{provider}}/{{symbol}}/orderflow/{gran}/{{date}}{ext}",
            _ => $"{_options.RootPath}/{{provider}}/{{symbol}}/{category.ToFolderName()}/{{date}}{ext}"
        };
    }

    /// <summary>
    /// Lists all available time granularities for a symbol's bar data.
    /// </summary>
    public IEnumerable<TimeGranularity> ListAvailableGranularities(string provider, string symbol)
    {
        var barsPath = Path.Combine(_options.RootPath, Sanitize(provider), Sanitize(symbol), "bars");

        if (!Directory.Exists(barsPath))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(barsPath))
        {
            var dirName = Path.GetFileName(dir);
            var granularity = TimeGranularityExtensions.ParseFileSuffix(dirName);
            if (granularity.HasValue)
                yield return granularity.Value;
        }
    }

    private string BuildPath(
        string provider,
        string symbol,
        CrystallizedDataCategory category,
        TimeGranularity granularity,
        string datePartition)
    {
        var sanitizedProvider = Sanitize(provider);
        var sanitizedSymbol = Sanitize(symbol);
        var categoryFolder = category.ToFolderName();
        var ext = GetExtension();

        // Build directory path
        var dirPath = category switch
        {
            CrystallizedDataCategory.Bars =>
                Path.Combine(_options.RootPath, sanitizedProvider, sanitizedSymbol, categoryFolder, granularity.ToFileSuffix()),
            CrystallizedDataCategory.OrderFlow =>
                Path.Combine(_options.RootPath, sanitizedProvider, sanitizedSymbol, categoryFolder, granularity.ToFileSuffix()),
            _ =>
                Path.Combine(_options.RootPath, sanitizedProvider, sanitizedSymbol, categoryFolder)
        };

        // Build filename
        var fileName = _options.SelfDocumentingFileNames
            ? GetSelfDocumentingFileName(sanitizedSymbol, sanitizedProvider, category, granularity, datePartition)
            : $"{datePartition}{ext}";

        return Path.Combine(dirPath, fileName);
    }

    private string FormatDatePartition(DateOnly date, TimeGranularity granularity)
    {
        var partition = granularity.GetRecommendedPartition();

        return partition switch
        {
            DatePartition.Hourly => date.ToString("yyyy-MM-dd"),
            DatePartition.Daily => date.ToString("yyyy-MM-dd"),
            DatePartition.Monthly => date.ToString("yyyy-MM"),
            DatePartition.None => "all",
            _ => date.ToString("yyyy-MM-dd")
        };
    }

    private string GetExtension()
    {
        var baseExt = _options.PreferCsv ? ".csv" : ".jsonl";

        if (!_options.Compress)
            return baseExt;

        return _options.CompressionCodec switch
        {
            CompressionCodec.Gzip => $"{baseExt}.gz",
            CompressionCodec.Zstd => $"{baseExt}.zst",
            CompressionCodec.LZ4 => $"{baseExt}.lz4",
            CompressionCodec.Brotli => $"{baseExt}.br",
            _ => $"{baseExt}.gz"
        };
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "_unknown";

        Span<char> buf = stackalloc char[s.Length];
        int j = 0;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '.')
                buf[j++] = char.ToLowerInvariant(ch);
            else
                buf[j++] = '_';
        }
        return new string(buf[..j]);
    }
}

/// <summary>
/// Metadata extracted from a self-documenting filename.
/// </summary>
public sealed record CrystallizedFileMetadata(
    string Symbol,
    string Provider,
    string Category,
    TimeGranularity? Granularity,
    string DatePartition);
