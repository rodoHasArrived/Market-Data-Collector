using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.DataSources;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Alpha Vantage API (free tier with API key).
/// Unique capability: Intraday historical data (1, 5, 15, 30, 60 min intervals).
/// Coverage: US equities, global indices, forex, crypto.
/// Free tier: 25 requests/day (severely limited), 5 calls/minute.
/// </summary>
public sealed class AlphaVantageHistoricalDataProvider : IHistoricalDataProvider, IDisposable
{
    private const string BaseUrl = "https://www.alphavantage.co/query";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _apiKey;
    private bool _disposed;

    public string Name => "alphavantage";
    public string DisplayName => "Alpha Vantage (free tier)";
    public string Description => "US equities with unique intraday historical data support. Limited free tier (25 req/day).";

    public int Priority => 25; // Lower priority due to very limited free tier
    public TimeSpan RateLimitDelay => TimeSpan.FromSeconds(12); // 5 requests/minute = 12 seconds between requests
    public int MaxRequestsPerWindow => 5;
    public TimeSpan RateLimitWindow => TimeSpan.FromMinutes(1);

    public bool SupportsAdjustedPrices => true;
    public bool SupportsIntraday => true; // Key differentiator!
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US" };

    /// <summary>
    /// Supported intraday intervals.
    /// </summary>
    public static IReadOnlyList<string> SupportedIntervals => new[] { "1min", "5min", "15min", "30min", "60min" };

    public AlphaVantageHistoricalDataProvider(string? apiKey = null, HttpClient? httpClient = null, ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<AlphaVantageHistoricalDataProvider>();
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ALPHA_VANTAGE_API_KEY");

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");

        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _log.Warning("Alpha Vantage API key not configured. Set ALPHA_VANTAGE_API_KEY environment variable or configure in settings.");
            return false;
        }

        try
        {
            // Quick health check with quote endpoint
            var url = $"{BaseUrl}?function=GLOBAL_QUOTE&symbol=AAPL&apikey={_apiKey}";
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            // Check for rate limit error message in response
            return !json.Contains("Note") && !json.Contains("Thank you for using Alpha Vantage");
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
            throw new InvalidOperationException("Alpha Vantage API key is required. Set ALPHA_VANTAGE_API_KEY environment variable.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Use TIME_SERIES_DAILY_ADJUSTED for full history with adjusted prices
        var url = $"{BaseUrl}?function=TIME_SERIES_DAILY_ADJUSTED&symbol={normalizedSymbol}&outputsize=full&apikey={_apiKey}";

        _log.Information("Requesting Alpha Vantage daily adjusted history for {Symbol}", symbol);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning("Alpha Vantage returned {Status} for {Symbol}: {Error}",
                    response.StatusCode, symbol, error);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Alpha Vantage rate limit exceeded (429) for {symbol}. Retry-After: 60");
                }

                throw new InvalidOperationException($"Alpha Vantage returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Check for API error messages in response
            if (json.Contains("\"Note\"") || json.Contains("Thank you for using Alpha Vantage"))
            {
                _log.Warning("Alpha Vantage rate limit hit for {Symbol}. Message in response.", symbol);
                throw new HttpRequestException($"Alpha Vantage rate limit exceeded for {symbol}. Please wait before retrying.");
            }

            if (json.Contains("\"Error Message\""))
            {
                _log.Warning("Alpha Vantage error for {Symbol}: Invalid symbol or other error", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var data = JsonSerializer.Deserialize<AlphaVantageDailyAdjustedResponse>(json);

            if (data?.TimeSeries is null || data.TimeSeries.Count == 0)
            {
                _log.Warning("No data returned from Alpha Vantage for {Symbol}", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var bars = new List<AdjustedHistoricalBar>();

            foreach (var kvp in data.TimeSeries)
            {
                if (!DateOnly.TryParse(kvp.Key, out var sessionDate))
                    continue;

                // Skip if outside requested range
                if (from.HasValue && sessionDate < from.Value) continue;
                if (to.HasValue && sessionDate > to.Value) continue;

                var price = kvp.Value;

                // Parse values
                if (!TryParseDecimal(price.Open, out var open)) continue;
                if (!TryParseDecimal(price.High, out var high)) continue;
                if (!TryParseDecimal(price.Low, out var low)) continue;
                if (!TryParseDecimal(price.Close, out var close)) continue;
                TryParseLong(price.Volume, out var volume);
                TryParseDecimal(price.AdjustedClose, out var adjClose);
                TryParseDecimal(price.DividendAmount, out var dividend);
                TryParseDecimal(price.SplitCoefficient, out var splitCoeff);

                // Validate OHLC
                if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                    continue;

                // Calculate adjustment factor
                decimal? splitFactor = null;
                if (adjClose > 0 && close > 0)
                {
                    var factor = adjClose / close;
                    if (Math.Abs(factor - 1m) > 0.0001m)
                    {
                        splitFactor = factor;
                    }
                }

                // Also use split coefficient if provided
                if (splitCoeff != 1m && splitCoeff > 0)
                {
                    splitFactor = splitCoeff;
                }

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
                    AdjustedOpen: splitFactor.HasValue ? open * splitFactor.Value : null,
                    AdjustedHigh: splitFactor.HasValue ? high * splitFactor.Value : null,
                    AdjustedLow: splitFactor.HasValue ? low * splitFactor.Value : null,
                    AdjustedClose: adjClose > 0 ? adjClose : null,
                    AdjustedVolume: null,
                    SplitFactor: splitCoeff != 1m && splitCoeff > 0 ? splitCoeff : splitFactor,
                    DividendAmount: dividend > 0 ? dividend : null
                ));
            }

            _log.Information("Fetched {Count} bars for {Symbol} from Alpha Vantage", bars.Count, symbol);
            return bars.OrderBy(b => b.SessionDate).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Alpha Vantage response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Alpha Vantage data for {symbol}", ex);
        }
    }

    /// <summary>
    /// Get intraday historical bars. This is the unique capability of Alpha Vantage.
    /// Note: Free tier only returns 1-2 months of intraday data.
    /// </summary>
    public async Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(
        string symbol,
        string interval,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Alpha Vantage API key is required.");

        // Validate interval
        var normalizedInterval = NormalizeInterval(interval);
        if (!SupportedIntervals.Contains(normalizedInterval))
            throw new ArgumentException($"Unsupported interval: {interval}. Supported: {string.Join(", ", SupportedIntervals)}", nameof(interval));

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Use TIME_SERIES_INTRADAY with extended hours and full output
        var url = $"{BaseUrl}?function=TIME_SERIES_INTRADAY&symbol={normalizedSymbol}&interval={normalizedInterval}&outputsize=full&adjusted=true&extended_hours=true&apikey={_apiKey}";

        _log.Information("Requesting Alpha Vantage {Interval} intraday data for {Symbol}", normalizedInterval, symbol);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Alpha Vantage rate limit exceeded (429) for {symbol}");
                }

                throw new InvalidOperationException($"Alpha Vantage returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Check for API error messages in response
            if (json.Contains("\"Note\"") || json.Contains("Thank you for using Alpha Vantage"))
            {
                throw new HttpRequestException($"Alpha Vantage rate limit exceeded for {symbol}");
            }

            if (json.Contains("\"Error Message\""))
            {
                return Array.Empty<IntradayBar>();
            }

            // Parse the dynamic time series key based on interval
            var timeSeriesKey = $"Time Series ({normalizedInterval})";
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(timeSeriesKey, out var timeSeries))
            {
                _log.Warning("No intraday data returned from Alpha Vantage for {Symbol}", symbol);
                return Array.Empty<IntradayBar>();
            }

            var bars = new List<IntradayBar>();

            foreach (var prop in timeSeries.EnumerateObject())
            {
                if (!DateTimeOffset.TryParse(prop.Name, out var timestamp))
                    continue;

                // Skip if outside requested range
                if (from.HasValue && timestamp < from.Value) continue;
                if (to.HasValue && timestamp > to.Value) continue;

                var price = prop.Value;

                if (!TryParseDecimalFromJson(price, "1. open", out var open)) continue;
                if (!TryParseDecimalFromJson(price, "2. high", out var high)) continue;
                if (!TryParseDecimalFromJson(price, "3. low", out var low)) continue;
                if (!TryParseDecimalFromJson(price, "4. close", out var close)) continue;
                TryParseLongFromJson(price, "5. volume", out var volume);

                if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                    continue;

                bars.Add(new IntradayBar(
                    Symbol: symbol.ToUpperInvariant(),
                    Timestamp: timestamp,
                    Interval: normalizedInterval,
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close,
                    Volume: volume,
                    Source: Name
                ));
            }

            _log.Information("Fetched {Count} {Interval} bars for {Symbol} from Alpha Vantage",
                bars.Count, normalizedInterval, symbol);
            return bars.OrderBy(b => b.Timestamp).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Alpha Vantage intraday response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Alpha Vantage intraday data for {symbol}", ex);
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Alpha Vantage uses standard uppercase tickers
        return symbol.ToUpperInvariant();
    }

    private static string NormalizeInterval(string interval)
    {
        return interval.ToLowerInvariant().Replace(" ", "") switch
        {
            "1m" or "1min" or "1minute" => "1min",
            "5m" or "5min" or "5minute" => "5min",
            "15m" or "15min" or "15minute" => "15min",
            "30m" or "30min" or "30minute" => "30min",
            "60m" or "60min" or "1h" or "1hour" => "60min",
            _ => interval.ToLowerInvariant()
        };
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrEmpty(value)) return false;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseLong(string? value, out long result)
    {
        result = 0;
        if (string.IsNullOrEmpty(value)) return false;
        return long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDecimalFromJson(JsonElement element, string propertyName, out decimal result)
    {
        result = 0m;
        if (!element.TryGetProperty(propertyName, out var prop)) return false;
        return decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseLongFromJson(JsonElement element, string propertyName, out long result)
    {
        result = 0;
        if (!element.TryGetProperty(propertyName, out var prop)) return false;
        return long.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Alpha Vantage API Models

    private sealed class AlphaVantageDailyAdjustedResponse
    {
        [JsonPropertyName("Meta Data")]
        public AlphaVantageMetaData? MetaData { get; set; }

        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, AlphaVantageDailyPrice>? TimeSeries { get; set; }
    }

    private sealed class AlphaVantageMetaData
    {
        [JsonPropertyName("1. Information")]
        public string? Information { get; set; }

        [JsonPropertyName("2. Symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("3. Last Refreshed")]
        public string? LastRefreshed { get; set; }

        [JsonPropertyName("4. Output Size")]
        public string? OutputSize { get; set; }

        [JsonPropertyName("5. Time Zone")]
        public string? TimeZone { get; set; }
    }

    private sealed class AlphaVantageDailyPrice
    {
        [JsonPropertyName("1. open")]
        public string? Open { get; set; }

        [JsonPropertyName("2. high")]
        public string? High { get; set; }

        [JsonPropertyName("3. low")]
        public string? Low { get; set; }

        [JsonPropertyName("4. close")]
        public string? Close { get; set; }

        [JsonPropertyName("5. adjusted close")]
        public string? AdjustedClose { get; set; }

        [JsonPropertyName("6. volume")]
        public string? Volume { get; set; }

        [JsonPropertyName("7. dividend amount")]
        public string? DividendAmount { get; set; }

        [JsonPropertyName("8. split coefficient")]
        public string? SplitCoefficient { get; set; }
    }

    #endregion
}
