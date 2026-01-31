# MarketDataCollector.UWP Simplification Recommendations

This document provides actionable recommendations to simplify the UWP codebase while preserving all existing functionality. All suggestions leverage existing dependencies.

---

## 1. Singleton Service Pattern (High Impact - 67 services)

### Current Pattern
Every service repeats ~15 lines of identical boilerplate:

```csharp
// StatusService.cs:24-42
public sealed class StatusService : IStatusService
{
    private static StatusService? _instance;
    private static readonly object _lock = new();

    public static StatusService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new StatusService();
                }
            }
            return _instance;
        }
    }
    // ... rest of implementation
}
```

### Simplified Approach
Use `Lazy<T>` for thread-safe lazy initialization (built into .NET):

```csharp
public sealed class StatusService : IStatusService
{
    private static readonly Lazy<StatusService> _lazy = new(() => new StatusService());
    public static StatusService Instance => _lazy.Value;

    private StatusService()
    {
        _apiClient = ApiClientService.Instance;
    }
    // ... rest of implementation
}
```

### Functionality Preserved
- Thread-safe singleton instantiation
- Lazy initialization (only created when first accessed)
- All existing `Service.Instance` call sites continue working

### Expected Benefit
- **~800 lines removed** across 67 services (12 lines per service)
- Clearer intent with declarative `Lazy<T>`
- No lock object to maintain
- Guaranteed initialization-time exception handling

---

## 2. Value Converters (Medium Impact)

### Current Pattern
`Converters/BoolConverters.cs` defines 5 converters with 128 lines:

```csharp
// BoolConverters.cs:11-30
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            return v == Visibility.Visible;
        }
        return false;
    }
}
```

### Simplified Approach
CommunityToolkit.WinUI.Controls (already in dependencies) includes converters. For x:Bind, use function binding:

```xml
<!-- In XAML -->
xmlns:converters="using:CommunityToolkit.WinUI.Converters"

<Page.Resources>
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
    <converters:BoolNegationConverter x:Key="InverseBool"/>
</Page.Resources>
```

Or use `x:Bind` with functions (zero-allocation, faster):
```xml
<!-- Direct function binding eliminates converter objects -->
<TextBlock Visibility="{x:Bind local:Converters.ToVisibility(IsConnected), Mode=OneWay}"/>
```

```csharp
// Static converter functions (compile-time type safety)
public static class Converters
{
    public static Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToVisibilityInverse(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static string ToYesNo(bool value) => value ? "Yes" : "No";
}
```

### Functionality Preserved
- All boolean-to-visibility conversions work identically
- Two-way binding (where needed) still supported

### Expected Benefit
- **~100 lines removed** (custom converters file)
- Compile-time type checking with x:Bind functions
- Better performance (no boxing, no reflection)

---

## 3. CommunityToolkit.Mvvm IMessenger (Medium Impact)

### Current Pattern
Services use events for cross-component communication with manual subscription management:

```csharp
// DashboardViewModel.cs:144-158
public DashboardViewModel()
{
    // Subscribe to connection events
    _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
    _connectionService.LatencyUpdated += OnLatencyUpdated;
    _schedulerService.TaskStarted += OnTaskStarted;
    _schedulerService.TaskCompleted += OnTaskCompleted;
    _activityFeedService.ActivityAdded += OnActivityAdded;
    // ...
}

public void Dispose()
{
    // Manual unsubscription required
    _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
    _connectionService.LatencyUpdated -= OnLatencyUpdated;
    // ... 5 more unsubscriptions
}
```

### Simplified Approach
CommunityToolkit.Mvvm includes `IMessenger` (WeakReferenceMessenger) - already in your dependencies:

```csharp
// Define messages
public sealed record ConnectionStateMessage(ConnectionState State, string Provider);
public sealed record LatencyUpdatedMessage(int LatencyMs);
public sealed record ActivityAddedMessage(ActivityItem Activity);

// In ConnectionService - publish instead of raising events
public class ConnectionService
{
    private void UpdateState(ConnectionState newState)
    {
        _currentState = newState;
        WeakReferenceMessenger.Default.Send(new ConnectionStateMessage(newState, _currentProvider));
    }
}

// In DashboardViewModel - subscribe with auto-cleanup
public sealed partial class DashboardViewModel : ObservableRecipient, IDisposable
{
    public DashboardViewModel()
    {
        // Single line enables all message handlers
        IsActive = true;
    }

    // Auto-discovered and registered when IsActive = true
    [RelayCommand]
    private void Receive(ConnectionStateMessage message)
    {
        IsConnected = message.State == ConnectionState.Connected;
        ConnectionStatusText = message.State.ToString();
    }

    [RelayCommand]
    private void Receive(LatencyUpdatedMessage message)
    {
        LatencyMs = message.LatencyMs;
    }
}
```

### Functionality Preserved
- All inter-component communication works identically
- State changes propagate to all subscribers

### Expected Benefit
- **No manual subscription/unsubscription** (automatic via ObservableRecipient)
- No memory leak risk from forgotten unsubscription
- Looser coupling between components
- Type-safe messages with records

---

## 4. Duplicated Utility Methods

### Current Pattern
`FormatBytes` is implemented identically in two places:

```csharp
// NotificationService.cs:359-370
private static string FormatBytes(long bytes)
{
    string[] sizes = { "B", "KB", "MB", "GB", "TB" };
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
        order++;
        len /= 1024;
    }
    return $"{len:F1} {sizes[order]}";
}

// ActivityFeedService.cs:312-324 - identical implementation
private static string FormatBytes(long bytes) { /* same code */ }
```

### Simplified Approach
Create a shared `Formatters` utility class:

```csharp
// Helpers/Formatters.cs
namespace MarketDataCollector.Uwp.Helpers;

public static class Formatters
{
    private static readonly string[] ByteSuffixes = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatBytes(long bytes)
    {
        var len = (double)bytes;
        var order = 0;
        while (len >= 1024 && order < ByteSuffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F1} {ByteSuffixes[order]}";
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.Hours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    public static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return timestamp.ToString("MMM d");
    }

    public static string FormatCount(long count) => count switch
    {
        >= 1_000_000 => $"{count / 1_000_000.0:N1}M",
        >= 1_000 => $"{count / 1_000.0:N1}K",
        _ => count.ToString("N0")
    };
}
```

### Functionality Preserved
- All formatting behaves identically
- Same output format

### Expected Benefit
- **Single source of truth** for formatting logic
- Easier to update formatting rules
- Also consolidates `FormatDuration` (found in NotificationService) and `FormatRelativeTime` (found in ActivityItem)

---

## 5. MainViewModel Service Instantiation

### Current Pattern
`MainViewModel.cs:57-61` creates new service instances instead of using singletons:

```csharp
// MainViewModel.cs:57-61
public MainViewModel()
{
    _configService = new ConfigService();  // Creates new instance!
    _statusService = new StatusService();  // Creates new instance!
}
```

### Simplified Approach
Use the established singleton pattern:

```csharp
public MainViewModel()
{
    _configService = ConfigService.Instance;
    _statusService = StatusService.Instance;
}
```

### Functionality Preserved
- All configuration and status operations work identically
- Same API surface

### Expected Benefit
- Consistent with all other ViewModels (DashboardViewModel uses singletons correctly)
- Shared state across the application
- No duplicate service instances consuming memory

---

## 6. CircularBuffer LINQ Extensions

### Current Pattern
`CircularBuffer.cs:204-276` implements Sum, Average, Max, Min manually:

```csharp
// CircularBuffer.cs:209-217
public static double Sum(this CircularBuffer<double> buffer)
{
    var sum = 0.0;
    for (var i = 0; i < buffer.Count; i++)
    {
        sum += buffer[i];
    }
    return sum;
}
```

### Simplified Approach
Since `CircularBuffer<T>` implements `IEnumerable<T>`, use LINQ directly:

```csharp
public static class CircularBufferExtensions
{
    // All standard LINQ operations work via IEnumerable<T>
    // Only keep custom operations not available in LINQ:

    public static double CalculateRate(this CircularBuffer<double> buffer, double intervalSeconds = 1.0)
    {
        if (buffer.Count < 2 || intervalSeconds <= 0) return 0.0;

        if (buffer.TryGetFromNewest(0, out var newest) &&
            buffer.TryGetFromNewest(1, out var previous))
        {
            return (newest!.Value - previous!.Value) / intervalSeconds;
        }
        return 0.0;
    }
}

// Usage remains the same:
var sum = _throughputHistory.Sum();           // Uses LINQ
var avg = _throughputHistory.Average();       // Uses LINQ
var max = _throughputHistory.Max();           // Uses LINQ
var rate = _throughputHistory.CalculateRate(); // Custom extension
```

### Functionality Preserved
- All numeric operations return identical results
- Same call sites, same behavior

### Expected Benefit
- **~50 lines removed** from CircularBufferExtensions
- LINQ is well-tested and optimized
- Only keep truly custom logic (CalculateRate)

---

## 7. RelayCommand CancellationToken Support

### Current Pattern
Async commands don't utilize CancellationToken:

```csharp
// DashboardViewModel.cs:200-201
[RelayCommand]
private async Task RefreshStatusAsync()
{
    var status = await _statusService.GetStatusAsync();
    // ...
}
```

### Simplified Approach
CommunityToolkit.Mvvm automatically passes CancellationToken to async commands:

```csharp
[RelayCommand]
private async Task RefreshStatusAsync(CancellationToken ct)
{
    var status = await _statusService.GetStatusAsync(ct);
    // Cancellation automatically handled when command is cancelled
}
```

### Functionality Preserved
- Same async behavior
- Commands still work identically

### Expected Benefit
- Proper cancellation support for long-running operations
- Automatic cleanup when user navigates away
- Consistent with ADR-004 (CancellationToken on all async methods)

---

## 8. View Event Handler Cleanup Pattern

### Current Pattern
Views manually manage event subscriptions:

```csharp
// DashboardPage.xaml.cs:130-144
private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
{
    _unifiedTimer.Stop();
    _unifiedTimer.Tick -= UnifiedTimer_Tick;
    _infoDismissCts?.Cancel();
    _infoDismissCts?.Dispose();
    _activityFeedService.ActivityAdded -= ActivityFeedService_ActivityAdded;
    _integrityEventsService.EventRecorded -= IntegrityEventsService_EventRecorded;
    _integrityEventsService.EventsCleared -= IntegrityEventsService_EventsCleared;
}
```

### Simplified Approach
Use a disposable subscription pattern or move to IMessenger (Recommendation #3). Alternative using CompositeDisposable pattern:

```csharp
public sealed partial class DashboardPage : Page, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();

    public DashboardPage()
    {
        InitializeComponent();

        // Subscribe with automatic tracking
        _subscriptions.Add(
            _activityFeedService.Subscribe(e => ActivityFeedService_ActivityAdded(e)));
        _subscriptions.Add(
            _integrityEventsService.SubscribeRecorded(e => IntegrityEventsService_EventRecorded(e)));
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _subscriptions.Dispose(); // Cleans up all subscriptions
    }
}
```

Or better yet, with IMessenger from Recommendation #3, no cleanup is needed at all.

### Functionality Preserved
- All event handlers still called
- Cleanup still happens on Unload

### Expected Benefit
- Less error-prone cleanup code
- Harder to forget to unsubscribe
- Clearer intent

---

## Implementation Priority

| # | Recommendation | Impact | Effort | Lines Saved |
|---|---------------|--------|--------|-------------|
| 1 | Lazy<T> Singletons | High | Low | ~800 |
| 4 | Shared Formatters | Medium | Low | ~50 |
| 5 | MainViewModel Singletons | Low | Trivial | ~4 |
| 6 | CircularBuffer LINQ | Low | Low | ~50 |
| 7 | CancellationToken | Medium | Low | 0 (quality) |
| 2 | x:Bind Converters | Medium | Medium | ~100 |
| 3 | IMessenger | High | Medium | ~200 |
| 8 | Subscription Cleanup | Medium | Medium | ~50 |

**Recommended order**: Start with #1, #4, #5, #6 (low effort, high/medium impact), then tackle #2, #3, #7, #8.

---

## Summary

Total potential reduction: **~1,250 lines** of boilerplate and duplicated code while:
- Preserving all functionality
- Using only existing dependencies
- Improving maintainability and type safety
