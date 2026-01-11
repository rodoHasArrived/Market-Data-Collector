using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Background worker service that processes the backfill request queue.
/// Handles rate limits, retries, and writes data to storage.
/// </summary>
public sealed class BackfillWorkerService : IDisposable
{
    private readonly BackfillJobManager _jobManager;
    private readonly BackfillRequestQueue _requestQueue;
    private readonly CompositeHistoricalDataProvider _provider;
    private readonly ProviderRateLimitTracker _rateLimitTracker;
    private readonly BackfillJobsConfig _config;
    private readonly string _dataRoot;
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private Task? _workerTask;
    private Task? _completionTask;
    private bool _disposed;
    private bool _isRunning;

    /// <summary>
    /// Event raised when a bar is successfully written to storage.
    /// </summary>
    public event Action<string, HistoricalBar>? OnBarWritten;

    /// <summary>
    /// Event raised when worker status changes.
    /// </summary>
    public event Action<bool>? OnRunningStateChanged;

    public bool IsRunning => _isRunning;

    private const int MinConcurrentRequests = 1;
    private const int MaxConcurrentRequests = 100;

    public BackfillWorkerService(
        BackfillJobManager jobManager,
        BackfillRequestQueue requestQueue,
        CompositeHistoricalDataProvider provider,
        ProviderRateLimitTracker rateLimitTracker,
        BackfillJobsConfig config,
        string dataRoot,
        ILogger? log = null)
    {
        if (config.MaxConcurrentRequests < MinConcurrentRequests || config.MaxConcurrentRequests > MaxConcurrentRequests)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                config.MaxConcurrentRequests,
                $"MaxConcurrentRequests must be between {MinConcurrentRequests} and {MaxConcurrentRequests}");
        }

        _jobManager = jobManager;
        _requestQueue = requestQueue;
        _provider = provider;
        _rateLimitTracker = rateLimitTracker;
        _config = config;
        _dataRoot = dataRoot;
        _log = log ?? LoggingSetup.ForContext<BackfillWorkerService>();
    }

    /// <summary>
    /// Start the worker service.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _workerTask = RunWorkerLoopAsync(_cts.Token);
        _completionTask = RunCompletionLoopAsync(_cts.Token);

        OnRunningStateChanged?.Invoke(true);
        _log.Information("Backfill worker service started");
    }

    /// <summary>
    /// Stop the worker service.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _cts.Cancel();

        try
        {
            if (_workerTask != null)
                await _workerTask.ConfigureAwait(false);
            if (_completionTask != null)
                await _completionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _isRunning = false;
        OnRunningStateChanged?.Invoke(false);
        _log.Information("Backfill worker service stopped");
    }

    /// <summary>
    /// Main worker loop that processes requests from the queue.
    /// </summary>
    // TODO: Move SemaphoreSlim to class field - creating in loop causes resource leak on each restart
    private async Task RunWorkerLoopAsync(CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(_config.MaxConcurrentRequests);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Wait for a slot
                await semaphore.WaitAsync(ct).ConfigureAwait(false);

                // Try to get a request
                var request = await _requestQueue.TryDequeueAsync(ct).ConfigureAwait(false);

                if (request == null)
                {
                    semaphore.Release();

                    // No requests available, check if all providers are rate-limited
                    if (CheckAllProvidersRateLimited())
                    {
                        await HandleAllProvidersRateLimitedAsync(ct).ConfigureAwait(false);
                    }
                    else
                    {
                        // Wait a bit before trying again
                        await Task.Delay(100, ct).ConfigureAwait(false);
                    }
                    continue;
                }

                // Process request in background
                _ = ProcessRequestAsync(request, semaphore, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in worker loop");
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Process a single backfill request.
    /// </summary>
    private async Task ProcessRequestAsync(BackfillRequest request, SemaphoreSlim semaphore, CancellationToken ct)
    {
        try
        {
            _log.Debug("Processing request: {Symbol} {From}-{To} via {Provider}",
                request.Symbol, request.FromDate, request.ToDate, request.AssignedProvider);

            // Fetch data from provider
            var bars = await FetchBarsAsync(request, ct).ConfigureAwait(false);

            if (bars.Count > 0)
            {
                // Write to storage
                await WriteBarsToStorageAsync(request, bars, ct).ConfigureAwait(false);
                request.BarsRetrieved = bars.Count;
            }

            // Mark as complete
            await _requestQueue.CompleteRequestAsync(request, true, ct: ct).ConfigureAwait(false);
            await _jobManager.UpdateJobProgressAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var isRateLimited = ex.Message.Contains("429") ||
                                ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);

            if (isRateLimited && request.AssignedProvider != null)
            {
                _requestQueue.RecordProviderRateLimitHit(request.AssignedProvider);
            }

            await _requestQueue.CompleteRequestAsync(request, false, ex.Message, ct).ConfigureAwait(false);
            await _jobManager.UpdateJobProgressAsync(request, ct).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Fetch bars from the assigned provider.
    /// </summary>
    private async Task<IReadOnlyList<HistoricalBar>> FetchBarsAsync(BackfillRequest request, CancellationToken ct)
    {
        // Track the request
        if (request.AssignedProvider != null)
        {
            _rateLimitTracker.RecordRequest(request.AssignedProvider);
        }

        // Use composite provider which handles fallback
        var bars = await _provider.GetDailyBarsAsync(
            request.Symbol,
            request.FromDate,
            request.ToDate,
            ct).ConfigureAwait(false);

        return bars;
    }

    /// <summary>
    /// Write bars to storage.
    /// </summary>
    private async Task WriteBarsToStorageAsync(BackfillRequest request, IReadOnlyList<HistoricalBar> bars, CancellationToken ct)
    {
        // Group by date for daily partitioning
        var barsByDate = bars.GroupBy(b => b.SessionDate);

        foreach (var dateGroup in barsByDate)
        {
            ct.ThrowIfCancellationRequested();

            var date = dateGroup.Key;
            var dateBars = dateGroup.ToList();

            // Build file path based on naming convention
            var filePath = BuildFilePath(request.Symbol, date, request.Granularity);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write bars as JSONL
            var lines = dateBars.Select(b => JsonSerializer.Serialize(b));
            await File.AppendAllLinesAsync(filePath, lines, ct).ConfigureAwait(false);

            foreach (var bar in dateBars)
            {
                OnBarWritten?.Invoke(filePath, bar);
            }
        }

        _log.Debug("Wrote {BarCount} bars for {Symbol} to storage", bars.Count, request.Symbol);
    }

    /// <summary>
    /// Build the file path for storing bars.
    /// </summary>
    private string BuildFilePath(string symbol, DateOnly date, DataGranularity granularity)
    {
        var granularityName = granularity switch
        {
            DataGranularity.Daily => "daily",
            DataGranularity.Hour1 => "hourly",
            DataGranularity.Minute1 => "1min",
            DataGranularity.Minute5 => "5min",
            DataGranularity.Minute15 => "15min",
            DataGranularity.Minute30 => "30min",
            _ => "daily"
        };

        // Default: BySymbol naming convention
        // {DataRoot}/{Symbol}/bar_{granularity}/{date}.jsonl
        var symbolDir = Path.Combine(_dataRoot, symbol.ToUpperInvariant());
        var typeDir = Path.Combine(symbolDir, $"bar_{granularityName}");
        var fileName = $"{date:yyyy-MM-dd}.jsonl";

        return Path.Combine(typeDir, fileName);
    }

    /// <summary>
    /// Check if all providers are rate-limited.
    /// </summary>
    private bool CheckAllProvidersRateLimited()
    {
        var status = _rateLimitTracker.GetAllStatus();
        return status.Values.All(s => s.IsRateLimited);
    }

    /// <summary>
    /// Handle situation where all providers are rate-limited.
    /// </summary>
    private async Task HandleAllProvidersRateLimitedAsync(CancellationToken ct)
    {
        var status = _rateLimitTracker.GetAllStatus();
        var shortestWait = status.Values
            .Where(s => s.TimeUntilReset.HasValue)
            .Select(s => s.TimeUntilReset!.Value)
            .DefaultIfEmpty(TimeSpan.FromMinutes(1))
            .Min();

        if (shortestWait > TimeSpan.FromMinutes(_config.MaxRateLimitWaitMinutes))
        {
            // Pause all running jobs if wait is too long
            if (_config.AutoPauseOnRateLimit)
            {
                var runningJobs = _jobManager.GetJobsByStatus(BackfillJobStatus.Running);
                foreach (var job in runningJobs)
                {
                    await _jobManager.SetJobRateLimitedAsync(job.JobId, shortestWait, ct).ConfigureAwait(false);
                }
            }

            _log.Information("All providers rate-limited for {Wait}, jobs paused", shortestWait);
        }
        else
        {
            _log.Information("All providers rate-limited, waiting {Wait} for reset", shortestWait);
            await Task.Delay(shortestWait, ct).ConfigureAwait(false);

            // Resume rate-limited jobs if auto-resume is enabled
            if (_config.AutoResumeAfterRateLimit)
            {
                var rateLimitedJobs = _jobManager.GetJobsByStatus(BackfillJobStatus.RateLimited);
                foreach (var job in rateLimitedJobs)
                {
                    await _jobManager.ResumeJobAsync(job.JobId, ct).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Process completed requests and update job progress.
    /// </summary>
    private async Task RunCompletionLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var request in _requestQueue.CompletedRequests.ReadAllAsync(ct))
            {
                // Progress is already updated in ProcessRequestAsync
                // This loop is for additional processing if needed

                _log.Verbose("Request {RequestId} completed: {Status}",
                    request.RequestId, request.Status);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
    }
}

/// <summary>
/// Factory for creating backfill service instances.
/// </summary>
public sealed class BackfillServiceFactory
{
    private readonly ILogger _log;

    public BackfillServiceFactory(ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<BackfillServiceFactory>();
    }

    /// <summary>
    /// Create a complete backfill service stack from configuration.
    /// </summary>
    public BackfillServices CreateServices(
        BackfillConfig config,
        string dataRoot,
        IEnumerable<IHistoricalDataProvider> providers)
    {
        var jobsConfig = config.Jobs ?? new BackfillJobsConfig();
        var jobsDirectory = Path.Combine(dataRoot, jobsConfig.JobsDirectory);

        // Create rate limit tracker
        var rateLimitTracker = new ProviderRateLimitTracker(_log);

        // Register providers with rate limit tracker
        foreach (var provider in providers.OfType<IHistoricalDataProviderV2>())
        {
            rateLimitTracker.RegisterProvider(provider);
        }

        // Create composite provider
        var composite = new CompositeHistoricalDataProvider(
            providers,
            enableRateLimitRotation: config.EnableRateLimitRotation,
            rateLimitRotationThreshold: config.RateLimitRotationThreshold,
            log: _log);

        // Create gap analyzer
        var gapAnalyzer = new DataGapAnalyzer(dataRoot, _log);

        // Create request queue
        var requestQueue = new BackfillRequestQueue(rateLimitTracker, _log)
        {
            MaxConcurrentRequests = jobsConfig.MaxConcurrentRequests,
            MaxConcurrentPerProvider = jobsConfig.MaxConcurrentPerProvider
        };

        // Create job manager
        var jobManager = new BackfillJobManager(gapAnalyzer, requestQueue, jobsDirectory, _log);

        // Create worker service
        var worker = new BackfillWorkerService(
            jobManager,
            requestQueue,
            composite,
            rateLimitTracker,
            jobsConfig,
            dataRoot,
            _log);

        return new BackfillServices(
            jobManager,
            requestQueue,
            gapAnalyzer,
            rateLimitTracker,
            composite,
            worker);
    }
}

/// <summary>
/// Container for all backfill-related services.
/// </summary>
public sealed class BackfillServices : IDisposable
{
    public BackfillJobManager JobManager { get; }
    public BackfillRequestQueue RequestQueue { get; }
    public DataGapAnalyzer GapAnalyzer { get; }
    public ProviderRateLimitTracker RateLimitTracker { get; }
    public CompositeHistoricalDataProvider Provider { get; }
    public BackfillWorkerService Worker { get; }

    public BackfillServices(
        BackfillJobManager jobManager,
        BackfillRequestQueue requestQueue,
        DataGapAnalyzer gapAnalyzer,
        ProviderRateLimitTracker rateLimitTracker,
        CompositeHistoricalDataProvider provider,
        BackfillWorkerService worker)
    {
        JobManager = jobManager;
        RequestQueue = requestQueue;
        GapAnalyzer = gapAnalyzer;
        RateLimitTracker = rateLimitTracker;
        Provider = provider;
        Worker = worker;
    }

    /// <summary>
    /// Initialize services (load persisted jobs).
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await JobManager.LoadJobsAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Start the worker service.
    /// </summary>
    public void StartWorker()
    {
        Worker.Start();
    }

    /// <summary>
    /// Stop the worker service.
    /// </summary>
    public async Task StopWorkerAsync()
    {
        await Worker.StopAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        Worker.Dispose();
        RequestQueue.Dispose();
        JobManager.Dispose();
        RateLimitTracker.Dispose();
        Provider.Dispose();
    }
}
