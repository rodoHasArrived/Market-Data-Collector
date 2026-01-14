using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing backfill operations with progress tracking.
/// Uses real API integration with the core Market Data Collector service.
/// </summary>
public sealed class BackfillService
{
    private static BackfillService? _instance;
    private static readonly object _lock = new();

    private readonly NotificationService _notificationService;
    private readonly BackfillApiService _backfillApiService;
    private BackfillProgress? _currentProgress;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _startTime;
    private int _totalBarsDownloaded;
    private readonly object _progressLock = new();

    // Polling configuration for progress updates
    private const int ProgressPollIntervalMs = 1000;
    private const int MaxPollAttempts = 3600; // 1 hour max at 1 second intervals

    public static BackfillService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new BackfillService();
                }
            }
            return _instance;
        }
    }

    private BackfillService()
    {
        _notificationService = NotificationService.Instance;
        _backfillApiService = new BackfillApiService();
    }

    /// <summary>
    /// Gets the current backfill progress.
    /// </summary>
    public BackfillProgress? CurrentProgress => _currentProgress;

    /// <summary>
    /// Gets whether a backfill is currently running.
    /// </summary>
    public bool IsRunning => _currentProgress?.Status == "Running";

    /// <summary>
    /// Gets whether a backfill is paused.
    /// </summary>
    public bool IsPaused => _currentProgress?.Status == "Paused";

    /// <summary>
    /// Gets the download speed in bars per second.
    /// </summary>
    public double BarsPerSecond
    {
        get
        {
            if (_currentProgress == null || !IsRunning) return 0;
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0
                ? _totalBarsDownloaded / elapsed.TotalSeconds
                : 0;
        }
    }

    /// <summary>
    /// Gets the estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (_currentProgress == null || !IsRunning) return null;
            var speed = BarsPerSecond;
            if (speed <= 0) return null;

            var remainingBars = _currentProgress.TotalBars - _currentProgress.DownloadedBars;
            return TimeSpan.FromSeconds(remainingBars / speed);
        }
    }

    /// <summary>
    /// Starts a new backfill operation using the real API.
    /// </summary>
    public async Task StartBackfillAsync(
        string[] symbols,
        string provider,
        DateTime fromDate,
        DateTime toDate,
        Action<BackfillProgress>? progressCallback = null)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("A backfill operation is already running");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _startTime = DateTime.UtcNow;
        _totalBarsDownloaded = 0;

        _currentProgress = new BackfillProgress
        {
            JobId = Guid.NewGuid().ToString(),
            Status = "Running",
            TotalSymbols = symbols.Length,
            StartedAt = DateTime.UtcNow,
            CurrentProvider = provider,
            SymbolProgress = symbols.Select(s => new SymbolBackfillProgress
            {
                Symbol = s,
                Status = "Pending"
            }).ToArray()
        };

        ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });

        try
        {
            await RunBackfillAsync(symbols, provider, fromDate, toDate, progressCallback, _cancellationTokenSource.Token);

            _currentProgress.Status = "Completed";
            _currentProgress.CompletedAt = DateTime.UtcNow;

            await _notificationService.NotifyBackfillCompleteAsync(
                true,
                _currentProgress.CompletedSymbols,
                (int)_currentProgress.DownloadedBars,
                DateTime.UtcNow - _startTime);

            BackfillCompleted?.Invoke(this, new BackfillCompletedEventArgs
            {
                Success = true,
                Progress = _currentProgress
            });
        }
        catch (OperationCanceledException)
        {
            _currentProgress.Status = "Cancelled";
            _currentProgress.CompletedAt = DateTime.UtcNow;

            BackfillCompleted?.Invoke(this, new BackfillCompletedEventArgs
            {
                Success = false,
                Progress = _currentProgress,
                WasCancelled = true
            });
        }
        catch (Exception ex)
        {
            _currentProgress.Status = "Failed";
            _currentProgress.ErrorMessage = ex.Message;
            _currentProgress.CompletedAt = DateTime.UtcNow;

            await _notificationService.NotifyBackfillCompleteAsync(
                false,
                _currentProgress.CompletedSymbols,
                (int)_currentProgress.DownloadedBars,
                DateTime.UtcNow - _startTime);

            BackfillCompleted?.Invoke(this, new BackfillCompletedEventArgs
            {
                Success = false,
                Progress = _currentProgress,
                Error = ex
            });
        }
        finally
        {
            ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
        }
    }

    private async Task RunBackfillAsync(
        string[] symbols,
        string provider,
        DateTime fromDate,
        DateTime toDate,
        Action<BackfillProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        // Estimate total bars (rough estimate: ~252 trading days per year * date range)
        var tradingDays = (int)((toDate - fromDate).TotalDays * 252 / 365);
        _currentProgress!.TotalBars = symbols.Length * tradingDays;

        // Format dates for API
        var fromStr = fromDate.ToString("yyyy-MM-dd");
        var toStr = toDate.ToString("yyyy-MM-dd");

        // Call the real API to start the backfill
        var result = await _backfillApiService.RunBackfillAsync(
            provider,
            symbols,
            fromStr,
            toStr,
            cancellationToken);

        if (result == null)
        {
            throw new InvalidOperationException("Failed to connect to the Market Data Collector service. Please ensure the service is running.");
        }

        if (!result.Success)
        {
            throw new InvalidOperationException(result.Error ?? "Backfill operation failed");
        }

        // Update progress based on API result
        lock (_progressLock)
        {
            _totalBarsDownloaded = result.BarsWritten;
            _currentProgress.DownloadedBars = result.BarsWritten;
            _currentProgress.CompletedSymbols = result.Symbols?.Length ?? symbols.Length;
            _currentProgress.BarsPerSecond = BarsPerSecond;

            // Mark all symbols as completed based on API response
            if (_currentProgress.SymbolProgress != null)
            {
                var completedSymbols = result.Symbols ?? symbols;
                foreach (var symbolProgress in _currentProgress.SymbolProgress)
                {
                    if (completedSymbols.Contains(symbolProgress.Symbol))
                    {
                        symbolProgress.Status = "Completed";
                        symbolProgress.Progress = 100;
                        symbolProgress.CompletedAt = result.CompletedUtc;
                        symbolProgress.BarsDownloaded = result.BarsWritten / Math.Max(1, completedSymbols.Length);
                        symbolProgress.Provider = result.Provider;
                    }
                }
            }
        }

        ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
        progressCallback?.Invoke(_currentProgress);
    }

    /// <summary>
    /// Starts a quick gap-fill operation for immediate data gaps.
    /// </summary>
    public async Task StartGapFillAsync(
        string[] symbols,
        int lookbackDays = 30,
        Action<BackfillProgress>? progressCallback = null)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("A backfill operation is already running");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _startTime = DateTime.UtcNow;
        _totalBarsDownloaded = 0;

        _currentProgress = new BackfillProgress
        {
            JobId = Guid.NewGuid().ToString(),
            Status = "Running",
            TotalSymbols = symbols.Length,
            StartedAt = DateTime.UtcNow,
            CurrentProvider = "composite",
            SymbolProgress = symbols.Select(s => new SymbolBackfillProgress
            {
                Symbol = s,
                Status = "Pending"
            }).ToArray()
        };

        ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });

        try
        {
            var result = await _backfillApiService.RunGapFillAsync(
                symbols,
                lookbackDays,
                "High",
                _cancellationTokenSource.Token);

            if (result == null)
            {
                throw new InvalidOperationException("Gap-fill request failed - service may be unavailable");
            }

            _currentProgress.Status = "Completed";
            _currentProgress.CompletedAt = DateTime.UtcNow;
            _currentProgress.CompletedSymbols = symbols.Length;

            await _notificationService.NotifyBackfillCompleteAsync(
                true,
                symbols.Length,
                0,
                DateTime.UtcNow - _startTime);

            BackfillCompleted?.Invoke(this, new BackfillCompletedEventArgs
            {
                Success = true,
                Progress = _currentProgress
            });
        }
        catch (OperationCanceledException)
        {
            _currentProgress.Status = "Cancelled";
            _currentProgress.CompletedAt = DateTime.UtcNow;

            BackfillCompleted?.Invoke(this, new BackfillCompletedEventArgs
            {
                Success = false,
                Progress = _currentProgress,
                WasCancelled = true
            });
        }
        catch (Exception ex)
        {
            _currentProgress.Status = "Failed";
            _currentProgress.ErrorMessage = ex.Message;
            _currentProgress.CompletedAt = DateTime.UtcNow;

            BackfillCompleted?.Invoke(this, new BackfillCompletedEventArgs
            {
                Success = false,
                Progress = _currentProgress,
                Error = ex
            });
        }
        finally
        {
            ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
            progressCallback?.Invoke(_currentProgress);
        }
    }

    /// <summary>
    /// Gets available backfill providers from the API.
    /// </summary>
    public async Task<List<BackfillProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        return await _backfillApiService.GetProvidersAsync(ct);
    }

    /// <summary>
    /// Gets backfill presets from the API.
    /// </summary>
    public async Task<List<BackfillPreset>> GetPresetsAsync(CancellationToken ct = default)
    {
        return await _backfillApiService.GetPresetsAsync(ct);
    }

    /// <summary>
    /// Checks provider health.
    /// </summary>
    public async Task<BackfillHealthResponse?> CheckProviderHealthAsync(CancellationToken ct = default)
    {
        return await _backfillApiService.CheckProviderHealthAsync(ct);
    }

    /// <summary>
    /// Gets execution history.
    /// </summary>
    public async Task<List<BackfillExecution>> GetExecutionHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        return await _backfillApiService.GetExecutionHistoryAsync(limit, ct);
    }

    /// <summary>
    /// Gets backfill statistics.
    /// </summary>
    public async Task<BackfillStatistics?> GetStatisticsAsync(int? hours = null, CancellationToken ct = default)
    {
        return await _backfillApiService.GetStatisticsAsync(hours, ct);
    }

    /// <summary>
    /// Pauses the current backfill operation.
    /// </summary>
    public void Pause()
    {
        if (_currentProgress != null && _currentProgress.Status == "Running")
        {
            _currentProgress.Status = "Paused";
            ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
        }
    }

    /// <summary>
    /// Resumes a paused backfill operation.
    /// </summary>
    public void Resume()
    {
        if (_currentProgress != null && _currentProgress.Status == "Paused")
        {
            _currentProgress.Status = "Running";
            ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
        }
    }

    /// <summary>
    /// Cancels the current backfill operation.
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Reorders the symbol queue (drag-drop priority).
    /// </summary>
    public void ReorderQueue(int oldIndex, int newIndex)
    {
        if (_currentProgress?.SymbolProgress == null) return;

        var symbols = _currentProgress.SymbolProgress.ToList();
        if (oldIndex < 0 || oldIndex >= symbols.Count || newIndex < 0 || newIndex >= symbols.Count) return;

        // Only reorder pending symbols
        if (symbols[oldIndex].Status != "Pending") return;

        var item = symbols[oldIndex];
        symbols.RemoveAt(oldIndex);
        symbols.Insert(newIndex, item);
        _currentProgress.SymbolProgress = symbols.ToArray();

        ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
    }

    /// <summary>
    /// Gets a formatted ETA string.
    /// </summary>
    public string GetFormattedEta()
    {
        var eta = EstimatedTimeRemaining;
        if (!eta.HasValue) return "Calculating...";

        if (eta.Value.TotalHours >= 1)
            return $"{(int)eta.Value.TotalHours}h {eta.Value.Minutes}m remaining";
        if (eta.Value.TotalMinutes >= 1)
            return $"{eta.Value.Minutes}m {eta.Value.Seconds}s remaining";
        return $"{eta.Value.Seconds}s remaining";
    }

    /// <summary>
    /// Gets a formatted speed string.
    /// </summary>
    public string GetFormattedSpeed()
    {
        var speed = BarsPerSecond;
        if (speed >= 1000)
            return $"{speed / 1000:F1}k bars/s";
        return $"{speed:F0} bars/s";
    }

    /// <summary>
    /// Fills a specific data gap for a symbol.
    /// </summary>
    public async Task FillGapAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (startDate > endDate)
            throw new ArgumentException("Start date must be before end date");

        await StartBackfillAsync(
            new[] { symbol },
            "composite", // Use composite provider to try multiple sources
            startDate,
            endDate);
    }

    /// <summary>
    /// Fills multiple data gaps in batch.
    /// </summary>
    public async Task FillGapsBatchAsync(
        IEnumerable<(string Symbol, DateTime Start, DateTime End)> gaps,
        CancellationToken ct = default)
    {
        var gapsList = gaps.ToList();
        if (gapsList.Count == 0) return;

        foreach (var (symbol, start, end) in gapsList)
        {
            ct.ThrowIfCancellationRequested();
            await FillGapAsync(symbol, start, end, ct);
        }
    }

    /// <summary>
    /// Event raised when progress is updated.
    /// </summary>
    public event EventHandler<BackfillProgressEventArgs>? ProgressUpdated;

    /// <summary>
    /// Event raised when backfill completes.
    /// </summary>
    public event EventHandler<BackfillCompletedEventArgs>? BackfillCompleted;
}

/// <summary>
/// Backfill progress event args.
/// </summary>
public class BackfillProgressEventArgs : EventArgs
{
    public BackfillProgress? Progress { get; set; }
}

/// <summary>
/// Backfill completed event args.
/// </summary>
public class BackfillCompletedEventArgs : EventArgs
{
    public bool Success { get; set; }
    public BackfillProgress? Progress { get; set; }
    public bool WasCancelled { get; set; }
    public Exception? Error { get; set; }
}
