using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Polygon.io REST API (free tier with API key).
/// Provides high-quality OHLCV aggregates with trades, quotes, and reference data.
/// Coverage: US equities, options, forex, crypto.
/// Free tier: 5 API calls/minute, delayed data, 2 years history.
/// </summary>
public sealed class PolygonHistoricalDataProvider : IHistoricalDataProviderV2, IDisposable
{
    private const string BaseUrl = "https://api.polygon.io";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _apiKey;
    private bool _disposed;

    public string Name => "polygon";
    public string DisplayName => "Polygon.io (free tier)";
    public string Description => "High-quality OHLCV aggregates for US equities with 2-year history on free tier.";

    public int Priority => 12;
    public TimeSpan RateLimitDelay => TimeSpan.FromSeconds(12); // 5 requests/minute = 12 seconds between requests
    public int MaxRequestsPerWindow => 5;
    public TimeSpan RateLimitWindow => TimeSpan.FromMinutes(1);

    public bool SupportsAdjustedPrices => true;
    public bool SupportsIntraday => true; // Polygon supports intraday aggregates
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US" };

    public PolygonHistoricalDataProvider(string? apiKey = null, HttpClient? httpClient = null, ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<PolygonHistoricalDataProvider>();
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("POLYGON_API_KEY");

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");

        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _log.Warning("Polygon API key not configured. Set POLYGON_API_KEY environment variable or configure in settings.");
            return false;
        }

        try
        {
            // Quick health check with ticker details endpoint
            var url = $"{BaseUrl}/v3/reference/tickers/AAPL?apiKey={_apiKey}";
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
        return adjustedBars.Select(b => b.ToHistoricalBar(preferAdjusted: true)).ToList();
    }

    public async Task<IReadOnlyList<AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Polygon API key is required. Set POLYGON_API_KEY environment variable.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Build URL with date range
        // Free tier limited to 2 years
        var startDate = from?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)).ToString("yyyy-MM-dd");
        var endDate = to?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Use aggregates/range endpoint with adjusted=true for split/dividend adjusted data
        var url = $"{BaseUrl}/v2/aggs/ticker/{normalizedSymbol}/range/1/day/{startDate}/{endDate}?adjusted=true&sort=asc&limit=50000&apiKey={_apiKey}";

        _log.Information("Requesting Polygon history for {Symbol} ({StartDate} to {EndDate})", symbol, startDate, endDate);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning("Polygon returned {Status} for {Symbol}: {Error}",
                    response.StatusCode, symbol, error);

                if ((int)response.StatusCode == 429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
                    throw new HttpRequestException($"Polygon rate limit exceeded (429) for {symbol}. Retry-After: {retryAfter}");
                }

                throw new InvalidOperationException($"Polygon returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<PolygonAggregatesResponse>(json);

            if (data?.Results is null || data.Results.Count == 0)
            {
                _log.Warning("No data returned from Polygon for {Symbol} (resultsCount: {Count})",
                    symbol, data?.ResultsCount ?? 0);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var bars = new List<AdjustedHistoricalBar>();

            foreach (var result in data.Results)
            {
                // Polygon timestamp is in milliseconds
                var date = DateTimeOffset.FromUnixTimeMilliseconds(result.Timestamp).Date;
                var sessionDate = DateOnly.FromDateTime(date);

                // Skip if outside requested range
                if (from.HasValue && sessionDate < from.Value) continue;
                if (to.HasValue && sessionDate > to.Value) continue;

                // Validate OHLC
                if (result.Open <= 0 || result.High <= 0 || result.Low <= 0 || result.Close <= 0)
                    continue;

                bars.Add(new AdjustedHistoricalBar(
                    Symbol: symbol.ToUpperInvariant(),
                    SessionDate: sessionDate,
                    Open: (decimal)result.Open,
                    High: (decimal)result.High,
                    Low: (decimal)result.Low,
                    Close: (decimal)result.Close,
                    Volume: (long)(result.Volume ?? 0),
                    Source: Name,
                    SequenceNumber: sessionDate.DayNumber,
                    // Polygon returns adjusted prices by default when adjusted=true
                    // The raw prices are the adjusted prices in this case
                    AdjustedOpen: (decimal)result.Open,
                    AdjustedHigh: (decimal)result.High,
                    AdjustedLow: (decimal)result.Low,
                    AdjustedClose: (decimal)result.Close,
                    AdjustedVolume: (long)(result.Volume ?? 0),
                    SplitFactor: null, // Would need separate call to get split info
                    DividendAmount: null // Would need separate call to get dividend info
                ));
            }

            _log.Information("Fetched {Count} bars for {Symbol} from Polygon", bars.Count, symbol);
            return bars.OrderBy(b => b.SessionDate).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Polygon response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Polygon data for {symbol}", ex);
        }
    }

    /// <summary>
    /// Get intraday bars for a symbol. Polygon supports various intervals.
    /// </summary>
    public async Task<IReadOnlyList<AdjustedHistoricalBar>> GetIntradayBarsAsync(
        string symbol,
        string interval,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Polygon API key is required.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Parse interval (e.g., "1min", "5min", "15min", "1hour")
        var (multiplier, timespan) = ParseInterval(interval);

        var startDate = from?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)).ToString("yyyy-MM-dd");
        var endDate = to?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var url = $"{BaseUrl}/v2/aggs/ticker/{normalizedSymbol}/range/{multiplier}/{timespan}/{startDate}/{endDate}?adjusted=true&sort=asc&limit=50000&apiKey={_apiKey}";

        _log.Information("Requesting Polygon {Interval} bars for {Symbol} ({StartDate} to {EndDate})",
            interval, symbol, startDate, endDate);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Polygon rate limit exceeded (429) for {symbol}");
                }

                throw new InvalidOperationException($"Polygon returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<PolygonAggregatesResponse>(json);

            if (data?.Results is null || data.Results.Count == 0)
            {
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var bars = new List<AdjustedHistoricalBar>();

            foreach (var result in data.Results)
            {
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(result.Timestamp);
                var sessionDate = DateOnly.FromDateTime(timestamp.Date);

                if (result.Open <= 0 || result.High <= 0 || result.Low <= 0 || result.Close <= 0)
                    continue;

                bars.Add(new AdjustedHistoricalBar(
                    Symbol: symbol.ToUpperInvariant(),
                    SessionDate: sessionDate,
                    Open: (decimal)result.Open,
                    High: (decimal)result.High,
                    Low: (decimal)result.Low,
                    Close: (decimal)result.Close,
                    Volume: (long)(result.Volume ?? 0),
                    Source: Name,
                    SequenceNumber: result.Timestamp,
                    AdjustedOpen: (decimal)result.Open,
                    AdjustedHigh: (decimal)result.High,
                    AdjustedLow: (decimal)result.Low,
                    AdjustedClose: (decimal)result.Close,
                    AdjustedVolume: (long)(result.Volume ?? 0)
                ));
            }

            _log.Information("Fetched {Count} {Interval} bars for {Symbol} from Polygon",
                bars.Count, interval, symbol);
            return bars;
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Polygon intraday response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Polygon intraday data for {symbol}", ex);
        }
    }

    /// <summary>
    /// Get stock splits for a symbol.
    /// </summary>
    public async Task<IReadOnlyList<SplitInfo>> GetSplitsAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Polygon API key is required.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);
        var url = $"{BaseUrl}/v3/reference/splits?ticker={normalizedSymbol}&apiKey={_apiKey}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<SplitInfo>();
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<PolygonSplitsResponse>(json);

            if (data?.Results is null)
                return Array.Empty<SplitInfo>();

            return data.Results.Select(s => new SplitInfo(
                Symbol: symbol.ToUpperInvariant(),
                ExDate: DateOnly.ParseExact(s.ExecutionDate ?? s.ExDate ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture),
                SplitFrom: (decimal)(s.SplitFrom ?? 1),
                SplitTo: (decimal)(s.SplitTo ?? 1),
                Source: Name
            )).ToList();
        }
        catch
        {
            return Array.Empty<SplitInfo>();
        }
    }

    /// <summary>
    /// Get dividends for a symbol.
    /// </summary>
    public async Task<IReadOnlyList<DividendInfo>> GetDividendsAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Polygon API key is required.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);
        var url = $"{BaseUrl}/v3/reference/dividends?ticker={normalizedSymbol}&apiKey={_apiKey}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<DividendInfo>();
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<PolygonDividendsResponse>(json);

            if (data?.Results is null)
                return Array.Empty<DividendInfo>();

            return data.Results.Select(d => new DividendInfo(
                Symbol: symbol.ToUpperInvariant(),
                ExDate: DateOnly.ParseExact(d.ExDividendDate ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture),
                PaymentDate: !string.IsNullOrEmpty(d.PayDate) ? DateOnly.ParseExact(d.PayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
                RecordDate: !string.IsNullOrEmpty(d.RecordDate) ? DateOnly.ParseExact(d.RecordDate, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
                Amount: (decimal)(d.CashAmount ?? 0),
                Currency: d.Currency ?? "USD",
                Type: ParseDividendType(d.DividendType),
                Source: Name
            )).ToList();
        }
        catch
        {
            return Array.Empty<DividendInfo>();
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Polygon uses standard uppercase tickers
        return symbol.ToUpperInvariant();
    }

    private static (int multiplier, string timespan) ParseInterval(string interval)
    {
        var normalized = interval.ToLowerInvariant().Replace(" ", "");

        return normalized switch
        {
            "1min" or "1m" or "minute" => (1, "minute"),
            "5min" or "5m" => (5, "minute"),
            "15min" or "15m" => (15, "minute"),
            "30min" or "30m" => (30, "minute"),
            "1hour" or "1h" or "hour" => (1, "hour"),
            "4hour" or "4h" => (4, "hour"),
            "1day" or "1d" or "day" or "daily" => (1, "day"),
            "1week" or "1w" or "week" => (1, "week"),
            "1month" or "1mo" or "month" => (1, "month"),
            _ => (1, "day")
        };
    }

    private static DividendType ParseDividendType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "cd" or "regular" => DividendType.Regular,
            "sc" or "special" => DividendType.Special,
            "lt" or "return" => DividendType.Return,
            "lq" or "liquidation" => DividendType.Liquidation,
            _ => DividendType.Regular
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Polygon API Models

    private sealed class PolygonAggregatesResponse
    {
        [JsonPropertyName("ticker")]
        public string? Ticker { get; set; }

        [JsonPropertyName("queryCount")]
        public int QueryCount { get; set; }

        [JsonPropertyName("resultsCount")]
        public int ResultsCount { get; set; }

        [JsonPropertyName("adjusted")]
        public bool Adjusted { get; set; }

        [JsonPropertyName("results")]
        public List<PolygonAggregate>? Results { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("next_url")]
        public string? NextUrl { get; set; }
    }

    private sealed class PolygonAggregate
    {
        [JsonPropertyName("o")]
        public double Open { get; set; }

        [JsonPropertyName("h")]
        public double High { get; set; }

        [JsonPropertyName("l")]
        public double Low { get; set; }

        [JsonPropertyName("c")]
        public double Close { get; set; }

        [JsonPropertyName("v")]
        public double? Volume { get; set; }

        [JsonPropertyName("vw")]
        public double? VWAP { get; set; }

        [JsonPropertyName("t")]
        public long Timestamp { get; set; }

        [JsonPropertyName("n")]
        public int? NumberOfTrades { get; set; }
    }

    private sealed class PolygonSplitsResponse
    {
        [JsonPropertyName("results")]
        public List<PolygonSplit>? Results { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private sealed class PolygonSplit
    {
        [JsonPropertyName("execution_date")]
        public string? ExecutionDate { get; set; }

        [JsonPropertyName("ex_date")]
        public string? ExDate { get; set; }

        [JsonPropertyName("split_from")]
        public double? SplitFrom { get; set; }

        [JsonPropertyName("split_to")]
        public double? SplitTo { get; set; }

        [JsonPropertyName("ticker")]
        public string? Ticker { get; set; }
    }

    private sealed class PolygonDividendsResponse
    {
        [JsonPropertyName("results")]
        public List<PolygonDividend>? Results { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private sealed class PolygonDividend
    {
        [JsonPropertyName("cash_amount")]
        public double? CashAmount { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("declaration_date")]
        public string? DeclarationDate { get; set; }

        [JsonPropertyName("dividend_type")]
        public string? DividendType { get; set; }

        [JsonPropertyName("ex_dividend_date")]
        public string? ExDividendDate { get; set; }

        [JsonPropertyName("frequency")]
        public int? Frequency { get; set; }

        [JsonPropertyName("pay_date")]
        public string? PayDate { get; set; }

        [JsonPropertyName("record_date")]
        public string? RecordDate { get; set; }

        [JsonPropertyName("ticker")]
        public string? Ticker { get; set; }
    }

    #endregion
}
