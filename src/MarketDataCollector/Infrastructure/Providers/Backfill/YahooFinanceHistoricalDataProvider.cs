using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Yahoo Finance (free, unofficial API).
/// Provides daily OHLCV with adjusted close prices.
/// Coverage: 50,000+ global equities, ETFs, indices, crypto.
/// </summary>
[ImplementsAdr("ADR-001", "Yahoo Finance historical data provider implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class YahooFinanceHistoricalDataProvider : IHistoricalDataProvider, IDisposable
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private bool _disposed;

    public string Name => "yahoo";
    public string DisplayName => "Yahoo Finance (free)";
    public string Description => "Free daily OHLCV with adjusted prices for global equities, ETFs, indices.";

    public int Priority => 10;
    public TimeSpan RateLimitDelay => TimeSpan.FromSeconds(0.5);
    public int MaxRequestsPerWindow => 2000;
    public TimeSpan RateLimitWindow => TimeSpan.FromHours(1);

    public bool SupportsAdjustedPrices => true;
    public bool SupportsIntraday => false;
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US", "UK", "DE", "JP", "CA", "AU", "HK", "SG" };

    public YahooFinanceHistoricalDataProvider(HttpClient? httpClient = null, ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<YahooFinanceHistoricalDataProvider>();
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // Quick health check with a known symbol
            using var response = await _http.GetAsync($"{BaseUrl}/AAPL?range=1d&interval=1d", ct).ConfigureAwait(false);
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

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Build URL with date range
        var period1 = from.HasValue
            ? new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds()
            : new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        var period2 = to.HasValue
            ? new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var url = $"{BaseUrl}/{normalizedSymbol}?period1={period1}&period2={period2}&interval=1d&events=div,splits";

        _log.Information("Requesting Yahoo Finance history for {Symbol} ({Url})", symbol, url);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning("Yahoo Finance returned {Status} for {Symbol}: {Error}",
                    response.StatusCode, symbol, error);
                throw new InvalidOperationException($"Yahoo Finance returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<YahooChartResponse>(json);

            var result = data?.Chart?.Result?.FirstOrDefault();
            if (result?.Indicators?.Quote?.FirstOrDefault() is null)
            {
                _log.Warning("No data returned from Yahoo Finance for {Symbol}", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var timestamps = result.Timestamp ?? Array.Empty<long>();
            var quote = result.Indicators.Quote[0];
            var adjClose = result.Indicators.AdjClose?.FirstOrDefault()?.AdjClose;
            var events = result.Events;

            var bars = new List<AdjustedHistoricalBar>();

            for (int i = 0; i < timestamps.Length; i++)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).Date;
                var sessionDate = DateOnly.FromDateTime(date);

                // Skip if outside requested range
                if (from.HasValue && sessionDate < from.Value) continue;
                if (to.HasValue && sessionDate > to.Value) continue;

                var open = GetDecimalValue(quote.Open, i);
                var high = GetDecimalValue(quote.High, i);
                var low = GetDecimalValue(quote.Low, i);
                var close = GetDecimalValue(quote.Close, i);
                var volume = GetLongValue(quote.Volume, i);

                if (open is null || high is null || low is null || close is null)
                    continue;

                // Validate OHLC
                if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                    continue;

                var adjCloseValue = adjClose is not null && i < adjClose.Length
                    ? GetDecimalValue(adjClose, i)
                    : null;

                // Calculate adjustment factor from adjusted close
                decimal? splitFactor = null;
                decimal? dividendAmount = null;

                if (adjCloseValue.HasValue && close.Value > 0)
                {
                    var factor = adjCloseValue.Value / close.Value;
                    if (Math.Abs(factor - 1m) > 0.0001m)
                    {
                        splitFactor = factor;
                    }
                }

                // Check for dividend/split events on this date
                var dateKey = timestamps[i].ToString();
                if (events?.Dividends?.TryGetValue(dateKey, out var dividend) == true)
                {
                    dividendAmount = dividend.Amount;
                }

                bars.Add(new AdjustedHistoricalBar(
                    Symbol: symbol.ToUpperInvariant(),
                    SessionDate: sessionDate,
                    Open: open.Value,
                    High: high.Value,
                    Low: low.Value,
                    Close: close.Value,
                    Volume: volume ?? 0,
                    Source: Name,
                    SequenceNumber: sessionDate.DayNumber,
                    AdjustedOpen: adjCloseValue.HasValue ? open.Value * (adjCloseValue.Value / close.Value) : null,
                    AdjustedHigh: adjCloseValue.HasValue ? high.Value * (adjCloseValue.Value / close.Value) : null,
                    AdjustedLow: adjCloseValue.HasValue ? low.Value * (adjCloseValue.Value / close.Value) : null,
                    AdjustedClose: adjCloseValue,
                    AdjustedVolume: null,
                    SplitFactor: splitFactor,
                    DividendAmount: dividendAmount
                ));
            }

            _log.Information("Fetched {Count} bars for {Symbol} from Yahoo Finance", bars.Count, symbol);
            return bars.OrderBy(b => b.SessionDate).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Yahoo Finance response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Yahoo Finance data for {symbol}", ex);
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Yahoo uses standard tickers for US stocks
        // International: append exchange suffix (e.g., .L for London, .T for Tokyo)
        return symbol.ToUpperInvariant();
    }

    private static decimal? GetDecimalValue(decimal?[]? array, int index)
    {
        if (array is null || index >= array.Length || array[index] is null)
            return null;
        return array[index]!.Value;
    }

    private static long? GetLongValue(long?[]? array, int index)
    {
        if (array is null || index >= array.Length)
            return null;
        return array[index];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Yahoo Finance API Models

    private sealed class YahooChartResponse
    {
        [JsonPropertyName("chart")]
        public YahooChart? Chart { get; set; }
    }

    private sealed class YahooChart
    {
        [JsonPropertyName("result")]
        public List<YahooChartResult>? Result { get; set; }

        [JsonPropertyName("error")]
        public object? Error { get; set; }
    }

    private sealed class YahooChartResult
    {
        [JsonPropertyName("meta")]
        public YahooMeta? Meta { get; set; }

        [JsonPropertyName("timestamp")]
        public long[]? Timestamp { get; set; }

        [JsonPropertyName("events")]
        public YahooEvents? Events { get; set; }

        [JsonPropertyName("indicators")]
        public YahooIndicators? Indicators { get; set; }
    }

    private sealed class YahooMeta
    {
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("exchangeName")]
        public string? ExchangeName { get; set; }

        [JsonPropertyName("instrumentType")]
        public string? InstrumentType { get; set; }

        [JsonPropertyName("regularMarketPrice")]
        public decimal? RegularMarketPrice { get; set; }
    }

    private sealed class YahooEvents
    {
        [JsonPropertyName("dividends")]
        public Dictionary<string, YahooDividend>? Dividends { get; set; }

        [JsonPropertyName("splits")]
        public Dictionary<string, YahooSplit>? Splits { get; set; }
    }

    private sealed class YahooDividend
    {
        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("date")]
        public long? Date { get; set; }
    }

    private sealed class YahooSplit
    {
        [JsonPropertyName("numerator")]
        public decimal? Numerator { get; set; }

        [JsonPropertyName("denominator")]
        public decimal? Denominator { get; set; }

        [JsonPropertyName("splitRatio")]
        public string? SplitRatio { get; set; }
    }

    private sealed class YahooIndicators
    {
        [JsonPropertyName("quote")]
        public List<YahooQuote>? Quote { get; set; }

        [JsonPropertyName("adjclose")]
        public List<YahooAdjClose>? AdjClose { get; set; }
    }

    private sealed class YahooQuote
    {
        [JsonPropertyName("open")]
        public decimal?[]? Open { get; set; }

        [JsonPropertyName("high")]
        public decimal?[]? High { get; set; }

        [JsonPropertyName("low")]
        public decimal?[]? Low { get; set; }

        [JsonPropertyName("close")]
        public decimal?[]? Close { get; set; }

        [JsonPropertyName("volume")]
        public long?[]? Volume { get; set; }
    }

    private sealed class YahooAdjClose
    {
        [JsonPropertyName("adjclose")]
        public decimal?[]? AdjClose { get; set; }
    }

    #endregion
}
