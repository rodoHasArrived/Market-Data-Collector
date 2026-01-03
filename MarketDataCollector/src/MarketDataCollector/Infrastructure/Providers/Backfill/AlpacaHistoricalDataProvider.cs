using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Alpaca Markets Data API v2.
/// Provides daily and intraday OHLCV bars with split/dividend adjustments.
/// Coverage: US equities, ETFs.
/// </summary>
public sealed class AlpacaHistoricalDataProvider : IHistoricalDataProviderV2, IRateLimitAwareProvider, IDisposable
{
    private const string BaseUrl = "https://data.alpaca.markets/v2/stocks";
    private const string EnvKeyId = "ALPACA_KEY_ID";
    private const string EnvSecretKey = "ALPACA_SECRET_KEY";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _keyId;
    private readonly string? _secretKey;
    private readonly string _feed;
    private readonly string _adjustment;
    private readonly int _priority;
    private int _requestCount;
    private DateTimeOffset _windowStart;
    private DateTimeOffset? _rateLimitResetsAt;
    private bool _isRateLimited;
    private bool _disposed;

    public string Name => "alpaca";
    public string DisplayName => "Alpaca Markets";
    public string Description => "Daily and intraday OHLCV bars with split/dividend adjustments for US equities.";

    public int Priority => _priority;
    public TimeSpan RateLimitDelay => TimeSpan.FromMilliseconds(300);
    public int MaxRequestsPerWindow { get; }
    public TimeSpan RateLimitWindow => TimeSpan.FromMinutes(1);

    public bool SupportsAdjustedPrices => true;
    public bool SupportsIntraday => true;
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US" };

    /// <summary>
    /// Event raised when the provider hits a rate limit (HTTP 429).
    /// </summary>
    public event Action<RateLimitInfo>? OnRateLimitHit;

    /// <summary>
    /// Creates a new Alpaca historical data provider.
    /// </summary>
    /// <param name="keyId">API Key ID (falls back to ALPACA_KEY_ID env var).</param>
    /// <param name="secretKey">API Secret Key (falls back to ALPACA_SECRET_KEY env var).</param>
    /// <param name="feed">Data feed: "iex" (free), "sip" (paid), or "delayed_sip" (free, 15-min delay).</param>
    /// <param name="adjustment">Price adjustment: "raw", "split", "dividend", or "all".</param>
    /// <param name="priority">Priority in fallback chain (lower = tried first).</param>
    /// <param name="rateLimitPerMinute">Maximum requests per minute.</param>
    /// <param name="httpClient">Optional HTTP client instance.</param>
    /// <param name="log">Optional logger instance.</param>
    public AlpacaHistoricalDataProvider(
        string? keyId = null,
        string? secretKey = null,
        string feed = "iex",
        string adjustment = "all",
        int priority = 5,
        int rateLimitPerMinute = 200,
        HttpClient? httpClient = null,
        ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<AlpacaHistoricalDataProvider>();
        _keyId = keyId ?? Environment.GetEnvironmentVariable(EnvKeyId);
        _secretKey = secretKey ?? Environment.GetEnvironmentVariable(EnvSecretKey);
        _feed = ValidateFeed(feed);
        _adjustment = ValidateAdjustment(adjustment);
        _priority = priority;
        MaxRequestsPerWindow = rateLimitPerMinute;

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        ConfigureHttpClient();

        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
        _windowStart = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Get current rate limit usage information.
    /// </summary>
    public RateLimitInfo GetRateLimitInfo()
    {
        // Reset window if expired
        if (DateTimeOffset.UtcNow - _windowStart > RateLimitWindow)
        {
            _requestCount = 0;
            _windowStart = DateTimeOffset.UtcNow;
            _isRateLimited = false;
            _rateLimitResetsAt = null;
        }

        return new RateLimitInfo(
            Name,
            _requestCount,
            MaxRequestsPerWindow,
            RateLimitWindow,
            _rateLimitResetsAt,
            _isRateLimited,
            _rateLimitResetsAt.HasValue ? _rateLimitResetsAt.Value - DateTimeOffset.UtcNow : null
        );
    }

    private void ConfigureHttpClient()
    {
        if (!string.IsNullOrEmpty(_keyId) && !string.IsNullOrEmpty(_secretKey))
        {
            _http.DefaultRequestHeaders.Add("APCA-API-KEY-ID", _keyId);
            _http.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", _secretKey);
        }
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static string ValidateFeed(string feed)
    {
        return feed.ToLowerInvariant() switch
        {
            "iex" or "sip" or "delayed_sip" => feed.ToLowerInvariant(),
            _ => throw new ArgumentException($"Invalid feed '{feed}'. Must be 'iex', 'sip', or 'delayed_sip'.", nameof(feed))
        };
    }

    private static string ValidateAdjustment(string adjustment)
    {
        return adjustment.ToLowerInvariant() switch
        {
            "raw" or "split" or "dividend" or "all" => adjustment.ToLowerInvariant(),
            _ => throw new ArgumentException($"Invalid adjustment '{adjustment}'. Must be 'raw', 'split', 'dividend', or 'all'.", nameof(adjustment))
        };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_secretKey))
        {
            _log.Warning("Alpaca API credentials not configured");
            return false;
        }

        try
        {
            // Quick health check with a known symbol
            var url = $"{BaseUrl}/AAPL/bars?timeframe=1Day&start=2024-01-02&end=2024-01-03&limit=1&feed={_feed}";
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Alpaca availability check failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var adjustedBars = await GetAdjustedDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);
        return adjustedBars.Select(b => b.ToHistoricalBar(preferAdjusted: true)).ToList();
    }

    public async Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_secretKey))
            throw new InvalidOperationException("Alpaca API credentials are required. Set ALPACA_KEY_ID and ALPACA_SECRET_KEY environment variables or provide them in configuration.");

        var normalizedSymbol = NormalizeSymbol(symbol);
        var allBars = new List<AdjustedHistoricalBar>();
        string? nextPageToken = null;

        do
        {
            await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

            // Track request count for rate limit reporting
            Interlocked.Increment(ref _requestCount);

            var url = BuildUrl(normalizedSymbol, from, to, nextPageToken);
            _log.Information("Requesting Alpaca history for {Symbol} ({Url})", symbol, url);

            try
            {
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var statusCode = (int)response.StatusCode;

                    if (statusCode == 403)
                    {
                        _log.Error("Alpaca API returned 403 for {Symbol}: Authentication failed. Verify API keys.", symbol);
                        throw new InvalidOperationException($"Alpaca API returned 403: Authentication failed for symbol {symbol}");
                    }

                    if (statusCode == 429)
                    {
                        // Extract Retry-After header if present
                        var retryAfter = ExtractRetryAfterFromResponse(response);
                        _isRateLimited = true;
                        _rateLimitResetsAt = DateTimeOffset.UtcNow + (retryAfter ?? RateLimitWindow);

                        var rateLimitInfo = GetRateLimitInfo();
                        OnRateLimitHit?.Invoke(rateLimitInfo);

                        _log.Warning("Alpaca API returned 429 for {Symbol}: Rate limit exceeded. Resets at {ResetsAt}",
                            symbol, _rateLimitResetsAt);
                        throw new InvalidOperationException($"Alpaca API returned 429: Rate limit exceeded for symbol {symbol}. Retry-After: {retryAfter?.TotalSeconds ?? 60}s");
                    }

                    if (statusCode == 404)
                    {
                        _log.Warning("Alpaca API returned 404 for {Symbol}: Symbol not found", symbol);
                        return Array.Empty<AdjustedHistoricalBar>();
                    }

                    _log.Warning("Alpaca API returned {Status} for {Symbol}: {Error}",
                        response.StatusCode, symbol, error);
                    throw new InvalidOperationException($"Alpaca API returned {statusCode} for symbol {symbol}");
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<AlpacaBarsResponse>(json);

                if (data?.Bars is null || data.Bars.Count == 0)
                {
                    if (allBars.Count == 0)
                    {
                        _log.Warning("No data returned from Alpaca for {Symbol}", symbol);
                    }
                    break;
                }

                foreach (var bar in data.Bars)
                {
                    if (bar.Timestamp is null) continue;

                    var sessionDate = DateOnly.FromDateTime(bar.Timestamp.Value.UtcDateTime);

                    // Skip if outside requested range
                    if (from.HasValue && sessionDate < from.Value) continue;
                    if (to.HasValue && sessionDate > to.Value) continue;

                    // Validate OHLC
                    if (bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0)
                        continue;

                    // Alpaca returns adjusted prices when adjustment is specified
                    // The raw prices are not available separately in the same response
                    var isAdjusted = _adjustment != "raw";

                    allBars.Add(new AdjustedHistoricalBar(
                        Symbol: symbol.ToUpperInvariant(),
                        SessionDate: sessionDate,
                        Open: bar.Open,
                        High: bar.High,
                        Low: bar.Low,
                        Close: bar.Close,
                        Volume: bar.Volume,
                        Source: Name,
                        SequenceNumber: sessionDate.DayNumber,
                        AdjustedOpen: isAdjusted ? bar.Open : null,
                        AdjustedHigh: isAdjusted ? bar.High : null,
                        AdjustedLow: isAdjusted ? bar.Low : null,
                        AdjustedClose: isAdjusted ? bar.Close : null,
                        AdjustedVolume: isAdjusted ? bar.Volume : null,
                        SplitFactor: null,
                        DividendAmount: null
                    ));
                }

                nextPageToken = data.NextPageToken;
            }
            catch (JsonException ex)
            {
                _log.Error(ex, "Failed to parse Alpaca response for {Symbol}", symbol);
                throw new InvalidOperationException($"Failed to parse Alpaca data for {symbol}", ex);
            }

        } while (!string.IsNullOrEmpty(nextPageToken));

        _log.Information("Fetched {Count} bars for {Symbol} from Alpaca", allBars.Count, symbol);
        return allBars.OrderBy(b => b.SessionDate).ToList();
    }

    private string BuildUrl(string symbol, DateOnly? from, DateOnly? to, string? pageToken)
    {
        var startDate = from?.ToString("yyyy-MM-dd") ?? "2000-01-01";
        var endDate = to?.AddDays(1).ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        var url = $"{BaseUrl}/{symbol}/bars?timeframe=1Day&start={startDate}&end={endDate}&limit=10000&feed={_feed}&adjustment={_adjustment}";

        if (!string.IsNullOrEmpty(pageToken))
        {
            url += $"&page_token={Uri.EscapeDataString(pageToken)}";
        }

        return url;
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Alpaca uses standard ticker symbols in uppercase
        return symbol.ToUpperInvariant().Trim();
    }

    private static TimeSpan? ExtractRetryAfterFromResponse(HttpResponseMessage response)
    {
        // Try to get Retry-After header
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var retryAfterValue = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(retryAfterValue))
            {
                // Try parsing as seconds
                if (int.TryParse(retryAfterValue, out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }

                // Try parsing as HTTP date
                if (DateTimeOffset.TryParse(retryAfterValue, out var retryDate))
                {
                    var delay = retryDate - DateTimeOffset.UtcNow;
                    if (delay > TimeSpan.Zero)
                        return delay;
                }
            }
        }

        // Default to 60 seconds if no Retry-After header
        return TimeSpan.FromSeconds(60);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Alpaca API Models

    private sealed class AlpacaBarsResponse
    {
        [JsonPropertyName("bars")]
        public List<AlpacaBar>? Bars { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken { get; set; }
    }

    private sealed class AlpacaBar
    {
        [JsonPropertyName("t")]
        public DateTimeOffset? Timestamp { get; set; }

        [JsonPropertyName("o")]
        public decimal Open { get; set; }

        [JsonPropertyName("h")]
        public decimal High { get; set; }

        [JsonPropertyName("l")]
        public decimal Low { get; set; }

        [JsonPropertyName("c")]
        public decimal Close { get; set; }

        [JsonPropertyName("v")]
        public long Volume { get; set; }

        [JsonPropertyName("n")]
        public int TradeCount { get; set; }

        [JsonPropertyName("vw")]
        public decimal VolumeWeightedAvgPrice { get; set; }
    }

    #endregion
}
