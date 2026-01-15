using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.SymbolSearch;

/// <summary>
/// Symbol search provider using Alpaca Markets API.
/// Provides asset search for US equities with trading status information.
/// </summary>
public sealed class AlpacaSymbolSearchProvider : IFilterableSymbolSearchProvider, IDisposable
{
    private const string BaseUrl = "https://api.alpaca.markets/v2";
    private const string EnvKeyId = "ALPACA_KEY_ID";
    private const string EnvSecretKey = "ALPACA_SECRET_KEY";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger _log;
    private readonly string? _keyId;
    private readonly string? _secretKey;
    private bool _disposed;

    public string Name => "alpaca";
    public string DisplayName => "Alpaca Markets";
    public int Priority => 5;

    public IReadOnlyList<string> SupportedAssetTypes => new[] { "us_equity", "crypto" };
    public IReadOnlyList<string> SupportedExchanges => new[] { "NASDAQ", "NYSE", "ARCA", "AMEX", "BATS" };

    public AlpacaSymbolSearchProvider(
        string? keyId = null,
        string? secretKey = null,
        HttpClient? httpClient = null,
        ILogger? log = null)
    {
        _log = log ?? LoggingSetup.ForContext<AlpacaSymbolSearchProvider>();
        _keyId = keyId ?? Environment.GetEnvironmentVariable(EnvKeyId)
                       ?? Environment.GetEnvironmentVariable("ALPACA__KEYID");
        _secretKey = secretKey ?? Environment.GetEnvironmentVariable(EnvSecretKey)
                                ?? Environment.GetEnvironmentVariable("ALPACA__SECRETKEY");

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        ConfigureHttpClient();

        // Alpaca has generous rate limits: 200/min
        _rateLimiter = new RateLimiter(200, TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(300), _log);
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

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_secretKey))
        {
            _log.Debug("Alpaca API credentials not configured");
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

        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_secretKey))
            return Array.Empty<SymbolSearchResult>();

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        // Alpaca doesn't have a dedicated search endpoint, so we get all assets and filter
        // For efficiency, we'll try to match by symbol prefix first
        var url = $"{BaseUrl}/assets?status=active";

        if (!string.IsNullOrEmpty(assetType))
        {
            var assetClass = assetType.ToLowerInvariant() switch
            {
                "us_equity" or "stock" or "equity" => "us_equity",
                "crypto" => "crypto",
                _ => "us_equity"
            };
            url += $"&asset_class={assetClass}";
        }
        else
        {
            url += "&asset_class=us_equity";
        }

        if (!string.IsNullOrEmpty(exchange))
        {
            url += $"&exchange={Uri.EscapeDataString(exchange)}";
        }

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.Warning("Alpaca assets returned {Status}: {Error}", response.StatusCode, error);
                return Array.Empty<SymbolSearchResult>();
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var assets = JsonSerializer.Deserialize<List<AlpacaAsset>>(json);

            if (assets is null || assets.Count == 0)
                return Array.Empty<SymbolSearchResult>();

            var queryUpper = query.ToUpperInvariant();

            return assets
                .Where(a => !string.IsNullOrEmpty(a.Symbol) && a.Tradable)
                .Where(a => a.Symbol!.ToUpperInvariant().Contains(queryUpper) ||
                           (a.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
                .Select((a, i) => new SymbolSearchResult(
                    Symbol: a.Symbol!,
                    Name: a.Name ?? a.Symbol!,
                    Exchange: a.Exchange,
                    AssetType: MapAssetClass(a.AssetClass),
                    Country: "US",
                    Currency: "USD",
                    Source: Name,
                    MatchScore: CalculateMatchScore(query, a.Symbol!, a.Name, i)
                ))
                .OrderByDescending(r => r.MatchScore)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Alpaca search failed for query {Query}", query);
            return Array.Empty<SymbolSearchResult>();
        }
    }

    public async Task<SymbolDetails?> GetDetailsAsync(string symbol, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_secretKey))
            return null;

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = symbol.ToUpperInvariant();
        var url = $"{BaseUrl}/assets/{normalizedSymbol}";

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _log.Debug("Alpaca asset returned {Status} for {Symbol}", response.StatusCode, symbol);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var asset = JsonSerializer.Deserialize<AlpacaAsset>(json);

            if (asset is null)
                return null;

            return new SymbolDetails(
                Symbol: normalizedSymbol,
                Name: asset.Name ?? normalizedSymbol,
                Description: null,
                Exchange: asset.Exchange,
                AssetType: MapAssetClass(asset.AssetClass),
                Sector: null,
                Industry: null,
                Country: "US",
                Currency: "USD",
                MarketCap: null,
                AverageVolume: null,
                Week52High: null,
                Week52Low: null,
                LastPrice: null,
                WebUrl: null,
                LogoUrl: null,
                IpoDate: null,
                PaysDividend: null,
                DividendYield: null,
                PeRatio: null,
                SharesOutstanding: null,
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
            _log.Error(ex, "Alpaca asset lookup failed for {Symbol}", symbol);
            return null;
        }
    }

    private static string? MapAssetClass(string? assetClass)
    {
        return assetClass?.ToLowerInvariant() switch
        {
            "us_equity" => "Stock",
            "crypto" => "Crypto",
            _ => assetClass
        };
    }

    private static int CalculateMatchScore(string query, string symbol, string? name, int position)
    {
        var score = 50;
        var queryUpper = query.ToUpperInvariant();
        var symbolUpper = symbol.ToUpperInvariant();

        // Exact symbol match
        if (symbolUpper == queryUpper)
            score = 100;
        // Symbol starts with query
        else if (symbolUpper.StartsWith(queryUpper))
            score = 80 + (10 - Math.Min(10, symbolUpper.Length - queryUpper.Length));
        // Symbol contains query
        else if (symbolUpper.Contains(queryUpper))
            score = 60;
        // Name contains query
        else if (name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            score = 40;

        // Position penalty (minimal for Alpaca since we do client-side filtering)
        score -= Math.Min(10, position);

        return Math.Max(0, score);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Alpaca API Models

    private sealed class AlpacaAsset
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("class")]
        public string? AssetClass { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("tradable")]
        public bool Tradable { get; set; }

        [JsonPropertyName("marginable")]
        public bool Marginable { get; set; }

        [JsonPropertyName("shortable")]
        public bool Shortable { get; set; }

        [JsonPropertyName("easy_to_borrow")]
        public bool EasyToBorrow { get; set; }

        [JsonPropertyName("fractionable")]
        public bool Fractionable { get; set; }

        [JsonPropertyName("maintenance_margin_requirement")]
        public decimal? MaintenanceMarginRequirement { get; set; }

        [JsonPropertyName("min_order_size")]
        public decimal? MinOrderSize { get; set; }

        [JsonPropertyName("min_trade_increment")]
        public decimal? MinTradeIncrement { get; set; }

        [JsonPropertyName("price_increment")]
        public decimal? PriceIncrement { get; set; }
    }

    #endregion
}
