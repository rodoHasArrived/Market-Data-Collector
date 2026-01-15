using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.ViewModels;

/// <summary>
/// ViewModel for the BackfillPage with real-time progress updates.
/// </summary>
public sealed partial class BackfillViewModel : ObservableObject, IDisposable
{
    private readonly BackfillService _backfillService;
    private readonly ConfigService _configService;
    private readonly BackgroundTaskSchedulerService _schedulerService;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBackfillRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _symbolsText = string.Empty;

    [ObservableProperty]
    private string _selectedProvider = "stooq";

    [ObservableProperty]
    private DateTimeOffset? _fromDate;

    [ObservableProperty]
    private DateTimeOffset? _toDate;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _elapsedTime = "00:00";

    [ObservableProperty]
    private int _completedSymbols;

    [ObservableProperty]
    private int _totalSymbols;

    [ObservableProperty]
    private long _totalBarsDownloaded;

    [ObservableProperty]
    private BackfillStatus? _lastStatus;

    [ObservableProperty]
    private bool _hasLastStatus;

    // Statistics
    [ObservableProperty]
    private string _totalBarsText = "0";

    [ObservableProperty]
    private string _symbolsWithDataText = "0";

    [ObservableProperty]
    private string _dateCoverageText = "N/A";

    [ObservableProperty]
    private string _lastSuccessfulText = "Never";

    public ObservableCollection<SymbolBackfillProgress> SymbolProgress { get; } = new();
    public ObservableCollection<BackfillHistoryEntry> History { get; } = new();
    public ObservableCollection<ScheduledBackfillJob> ScheduledJobs { get; } = new();
    public ObservableCollection<DataValidationIssue> ValidationIssues { get; } = new();

    public ObservableCollection<string> Providers { get; } = new()
    {
        "stooq", "yahoo", "tiingo", "alphavantage", "finnhub", "polygon", "alpaca", "nasdaq"
    };

    public BackfillViewModel()
    {
        _backfillService = BackfillService.Instance;
        _configService = ConfigService.Instance;
        _schedulerService = BackgroundTaskSchedulerService.Instance;

        // Subscribe to scheduler events for backfill tasks
        _schedulerService.TaskStarted += OnTaskStarted;
        _schedulerService.TaskCompleted += OnTaskCompleted;

        // Set default dates
        ToDate = DateTimeOffset.Now;
        FromDate = DateTimeOffset.Now.AddDays(-30);
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await LoadLastStatusAsync();
            await LoadHistoryAsync();
            await LoadScheduledJobsAsync();
            await LoadStatisticsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLastStatusAsync()
    {
        var status = await _backfillService.GetLastStatusAsync();
        LastStatus = status;
        HasLastStatus = status != null;

        if (status != null)
        {
            LastSuccessfulText = status.Success
                ? GetRelativeTime(status.CompletedUtc)
                : "Failed: " + GetRelativeTime(status.CompletedUtc);
        }
    }

    private Task LoadHistoryAsync()
    {
        History.Clear();
        // Load from persistence or service
        // For now, add sample data that will be replaced with real data
        return Task.CompletedTask;
    }

    private async Task LoadScheduledJobsAsync()
    {
        ScheduledJobs.Clear();
        var tasks = _schedulerService.Tasks
            .Where(t => t.TaskType == ScheduledTaskType.RunBackfill && t.IsEnabled)
            .ToList();

        foreach (var task in tasks)
        {
            ScheduledJobs.Add(new ScheduledBackfillJob
            {
                Id = task.Id,
                Name = task.Name,
                NextRun = task.NextRunAt?.ToString("g") ?? "Not scheduled",
                IsEnabled = task.IsEnabled
            });
        }

        await Task.CompletedTask;
    }

    private Task LoadStatisticsAsync()
    {
        // Statistics would be loaded from storage analytics service
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddAllSubscribedSymbolsAsync()
    {
        var config = await _configService.LoadConfigAsync();
        if (config?.Symbols != null && config.Symbols.Count > 0)
        {
            SymbolsText = string.Join(",", config.Symbols.Select(s => s.Symbol));
        }
    }

    [RelayCommand]
    private void AddMajorETFs()
    {
        SymbolsText = "SPY,QQQ,IWM,DIA,VTI";
    }

    [RelayCommand]
    private void SetDateRange(string range)
    {
        ToDate = DateTimeOffset.Now;
        FromDate = range switch
        {
            "30" => DateTimeOffset.Now.AddDays(-30),
            "90" => DateTimeOffset.Now.AddDays(-90),
            "ytd" => new DateTimeOffset(DateTimeOffset.Now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "1y" => DateTimeOffset.Now.AddYears(-1),
            "5y" => DateTimeOffset.Now.AddYears(-5),
            _ => DateTimeOffset.Now.AddDays(-30)
        };
    }

    [RelayCommand]
    private async Task StartBackfillAsync()
    {
        if (string.IsNullOrWhiteSpace(SymbolsText))
        {
            StatusText = "Please enter at least one symbol";
            return;
        }

        var symbols = SymbolsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .ToArray();

        if (symbols.Length == 0)
        {
            StatusText = "Please enter valid symbols";
            return;
        }

        IsBackfillRunning = true;
        IsPaused = false;
        _cts = new CancellationTokenSource();

        // Initialize progress tracking
        SymbolProgress.Clear();
        foreach (var symbol in symbols)
        {
            SymbolProgress.Add(new SymbolBackfillProgress { Symbol = symbol });
        }

        TotalSymbols = symbols.Length;
        CompletedSymbols = 0;
        TotalBarsDownloaded = 0;
        OverallProgress = 0;
        StatusText = "Starting backfill...";

        var startTime = DateTime.UtcNow;

        try
        {
            var fromDateStr = FromDate?.ToString("yyyy-MM-dd");
            var toDateStr = ToDate?.ToString("yyyy-MM-dd");

            // Process symbols
            for (var i = 0; i < symbols.Length; i++)
            {
                if (_cts.Token.IsCancellationRequested) break;

                while (IsPaused && !_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                var symbol = symbols[i];
                var progress = SymbolProgress[i];

                progress.Status = SymbolBackfillStatus.Running;
                StatusText = $"Downloading {symbol}...";

                try
                {
                    var result = await _backfillService.RunBackfillAsync(
                        new[] { symbol },
                        SelectedProvider,
                        fromDateStr,
                        toDateStr,
                        _cts.Token);

                    if (result?.Success == true)
                    {
                        progress.Status = SymbolBackfillStatus.Completed;
                        progress.BarsDownloaded = result.BarsWritten;
                        TotalBarsDownloaded += result.BarsWritten;
                    }
                    else
                    {
                        progress.Status = SymbolBackfillStatus.Failed;
                        progress.ErrorMessage = result?.Error ?? "Unknown error";
                    }
                }
                catch (OperationCanceledException)
                {
                    progress.Status = SymbolBackfillStatus.Cancelled;
                    break;
                }
                catch (Exception ex)
                {
                    progress.Status = SymbolBackfillStatus.Failed;
                    progress.ErrorMessage = ex.Message;
                }

                CompletedSymbols = i + 1;
                OverallProgress = (double)CompletedSymbols / TotalSymbols * 100;

                // Update elapsed time
                var elapsed = DateTime.UtcNow - startTime;
                ElapsedTime = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            }

            StatusText = _cts.Token.IsCancellationRequested
                ? "Backfill cancelled"
                : $"Backfill completed: {TotalBarsDownloaded:N0} bars downloaded";

            // Reload status
            await LoadLastStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Backfill failed: {ex.Message}";
        }
        finally
        {
            IsBackfillRunning = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void PauseBackfill()
    {
        IsPaused = !IsPaused;
        StatusText = IsPaused ? "Paused" : "Resuming...";
    }

    [RelayCommand]
    private void CancelBackfill()
    {
        _cts?.Cancel();
        StatusText = "Cancelling...";
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        await LoadLastStatusAsync();
    }

    [RelayCommand]
    private async Task RunScheduledJobAsync(string jobId)
    {
        await _schedulerService.RunTaskNowAsync(jobId);
    }

    [RelayCommand]
    private async Task DeleteScheduledJobAsync(string jobId)
    {
        await _schedulerService.DeleteTaskAsync(jobId);
        await LoadScheduledJobsAsync();
    }

    private void OnTaskStarted(object? sender, TaskExecutionEventArgs e)
    {
        if (e.Task?.TaskType == ScheduledTaskType.RunBackfill)
        {
            StatusText = $"Scheduled backfill started: {e.Task.Name}";
        }
    }

    private void OnTaskCompleted(object? sender, TaskExecutionEventArgs e)
    {
        if (e.Task?.TaskType == ScheduledTaskType.RunBackfill)
        {
            StatusText = $"Scheduled backfill completed: {e.Task.Name}";
            _ = LoadLastStatusAsync();
        }
    }

    private static string GetRelativeTime(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return utcTime.ToString("g");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _schedulerService.TaskStarted -= OnTaskStarted;
        _schedulerService.TaskCompleted -= OnTaskCompleted;
        _cts?.Cancel();
        _cts?.Dispose();

        _disposed = true;
    }
}

/// <summary>
/// Progress tracking for a single symbol during backfill.
/// </summary>
public partial class SymbolBackfillProgress : ObservableObject
{
    [ObservableProperty]
    private string _symbol = string.Empty;

    [ObservableProperty]
    private SymbolBackfillStatus _status = SymbolBackfillStatus.Pending;

    [ObservableProperty]
    private int _barsDownloaded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private double _progress;
}

/// <summary>
/// Status of a symbol's backfill operation.
/// </summary>
public enum SymbolBackfillStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Backfill history entry.
/// </summary>
public class BackfillHistoryEntry
{
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Scheduled backfill job.
/// </summary>
public class ScheduledBackfillJob
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NextRun { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Data validation issue found during verification.
/// </summary>
public class DataValidationIssue
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
}
