namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default schema service for the shared UI services layer.
/// Platform-specific projects (WPF) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public sealed class SchemaService : SchemaServiceBase
{
    private static SchemaService? _instance;
    private static readonly object _lock = new();

    public static SchemaService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SchemaService();
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
