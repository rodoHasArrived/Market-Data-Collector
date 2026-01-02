using System.Text.Json;
using DataIngestion.QuoteService.Configuration;
using DataIngestion.QuoteService.Models;
using Serilog;

namespace DataIngestion.QuoteService.Services;

public interface IQuoteStorage
{
    Task WriteBatchAsync(IEnumerable<ProcessedQuote> quotes);
    Task FlushAsync();
}

public sealed class JsonlQuoteStorage : IQuoteStorage, IAsyncDisposable
{
    private readonly QuoteServiceConfig _config;
    private readonly ILogger _log = Log.ForContext<JsonlQuoteStorage>();
    private readonly Dictionary<string, StreamWriter> _writers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public JsonlQuoteStorage(QuoteServiceConfig config)
    {
        _config = config;
        var dataDir = config.Storage.DataDirectory;
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
    }

    public async Task WriteBatchAsync(IEnumerable<ProcessedQuote> quotes)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var group in quotes.GroupBy(q => q.Symbol))
            {
                var writer = GetOrCreateWriter(group.Key);
                foreach (var quote in group)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(quote, _jsonOptions));
                }
            }
        }
        finally { _lock.Release(); }
    }

    public async Task FlushAsync()
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var writer in _writers.Values)
                await writer.FlushAsync();
        }
        finally { _lock.Release(); }
    }

    private StreamWriter GetOrCreateWriter(string symbol)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var key = $"{symbol}_{today}";
        if (!_writers.TryGetValue(key, out var writer))
        {
            var safeName = symbol.Replace("/", "_").Replace("\\", "_");
            var path = Path.Combine(_config.Storage.DataDirectory, $"quotes_{safeName}_{today}.jsonl");
            writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write,
                FileShare.Read, 65536, true)) { AutoFlush = false };
            _writers[key] = writer;
        }
        return writer;
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync();
        foreach (var w in _writers.Values) await w.DisposeAsync();
        _writers.Clear();
        _lock.Dispose();
    }
}
