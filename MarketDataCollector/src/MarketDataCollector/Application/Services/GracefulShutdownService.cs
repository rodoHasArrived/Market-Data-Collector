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

    /// <summary>
    /// Creates a new graceful shutdown service.
    /// </summary>
    /// <param name="flushables">Collection of flushable components to flush on shutdown</param>
    /// <param name="shutdownTimeout">Maximum time to wait for flush operations (default: 30 seconds)</param>
    public GracefulShutdownService(
        IEnumerable<IFlushable> flushables,
        TimeSpan? shutdownTimeout = null)
    {
        _flushables = flushables ?? throw new ArgumentNullException(nameof(flushables));
        _shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(30);
        _log = Log.ForContext<GracefulShutdownService>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Information("Graceful shutdown service initialized with {Count} flushable components",
            _flushables.Count());
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
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

/// <summary>
/// Interface for components that can be flushed during shutdown.
/// </summary>
public interface IFlushable
{
    /// <summary>
    /// Flushes any buffered data to persistent storage.
    /// </summary>
    Task FlushAsync(CancellationToken ct = default);
}
