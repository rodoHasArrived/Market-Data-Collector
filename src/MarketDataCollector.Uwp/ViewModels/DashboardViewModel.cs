using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Uwp.Collections;
using MarketDataCollector.Uwp.Contracts;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.ViewModels;

/// <summary>
/// ViewModel for the Dashboard with real-time updates from all services.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ConnectionService _connectionService;
    private readonly StatusService _statusService;
    private readonly BackgroundTaskSchedulerService _schedulerService;
    private readonly ActivityFeedService _activityFeedService;
    private readonly CollectionSessionService _sessionService;
    private readonly ConfigService _configService;
    private readonly System.Timers.Timer _refreshTimer;
    private bool _disposed;

    // Connection state
    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private int _latencyMs;

    [ObservableProperty]
    private string _uptimeText = "0m 0s";

    // Collector state
    [ObservableProperty]
    private bool _isCollectorRunning;

    [ObservableProperty]
    private bool _isCollectorPaused;

    [ObservableProperty]
    private string _collectorStatusText = "Stopped";

    [ObservableProperty]
    private string _collectorUptimeText = "00:00:00";

    // Metrics
    [ObservableProperty]
    private long _publishedCount;

    [ObservableProperty]
    private long _droppedCount;

    [ObservableProperty]
    private long _integrityCount;

    [ObservableProperty]
    private long _historicalBarsCount;

    [ObservableProperty]
    private double _publishedRate;

    [ObservableProperty]
    private double _currentThroughput;

    [ObservableProperty]
    private double _averageThroughput;

    [ObservableProperty]
    private double _peakThroughput;

    // Stream status
    [ObservableProperty]
    private int _tradesStreamCount;

    [ObservableProperty]
    private int _depthStreamCount;

    [ObservableProperty]
    private int _quotesStreamCount;

    [ObservableProperty]
    private bool _tradesStreamActive;

    [ObservableProperty]
    private bool _depthStreamActive;

    [ObservableProperty]
    private bool _quotesStreamActive;

    // Quick stats
    [ObservableProperty]
    private string _totalEventsToday = "0";

    [ObservableProperty]
    private string _activeSymbolsCount = "0";

    [ObservableProperty]
    private string _storageUsed = "0 MB";

    [ObservableProperty]
    private string _dataQuality = "100%";

    // Data source
    [ObservableProperty]
    private string _selectedDataSource = "IB";

    [ObservableProperty]
    private string _providerDescription = "Interactive Brokers";

    // Running tasks
    [ObservableProperty]
    private int _runningTasksCount;

    [ObservableProperty]
    private int _scheduledTasksCount;

    public ObservableCollection<SymbolViewModel> Symbols { get; } = new();
    public BoundedObservableCollection<ActivityItem> RecentActivities { get; } = new(5);
    public ObservableCollection<RunningTaskInfo> RunningTasks { get; } = new();

    private DateTime _collectorStartTime;

    // Use CircularBuffer for O(1) throughput history operations
    private const int ThroughputHistoryCapacity = 30;
    private readonly CircularBuffer<double> _throughputHistory = new(ThroughputHistoryCapacity);

    public DashboardViewModel()
    {
        _connectionService = ConnectionService.Instance;
        _statusService = StatusService.Instance;
        _schedulerService = BackgroundTaskSchedulerService.Instance;
        _activityFeedService = ActivityFeedService.Instance;
        _sessionService = CollectionSessionService.Instance;
        _configService = ConfigService.Instance;

        // Initialize logging service
        LoggingService.Instance.LogInfo("DashboardViewModel created");

        // Subscribe to connection events
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.LatencyUpdated += OnLatencyUpdated;

        // Subscribe to scheduler events
        _schedulerService.TaskStarted += OnTaskStarted;
        _schedulerService.TaskCompleted += OnTaskCompleted;

        // Subscribe to activity feed
        _activityFeedService.ActivityAdded += OnActivityAdded;

        // Set up refresh timer for periodic updates
        _refreshTimer = new System.Timers.Timer(2000);
        _refreshTimer.Elapsed += OnRefreshTimerElapsed;
    }

    public async Task InitializeAsync()
    {
        await LoadConfigAsync();
        await RefreshStatusAsync();
        UpdateSchedulerInfo();
        LoadRecentActivities();
        _refreshTimer.Start();
    }

    private async Task LoadConfigAsync()
    {
        var config = await _configService.LoadConfigAsync();
        if (config != null)
        {
            SelectedDataSource = config.DataSource ?? "IB";
            UpdateProviderDescription();

            Symbols.Clear();
            if (config.Symbols != null)
            {
                foreach (var symbol in config.Symbols)
                {
                    Symbols.Add(new SymbolViewModel(symbol));
                }
            }
            ActiveSymbolsCount = Symbols.Count.ToString();
        }
    }

    private void UpdateProviderDescription()
    {
        ProviderDescription = SelectedDataSource switch
        {
            "Alpaca" => "Alpaca Markets - WebSocket streaming",
            "Polygon" => "Polygon.io - Real-time data feed",
            "NYSE" => "NYSE Data Services - Direct feed",
            _ => "Interactive Brokers - TWS/Gateway"
        };
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        var status = await _statusService.GetStatusAsync();
        if (status != null)
        {
            IsConnected = status.IsConnected;
            ConnectionStatusText = status.IsConnected ? "Connected" : "Disconnected";

            if (status.Metrics != null)
            {
                PublishedCount = status.Metrics.Published;
                DroppedCount = status.Metrics.Dropped;
                IntegrityCount = status.Metrics.Integrity;
                HistoricalBarsCount = status.Metrics.HistoricalBars;

                // Get previous value before adding new one
                _throughputHistory.TryGetNewest(out var previousValue);

                // Add current value to circular buffer
                _throughputHistory.Add(status.Metrics.Published);

                if (_throughputHistory.Count > 1)
                {
                    var currentValue = _throughputHistory.GetNewest();
                    CurrentThroughput = (currentValue - previousValue) / 2.0; // Per second

                    // Calculate average using CircularBuffer extension method
                    AverageThroughput = _throughputHistory.Average();
                    PeakThroughput = Math.Max(PeakThroughput, CurrentThroughput);
                }

                // Format events today
                TotalEventsToday = status.Metrics.Published >= 1000000
                    ? $"{status.Metrics.Published / 1000000.0:N1}M"
                    : status.Metrics.Published >= 1000
                        ? $"{status.Metrics.Published / 1000.0:N1}K"
                        : status.Metrics.Published.ToString("N0");

                // Calculate data quality
                var total = status.Metrics.Published + status.Metrics.Dropped;
                if (total > 0)
                {
                    var quality = (double)status.Metrics.Published / total * 100;
                    DataQuality = $"{quality:F1}%";
                }
            }
        }
        else
        {
            IsConnected = false;
            ConnectionStatusText = "No Status";
        }
    }

    private void UpdateSchedulerInfo()
    {
        RunningTasksCount = _schedulerService.RunningTasks.Count;
        ScheduledTasksCount = _schedulerService.Tasks.Count(t => t.IsEnabled);

        RunningTasks.Clear();
        foreach (var log in _schedulerService.RunningTasks)
        {
            RunningTasks.Add(new RunningTaskInfo
            {
                TaskId = log.TaskId,
                TaskName = log.TaskName,
                StartedAt = log.StartedAt,
                Status = log.Status
            });
        }
    }

    private void LoadRecentActivities()
    {
        RecentActivities.Clear();
        foreach (var activity in _activityFeedService.Activities.Take(5))
        {
            RecentActivities.Add(activity);
        }
    }

    [RelayCommand]
    private async Task StartCollectorAsync()
    {
        IsCollectorRunning = true;
        IsCollectorPaused = false;
        _collectorStartTime = DateTime.UtcNow;
        CollectorStatusText = "Running";

        TradesStreamActive = true;
        DepthStreamActive = true;

        _activityFeedService.AddActivity(new ActivityItem
        {
            Title = "Collector Started",
            Description = "Market data collection has been started",
            Icon = "\uE768",
            ColorCategory = "Success"
        });

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task StopCollectorAsync()
    {
        IsCollectorRunning = false;
        IsCollectorPaused = false;
        CollectorStatusText = "Stopped";

        TradesStreamActive = false;
        DepthStreamActive = false;
        QuotesStreamActive = false;

        _activityFeedService.AddActivity(new ActivityItem
        {
            Title = "Collector Stopped",
            Description = "Market data collection has been stopped",
            Icon = "\uE71A",
            ColorCategory = "Warning"
        });

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task PauseCollectorAsync()
    {
        if (!IsCollectorRunning) return;

        IsCollectorPaused = !IsCollectorPaused;
        CollectorStatusText = IsCollectorPaused ? "Paused" : "Running";

        _activityFeedService.AddActivity(new ActivityItem
        {
            Title = IsCollectorPaused ? "Collection Paused" : "Collection Resumed",
            Description = IsCollectorPaused
                ? "Market data collection has been paused"
                : "Market data collection has been resumed",
            Icon = IsCollectorPaused ? "\uE769" : "\uE768",
            ColorCategory = "Info"
        });

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddSymbolAsync(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;

        var symbolUpper = symbol.ToUpperInvariant();

        // Check if already exists
        if (Symbols.Any(s => s.Symbol == symbolUpper)) return;

        var config = new SymbolConfig
        {
            Symbol = symbolUpper,
            SubscribeTrades = true,
            SubscribeDepth = true,
            DepthLevels = 10
        };

        await _configService.AddSymbolAsync(config);
        Symbols.Add(new SymbolViewModel(config));
        ActiveSymbolsCount = Symbols.Count.ToString();

        TradesStreamCount++;
        DepthStreamCount++;

        _activityFeedService.AddActivity(new ActivityItem
        {
            Title = "Symbol Added",
            Description = $"{symbolUpper} has been added to watchlist",
            Icon = "\uE710",
            ColorCategory = "Success"
        });
    }

    [RelayCommand]
    private async Task SaveDataSourceAsync()
    {
        await _configService.SaveDataSourceAsync(SelectedDataSource);
        UpdateProviderDescription();

        _activityFeedService.AddActivity(new ActivityItem
        {
            Title = "Provider Changed",
            Description = $"Data source changed to {SelectedDataSource}",
            Icon = "\uE8D4",
            ColorCategory = "Info"
        });
    }

    private void OnConnectionStateChanged(object? sender, Contracts.ConnectionStateEventArgs e)
    {
        IsConnected = e.State == Contracts.ConnectionState.Connected;
        ConnectionStatusText = e.State switch
        {
            Contracts.ConnectionState.Connected => "Connected",
            Contracts.ConnectionState.Connecting => "Connecting...",
            Contracts.ConnectionState.Reconnecting => "Reconnecting...",
            Contracts.ConnectionState.Disconnected => "Disconnected",
            Contracts.ConnectionState.Error => "Error",
            _ => "Unknown"
        };
    }

    private void OnLatencyUpdated(object? sender, int latency)
    {
        LatencyMs = latency;
    }

    private void OnTaskStarted(object? sender, TaskExecutionEventArgs e)
    {
        UpdateSchedulerInfo();
    }

    private void OnTaskCompleted(object? sender, TaskExecutionEventArgs e)
    {
        UpdateSchedulerInfo();
    }

    private void OnActivityAdded(object? sender, ActivityItem e)
    {
        // Prepend to collection - automatically handles capacity limit
        RecentActivities.Prepend(e);
    }

    private void OnRefreshTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Fire-and-forget the async work, with proper exception handling in the async method
        _ = SafeRefreshTimerWorkAsync();
    }

    private async Task SafeRefreshTimerWorkAsync()
    {
        try
        {
            await RefreshStatusAsync();

            // Update collector uptime
            if (IsCollectorRunning)
            {
                var uptime = DateTime.UtcNow - _collectorStartTime;
                CollectorUptimeText = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }

            // Update dashboard uptime
            var dashboardUptime = DateTime.UtcNow - _collectorStartTime;
            UptimeText = dashboardUptime.TotalHours >= 1
                ? $"{(int)dashboardUptime.TotalHours}h {dashboardUptime.Minutes}m"
                : $"{dashboardUptime.Minutes}m {dashboardUptime.Seconds}s";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Error in refresh timer: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _refreshTimer.Stop();
        _refreshTimer.Dispose();

        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _connectionService.LatencyUpdated -= OnLatencyUpdated;
        _schedulerService.TaskStarted -= OnTaskStarted;
        _schedulerService.TaskCompleted -= OnTaskCompleted;
        _activityFeedService.ActivityAdded -= OnActivityAdded;

        _disposed = true;
    }
}

/// <summary>
/// Information about a currently running task.
/// </summary>
public class RunningTaskInfo
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public string Status { get; set; } = string.Empty;

    public string ElapsedTime
    {
        get
        {
            var elapsed = DateTime.UtcNow - StartedAt;
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }
    }
}
