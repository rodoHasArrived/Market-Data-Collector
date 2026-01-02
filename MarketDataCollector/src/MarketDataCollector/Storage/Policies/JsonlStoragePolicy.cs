using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage.Interfaces;

namespace MarketDataCollector.Storage.Policies;

/// <summary>
/// Storage policy that generates file paths based on configurable naming conventions.
/// Supports multiple directory structures and date partitioning strategies.
/// </summary>
public sealed class JsonlStoragePolicy : IStoragePolicy
{
    private readonly StorageOptions _options;
    private readonly ISourceRegistry? _sourceRegistry;

    public JsonlStoragePolicy(StorageOptions options, ISourceRegistry? sourceRegistry = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sourceRegistry = sourceRegistry;
    }

    /// <summary>
    /// Generates the file path for a market event based on configured naming convention.
    /// </summary>
    public string GetPath(MarketEvent evt)
    {
        var root = string.IsNullOrWhiteSpace(_options.RootPath) ? "data" : _options.RootPath;
        var symbol = Sanitize(evt.Symbol);
        var type = evt.Type.ToString();
        var dateStr = FormatDate(evt.Timestamp.UtcDateTime);
        var ext = GetExtension();
        var prefix = string.IsNullOrWhiteSpace(_options.FilePrefix) ? "" : $"{_options.FilePrefix}_";
        var source = Sanitize(evt.Source);
        var assetClass = GetAssetClass(evt.Symbol);

        // Build path based on naming convention
        return _options.NamingConvention switch
        {
            FileNamingConvention.Flat => BuildFlatPath(root, symbol, type, dateStr, prefix, ext, source),
            FileNamingConvention.BySymbol => BuildBySymbolPath(root, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.ByDate => BuildByDatePath(root, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.ByType => BuildByTypePath(root, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.BySource => BuildBySourcePath(root, source, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.ByAssetClass => BuildByAssetClassPath(root, assetClass, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.Hierarchical => BuildHierarchicalPath(root, source, assetClass, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.Canonical => BuildCanonicalPath(root, evt.Timestamp.UtcDateTime, source, symbol, type, prefix, ext),
            _ => BuildBySymbolPath(root, symbol, type, dateStr, prefix, ext)
        };
    }

    /// <summary>
    /// Gets a preview of the file path pattern for display purposes.
    /// </summary>
    public string GetPathPreview()
    {
        var root = string.IsNullOrWhiteSpace(_options.RootPath) ? "data" : _options.RootPath;
        var ext = GetExtension();
        var prefix = string.IsNullOrWhiteSpace(_options.FilePrefix) ? "" : $"{_options.FilePrefix}_";
        var dateExample = _options.DatePartition switch
        {
            DatePartition.None => "",
            DatePartition.Hourly => "2024-01-15_14",
            DatePartition.Monthly => "2024-01",
            _ => "2024-01-15"
        };

        return _options.NamingConvention switch
        {
            FileNamingConvention.Flat => string.IsNullOrEmpty(dateExample)
                ? $"{root}/{prefix}AAPL_Trade{ext}"
                : $"{root}/{prefix}AAPL_Trade_{dateExample}{ext}",
            FileNamingConvention.BySymbol => string.IsNullOrEmpty(dateExample)
                ? $"{root}/AAPL/Trade/{prefix}data{ext}"
                : $"{root}/AAPL/Trade/{prefix}{dateExample}{ext}",
            FileNamingConvention.ByDate => string.IsNullOrEmpty(dateExample)
                ? $"{root}/AAPL/{prefix}Trade{ext}"
                : $"{root}/{dateExample}/AAPL/{prefix}Trade{ext}",
            FileNamingConvention.ByType => string.IsNullOrEmpty(dateExample)
                ? $"{root}/Trade/AAPL/{prefix}data{ext}"
                : $"{root}/Trade/AAPL/{prefix}{dateExample}{ext}",
            FileNamingConvention.BySource => string.IsNullOrEmpty(dateExample)
                ? $"{root}/alpaca/AAPL/Trade/{prefix}data{ext}"
                : $"{root}/alpaca/AAPL/Trade/{prefix}{dateExample}{ext}",
            FileNamingConvention.ByAssetClass => string.IsNullOrEmpty(dateExample)
                ? $"{root}/equity/AAPL/Trade/{prefix}data{ext}"
                : $"{root}/equity/AAPL/Trade/{prefix}{dateExample}{ext}",
            FileNamingConvention.Hierarchical => string.IsNullOrEmpty(dateExample)
                ? $"{root}/alpaca/equity/AAPL/Trade/{prefix}data{ext}"
                : $"{root}/alpaca/equity/AAPL/Trade/{prefix}{dateExample}{ext}",
            FileNamingConvention.Canonical => $"{root}/2024/01/15/alpaca/AAPL/{prefix}Trade{ext}",
            _ => $"{root}/AAPL/Trade/{prefix}{dateExample}{ext}"
        };
    }

    private string GetExtension()
    {
        if (!_options.Compress) return ".jsonl";

        return _options.CompressionCodec switch
        {
            CompressionCodec.Gzip => ".jsonl.gz",
            CompressionCodec.Zstd => ".jsonl.zst",
            CompressionCodec.LZ4 => ".jsonl.lz4",
            CompressionCodec.Brotli => ".jsonl.br",
            _ => ".jsonl.gz"
        };
    }

    private string GetAssetClass(string symbol)
    {
        // Try to get asset class from source registry
        if (_sourceRegistry != null)
        {
            var symbolInfo = _sourceRegistry.GetSymbolInfo(symbol);
            if (symbolInfo?.AssetClass != null)
                return Sanitize(symbolInfo.AssetClass);
        }

        // Default to equity
        return "equity";
    }

    private string BuildFlatPath(string root, string symbol, string type, string dateStr, string prefix, string ext, string source)
    {
        // Flat: {root}/{prefix}{symbol}_{type}_{date}[_{source}].jsonl
        var sourceSegment = _options.IncludeProvider ? $"_{source}" : "";
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}{symbol}_{type}{sourceSegment}{ext}"
            : $"{prefix}{symbol}_{type}_{dateStr}{sourceSegment}{ext}";
        return Path.Combine(root, fileName);
    }

    private string BuildBySymbolPath(string root, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // BySymbol: {root}/{symbol}/{type}/{prefix}{date}.jsonl
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}data{ext}"
            : $"{prefix}{dateStr}{ext}";
        return Path.Combine(root, symbol, type, fileName);
    }

    private string BuildByDatePath(string root, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // ByDate: {root}/{date}/{symbol}/{prefix}{type}.jsonl
        if (string.IsNullOrEmpty(dateStr))
        {
            // No date partition - put directly under symbol
            return Path.Combine(root, symbol, $"{prefix}{type}{ext}");
        }
        return Path.Combine(root, dateStr, symbol, $"{prefix}{type}{ext}");
    }

    private string BuildByTypePath(string root, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // ByType: {root}/{type}/{symbol}/{prefix}{date}.jsonl
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}data{ext}"
            : $"{prefix}{dateStr}{ext}";
        return Path.Combine(root, type, symbol, fileName);
    }

    private string BuildBySourcePath(string root, string source, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // BySource: {root}/{source}/{symbol}/{type}/{prefix}{date}.jsonl
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}data{ext}"
            : $"{prefix}{dateStr}{ext}";
        return Path.Combine(root, source, symbol, type, fileName);
    }

    private string BuildByAssetClassPath(string root, string assetClass, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // ByAssetClass: {root}/{asset_class}/{symbol}/{type}/{prefix}{date}.jsonl
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}data{ext}"
            : $"{prefix}{dateStr}{ext}";
        return Path.Combine(root, assetClass, symbol, type, fileName);
    }

    private string BuildHierarchicalPath(string root, string source, string assetClass, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // Hierarchical: {root}/{source}/{asset_class}/{symbol}/{type}/{prefix}{date}.jsonl
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}data{ext}"
            : $"{prefix}{dateStr}{ext}";
        return Path.Combine(root, source, assetClass, symbol, type, fileName);
    }

    private string BuildCanonicalPath(string root, DateTime utc, string source, string symbol, string type, string prefix, string ext)
    {
        // Canonical: {root}/{year}/{month}/{day}/{source}/{symbol}/{prefix}{type}.jsonl
        var year = utc.Year.ToString("D4");
        var month = utc.Month.ToString("D2");
        var day = utc.Day.ToString("D2");
        return Path.Combine(root, year, month, day, source, symbol, $"{prefix}{type}{ext}");
    }

    private string FormatDate(DateTime utc)
    {
        return _options.DatePartition switch
        {
            DatePartition.None => "",
            DatePartition.Hourly => utc.ToString("yyyy-MM-dd_HH"),
            DatePartition.Monthly => utc.ToString("yyyy-MM"),
            DatePartition.Daily => utc.ToString("yyyy-MM-dd"),
            _ => utc.ToString("yyyy-MM-dd")
        };
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "_unknown";
        Span<char> buf = stackalloc char[s.Length];
        int j = 0;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '.')
                buf[j++] = ch;
            else
                buf[j++] = '_';
        }
        return new string(buf[..j]);
    }
}
