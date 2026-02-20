namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Default archive health service for the shared UI services layer.
/// Platform-specific projects (WPF, UWP) override this with their own implementations
/// by setting the Instance property during app startup.
/// </summary>
public class ArchiveHealthService
{
    private static readonly Lazy<ArchiveHealthService> _instance = new(() => new ArchiveHealthService());

    public static ArchiveHealthService Instance => _instance.Value;
}
