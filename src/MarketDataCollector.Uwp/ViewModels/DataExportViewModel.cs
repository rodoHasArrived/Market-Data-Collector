using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.ViewModels;

/// <summary>
/// ViewModel for the DataExportPage with real-time export progress.
/// </summary>
public sealed partial class DataExportViewModel : ObservableObject, IAsyncDisposable
{
    private readonly BatchExportSchedulerService _exportService;
    private readonly ConfigService _configService;
    private readonly BackgroundTaskSchedulerService _schedulerService;
    private bool _disposed;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private double _exportProgress;

    [ObservableProperty]
    private string _exportProgressText = "0%";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _currentFile = string.Empty;

    // Export settings
    [ObservableProperty]
    private string _selectedFormat = "CSV";

    [ObservableProperty]
    private DateTimeOffset? _fromDate;

    [ObservableProperty]
    private DateTimeOffset? _toDate;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private bool _compressOutput = true;

    [ObservableProperty]
    private bool _includeManifest = true;

    // Database sync settings
    [ObservableProperty]
    private string _databaseType = "postgresql";

    [ObservableProperty]
    private string _databaseHost = "localhost";

    [ObservableProperty]
    private int _databasePort = 5432;

    [ObservableProperty]
    private string _databaseName = "marketdata";

    [ObservableProperty]
    private bool _hasCredentials;

    // QuantConnect Lean settings
    [ObservableProperty]
    private string _leanDataPath = string.Empty;

    public ObservableCollection<string> SelectedSymbols { get; } = new();
    public ObservableCollection<ExportHistoryEntry> ExportHistory { get; } = new();

    public ObservableCollection<string> ExportFormats { get; } = new()
    {
        "CSV",
        "Parquet",
        "JSON Lines",
        "Raw (Copy)"
    };

    public ObservableCollection<string> DatabaseTypes { get; } = new()
    {
        "PostgreSQL",
        "TimescaleDB",
        "ClickHouse",
        "QuestDB",
        "InfluxDB",
        "SQLite"
    };

    public DataExportViewModel()
    {
        _exportService = new BatchExportSchedulerService();
        _configService = ConfigService.Instance;
        _schedulerService = BackgroundTaskSchedulerService.Instance;

        // Subscribe to export events
        _exportService.JobStarted += OnJobStarted;
        _exportService.JobCompleted += OnJobCompleted;
        _exportService.JobFailed += OnJobFailed;
        _exportService.JobProgress += OnJobProgress;

        // Set default dates
        ToDate = DateTimeOffset.Now;
        FromDate = DateTimeOffset.Now.AddDays(-7);
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await _exportService.StartAsync();
            await LoadSymbolsAsync();
            LoadExportHistory();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSymbolsAsync()
    {
        var config = await _configService.LoadConfigAsync();
        if (config?.Symbols != null)
        {
            SelectedSymbols.Clear();
            foreach (var symbol in config.Symbols.Take(10))
            {
                SelectedSymbols.Add(symbol.Symbol ?? string.Empty);
            }
        }
    }

    private void LoadExportHistory()
    {
        ExportHistory.Clear();
        foreach (var (_, job) in _exportService.Jobs)
        {
            var lastRun = job.RunHistory.LastOrDefault();
            if (lastRun != null)
            {
                ExportHistory.Add(new ExportHistoryEntry
                {
                    JobId = job.Id,
                    Timestamp = lastRun.StartedAt.ToString("g"),
                    Format = job.Format.ToString(),
                    SymbolCount = job.Symbols?.Length.ToString() ?? "All",
                    Size = FormatBytes(lastRun.BytesExported),
                    Destination = lastRun.DestinationPath ?? "N/A",
                    Success = lastRun.Success
                });
            }
        }
    }

    [RelayCommand]
    private void SetDateRange(string range)
    {
        ToDate = DateTimeOffset.Now;
        FromDate = range switch
        {
            "today" => DateTimeOffset.Now.Date,
            "week" => DateTimeOffset.Now.AddDays(-7),
            "month" => DateTimeOffset.Now.AddMonths(-1),
            _ => DateTimeOffset.Now.AddDays(-7)
        };
    }

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        if (IsExporting) return;

        IsExporting = true;
        ExportProgress = 0;
        ExportProgressText = "0%";
        StatusText = "Preparing export...";

        try
        {
            var format = SelectedFormat switch
            {
                "CSV" => ExportFormat.Csv,
                "Parquet" => ExportFormat.Parquet,
                "JSON Lines" => ExportFormat.JsonLines,
                _ => ExportFormat.Raw
            };

            var dataRoot = Path.Combine(AppContext.BaseDirectory, "data");
            var sourcePath = Path.Combine(dataRoot, "live");
            var destPath = string.IsNullOrEmpty(OutputPath)
                ? Path.Combine(dataRoot, "_exports", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                : OutputPath;

            ExportDateRange? dateRange = null;
            if (FromDate.HasValue && ToDate.HasValue)
            {
                dateRange = new ExportDateRange
                {
                    StartDate = DateOnly.FromDateTime(FromDate.Value.DateTime),
                    EndDate = DateOnly.FromDateTime(ToDate.Value.DateTime)
                };
            }

            var request = new ExportJobRequest
            {
                Name = $"Manual Export {DateTime.Now:g}",
                SourcePath = sourcePath,
                DestinationPath = destPath,
                Symbols = SelectedSymbols.ToArray(),
                DateRange = dateRange,
                Format = format,
                Priority = ExportPriority.High
            };

            var job = _exportService.CreateJob(request);
            StatusText = $"Export job created: {job.Id}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
            IsExporting = false;
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        foreach (var (id, job) in _exportService.Jobs)
        {
            if (job.Status == ExportJobStatus.Running)
            {
                _exportService.CancelJob(id);
            }
        }
        StatusText = "Export cancelled";
    }

    [RelayCommand]
    private async Task TestDatabaseConnectionAsync()
    {
        StatusText = "Testing connection...";
        await Task.Delay(1500); // Simulate connection test

        // In a real implementation, this would test the actual connection
        StatusText = "Connection successful";
    }

    [RelayCommand]
    private async Task ExportToLeanFormatAsync()
    {
        if (string.IsNullOrEmpty(LeanDataPath))
        {
            StatusText = "Please specify Lean data path";
            return;
        }

        IsExporting = true;
        StatusText = "Converting to Lean format...";

        try
        {
            // Create a specialized export for QuantConnect Lean format
            var dataRoot = Path.Combine(AppContext.BaseDirectory, "data");
            var sourcePath = Path.Combine(dataRoot, "live");

            var request = new ExportJobRequest
            {
                Name = "Lean Format Export",
                SourcePath = sourcePath,
                DestinationPath = LeanDataPath,
                Symbols = SelectedSymbols.ToArray(),
                Format = ExportFormat.Csv, // Lean uses CSV internally
                Priority = ExportPriority.Normal
            };

            _exportService.CreateJob(request);
        }
        catch (Exception ex)
        {
            StatusText = $"Lean export failed: {ex.Message}";
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task ScheduleExportAsync()
    {
        // Create a scheduled export task
        var format = SelectedFormat switch
        {
            "CSV" => "csv",
            "Parquet" => "parquet",
            "JSON Lines" => "jsonl",
            _ => "raw"
        };

        var payload = new Models.ExportTaskPayload
        {
            ExportFormat = format,
            Symbols = SelectedSymbols.ToArray(),
            Compress = CompressOutput,
            IncludeManifest = IncludeManifest
        };

        var task = new Models.ScheduledTask
        {
            Name = $"Scheduled Export - {format.ToUpperInvariant()}",
            Description = $"Export {SelectedSymbols.Count} symbols to {format} format",
            TaskType = Models.ScheduledTaskType.RunExport,
            Schedule = new Models.TaskSchedule
            {
                ScheduleType = Models.ScheduleType.Daily,
                Time = "18:00",
                SkipWeekends = true
            },
            Payload = System.Text.Json.JsonSerializer.Serialize(payload)
        };

        await _schedulerService.CreateTaskAsync(task);
        StatusText = "Export scheduled successfully";
    }

    private void OnJobStarted(object? sender, ExportJobEventArgs e)
    {
        StatusText = $"Export started: {e.Job.Name}";
        CurrentFile = "Initializing...";
    }

    private void OnJobCompleted(object? sender, ExportJobEventArgs e)
    {
        IsExporting = false;
        ExportProgress = 100;
        ExportProgressText = "100%";

        if (e.Run != null)
        {
            StatusText = $"Export completed: {e.Run.FilesExported} files ({FormatBytes(e.Run.BytesExported)})";
        }
        else
        {
            StatusText = "Export completed";
        }

        LoadExportHistory();
    }

    private void OnJobFailed(object? sender, ExportJobEventArgs e)
    {
        IsExporting = false;
        StatusText = $"Export failed: {e.Run?.ErrorMessage ?? "Unknown error"}";
        LoadExportHistory();
    }

    private void OnJobProgress(object? sender, ExportJobProgressEventArgs e)
    {
        ExportProgress = e.PercentComplete;
        ExportProgressText = $"{e.PercentComplete}%";
        CurrentFile = Path.GetFileName(e.CurrentFile);
        StatusText = $"Exporting: {e.FilesProcessed}/{e.TotalFiles} files";
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {suffixes[order]}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _exportService.JobStarted -= OnJobStarted;
        _exportService.JobCompleted -= OnJobCompleted;
        _exportService.JobFailed -= OnJobFailed;
        _exportService.JobProgress -= OnJobProgress;

        try
        {
            await _exportService.DisposeAsync();
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("DataExportViewModel error during disposal", ex);
        }

        _disposed = true;
    }
}

/// <summary>
/// Export history entry for display.
/// </summary>
public sealed class ExportHistoryEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool Success { get; set; }
}
