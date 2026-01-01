using System.Text.Json;
using MarketDataCollector.Application.Config;

namespace MarketDataCollector.Application.Backfill;

/// <summary>
/// Persists and reads last backfill status so both the collector and UI can surface progress.
/// </summary>
public sealed class BackfillStatusStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BackfillStatusStore(string dataRoot)
    {
        var root = string.IsNullOrWhiteSpace(dataRoot) ? "data" : dataRoot;
        _path = Path.Combine(root, "_status", "backfill.json");
    }

    public static BackfillStatusStore FromConfig(AppConfig cfg) => new(cfg.DataRoot);

    public async Task WriteAsync(BackfillResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(_path, json);
    }

    public BackfillResult? TryRead()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<BackfillResult>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
