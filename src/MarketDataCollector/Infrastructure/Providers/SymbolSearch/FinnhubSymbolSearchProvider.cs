using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.SymbolSearch;

/// <summary>
/// Symbol search provider using Finnhub API.
/// Provides symbol search and company profiles with generous free tier (60 calls/min).
/// </summary>
public sealed class FinnhubSymbolSearchProvider : IFilterableSymbolSearchProvider, IDisposable
{
    private const string BaseUrl = "https://finnhub.io/api/v1";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _apiKey;
    private bool _disposed;

    public string Name => "finnhub";
    public string DisplayName => "Finnhub";
    public int Priority => 10;

    public IReadOnlyList<string> SupportedAssetTypes => new[]
    {
        "Common Stock", "ADR", "ETF", "ETN", "Unit", "Warrant", "Right",
        "REIT", "Closed-end Fund", "Preferred Stock", "Trust"
    };

    public IReadOnlyList<string> SupportedExchanges => new[]
    {
        "US", "OTC", "NASDAQ", "NYSE", "NYSE ARCA", "BATS", "NYSE AMERICAN", "CBOE",
        "LSE", "TSX", "FRA", "XETRA", "ASX", "NSE", "BSE", "SGX", "HKEX", "TSE"
    };

    public FinnhubSymbolSearchProvider(string? apiKey = null, HttpClient? httpClient = null, ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<FinnhubSymbolSearchProvider>();
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("FINNHUB_API_KEY");

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Finnhub-Token", _apiKey);
        }

        _rateLimiter = new RateLimiter(60, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _log.Debug("Finnhub API key not configured");
            return false;
        }

        try
        {
            var results = await SearchAsync("AAPL", 1, ct).ConfigureAwait(false);
            return results.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        return SearchAsync(query, limit, null, null, ct);
    }

    public async Task<IReadOnlyList<SymbolSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        string? assetType = null,
        string? exchange = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SymbolSearchResult>();

        if (string.IsNullOrEmpty(_apiKey))
            return Array.Empty<SymbolSearchResult>();

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var url = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&token={_apiKey}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("Finnhub search returned {Status} for query {Query}", response.StatusCode, query);
                return Array.Empty<SymbolSearchResult>();
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<FinnhubSearchResponse>(json);

            if (data?.Result is null || data.Count == 0)
                return Array.Empty<SymbolSearchResult>();

            var results = data.Result
                .Where(r => !string.IsNullOrEmpty(r.Symbol))
                .Select((r, i) => new SymbolSearchResult(
                    Symbol: r.Symbol!,
                    Name: r.Description ?? r.Symbol!,
                    Exchange: MapDisplayExchange(r.DisplaySymbol),
                    AssetType: r.Type,
                    Country: null,
                    Currency: null,
                    Source: Name,
                    MatchScore: CalculateMatchScore(query, r.Symbol!, r.Description, i)
                ));

            // Apply filters
            if (!string.IsNullOrEmpty(assetType))
            {
                results = results.Where(r =>
                    r.AssetType?.Equals(assetType, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrEmpty(exchange))
            {
                results = results.Where(r =>
                    r.Exchange?.Contains(exchange, StringComparison.OrdinalIgnoreCase) == true);
            }

            return results
                .OrderByDescending(r => r.MatchScore)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Finnhub search failed for query {Query}", query);
            return Array.Empty<SymbolSearchResult>();
        }
    }

    public async Task<SymbolDetails?> GetDetailsAsync(string symbol, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        if (string.IsNullOrEmpty(_apiKey))
            return null;

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = symbol.ToUpperInvariant();
        var url = $"{BaseUrl}/stock/profile2?symbol={normalizedSymbol}&token={_apiKey}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _log.Debug("Finnhub profile returned {Status} for {Symbol}", response.StatusCode, symbol);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var profile = JsonSerializer.Deserialize<FinnhubCompanyProfile>(json);

            if (profile is null || string.IsNullOrEmpty(profile.Name))
                return null;

            // Optionally fetch quote for last price and volume
            var quote = await GetQuoteAsync(normalizedSymbol, ct).ConfigureAwait(false);

            return new SymbolDetails(
                Symbol: normalizedSymbol,
                Name: profile.Name ?? normalizedSymbol,
                Description: null,
                Exchange: profile.Exchange,
                AssetType: "Stock",
                Sector: null,
                Industry: profile.Industry,
                Country: profile.Country,
                Currency: profile.Currency,
                MarketCap: profile.MarketCap.HasValue ? profile.MarketCap.Value * 1_000_000m : null, // Finnhub returns in millions
                AverageVolume: null,
                Week52High: quote?.Week52High,
                Week52Low: quote?.Week52Low,
                LastPrice: quote?.CurrentPrice,
                WebUrl: profile.WebUrl,
                LogoUrl: profile.LogoUrl,
                IpoDate: ParseIpoDate(profile.IpoDate),
                PaysDividend: null,
                DividendYield: null,
                PeRatio: null,
                SharesOutstanding: profile.SharesOutstanding.HasValue
                    ? (long)(profile.SharesOutstanding.Value * 1_000_000m)
                    : null,
                Figi: null,
                CompositeFigi: null,
                Isin: null,
                Cusip: null,
                Source: Name,
                LastUpdated: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Finnhub profile lookup failed for {Symbol}", symbol);
            return null;
        }
    }

    private async Task<FinnhubQuote?> GetQuoteAsync(string symbol, CancellationToken ct)
    {
        try
        {
            await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

            var url = $"{BaseUrl}/quote?symbol={symbol}&token={_apiKey}";
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<FinnhubQuote>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? MapDisplayExchange(string? displaySymbol)
    {
        if (string.IsNullOrEmpty(displaySymbol))
            return null;

        // Finnhub display symbol format is usually "SYMBOL.EXCHANGE" or just "SYMBOL"
        var parts = displaySymbol.Split('.');
        return parts.Length > 1 ? parts[^1] : "US";
    }

    private static int CalculateMatchScore(string query, string symbol, string? name, int position)
        => SymbolSearchUtility.CalculateMatchScore(query, symbol, name, position);

    private static DateOnly? ParseIpoDate(string? ipoDate)
    {
        if (string.IsNullOrEmpty(ipoDate))
            return null;

        if (DateOnly.TryParse(ipoDate, out var date))
            return date;

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Finnhub API Models

    private sealed class FinnhubSearchResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("result")]
        public List<FinnhubSearchResult>? Result { get; set; }
    }

    private sealed class FinnhubSearchResult
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("displaySymbol")]
        public string? DisplaySymbol { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    private sealed class FinnhubCompanyProfile
    {
        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("finnhubIndustry")]
        public string? Industry { get; set; }

        [JsonPropertyName("ipo")]
        public string? IpoDate { get; set; }

        [JsonPropertyName("logo")]
        public string? LogoUrl { get; set; }

        [JsonPropertyName("marketCapitalization")]
        public decimal? MarketCap { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("shareOutstanding")]
        public decimal? SharesOutstanding { get; set; }

        [JsonPropertyName("weburl")]
        public string? WebUrl { get; set; }
    }

    private sealed class FinnhubQuote
    {
        [JsonPropertyName("c")]
        public decimal? CurrentPrice { get; set; }

        [JsonPropertyName("h")]
        public decimal? HighPrice { get; set; }

        [JsonPropertyName("l")]
        public decimal? LowPrice { get; set; }

        [JsonPropertyName("o")]
        public decimal? OpenPrice { get; set; }

        [JsonPropertyName("pc")]
        public decimal? PreviousClose { get; set; }

        [JsonPropertyName("dp")]
        public decimal? PercentChange { get; set; }

        [JsonPropertyName("d")]
        public decimal? Change { get; set; }

        [JsonPropertyName("t")]
        public long? Timestamp { get; set; }

        // These are not in the basic quote endpoint
        public decimal? Week52High { get; set; }
        public decimal? Week52Low { get; set; }
    }

    #endregion
}
