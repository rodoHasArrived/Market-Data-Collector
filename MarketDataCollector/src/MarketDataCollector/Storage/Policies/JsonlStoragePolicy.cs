using MarketDataCollector.Domain.Events;
using MarketDataCollector.Storage.Interfaces;

namespace MarketDataCollector.Storage.Policies;

public sealed class JsonlStoragePolicy : IStoragePolicy
{
    private readonly string _rootPath;
    private readonly bool _compress;

    public JsonlStoragePolicy(string rootPath, bool compress)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? "data" : rootPath;
        _compress = compress;
    }

    public string GetPath(MarketEvent evt)
    {
        var date = evt.Timestamp.UtcDateTime.ToString("yyyy-MM-dd");
        var type = evt.Type.ToString();
        var symbol = Sanitize(evt.Symbol);

        var ext = _compress ? ".jsonl.gz" : ".jsonl";
        return Path.Combine(_rootPath, type, symbol, $"{date}{ext}");
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "_";
        Span<char> buf = stackalloc char[s.Length];
        int j = 0;
        foreach (var ch in s)
            buf[j++] = char.IsLetterOrDigit(ch) ? ch : '_';
        return new string(buf[..j]);
    }
}
