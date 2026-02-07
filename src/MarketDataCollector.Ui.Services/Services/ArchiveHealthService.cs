namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default archive health service for the shared UI services layer.
/// Platform-specific projects (WPF, UWP) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public class ArchiveHealthService
{
    private static ArchiveHealthService? _instance;
    private static readonly object _lock = new();

    public static ArchiveHealthService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ArchiveHealthService();
                }
            }
            return _instance;
        }
        set
        {
            lock (_lock)
            {
                _instance = value;
            }
        }
    }
}
