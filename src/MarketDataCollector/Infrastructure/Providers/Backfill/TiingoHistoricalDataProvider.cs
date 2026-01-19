using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Tiingo API (free tier with API key).
/// Provides excellent dividend-adjusted OHLCV data with full adjustment history.
/// Coverage: 65,000+ US/international equities, ETFs, mutual funds.
/// Free tier: 1,000 requests/day, 50 requests/hour.
/// </summary>
public sealed class TiingoHistoricalDataProvider : IHistoricalDataProvider, IDisposable
{
    private const string BaseUrl = "https://api.tiingo.com/tiingo/daily";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _apiToken;
    private bool _disposed;

    public string Name => "tiingo";
    public string DisplayName => "Tiingo (free tier)";
    public string Description => "High-quality dividend-adjusted OHLCV for US/international equities with corporate actions.";

    public int Priority => 15;
    public TimeSpan RateLimitDelay => TimeSpan.FromSeconds(1.5); // 50 requests/hour = ~72 seconds between requests
    public int MaxRequestsPerWindow => 50;
    public TimeSpan RateLimitWindow => TimeSpan.FromHours(1);

    public bool SupportsAdjustedPrices => true;
    public bool SupportsIntraday => false;
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US", "UK", "DE", "CA", "AU" };

    public TiingoHistoricalDataProvider(string? apiToken = null, HttpClient? httpClient = null, ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<TiingoHistoricalDataProvider>();
        _apiToken = apiToken ?? Environment.GetEnvironmentVariable("TIINGO_API_TOKEN");

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("Content-Type", "application/json");

        if (!string.IsNullOrEmpty(_apiToken))
        {
            _http.DefaultRequestHeaders.Add("Authorization", $"Token {_apiToken}");
        }

        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiToken))
        {
            _log.Warning("Tiingo API token not configured. Set TIINGO_API_TOKEN environment variable or configure in settings.");
            return false;
        }

        try
        {
            // Quick health check with metadata endpoint
            var url = $"{BaseUrl}/AAPL?token={_apiToken}";
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

        if (string.IsNullOrEmpty(_apiToken))
            throw new InvalidOperationException("Tiingo API token is required. Set TIINGO_API_TOKEN environment variable.");

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = NormalizeSymbol(symbol);

        // Build URL with date range
        var startDate = from?.ToString("yyyy-MM-dd") ?? "2000-01-01";
        var endDate = to?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var url = $"{BaseUrl}/{normalizedSymbol}/prices?startDate={startDate}&endDate={endDate}&token={_apiToken}";

        _log.Information("Requesting Tiingo history for {Symbol} ({StartDate} to {EndDate})", symbol, startDate, endDate);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning("Tiingo returned {Status} for {Symbol}: {Error}",
                    response.StatusCode, symbol, error);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Tiingo rate limit exceeded (429) for {symbol}. Retry-After: 60");
                }

                throw new InvalidOperationException($"Tiingo returned {(int)response.StatusCode} for symbol {symbol}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var prices = JsonSerializer.Deserialize<List<TiingoPriceData>>(json);

            if (prices is null || prices.Count == 0)
            {
                _log.Warning("No data returned from Tiingo for {Symbol}", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var bars = new List<AdjustedHistoricalBar>();

            foreach (var price in prices)
            {
                if (price.Date is null)
                    continue;

                var sessionDate = DateOnly.ParseExact(price.Date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);

                // Skip if outside requested range
                if (from.HasValue && sessionDate < from.Value) continue;
                if (to.HasValue && sessionDate > to.Value) continue;

                // Validate OHLC
                if (price.Open <= 0 || price.High <= 0 || price.Low <= 0 || price.Close <= 0)
                    continue;

                // Calculate split factor from adjusted/raw close ratio
                decimal? splitFactor = null;
                if (price.AdjClose.HasValue && price.Close > 0)
                {
                    var factor = price.AdjClose.Value / price.Close;
                    if (Math.Abs(factor - 1m) > 0.0001m)
                    {
                        splitFactor = factor;
                    }
                }

                // Tiingo provides divCash directly
                decimal? dividendAmount = price.DivCash.HasValue && price.DivCash.Value > 0
                    ? price.DivCash.Value
                    : null;

                bars.Add(new AdjustedHistoricalBar(
                    Symbol: symbol.ToUpperInvariant(),
                    SessionDate: sessionDate,
                    Open: price.Open,
                    High: price.High,
                    Low: price.Low,
                    Close: price.Close,
                    Volume: (long)(price.Volume ?? 0),
                    Source: Name,
                    SequenceNumber: sessionDate.DayNumber,
                    AdjustedOpen: price.AdjOpen,
                    AdjustedHigh: price.AdjHigh,
                    AdjustedLow: price.AdjLow,
                    AdjustedClose: price.AdjClose,
                    AdjustedVolume: price.AdjVolume.HasValue ? (long)price.AdjVolume.Value : null,
                    SplitFactor: price.SplitFactor.HasValue && price.SplitFactor.Value != 1.0m
                        ? price.SplitFactor.Value
                        : splitFactor,
                    DividendAmount: dividendAmount
                ));
            }

            _log.Information("Fetched {Count} bars for {Symbol} from Tiingo", bars.Count, symbol);
            return bars.OrderBy(b => b.SessionDate).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Tiingo response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Tiingo data for {symbol}", ex);
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Tiingo uses standard uppercase tickers
        return symbol.ToUpperInvariant().Replace(".", "-");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Tiingo API Models

    private sealed class TiingoPriceData
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("open")]
        public decimal Open { get; set; }

        [JsonPropertyName("high")]
        public decimal High { get; set; }

        [JsonPropertyName("low")]
        public decimal Low { get; set; }

        [JsonPropertyName("close")]
        public decimal Close { get; set; }

        [JsonPropertyName("volume")]
        public decimal? Volume { get; set; }

        [JsonPropertyName("adjOpen")]
        public decimal? AdjOpen { get; set; }

        [JsonPropertyName("adjHigh")]
        public decimal? AdjHigh { get; set; }

        [JsonPropertyName("adjLow")]
        public decimal? AdjLow { get; set; }

        [JsonPropertyName("adjClose")]
        public decimal? AdjClose { get; set; }

        [JsonPropertyName("adjVolume")]
        public decimal? AdjVolume { get; set; }

        [JsonPropertyName("divCash")]
        public decimal? DivCash { get; set; }

        [JsonPropertyName("splitFactor")]
        public decimal? SplitFactor { get; set; }
    }

    #endregion
}
