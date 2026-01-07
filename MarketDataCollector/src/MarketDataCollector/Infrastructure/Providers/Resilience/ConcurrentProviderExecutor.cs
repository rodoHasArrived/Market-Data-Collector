using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Providers.Abstractions;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Resilience;

/// <summary>
/// Configuration options for concurrent provider execution.
/// </summary>
public sealed record ConcurrentExecutionOptions
{
    /// <summary>Maximum concurrent operations.</summary>
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;

    /// <summary>Timeout per provider operation.</summary>
    public TimeSpan PerProviderTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Overall timeout for all operations.</summary>
    public TimeSpan? OverallTimeout { get; init; }

    /// <summary>Continue execution if some providers fail.</summary>
    public bool ContinueOnError { get; init; } = true;

    /// <summary>Minimum successful results required (null = all must succeed).</summary>
    public int? MinSuccessRequired { get; init; }

    /// <summary>Stop after first successful result.</summary>
    public bool StopOnFirstSuccess { get; init; } = false;

    /// <summary>Result aggregation strategy.</summary>
    public ResultAggregationStrategy AggregationStrategy { get; init; } = ResultAggregationStrategy.All;

    public static ConcurrentExecutionOptions Default => new();

    public static ConcurrentExecutionOptions FirstSuccess => new()
    {
        StopOnFirstSuccess = true,
        ContinueOnError = true,
        AggregationStrategy = ResultAggregationStrategy.FirstSuccess
    };

    public static ConcurrentExecutionOptions BestEffort => new()
    {
        ContinueOnError = true,
        AggregationStrategy = ResultAggregationStrategy.All
    };
}

/// <summary>
/// Strategy for aggregating results from multiple providers.
/// </summary>
public enum ResultAggregationStrategy
{
    /// <summary>Return all results.</summary>
    All,

    /// <summary>Return first successful result.</summary>
    FirstSuccess,

    /// <summary>Return result from highest priority provider.</summary>
    HighestPriority,

    /// <summary>Merge results from all providers.</summary>
    Merge,

    /// <summary>Return best quality result based on scoring.</summary>
    BestQuality
}

/// <summary>
/// Result of a single provider operation.
/// </summary>
public sealed record ProviderOperationResult<TResult>
{
    public required string ProviderId { get; init; }
    public bool IsSuccess { get; init; }
    public TResult? Value { get; init; }
    public Exception? Exception { get; init; }
    public TimeSpan Duration { get; init; }
    public bool WasCancelled { get; init; }
    public bool WasTimeout { get; init; }

    public static ProviderOperationResult<TResult> Success(string providerId, TResult value, TimeSpan duration) =>
        new()
        {
            ProviderId = providerId,
            IsSuccess = true,
            Value = value,
            Duration = duration
        };

    public static ProviderOperationResult<TResult> Failed(string providerId, Exception ex, TimeSpan duration) =>
        new()
        {
            ProviderId = providerId,
            IsSuccess = false,
            Exception = ex,
            Duration = duration
        };

    public static ProviderOperationResult<TResult> Timeout(string providerId, TimeSpan timeout) =>
        new()
        {
            ProviderId = providerId,
            IsSuccess = false,
            WasTimeout = true,
            Duration = timeout,
            Exception = new TimeoutException($"Provider {providerId} timed out after {timeout}")
        };

    public static ProviderOperationResult<TResult> Cancelled(string providerId) =>
        new()
        {
            ProviderId = providerId,
            IsSuccess = false,
            WasCancelled = true
        };
}

/// <summary>
/// Aggregated result of concurrent execution across multiple providers.
/// </summary>
public sealed record ConcurrentExecutionResult<TResult>
{
    public IReadOnlyList<ProviderOperationResult<TResult>> Results { get; init; } = Array.Empty<ProviderOperationResult<TResult>>();
    public bool HasSuccessfulResults => Results.Any(r => r.IsSuccess);
    public int SuccessCount => Results.Count(r => r.IsSuccess);
    public int FailureCount => Results.Count(r => !r.IsSuccess && !r.WasCancelled);
    public int CancelledCount => Results.Count(r => r.WasCancelled);
    public TimeSpan TotalDuration { get; init; }

    public IEnumerable<ProviderOperationResult<TResult>> SuccessfulResults =>
        Results.Where(r => r.IsSuccess);

    public IEnumerable<Exception> Errors =>
        Results.Where(r => r.Exception != null).Select(r => r.Exception!);

    public TResult FirstSuccessValue =>
        Results.FirstOrDefault(r => r.IsSuccess)?.Value!;

    public static ConcurrentExecutionResult<TResult> NoProviders() =>
        new() { Results = Array.Empty<ProviderOperationResult<TResult>>() };
}

/// <summary>
/// Executes operations across multiple providers concurrently with
/// configurable parallelism, error handling, and result aggregation.
/// </summary>
public sealed class ConcurrentProviderExecutor
{
    private readonly CircuitBreakerRegistry _circuitBreakers;
    private readonly ILogger _log;

    public ConcurrentProviderExecutor(CircuitBreakerRegistry? circuitBreakers = null, ILogger? log = null)
    {
        _circuitBreakers = circuitBreakers ?? new CircuitBreakerRegistry();
        _log = log ?? LoggingSetup.ForContext<ConcurrentProviderExecutor>();
    }

    /// <summary>
    /// Execute an operation on multiple providers concurrently.
    /// </summary>
    public async Task<ConcurrentExecutionResult<TResult>> ExecuteAsync<TResult>(
        IEnumerable<IDataProvider> providers,
        Func<IDataProvider, CancellationToken, Task<TResult>> operation,
        ConcurrentExecutionOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= ConcurrentExecutionOptions.Default;
        var providerList = providers.ToList();

        if (providerList.Count == 0)
            return ConcurrentExecutionResult<TResult>.NoProviders();

        var overallSw = Stopwatch.StartNew();
        using var overallCts = options.OverallTimeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : null;

        if (overallCts != null && options.OverallTimeout.HasValue)
            overallCts.CancelAfter(options.OverallTimeout.Value);

        var effectiveCt = overallCts?.Token ?? ct;
        var results = new ConcurrentBag<ProviderOperationResult<TResult>>();
        var successCount = 0;

        await Parallel.ForEachAsync(
            providerList,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.MaxConcurrency,
                CancellationToken = effectiveCt
            },
            async (provider, innerCt) =>
            {
                // Check if we should stop early
                if (options.StopOnFirstSuccess && Volatile.Read(ref successCount) > 0)
                    return;

                // Check circuit breaker
                if (!_circuitBreakers.IsProviderAvailable(provider.ProviderId))
                {
                    _log.Debug("Skipping provider {ProviderId} - circuit breaker open", provider.ProviderId);
                    results.Add(ProviderOperationResult<TResult>.Failed(
                        provider.ProviderId,
                        new CircuitBreakerOpenException(provider.ProviderId, null),
                        TimeSpan.Zero));
                    return;
                }

                var result = await ExecuteSingleAsync(provider, operation, options, innerCt).ConfigureAwait(false);
                results.Add(result);

                if (result.IsSuccess)
                    Interlocked.Increment(ref successCount);
            }).ConfigureAwait(false);

        overallSw.Stop();

        return new ConcurrentExecutionResult<TResult>
        {
            Results = results.ToList(),
            TotalDuration = overallSw.Elapsed
        };
    }

    /// <summary>
    /// Stream results from multiple providers as they complete.
    /// </summary>
    public async IAsyncEnumerable<ProviderOperationResult<TResult>> StreamResultsAsync<TResult>(
        IEnumerable<IDataProvider> providers,
        Func<IDataProvider, CancellationToken, Task<TResult>> operation,
        ConcurrentExecutionOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= ConcurrentExecutionOptions.Default;
        var providerList = providers.ToList();

        if (providerList.Count == 0)
            yield break;

        var channel = Channel.CreateUnbounded<ProviderOperationResult<TResult>>();

        // Start all operations
        var tasks = providerList.Select(async provider =>
        {
            // Check circuit breaker
            if (!_circuitBreakers.IsProviderAvailable(provider.ProviderId))
            {
                await channel.Writer.WriteAsync(
                    ProviderOperationResult<TResult>.Failed(
                        provider.ProviderId,
                        new CircuitBreakerOpenException(provider.ProviderId, null),
                        TimeSpan.Zero),
                    ct).ConfigureAwait(false);
                return;
            }

            var result = await ExecuteSingleAsync(provider, operation, options, ct).ConfigureAwait(false);
            await channel.Writer.WriteAsync(result, ct).ConfigureAwait(false);
        }).ToList();

        // Complete channel when all done
        _ = Task.WhenAll(tasks).ContinueWith(_ => channel.Writer.Complete(), ct);

        // Stream results as they arrive
        await foreach (var result in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return result;
        }
    }

    private async Task<ProviderOperationResult<TResult>> ExecuteSingleAsync<TResult>(
        IDataProvider provider,
        Func<IDataProvider, CancellationToken, Task<TResult>> operation,
        ConcurrentExecutionOptions options,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var circuitBreaker = _circuitBreakers.GetOrCreate(provider.ProviderId);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(options.PerProviderTimeout);

            var result = await circuitBreaker.ExecuteAsync(
                async token => await operation(provider, token).ConfigureAwait(false),
                timeoutCts.Token).ConfigureAwait(false);

            sw.Stop();

            _log.Debug("Provider {ProviderId} completed in {Duration}ms", provider.ProviderId, sw.ElapsedMilliseconds);

            return ProviderOperationResult<TResult>.Success(provider.ProviderId, result, sw.Elapsed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return ProviderOperationResult<TResult>.Cancelled(provider.ProviderId);
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Provider {ProviderId} timed out after {Timeout}", provider.ProviderId, options.PerProviderTimeout);
            return ProviderOperationResult<TResult>.Timeout(provider.ProviderId, options.PerProviderTimeout);
        }
        catch (CircuitBreakerOpenException ex)
        {
            _log.Debug("Provider {ProviderId} circuit breaker open", provider.ProviderId);
            return ProviderOperationResult<TResult>.Failed(provider.ProviderId, ex, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.Warning(ex, "Provider {ProviderId} failed after {Duration}ms", provider.ProviderId, sw.ElapsedMilliseconds);
            return ProviderOperationResult<TResult>.Failed(provider.ProviderId, ex, sw.Elapsed);
        }
    }

    /// <summary>
    /// Execute operation and return the first successful result, trying providers in order.
    /// </summary>
    public async Task<ProviderOperationResult<TResult>?> ExecuteFirstSuccessAsync<TResult>(
        IEnumerable<IDataProvider> providers,
        Func<IDataProvider, CancellationToken, Task<TResult>> operation,
        ConcurrentExecutionOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= ConcurrentExecutionOptions.FirstSuccess;

        var result = await ExecuteAsync(providers, operation, options, ct).ConfigureAwait(false);

        return result.SuccessfulResults.FirstOrDefault();
    }
}
