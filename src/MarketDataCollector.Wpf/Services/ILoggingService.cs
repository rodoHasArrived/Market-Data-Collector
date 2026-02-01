namespace MarketDataCollector.Wpf.Services;

public interface ILoggingService
{
    void Log(string message);
    void LogError(string message, Exception? exception = null);
    void LogWarning(string message);
    void LogDebug(string message);
}
