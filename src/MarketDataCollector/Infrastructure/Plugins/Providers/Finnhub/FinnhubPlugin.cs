using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Serilog;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.Finnhub;

/// <summary>
/// Finnhub historical data plugin providing daily and intraday bars
/// for US and international equities. Most generous free tier rate limits.
/// </summary>
[MarketDataPlugin(
    id: "finnhub",
    displayName: "Finnhub",
    type: PluginType.Historical,
    Category = PluginCategory.DataVendor,
    Priority = 18)]
public sealed class FinnhubPlugin : HistoricalPluginBase
{
    private const string BaseUrl = "https://finnhub.io/api/v1";

    private string _apiKey = string.Empty;

    public override string Id => "finnhub";
    public override string DisplayName => "Finnhub";
    public override string Description => "Historical daily and intraday bars from Finnhub";
    public override string Version => "1.0.0";

    public override PluginCapabilities Capabilities { get; protected set; } = new()
    {
        SupportsRealtime = false,
        SupportsHistorical = true,
        SupportsTrades = false,
        SupportsQuotes = false,
        SupportsBars = true,
        SupportsAdjustedPrices = false, // Finnhub returns raw prices
        SupportsDividends = true,
        SupportsSplits = true,
        SupportedMarkets = new[] { "US", "UK", "DE", "CA", "AU", "HK", "JP", "CN" },
        SupportedAssetClasses = new[] { "Equity", "ETF", "Index" },
        SupportedBarIntervals = new[] { "1min", "5min", "15min", "30min", "1hour", "1day", "1week", "1month" },
        RequiresAuthentication = true
    };

    public FinnhubPlugin() : base(
        Log.ForContext<FinnhubPlugin>(),
        maxRequestsPerWindow: 60,
        rateLimitWindow: TimeSpan.FromMinutes(1),
        minDelayBetweenRequests: TimeSpan.FromSeconds(1))
    {
    }

    protected override async Task OnInitializeAsync(IPluginConfig config, CancellationToken ct)
    {
        _apiKey = config.Get("FINNHUB__APIKEY", string.Empty);
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiKey = Environment.GetEnvironmentVariable("FINNHUB_API_KEY") ?? string.Empty;
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            Logger.Warning("Finnhub API key not configured - requests will fail");
        }
        else
        {
            Logger.Information("Finnhub plugin initialized with API key");
        }

        // Configure HTTP client
        HttpClient.BaseAddress = new Uri(BaseUrl);
        HttpClient.DefaultRequestHeaders.Add("X-Finnhub-Token", _apiKey);
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector/1.0");

        await Task.CompletedTask;
    }

    protected override async IAsyncEnumerable<MarketDataEvent> FetchBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        BarInterval interval,
        bool adjusted,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var resolution = GetResolution(interval);
        var fromTimestamp = from.HasValue
            ? new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeSeconds();
        var toTimestamp = to.HasValue
            ? new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var url = $"/stock/candle?symbol={symbol}&resolution={resolution}&from={fromTimestamp}&to={toTimestamp}";

        Logger.Debug("Fetching Finnhub data for {Symbol} with resolution {Resolution}", symbol, resolution);

        using var response = await HttpClient.GetAsync(url, ct);

        if ((int)response.StatusCode == 429)
        {
            Logger.Warning("Finnhub rate limit exceeded for {Symbol}", symbol);
            RecordHealth(false, "Rate limit exceeded");
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            Logger.Warning("Finnhub request failed for {Symbol}: {StatusCode}", symbol, response.StatusCode);
            RecordHealth(false, $"HTTP {response.StatusCode}");
            yield break;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<FinnhubCandleResponse>(json, JsonOptions);

        if (data == null || data.Status == "no_data" || data.Timestamps == null)
        {
            Logger.Debug("No data returned from Finnhub for {Symbol}", symbol);
            yield break;
        }

        RecordHealth(true);
        Logger.Information("Retrieved {Count} bars from Finnhub for {Symbol}", data.Timestamps.Length, symbol);

        for (var i = 0; i < data.Timestamps.Length; i++)
        {
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(data.Timestamps[i]);

            yield return MarketDataEvent.Bar(
                symbol: symbol,
                timestamp: timestamp,
                open: GetDecimalValue(data.Open, i),
                high: GetDecimalValue(data.High, i),
                low: GetDecimalValue(data.Low, i),
                close: GetDecimalValue(data.Close, i),
                volume: GetDecimalValue(data.Volume, i),
                interval: interval,
                isAdjusted: false // Finnhub returns unadjusted prices
            );
        }
    }

    private static string GetResolution(BarInterval interval) => interval switch
    {
        BarInterval.OneMinute => "1",
        BarInterval.FiveMinute => "5",
        BarInterval.FifteenMinute => "15",
        BarInterval.ThirtyMinute => "30",
        BarInterval.OneHour => "60",
        BarInterval.Daily => "D",
        BarInterval.Weekly => "W",
        BarInterval.Monthly => "M",
        _ => "D"
    };

    private static decimal GetDecimalValue(double[]? array, int index)
    {
        if (array == null || index >= array.Length)
            return 0;
        return (decimal)array[index];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Finnhub candle response DTO.
    /// Uses compact property names (c, h, l, o, s, t, v).
    /// </summary>
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
        public long[]? Timestamps { get; set; }

        [JsonPropertyName("v")]
        public double[]? Volume { get; set; }
    }
}
