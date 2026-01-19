using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Serilog;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.Tiingo;

/// <summary>
/// Tiingo historical data plugin providing daily bars with adjusted prices,
/// dividend and split data for US and international equities.
/// </summary>
[MarketDataPlugin(
    id: "tiingo",
    displayName: "Tiingo",
    type: PluginType.Historical,
    Category = PluginCategory.DataVendor,
    Priority = 15)]
public sealed class TiingoPlugin : HistoricalPluginBase
{
    private const string BaseUrl = "https://api.tiingo.com";

    private string _apiToken = string.Empty;

    public override string Id => "tiingo";
    public override string DisplayName => "Tiingo";
    public override string Description => "Historical daily bars with adjusted prices from Tiingo";
    public override string Version => "1.0.0";

    public override PluginCapabilities Capabilities { get; protected set; } = new()
    {
        SupportsRealtime = false,
        SupportsHistorical = true,
        SupportsTrades = false,
        SupportsQuotes = false,
        SupportsBars = true,
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true,
        SupportedMarkets = new[] { "US", "UK", "DE", "CA", "AU" },
        SupportedAssetClasses = new[] { "Equity", "ETF" },
        SupportedBarIntervals = new[] { "1day" },
        RequiresAuthentication = true
    };

    public TiingoPlugin() : base(
        Log.ForContext<TiingoPlugin>(),
        maxRequestsPerWindow: 50,
        rateLimitWindow: TimeSpan.FromHours(1),
        minDelayBetweenRequests: TimeSpan.FromMilliseconds(1500))
    {
    }

    protected override async Task OnInitializeAsync(IPluginConfig config, CancellationToken ct)
    {
        _apiToken = config.Get("TIINGO__TOKEN", string.Empty);
        if (string.IsNullOrEmpty(_apiToken))
        {
            _apiToken = Environment.GetEnvironmentVariable("TIINGO_API_TOKEN") ?? string.Empty;
        }

        if (string.IsNullOrEmpty(_apiToken))
        {
            Logger.Warning("Tiingo API token not configured - requests will fail");
        }
        else
        {
            Logger.Information("Tiingo plugin initialized with API token");
        }

        // Configure HTTP client
        HttpClient.BaseAddress = new Uri(BaseUrl);
        HttpClient.DefaultRequestHeaders.Add("Authorization", $"Token {_apiToken}");
        HttpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

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
        // Tiingo only supports daily bars
        if (interval != BarInterval.Daily)
        {
            Logger.Warning("Tiingo only supports daily bars, requested: {Interval}", interval);
            yield break;
        }

        var startDate = from?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd");
        var endDate = to?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var url = $"/tiingo/daily/{symbol}/prices?startDate={startDate}&endDate={endDate}";

        Logger.Debug("Fetching Tiingo data for {Symbol} from {Start} to {End}", symbol, startDate, endDate);

        HttpResponseMessage? response = null;
        try
        {
            response = await HttpClient.GetAsync(url, ct);

            if ((int)response.StatusCode == 429)
            {
                Logger.Warning("Tiingo rate limit exceeded for {Symbol}", symbol);
                RecordHealth(false, "Rate limit exceeded");
                yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Tiingo request failed for {Symbol}: {StatusCode}", symbol, response.StatusCode);
                RecordHealth(false, $"HTTP {response.StatusCode}");
                yield break;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var bars = JsonSerializer.Deserialize<TiingoPriceData[]>(json, JsonOptions);

            if (bars == null || bars.Length == 0)
            {
                Logger.Debug("No data returned from Tiingo for {Symbol}", symbol);
                yield break;
            }

            RecordHealth(true);
            Logger.Information("Retrieved {Count} bars from Tiingo for {Symbol}", bars.Length, symbol);

            foreach (var bar in bars)
            {
                if (bar.Date == null) continue;

                var date = DateTime.Parse(bar.Date);

                // Use adjusted prices if requested
                var open = adjusted ? bar.AdjOpen ?? bar.Open : bar.Open;
                var high = adjusted ? bar.AdjHigh ?? bar.High : bar.High;
                var low = adjusted ? bar.AdjLow ?? bar.Low : bar.Low;
                var close = adjusted ? bar.AdjClose ?? bar.Close : bar.Close;
                var volume = adjusted ? bar.AdjVolume ?? bar.Volume : bar.Volume;

                yield return MarketDataEvent.Bar(
                    symbol: symbol,
                    timestamp: new DateTimeOffset(date, TimeSpan.Zero),
                    open: open,
                    high: high,
                    low: low,
                    close: close,
                    volume: volume,
                    interval: BarInterval.Daily,
                    isAdjusted: adjusted,
                    dividend: bar.DivCash,
                    splitFactor: bar.SplitFactor != 1 ? bar.SplitFactor : null
                );
            }
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Tiingo price data response DTO.
    /// </summary>
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
        public decimal Volume { get; set; }

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
        public decimal SplitFactor { get; set; } = 1;
    }
}
