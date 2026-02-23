using System.Diagnostics;
using System.Text;
using MarketDataCollector.Application.Monitoring;
using Microsoft.Extensions.Hosting;
using System.Threading;
using Serilog;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// IHostedService that ensures graceful shutdown by flushing all registered buffers
/// before the application terminates. This prevents data loss during shutdown.
/// </summary>
public sealed class GracefulShutdownService : IHostedService
{
    private readonly IEnumerable<IFlushable> _flushables;
    private readonly ILogger _log;
    private readonly TimeSpan _shutdownTimeout;
    private readonly string? _dataRoot;
    private readonly Stopwatch _sessionStopwatch = new();

    /// <summary>
    /// Creates a new graceful shutdown service.
    /// </summary>
    /// <param name="flushables">Collection of flushable components to flush on shutdown</param>
    /// <param name="shutdownTimeout">Maximum time to wait for flush operations (default: 30 seconds)</param>
    /// <param name="dataRoot">Optional data root path for storage summary on shutdown</param>
    public GracefulShutdownService(
        IEnumerable<IFlushable> flushables,
        TimeSpan? shutdownTimeout = null,
        string? dataRoot = null)
    {
        _flushables = flushables ?? throw new ArgumentNullException(nameof(flushables));
        _shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(30);
        _dataRoot = dataRoot;
        _log = Log.ForContext<GracefulShutdownService>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionStopwatch.Start();
        _log.Information("Graceful shutdown service initialized with {Count} flushable components",
            _flushables.Count());
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionStopwatch.Stop();
        _log.Information("Graceful shutdown initiated - flushing all buffers...");

        using var timeoutCts = new CancellationTokenSource(_shutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        var flushTasks = new List<Task>();
        var flushableList = _flushables.ToList();

        foreach (var flushable in flushableList)
        {
            var name = flushable.GetType().Name;
            _log.Debug("Flushing {Component}...", name);

            flushTasks.Add(FlushWithLoggingAsync(flushable, name, linkedCts.Token));
        }

        try
        {
            await Task.WhenAll(flushTasks).ConfigureAwait(false);
            _log.Information("All {Count} buffers flushed successfully", flushableList.Count);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _log.Warning("Shutdown timeout ({Timeout}s) reached - some data may be lost",
                _shutdownTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during graceful shutdown flush");
        }

        PrintSessionSummary();
    }

    private void PrintSessionSummary()
    {
        try
        {
            var snapshot = Metrics.GetSnapshot();
            var duration = _sessionStopwatch.Elapsed;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"  Session Summary ({FormatDuration(duration)}):");
            sb.AppendLine($"    Events collected:  {snapshot.Published:N0}");
            sb.AppendLine($"    Events dropped:    {snapshot.Dropped:N0} ({snapshot.DropRate:F3}%)");

            if (snapshot.Trades > 0)
                sb.AppendLine($"    Trades:            {snapshot.Trades:N0}");
            if (snapshot.DepthUpdates > 0)
                sb.AppendLine($"    Depth updates:     {snapshot.DepthUpdates:N0}");
            if (snapshot.Quotes > 0)
                sb.AppendLine($"    Quotes:            {snapshot.Quotes:N0}");
            if (snapshot.HistoricalBars > 0)
                sb.AppendLine($"    Historical bars:   {snapshot.HistoricalBars:N0}");
            if (snapshot.Integrity > 0)
                sb.AppendLine($"    Integrity events:  {snapshot.Integrity:N0}");

            // Data completeness
            var totalAttempted = snapshot.Published + snapshot.Dropped;
            if (totalAttempted > 0)
            {
                var completeness = (double)snapshot.Published / totalAttempted * 100;
                sb.AppendLine($"    Data completeness: {completeness:F1}%");
            }

            sb.AppendLine($"    Avg latency:       {snapshot.AverageLatencyUs:F1} us");
            sb.AppendLine($"    Memory usage:      {snapshot.MemoryUsageMb:F1} MB");

            // Storage summary
            AppendStorageSummary(sb);

            var summaryText = sb.ToString();
            _log.Information("Session summary:{Summary}", summaryText);
            Console.Write(summaryText);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Failed to generate session summary");
        }
    }

    private void AppendStorageSummary(StringBuilder sb)
    {
        try
        {
            if (_dataRoot == null || !Directory.Exists(_dataRoot))
                return;

            var liveDir = Path.Combine(_dataRoot, "live");
            if (!Directory.Exists(liveDir))
                return;

            var files = Directory.GetFiles(liveDir, "*.*", SearchOption.AllDirectories);
            if (files.Length == 0)
                return;

            var totalBytes = files.Sum(f => new FileInfo(f).Length);

            sb.AppendLine($"    Storage written:   {FormatSize(totalBytes)} ({files.Length} files)");
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Failed to collect storage summary");
        }
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
            >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} bytes"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    private async Task FlushWithLoggingAsync(IFlushable flushable, string name, CancellationToken ct)
    {
        try
        {
            await flushable.FlushAsync(ct).ConfigureAwait(false);
            _log.Debug("Successfully flushed {Component}", name);
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Flush of {Component} was cancelled", name);
            throw;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to flush {Component}", name);
        }
    }
}
