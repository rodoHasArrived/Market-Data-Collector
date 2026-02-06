using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.ProviderSdk.Attributes;
using MarketDataCollector.ProviderSdk.Http;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Providers.FreeData.Tiingo;

/// <summary>
/// Historical data provider using Tiingo API (free tier with API key).
/// Provides dividend-adjusted OHLCV with corporate action data.
/// Free tier: 1,000 requests/day, 50 requests/hour.
/// </summary>
[DataSource("tiingo-plugin", "Tiingo (Plugin)", DataSourceType.Historical, DataSourceCategory.FreeApi, Priority = 25)]
[ImplementsAdr("ADR-001", "Tiingo plugin historical data provider")]
public sealed class TiingoProvider : BaseHttpProvider, IHistoricalProvider
{
    private const string BaseUrl = "https://api.tiingo.com/tiingo/daily";
    private readonly string? _apiToken;

    public string ProviderId => "tiingo-plugin";
    public string DisplayName => "Tiingo (Free Tier - Plugin)";
    public string Description => "Dividend-adjusted OHLCV for US/international equities. Requires free API token.";
    public int Priority => 25;

    public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.BackfillBarsOnly with
    {
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true,
        SupportedMarkets = new[] { "US", "UK", "DE", "CA", "AU" },
        MaxRequestsPerWindow = 50,
        RateLimitWindow = TimeSpan.FromHours(1),
        MinRequestDelay = TimeSpan.FromSeconds(1.5)
    };

    protected override string ProviderName => "tiingo";

    public TiingoProvider(IHttpClientFactory httpClientFactory, ILogger<TiingoProvider> logger)
        : base(
            httpClientFactory.CreateClient("tiingo-historical"),
            logger,
            maxRequestsPerWindow: 50,
            rateLimitWindow: TimeSpan.FromHours(1),
            minDelay: TimeSpan.FromSeconds(1.5))
    {
        _apiToken = Environment.GetEnvironmentVariable("TIINGO_API_TOKEN");

        if (!string.IsNullOrEmpty(_apiToken))
        {
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {_apiToken}");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiToken))
        {
            Logger.LogWarning("Tiingo API token not configured. Set TIINGO_API_TOKEN environment variable.");
            return false;
        }

        try
        {
            var url = $"{BaseUrl}/AAPL?token={_apiToken}";
            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ValidateSymbol(symbol);

        if (string.IsNullOrEmpty(_apiToken))
            throw new InvalidOperationException("Tiingo API token is required. Set TIINGO_API_TOKEN environment variable.");

        var normalizedSymbol = NormalizeForTiingo(symbol);
        var startDate = from?.ToString("yyyy-MM-dd") ?? "2000-01-01";
        var endDate = to?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var url = $"{BaseUrl}/{normalizedSymbol}/prices?startDate={startDate}&endDate={endDate}&token={_apiToken}";
        var json = await GetStringAsync(url, symbol, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(json))
            return Array.Empty<HistoricalBar>();

        var prices = DeserializeJson<List<TiingoPriceData>>(json, symbol);
        if (prices is null || prices.Count == 0)
            return Array.Empty<HistoricalBar>();

        var bars = new List<HistoricalBar>();

        foreach (var price in prices)
        {
            if (price.Date is null) continue;

            var sessionDate = DateOnly.ParseExact(price.Date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (from.HasValue && sessionDate < from.Value) continue;
            if (to.HasValue && sessionDate > to.Value) continue;
            if (!IsValidOhlc(price.Open, price.High, price.Low, price.Close)) continue;

            // Use adjusted close if available
            var close = price.AdjClose ?? price.Close;
            var open = price.AdjOpen ?? price.Open;
            var high = price.AdjHigh ?? price.High;
            var low = price.AdjLow ?? price.Low;
            var volume = price.AdjVolume.HasValue ? (long)price.AdjVolume.Value : (long)(price.Volume ?? 0);

            bars.Add(new HistoricalBar(
                Symbol: symbol.ToUpperInvariant(),
                SessionDate: sessionDate,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: volume,
                Source: "tiingo-plugin",
                SequenceNumber: sessionDate.DayNumber));
        }

        Logger.LogInformation("Fetched {Count} bars for {Symbol} from Tiingo", bars.Count, symbol);
        return bars.OrderBy(b => b.SessionDate).ToArray();
    }

    private static string NormalizeForTiingo(string symbol)
    {
        return symbol.ToUpperInvariant().Replace('.', '-');
    }

    private sealed class TiingoPriceData
    {
        [JsonPropertyName("date")] public string? Date { get; set; }
        [JsonPropertyName("open")] public decimal Open { get; set; }
        [JsonPropertyName("high")] public decimal High { get; set; }
        [JsonPropertyName("low")] public decimal Low { get; set; }
        [JsonPropertyName("close")] public decimal Close { get; set; }
        [JsonPropertyName("volume")] public decimal? Volume { get; set; }
        [JsonPropertyName("adjOpen")] public decimal? AdjOpen { get; set; }
        [JsonPropertyName("adjHigh")] public decimal? AdjHigh { get; set; }
        [JsonPropertyName("adjLow")] public decimal? AdjLow { get; set; }
        [JsonPropertyName("adjClose")] public decimal? AdjClose { get; set; }
        [JsonPropertyName("adjVolume")] public decimal? AdjVolume { get; set; }
        [JsonPropertyName("divCash")] public decimal? DivCash { get; set; }
        [JsonPropertyName("splitFactor")] public decimal? SplitFactor { get; set; }
    }
}
