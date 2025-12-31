using System.Text.Json;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// Watches a JSON config file and raises debounced change events with the parsed AppConfig.
/// </summary>
public sealed class ConfigWatcher : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher _fsw;
    private readonly Timer _debounce;
    private readonly object _gate = new();

    private volatile bool _pending;
    private volatile bool _disposed;

    public event Action<AppConfig>? ConfigChanged;
    public event Action<Exception>? Error;

    public ConfigWatcher(string path, TimeSpan? debounce = null)
    {
        _path = Path.GetFullPath(path);

        var dir = Path.GetDirectoryName(_path) ?? ".";
        var file = Path.GetFileName(_path);

        _fsw = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        _fsw.Changed += OnChanged;
        _fsw.Created += OnChanged;
        _fsw.Renamed += OnChanged;

        _debounce = new Timer(_ => Flush(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        DebounceInterval = debounce ?? TimeSpan.FromMilliseconds(350);
    }

    public TimeSpan DebounceInterval { get; }

    public void Start()
    {
        _fsw.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        _fsw.EnableRaisingEvents = false;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;

        lock (_gate)
        {
            _pending = true;
            _debounce.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        if (_disposed) return;

        bool doWork;
        lock (_gate)
        {
            doWork = _pending;
            _pending = false;
        }
        if (!doWork) return;

        try
        {
            // File writes can be non-atomic; retry briefly.
            var cfg = TryLoadWithRetries(_path, attempts: 5, delayMs: 120);
            if (cfg is not null)
                ConfigChanged?.Invoke(cfg);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }

    private static AppConfig? TryLoadWithRetries(string path, int attempts, int delayMs)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (!File.Exists(path)) return new AppConfig();
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read);
                return cfg ?? new AppConfig();
            }
            catch (Exception ex)
            {
                // TODO: Log the specific exception for debugging config load failures
                // Consider logging: ex.GetType().Name, ex.Message, and attempt number
                _ = ex; // Suppress unused variable warning until logging is added
                Thread.Sleep(delayMs);
            }
        }
        return null;
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
        _fsw.Dispose();
        _debounce.Dispose();
    }
}
