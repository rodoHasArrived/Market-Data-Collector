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

    public JsonlStoragePolicy(StorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
        var ext = _options.Compress ? ".jsonl.gz" : ".jsonl";
        var prefix = string.IsNullOrWhiteSpace(_options.FilePrefix) ? "" : $"{_options.FilePrefix}_";

        // Build path based on naming convention
        return _options.NamingConvention switch
        {
            FileNamingConvention.Flat => BuildFlatPath(root, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.BySymbol => BuildBySymbolPath(root, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.ByDate => BuildByDatePath(root, symbol, type, dateStr, prefix, ext),
            FileNamingConvention.ByType => BuildByTypePath(root, symbol, type, dateStr, prefix, ext),
            _ => BuildBySymbolPath(root, symbol, type, dateStr, prefix, ext)
        };
    }

    /// <summary>
    /// Gets a preview of the file path pattern for display purposes.
    /// </summary>
    public string GetPathPreview()
    {
        var root = string.IsNullOrWhiteSpace(_options.RootPath) ? "data" : _options.RootPath;
        var ext = _options.Compress ? ".jsonl.gz" : ".jsonl";
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
            _ => $"{root}/AAPL/Trade/{prefix}{dateExample}{ext}"
        };
    }

    private string BuildFlatPath(string root, string symbol, string type, string dateStr, string prefix, string ext)
    {
        // Flat: {root}/{prefix}{symbol}_{type}_{date}.jsonl
        var fileName = string.IsNullOrEmpty(dateStr)
            ? $"{prefix}{symbol}_{type}{ext}"
            : $"{prefix}{symbol}_{type}_{dateStr}{ext}";
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
