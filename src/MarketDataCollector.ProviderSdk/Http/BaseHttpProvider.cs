using System.Net;
using System.Text.Json;
using MarketDataCollector.ProviderSdk.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.ProviderSdk.Http;

/// <summary>
/// Base class for HTTP-based providers that need rate limiting, error handling,
/// and common HTTP patterns. Provider plugins can extend this to reduce boilerplate.
/// </summary>
public abstract class BaseHttpProvider : IDisposable
{
    protected readonly HttpClient Http;
    protected readonly RateLimiter RateLimiter;
    protected readonly ILogger Logger;
    protected bool Disposed;

    /// <summary>
    /// The provider identifier for logging and error context.
    /// </summary>
    protected abstract string ProviderName { get; }

    protected BaseHttpProvider(
        HttpClient httpClient,
        ILogger logger,
        int maxRequestsPerWindow = int.MaxValue,
        TimeSpan? rateLimitWindow = null,
        TimeSpan? minDelay = null)
    {
        Http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RateLimiter = new RateLimiter(
            maxRequestsPerWindow,
            rateLimitWindow ?? TimeSpan.FromHours(1),
            minDelay,
            logger);
    }

    /// <summary>
    /// Execute an HTTP GET request with rate limiting and error handling.
    /// </summary>
    protected async Task<string?> GetStringAsync(string url, string symbol, CancellationToken ct)
    {
        ThrowIfDisposed();
        await RateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        Logger.LogDebug("Requesting {Provider} data for {Symbol}: {Url}", ProviderName, symbol, url);

        using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
        return await HandleResponseAsync(response, symbol, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle an HTTP response, mapping error status codes to provider exceptions.
    /// </summary>
    protected virtual async Task<string?> HandleResponseAsync(
        HttpResponseMessage response,
        string symbol,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        var statusCode = (int)response.StatusCode;

        if (statusCode == 404)
        {
            Logger.LogDebug("{Provider} returned 404 for {Symbol}", ProviderName, symbol);
            return null;
        }

        if (statusCode is 401 or 403)
        {
            throw new ProviderConnectionException(
                $"{ProviderName} API returned {statusCode}: authentication failed for {symbol}",
                providerId: ProviderName);
        }

        if (statusCode == 429)
        {
            var retryAfter = ExtractRetryAfter(response);
            throw new ProviderRateLimitException(
                $"{ProviderName} API rate limit exceeded for {symbol}",
                providerId: ProviderName,
                symbol: symbol,
                retryAfter: retryAfter);
        }

        throw new ProviderException(
            $"{ProviderName} API returned {statusCode} for {symbol}",
            providerId: ProviderName,
            symbol: symbol);
    }

    /// <summary>
    /// Deserialize JSON response with error handling.
    /// </summary>
    protected T? DeserializeJson<T>(string? json, string symbol) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to parse {Provider} response for {Symbol}", ProviderName, symbol);
            throw new ProviderException(
                $"Failed to parse {ProviderName} data for {symbol}",
                providerId: ProviderName,
                symbol: symbol);
        }
    }

    /// <summary>
    /// Validate OHLC data is valid (all prices > 0).
    /// </summary>
    protected static bool IsValidOhlc(decimal open, decimal high, decimal low, decimal close)
    {
        return open > 0 && high > 0 && low > 0 && close > 0;
    }

    /// <summary>
    /// Validate that a symbol is provided.
    /// </summary>
    protected static void ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));
    }

    /// <summary>
    /// Extract Retry-After header value from an HTTP response.
    /// </summary>
    protected static TimeSpan? ExtractRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1);
        }

        return null;
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        Disposed = true;

        if (disposing)
        {
            RateLimiter.Dispose();
        }
    }
}
