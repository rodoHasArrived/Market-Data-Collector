using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing backfill operations with progress tracking.
/// </summary>
public class BackfillService
{
    private static BackfillService? _instance;
    private static readonly object _lock = new();

    private readonly NotificationService _notificationService;
    private BackfillProgress? _currentProgress;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _startTime;
    private int _totalBarsDownloaded;
    private readonly object _progressLock = new();

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
    }

    /// <summary>
    /// Public constructor for direct instantiation.
    /// </summary>
    public BackfillService(bool useInstance = false)
    {
        if (useInstance)
        {
            throw new InvalidOperationException("Use BackfillService.Instance for singleton access");
        }
        _notificationService = NotificationService.Instance;
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
    /// Starts a new backfill operation.
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

        for (int i = 0; i < symbols.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = symbols[i];
            var symbolProgress = _currentProgress.SymbolProgress![i];

            // Check if paused
            while (IsPaused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);
            }

            symbolProgress.Status = "Downloading";
            symbolProgress.StartedAt = DateTime.UtcNow;
            symbolProgress.Provider = provider;
            symbolProgress.ExpectedBars = tradingDays;

            ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
            progressCallback?.Invoke(_currentProgress);

            // Simulate download progress for demo
            // In real implementation, this would be actual download logic
            for (int j = 0; j <= 100; j += 10)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (IsPaused && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken);
                }

                await Task.Delay(100, cancellationToken); // Simulate network delay

                symbolProgress.Progress = j;
                symbolProgress.BarsDownloaded = (int)(tradingDays * j / 100.0);

                lock (_progressLock)
                {
                    _totalBarsDownloaded += symbolProgress.BarsDownloaded - (int)(tradingDays * (j - 10) / 100.0);
                    _currentProgress.DownloadedBars = _totalBarsDownloaded;
                    _currentProgress.BarsPerSecond = BarsPerSecond;

                    var remaining = EstimatedTimeRemaining;
                    _currentProgress.EstimatedSecondsRemaining = remaining.HasValue
                        ? (int)remaining.Value.TotalSeconds
                        : null;
                }

                ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
                progressCallback?.Invoke(_currentProgress);
            }

            symbolProgress.Status = "Completed";
            symbolProgress.Progress = 100;
            symbolProgress.CompletedAt = DateTime.UtcNow;
            symbolProgress.BarsDownloaded = tradingDays;

            _currentProgress.CompletedSymbols++;

            ProgressUpdated?.Invoke(this, new BackfillProgressEventArgs { Progress = _currentProgress });
            progressCallback?.Invoke(_currentProgress);
        }
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
    /// Gets the last backfill status.
    /// </summary>
    public async Task<BackfillResult?> GetLastStatusAsync()
    {
        // In real implementation, this would load from storage
        await Task.Delay(10);
        return _lastResult;
    }

    private BackfillResult? _lastResult;

    /// <summary>
    /// Runs a simple backfill operation (for UI compatibility).
    /// </summary>
    public async Task<BackfillResult?> RunBackfillAsync(string provider, string[] symbols, string? from, string? to)
    {
        var fromDate = from != null ? DateTime.Parse(from) : DateTime.Today.AddYears(-1);
        var toDate = to != null ? DateTime.Parse(to) : DateTime.Today;

        var result = new BackfillResult
        {
            Provider = provider,
            Symbols = symbols,
            StartedUtc = DateTime.UtcNow,
            Success = true
        };

        try
        {
            // Simulate backfill (in real implementation, this calls the actual provider)
            var random = new Random();
            var tradingDays = (int)((toDate - fromDate).TotalDays * 252 / 365);
            result.BarsWritten = symbols.Length * tradingDays;

            await Task.Delay(100); // Simulate some work

            result.CompletedUtc = DateTime.UtcNow;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedUtc = DateTime.UtcNow;
        }

        _lastResult = result;
        return result;
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
