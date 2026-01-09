using System.Collections.ObjectModel;
using System.Text.Json;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for tracking and displaying recent activity in the application.
/// Provides a timeline of events for user awareness.
/// </summary>
public sealed class ActivityFeedService
{
    private const string ActivityLogFileName = "activity_log.json";
    private const int MaxActivities = 100;

    private static ActivityFeedService? _instance;
    private static readonly object _lock = new();

    private readonly string _activityLogPath;
    private readonly ObservableCollection<ActivityItem> _activities;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Gets the singleton instance of the ActivityFeedService.
    /// </summary>
    public static ActivityFeedService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ActivityFeedService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets the observable collection of activities.
    /// </summary>
    public ObservableCollection<ActivityItem> Activities => _activities;

    /// <summary>
    /// Event raised when a new activity is added.
    /// </summary>
    public event EventHandler<ActivityItem>? ActivityAdded;

    private ActivityFeedService()
    {
        var appDir = AppContext.BaseDirectory;
        _activityLogPath = Path.Combine(appDir, "data", "_logs", ActivityLogFileName);
        _activities = new ObservableCollection<ActivityItem>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Load initial activities
        _ = LoadActivitiesAsync();
    }

    /// <summary>
    /// Logs a new activity event.
    /// </summary>
    public async Task LogActivityAsync(
        ActivityType type,
        string title,
        string? description = null,
        string? symbol = null,
        string? provider = null,
        Dictionary<string, object>? metadata = null)
    {
        var activity = new ActivityItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Title = title,
            Description = description,
            Symbol = symbol,
            Provider = provider,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata
        };

        // Add to beginning of collection
        _activities.Insert(0, activity);

        // Trim to max size
        while (_activities.Count > MaxActivities)
        {
            _activities.RemoveAt(_activities.Count - 1);
        }

        // Raise event
        ActivityAdded?.Invoke(this, activity);

        // Persist to disk
        await SaveActivitiesAsync();
    }

    /// <summary>
    /// Logs a collector status change.
    /// </summary>
    public Task LogCollectorStatusAsync(bool isConnected, string? provider = null)
    {
        return LogActivityAsync(
            isConnected ? ActivityType.CollectorStarted : ActivityType.CollectorStopped,
            isConnected ? "Collector Started" : "Collector Stopped",
            isConnected ? $"Data collection has started for {provider ?? "all providers"}" : "Data collection has been stopped",
            provider: provider
        );
    }

    /// <summary>
    /// Logs a backfill operation.
    /// </summary>
    public Task LogBackfillAsync(string[] symbols, string provider, bool success, int barsDownloaded)
    {
        return LogActivityAsync(
            success ? ActivityType.BackfillCompleted : ActivityType.BackfillFailed,
            success ? "Backfill Completed" : "Backfill Failed",
            success
                ? $"Downloaded {barsDownloaded:N0} bars for {symbols.Length} symbols from {provider}"
                : $"Backfill failed for {string.Join(", ", symbols)}",
            provider: provider,
            metadata: new Dictionary<string, object>
            {
                ["symbols"] = symbols,
                ["barsDownloaded"] = barsDownloaded
            }
        );
    }

    /// <summary>
    /// Logs a symbol subscription change.
    /// </summary>
    public Task LogSymbolChangeAsync(string symbol, bool added)
    {
        return LogActivityAsync(
            added ? ActivityType.SymbolAdded : ActivityType.SymbolRemoved,
            added ? "Symbol Added" : "Symbol Removed",
            added ? $"{symbol} has been added to your watchlist" : $"{symbol} has been removed from your watchlist",
            symbol: symbol
        );
    }

    /// <summary>
    /// Logs a data quality event.
    /// </summary>
    public Task LogDataQualityEventAsync(string symbol, string issue, string severity)
    {
        return LogActivityAsync(
            ActivityType.DataQualityIssue,
            $"Data Quality Alert - {symbol}",
            issue,
            symbol: symbol,
            metadata: new Dictionary<string, object>
            {
                ["severity"] = severity
            }
        );
    }

    /// <summary>
    /// Logs a storage event.
    /// </summary>
    public Task LogStorageEventAsync(string message, long bytesAffected = 0)
    {
        return LogActivityAsync(
            ActivityType.StorageEvent,
            "Storage Event",
            message,
            metadata: bytesAffected > 0
                ? new Dictionary<string, object> { ["bytesAffected"] = bytesAffected }
                : null
        );
    }

    /// <summary>
    /// Logs an export operation.
    /// </summary>
    public Task LogExportAsync(string format, string[] symbols, long bytesExported)
    {
        return LogActivityAsync(
            ActivityType.ExportCompleted,
            "Export Completed",
            $"Exported {symbols.Length} symbols to {format.ToUpperInvariant()} format ({FormatBytes(bytesExported)})",
            metadata: new Dictionary<string, object>
            {
                ["format"] = format,
                ["symbols"] = symbols,
                ["bytesExported"] = bytesExported
            }
        );
    }

    /// <summary>
    /// Logs a provider connection event.
    /// </summary>
    public Task LogProviderConnectionAsync(string provider, bool connected, string? message = null)
    {
        return LogActivityAsync(
            connected ? ActivityType.ProviderConnected : ActivityType.ProviderDisconnected,
            connected ? $"{provider} Connected" : $"{provider} Disconnected",
            message,
            provider: provider
        );
    }

    /// <summary>
    /// Gets activities filtered by type.
    /// </summary>
    public IEnumerable<ActivityItem> GetActivitiesByType(ActivityType type)
    {
        return _activities.Where(a => a.Type == type);
    }

    /// <summary>
    /// Gets activities for a specific symbol.
    /// </summary>
    public IEnumerable<ActivityItem> GetActivitiesForSymbol(string symbol)
    {
        return _activities.Where(a =>
            string.Equals(a.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets activities within a time range.
    /// </summary>
    public IEnumerable<ActivityItem> GetActivitiesSince(DateTime since)
    {
        return _activities.Where(a => a.Timestamp >= since);
    }

    /// <summary>
    /// Clears all activities.
    /// </summary>
    public async Task ClearActivitiesAsync()
    {
        _activities.Clear();
        await SaveActivitiesAsync();
    }

    private async Task LoadActivitiesAsync()
    {
        try
        {
            if (File.Exists(_activityLogPath))
            {
                var json = await File.ReadAllTextAsync(_activityLogPath);
                var items = JsonSerializer.Deserialize<List<ActivityItem>>(json, _jsonOptions);
                if (items != null)
                {
                    foreach (var item in items.Take(MaxActivities))
                    {
                        _activities.Add(item);
                    }
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private async Task SaveActivitiesAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_activityLogPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_activities.ToList(), _jsonOptions);
            await File.WriteAllTextAsync(_activityLogPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double value = bytes;
        while (value >= 1024 && i < suffixes.Length - 1)
        {
            value /= 1024;
            i++;
        }
        return $"{value:N1} {suffixes[i]}";
    }
}

/// <summary>
/// Types of activity events.
/// </summary>
public enum ActivityType
{
    // Collector events
    CollectorStarted,
    CollectorStopped,
    CollectorPaused,
    CollectorResumed,

    // Provider events
    ProviderConnected,
    ProviderDisconnected,
    ProviderError,

    // Symbol events
    SymbolAdded,
    SymbolRemoved,
    SymbolSubscribed,
    SymbolUnsubscribed,

    // Data events
    BackfillStarted,
    BackfillCompleted,
    BackfillFailed,
    BackfillProgress,

    // Quality events
    DataQualityIssue,
    GapDetected,
    GapRepaired,
    IntegrityError,

    // Storage events
    StorageEvent,
    ArchiveCreated,
    ArchiveVerified,
    CompressionCompleted,

    // Export events
    ExportStarted,
    ExportCompleted,
    ExportFailed,

    // System events
    SystemStarted,
    SystemStopped,
    ConfigurationChanged,
    Error,
    Warning,
    Info
}

/// <summary>
/// Individual activity item.
/// </summary>
public class ActivityItem
{
    public string Id { get; set; } = string.Empty;
    public ActivityType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Symbol { get; set; }
    public string? Provider { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets the icon glyph for this activity type.
    /// </summary>
    public string Icon => Type switch
    {
        ActivityType.CollectorStarted => "\uE768",
        ActivityType.CollectorStopped => "\uE71A",
        ActivityType.CollectorPaused => "\uE769",
        ActivityType.CollectorResumed => "\uE768",
        ActivityType.ProviderConnected => "\uE703",
        ActivityType.ProviderDisconnected => "\uE8CD",
        ActivityType.ProviderError => "\uE783",
        ActivityType.SymbolAdded => "\uE710",
        ActivityType.SymbolRemoved => "\uE74D",
        ActivityType.SymbolSubscribed => "\uE8AB",
        ActivityType.SymbolUnsubscribed => "\uE8D8",
        ActivityType.BackfillStarted => "\uE787",
        ActivityType.BackfillCompleted => "\uE73E",
        ActivityType.BackfillFailed => "\uE783",
        ActivityType.BackfillProgress => "\uE895",
        ActivityType.DataQualityIssue => "\uE7BA",
        ActivityType.GapDetected => "\uE946",
        ActivityType.GapRepaired => "\uE73E",
        ActivityType.IntegrityError => "\uE783",
        ActivityType.StorageEvent => "\uE8B7",
        ActivityType.ArchiveCreated => "\uE8F1",
        ActivityType.ArchiveVerified => "\uE73E",
        ActivityType.CompressionCompleted => "\uE8AA",
        ActivityType.ExportStarted => "\uEDE1",
        ActivityType.ExportCompleted => "\uE73E",
        ActivityType.ExportFailed => "\uE783",
        ActivityType.SystemStarted => "\uE7F4",
        ActivityType.SystemStopped => "\uE7F5",
        ActivityType.ConfigurationChanged => "\uE713",
        ActivityType.Error => "\uE783",
        ActivityType.Warning => "\uE7BA",
        ActivityType.Info => "\uE946",
        _ => "\uE946"
    };

    /// <summary>
    /// Gets the color category for this activity type.
    /// </summary>
    public string ColorCategory => Type switch
    {
        ActivityType.CollectorStarted or ActivityType.CollectorResumed or ActivityType.ProviderConnected
            or ActivityType.SymbolAdded or ActivityType.SymbolSubscribed or ActivityType.BackfillCompleted
            or ActivityType.GapRepaired or ActivityType.ArchiveVerified or ActivityType.ExportCompleted
            or ActivityType.SystemStarted => "Success",

        ActivityType.CollectorStopped or ActivityType.CollectorPaused or ActivityType.ProviderDisconnected
            or ActivityType.SymbolRemoved or ActivityType.SymbolUnsubscribed or ActivityType.SystemStopped => "Neutral",

        ActivityType.ProviderError or ActivityType.BackfillFailed or ActivityType.IntegrityError
            or ActivityType.ExportFailed or ActivityType.Error => "Error",

        ActivityType.DataQualityIssue or ActivityType.GapDetected or ActivityType.Warning => "Warning",

        _ => "Info"
    };

    /// <summary>
    /// Gets the relative time string (e.g., "5 minutes ago").
    /// </summary>
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - Timestamp;
            if (diff.TotalSeconds < 60) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return Timestamp.ToString("MMM d");
        }
    }
}
