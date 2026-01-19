using System.Net;
using System.Runtime.CompilerServices;
using MarketDataCollector.Infrastructure.Plugins.Core;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins.Base;

/// <summary>
/// Base class for historical data plugins (REST APIs, etc.).
/// Provides rate limiting, batching, and retry logic.
/// </summary>
public abstract class HistoricalPluginBase : MarketDataPluginBase
{
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTimeOffset _windowStart = DateTimeOffset.UtcNow;
    private int _requestsInWindow;

    /// <summary>
    /// HTTP client for making requests.
    /// Subclasses should use this shared client.
    /// </summary>
    protected HttpClient HttpClient { get; private set; } = null!;

    /// <summary>
    /// Retry policy configuration.
    /// </summary>
    protected RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

    #region Abstract Methods

    /// <summary>
    /// Fetches historical bars for a single symbol.
    /// Implement the actual API call here.
    /// </summary>
    protected abstract Task<IReadOnlyList<BarEvent>> FetchBarsAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        string interval,
        bool adjusted,
        CancellationToken ct);

    #endregion

    #region Lifecycle

    protected override Task OnInitializeAsync(IPluginConfig config, CancellationToken ct)
    {
        HttpClient = CreateHttpClient();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates and configures the HTTP client.
    /// Override to customize headers, timeout, etc.
    /// </summary>
    protected virtual HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            MaxConnectionsPerServer = 10
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                { "User-Agent", $"MarketDataCollector/{Version}" }
            }
        };
    }

    #endregion

    #region IMarketDataPlugin Implementation

    public override async IAsyncEnumerable<MarketDataEvent> StreamAsync(
        DataStreamRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!request.IsHistorical)
        {
            throw new ArgumentException(
                $"HistoricalPluginBase {Id} only supports historical requests. " +
                "Use a real-time plugin for streaming.",
                nameof(request));
        }

        var from = request.From!.Value;
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var interval = request.BarInterval ?? "1day";
        var adjusted = request.AdjustedPrices;

        State = PluginState.Streaming;

        foreach (var symbol in request.Symbols)
        {
            ct.ThrowIfCancellationRequested();

            Logger.LogDebug("Fetching {Interval} bars for {Symbol} from {From} to {To}",
                interval, symbol, from, to);

            IReadOnlyList<BarEvent>? bars = null;

            // Fetch with retry and rate limiting
            await ExecuteWithRateLimitAsync(async () =>
            {
                bars = await FetchWithRetryAsync(symbol, from, to, interval, adjusted, ct)
                    .ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            if (bars != null)
            {
                Logger.LogDebug("Received {Count} bars for {Symbol}", bars.Count, symbol);

                foreach (var bar in bars)
                {
                    yield return bar;
                }
            }
        }

        State = PluginState.Ready;
    }

    #endregion

    #region Rate Limiting

    private async Task ExecuteWithRateLimitAsync(Func<Task> action, CancellationToken ct)
    {
        await _rateLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var rateLimit = Capabilities.RateLimit;
            var now = DateTimeOffset.UtcNow;

            // Reset window if needed
            if (now - _windowStart >= rateLimit.Window)
            {
                _windowStart = now;
                _requestsInWindow = 0;
            }

            // Check if we're at the limit
            if (_requestsInWindow >= rateLimit.MaxRequests)
            {
                var waitTime = rateLimit.Window - (now - _windowStart);
                if (waitTime > TimeSpan.Zero)
                {
                    RecordRateLimit(waitTime, 0, rateLimit.MaxRequests);
                    Logger.LogWarning(
                        "Rate limit reached for {PluginId}, waiting {WaitMs}ms",
                        Id, waitTime.TotalMilliseconds);

                    await Task.Delay(waitTime, ct).ConfigureAwait(false);

                    _windowStart = DateTimeOffset.UtcNow;
                    _requestsInWindow = 0;
                    State = PluginState.Ready;
                }
            }

            _requestsInWindow++;
            await action().ConfigureAwait(false);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    #endregion

    #region Retry Logic

    private async Task<IReadOnlyList<BarEvent>> FetchWithRetryAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        string interval,
        bool adjusted,
        CancellationToken ct)
    {
        int attempt = 0;
        var delay = RetryPolicy.InitialDelay;

        while (true)
        {
            attempt++;
            var startTime = DateTimeOffset.UtcNow;

            try
            {
                var bars = await FetchBarsAsync(symbol, from, to, interval, adjusted, ct)
                    .ConfigureAwait(false);

                var latency = DateTimeOffset.UtcNow - startTime;
                RecordSuccess(latency);

                return bars;
            }
            catch (HttpRequestException ex) when (IsRetryable(ex) && attempt <= RetryPolicy.MaxRetries)
            {
                RecordFailure($"Request failed: {ex.Message}", isRecoverable: true);
                Logger.LogWarning(ex,
                    "Request attempt {Attempt}/{MaxAttempts} failed for {Symbol}, retrying in {Delay}ms",
                    attempt, RetryPolicy.MaxRetries, symbol, delay.TotalMilliseconds);

                await Task.Delay(delay, ct).ConfigureAwait(false);

                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * RetryPolicy.BackoffMultiplier,
                             RetryPolicy.MaxDelay.TotalMilliseconds));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure($"Request failed: {ex.Message}", isRecoverable: false);
                Logger.LogError(ex, "Non-retryable error fetching {Symbol}", symbol);
                throw;
            }
        }
    }

    private static bool IsRetryable(HttpRequestException ex)
    {
        if (ex.StatusCode == null)
            return true; // Network error

        return ex.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            _ => false
        };
    }

    #endregion

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        HttpClient?.Dispose();
        _rateLimiter.Dispose();
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}

/// <summary>
/// Retry policy for historical plugins.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>
    /// Initial delay before first retry.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Default retry policy.
    /// </summary>
    public static RetryPolicy Default => new();

    /// <summary>
    /// Conservative retry policy for rate-limited APIs.
    /// </summary>
    public static RetryPolicy Conservative => new()
    {
        InitialDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromMinutes(1),
        BackoffMultiplier = 2.5,
        MaxRetries = 5
    };
}
