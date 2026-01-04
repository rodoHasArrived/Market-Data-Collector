using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.DataSources;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Finnhub API (free tier with API key).
/// Generous free tier: 60 API calls/minute.
/// Coverage: 60,000+ global securities with company fundamentals.
/// Best for: Earnings data, fundamentals, news, and high-frequency backfill operations.
/// </summary>
public sealed class FinnhubHistoricalDataProvider : IHistoricalDataProviderV2, IDisposable
{
    private const string BaseUrl = "https://finnhub.io/api/v1";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _apiKey;
    private bool _disposed;

    public string Name => "finnhub";
    public string DisplayName => "Finnhub (free tier)";
    public string Description => "Global equities with generous 60 calls/min free tier. Includes fundamentals, earnings, and news.";

    public int Priority => 18;
    public TimeSpan RateLimitDelay => TimeSpan.FromSeconds(1); // 60 requests/minute = 1 second between requests
    public int MaxRequestsPerWindow => 60;
    public TimeSpan RateLimitWindow => TimeSpan.FromMinutes(1);

    public bool SupportsAdjustedPrices => false; // Finnhub returns raw prices
    public bool SupportsIntraday => true;
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US", "UK", "DE", "CA", "AU", "HK", "JP", "CN" };

    /// <summary>
    /// Supported candle resolutions.
    /// </summary>
    public static IReadOnlyList<string> SupportedResolutions => new[] { "1", "5", "15", "30", "60", "D", "W", "M" };

    public FinnhubHistoricalDataProvider(string? apiKey = null, HttpClient? httpClient = null, ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<FinnhubHistoricalDataProvider>();
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("FINNHUB_API_KEY");

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Finnhub-Token", _apiKey);
        }

        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _log.Warning("Finnhub API key not configured. Set FINNHUB_API_KEY environment variable or configure in settings.");
            return false;
        }

        try
        {
            // Quick health check with quote endpoint
            var url = $"{BaseUrl}/quote?symbol=AAPL&token={_apiKey}";
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var adjustedBars = await GetAdjustedDailyBarsAsync(symbol, from, to, ct).ConfigureAwait(false);
        return adjustedBars.Select(b => b.ToHistoricalBar(preferAdjusted: false)).ToList();
    }

    public async Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Finnhub API key is required. Set FINNHUB_API_KEY environment variable.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Convert dates to Unix timestamps
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var fromUnix = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var toUnix = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds();

        // Use stock/candle endpoint with D (daily) resolution
        var url = $"{BaseUrl}/stock/candle?symbol={normalizedSymbol}&resolution=D&from={fromUnix}&to={toUnix}&token={_apiKey}";

        _log.Information("Requesting Finnhub daily history for {Symbol} ({From} to {To})", symbol, fromDate, toDate);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning("Finnhub returned {Status} for {Symbol}: {Error}",
                    response.StatusCode, symbol, error);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Finnhub rate limit exceeded (429) for {symbol}. Retry-After: 60");
                }

                throw new InvalidOperationException($"Finnhub returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<FinnhubCandleResponse>(json);

            if (data?.Status == "no_data" || data?.Close is null || data.Close.Length == 0)
            {
                _log.Warning("No data returned from Finnhub for {Symbol}", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var bars = new List<AdjustedHistoricalBar>();
            var timestamps = data.Timestamp ?? Array.Empty<long>();

            for (int i = 0; i < timestamps.Length; i++)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).Date;
                var sessionDate = DateOnly.FromDateTime(date);

                // Skip if outside requested range
                if (from.HasValue && sessionDate < from.Value) continue;
                if (to.HasValue && sessionDate > to.Value) continue;

                var open = GetDecimalValue(data.Open, i);
                var high = GetDecimalValue(data.High, i);
                var low = GetDecimalValue(data.Low, i);
                var close = GetDecimalValue(data.Close, i);
                var volume = GetLongValue(data.Volume, i);

                // Validate OHLC
                if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                    continue;

                bars.Add(new AdjustedHistoricalBar(
                    Symbol: symbol.ToUpperInvariant(),
                    SessionDate: sessionDate,
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close,
                    Volume: volume,
                    Source: Name,
                    SequenceNumber: sessionDate.DayNumber,
                    // Finnhub doesn't provide adjusted prices directly
                    AdjustedOpen: null,
                    AdjustedHigh: null,
                    AdjustedLow: null,
                    AdjustedClose: null,
                    AdjustedVolume: null,
                    SplitFactor: null,
                    DividendAmount: null
                ));
            }

            _log.Information("Fetched {Count} bars for {Symbol} from Finnhub", bars.Count, symbol);
            return bars.OrderBy(b => b.SessionDate).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Finnhub response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Finnhub data for {symbol}", ex);
        }
    }

    /// <summary>
    /// Get intraday bars for a symbol.
    /// </summary>
    public async Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Finnhub API key is required.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedResolution = NormalizeResolution(resolution);

        // Convert dates to Unix timestamps
        var fromUnix = (from ?? DateTimeOffset.UtcNow.AddDays(-30)).ToUnixTimeSeconds();
        var toUnix = (to ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();

        var url = $"{BaseUrl}/stock/candle?symbol={normalizedSymbol}&resolution={normalizedResolution}&from={fromUnix}&to={toUnix}&token={_apiKey}";

        _log.Information("Requesting Finnhub {Resolution} bars for {Symbol}", normalizedResolution, symbol);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Finnhub rate limit exceeded (429) for {symbol}");
                }

                throw new InvalidOperationException($"Finnhub returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<FinnhubCandleResponse>(json);

            if (data?.Status == "no_data" || data?.Close is null || data.Close.Length == 0)
            {
                return Array.Empty<IntradayBar>();
            }

            var bars = new List<IntradayBar>();
            var timestamps = data.Timestamp ?? Array.Empty<long>();

            for (int i = 0; i < timestamps.Length; i++)
            {
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]);

                // Skip if outside requested range
                if (from.HasValue && timestamp < from.Value) continue;
                if (to.HasValue && timestamp > to.Value) continue;

                var open = GetDecimalValue(data.Open, i);
                var high = GetDecimalValue(data.High, i);
                var low = GetDecimalValue(data.Low, i);
                var close = GetDecimalValue(data.Close, i);
                var volume = GetLongValue(data.Volume, i);

                if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                    continue;

                bars.Add(new IntradayBar(
                    Symbol: symbol.ToUpperInvariant(),
                    Timestamp: timestamp,
                    Interval: ResolutionToInterval(normalizedResolution),
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close,
                    Volume: volume,
                    Source: Name
                ));
            }

            _log.Information("Fetched {Count} {Resolution} bars for {Symbol} from Finnhub",
                bars.Count, normalizedResolution, symbol);
            return bars.OrderBy(b => b.Timestamp).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Finnhub intraday response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Finnhub intraday data for {symbol}", ex);
        }
    }

    /// <summary>
    /// Get basic earnings data for a symbol.
    /// </summary>
    public async Task<IReadOnlyList<FinnhubEarning>> GetEarningsAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Finnhub API key is required.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);
        var url = $"{BaseUrl}/stock/earnings?symbol={normalizedSymbol}&token={_apiKey}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<FinnhubEarning>();
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<List<FinnhubEarning>>(json);

            return data ?? Array.Empty<FinnhubEarning>().ToList();
        }
        catch
        {
            return Array.Empty<FinnhubEarning>();
        }
    }

    /// <summary>
    /// Get company profile data.
    /// </summary>
    public async Task<FinnhubCompanyProfile?> GetCompanyProfileAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Finnhub API key is required.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);
        var url = $"{BaseUrl}/stock/profile2?symbol={normalizedSymbol}&token={_apiKey}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<FinnhubCompanyProfile>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Finnhub uses standard uppercase tickers
        return symbol.ToUpperInvariant();
    }

    private static string NormalizeResolution(string resolution)
    {
        return resolution.ToUpperInvariant() switch
        {
            "1MIN" or "1M" or "1" => "1",
            "5MIN" or "5M" or "5" => "5",
            "15MIN" or "15M" or "15" => "15",
            "30MIN" or "30M" or "30" => "30",
            "60MIN" or "60M" or "1H" or "60" => "60",
            "DAILY" or "1D" or "D" => "D",
            "WEEKLY" or "1W" or "W" => "W",
            "MONTHLY" or "1MO" or "M" => "M",
            _ => resolution
        };
    }

    private static string ResolutionToInterval(string resolution)
    {
        return resolution switch
        {
            "1" => "1min",
            "5" => "5min",
            "15" => "15min",
            "30" => "30min",
            "60" => "1hour",
            "D" => "1day",
            "W" => "1week",
            "M" => "1month",
            _ => resolution
        };
    }

    private static decimal GetDecimalValue(double[]? array, int index)
    {
        if (array is null || index >= array.Length)
            return 0m;
        return (decimal)array[index];
    }

    private static long GetLongValue(double[]? array, int index)
    {
        if (array is null || index >= array.Length)
            return 0;
        return (long)array[index];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Finnhub API Models

    private sealed class FinnhubCandleResponse
    {
        [JsonPropertyName("c")]
        public double[]? Close { get; set; }

        [JsonPropertyName("h")]
        public double[]? High { get; set; }

        [JsonPropertyName("l")]
        public double[]? Low { get; set; }

        [JsonPropertyName("o")]
        public double[]? Open { get; set; }

        [JsonPropertyName("s")]
        public string? Status { get; set; }

        [JsonPropertyName("t")]
        public long[]? Timestamp { get; set; }

        [JsonPropertyName("v")]
        public double[]? Volume { get; set; }
    }

    #endregion
}

#region Finnhub Data Types

/// <summary>
/// Finnhub earnings data.
/// </summary>
public sealed record FinnhubEarning
{
    [JsonPropertyName("actual")]
    public decimal? Actual { get; init; }

    [JsonPropertyName("estimate")]
    public decimal? Estimate { get; init; }

    [JsonPropertyName("period")]
    public string? Period { get; init; }

    [JsonPropertyName("quarter")]
    public int? Quarter { get; init; }

    [JsonPropertyName("surprise")]
    public decimal? Surprise { get; init; }

    [JsonPropertyName("surprisePercent")]
    public decimal? SurprisePercent { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("year")]
    public int? Year { get; init; }
}

/// <summary>
/// Finnhub company profile data.
/// </summary>
public sealed record FinnhubCompanyProfile
{
    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    [JsonPropertyName("finnhubIndustry")]
    public string? Industry { get; init; }

    [JsonPropertyName("ipo")]
    public string? IpoDate { get; init; }

    [JsonPropertyName("logo")]
    public string? LogoUrl { get; init; }

    [JsonPropertyName("marketCapitalization")]
    public decimal? MarketCap { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("shareOutstanding")]
    public decimal? SharesOutstanding { get; init; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    [JsonPropertyName("weburl")]
    public string? WebUrl { get; init; }
}

#endregion
