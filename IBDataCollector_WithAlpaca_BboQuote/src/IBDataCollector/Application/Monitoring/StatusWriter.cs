using System.Text.Json;
using IBDataCollector.Application.Config;

namespace IBDataCollector.Application.Monitoring;

/// <summary>
/// Periodically writes a small status snapshot JSON file for dashboards.
/// </summary>
public sealed class StatusWriter : IAsyncDisposable
{
    private readonly string _path;
    private readonly Func<AppConfig> _configProvider;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public StatusWriter(string path, Func<AppConfig> configProvider)
    {
        _path = path;
        _configProvider = configProvider;
    }

    public void Start(TimeSpan interval)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _loop = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                await WriteOnceAsync();
                try { await Task.Delay(interval, _cts.Token); }
                catch (TaskCanceledException) { }
            }
        });
    }

    public async Task WriteOnceAsync()
    {
        var cfg = _configProvider();
        var payload = new
        {
            timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            metrics = new { published = Metrics.Published, dropped = Metrics.Dropped, integrity = Metrics.Integrity },
            symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(_path, json);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
        }
        _cts.Dispose();
    }
}
