using System.Diagnostics;

namespace MarketDataCollector.Wpf.Services;

public sealed class LoggingService : ILoggingService
{
    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Debug.WriteLine($"[{timestamp}] [INFO] {message}");
    }

    public void LogError(string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Debug.WriteLine($"[{timestamp}] [ERROR] {message}");
        if (exception != null)
        {
            Debug.WriteLine($"[{timestamp}] [ERROR] Exception: {exception}");
        }
    }

    public void LogWarning(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Debug.WriteLine($"[{timestamp}] [WARN] {message}");
    }

    public void LogDebug(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Debug.WriteLine($"[{timestamp}] [DEBUG] {message}");
    }
}
