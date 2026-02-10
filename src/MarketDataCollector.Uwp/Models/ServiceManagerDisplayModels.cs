using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Represents a log entry in the log viewer.
/// </summary>
public sealed class LogEntry
{
    public string Timestamp { get; }
    public string Level { get; }
    public string Message { get; }
    public SolidColorBrush LevelColor { get; }

    public LogEntry(string timestamp, string level, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Message = message;
        LevelColor = level switch
        {
            "ERR" => BrushRegistry.Error,
            "WRN" => BrushRegistry.Warning,
            "DBG" => BrushRegistry.Inactive,
            _ => BrushRegistry.Success
        };
    }
}

/// <summary>
/// Represents a recovery event in the history.
/// </summary>
public sealed class RecoveryEvent
{
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = new SolidColorBrush(Colors.Gray);
}
