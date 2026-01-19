using System.Runtime.CompilerServices;
using System.Text.Json;
using MarketDataCollector.Infrastructure.Plugins.Base;
using MarketDataCollector.Infrastructure.Plugins.Core;
using MarketDataCollector.Infrastructure.Plugins.Discovery;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Infrastructure.Plugins.Providers.Yahoo;

/// <summary>
/// Yahoo Finance plugin for historical market data.
///
/// This is a simple example of a historical-only plugin using the new architecture.
///
/// Note: Yahoo Finance is free but unofficial. Use responsibly and be aware
/// that the API may change without notice.
///
/// Configuration (optional, via environment variables):
///   YAHOO__USER_AGENT - Custom user agent string
/// </summary>
[MarketDataPlugin(
    id: "yahoo",
    displayName: "Yahoo Finance",
    type: PluginType.Historical,
    Category = PluginCategory.Free,
    Priority = 50,
    Description = "Free historical data from Yahoo Finance (unofficial)",
    Author = "Market Data Collector",
    ConfigPrefix = "YAHOO",
    Version = "2.0.0")]
public sealed class YahooFinancePlugin : HistoricalPluginBase
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart";

    #region Identity

    public override string Id => "yahoo";
    public override string DisplayName => "Yahoo Finance";
    public override string Description => "Free historical data from Yahoo Finance (unofficial)";
    public override Version Version => new(2, 0, 0);

    #endregion

    #region Capabilities

    public override PluginCapabilities Capabilities => new()
    {
        SupportsRealtime = false,
        SupportsHistorical = true,
        SupportsTrades = false,
        SupportsQuotes = false,
        SupportsDepth = false,
        SupportsBars = true,
        SupportsAdjustedPrices = true,
        SupportsDividends = true,
        SupportsSplits = true,
        MaxHistoricalLookback = TimeSpan.FromDays(365 * 30), // ~30 years
        SupportedBarIntervals = ["1day", "1week", "1month"],
        SupportedAssetClasses = new HashSet<AssetClass>
        {
            AssetClass.Equity,
            AssetClass.ETF,
            AssetClass.Index,
            AssetClass.Crypto
        },
        SupportedMarkets = new HashSet<string> { "US", "UK", "EU", "GLOBAL" },
        MaxSymbolsPerRequest = 1, // Yahoo requires individual requests
        RateLimit = RateLimitPolicy.PerMinute(60) // Conservative rate limit
    };

    #endregion

    #region Lifecycle

    protected override HttpClient CreateHttpClient()
    {
        var client = base.CreateHttpClient();

        // Add cookies and headers to look like a browser
        client.DefaultRequestHeaders.Add("User-Agent",
            Config.Get("user_agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"));

        return client;
    }

    protected override ILogger CreateLogger()
    {
        return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    #endregion

    #region Historical Data Fetching

    protected override async Task<IReadOnlyList<BarEvent>> FetchBarsAsync(
        string symbol,
        DateOnly from,
        DateOnly to,
        string interval,
        bool adjusted,
        CancellationToken ct)
    {
        // Convert interval to Yahoo format
        var yahooInterval = interval switch
        {
            "1day" => "1d",
            "1week" => "1wk",
            "1month" => "1mo",
            _ => "1d"
        };

        // Convert dates to Unix timestamps
        var period1 = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var period2 = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds();

        // Build URL
        var url = $"{BaseUrl}/{Uri.EscapeDataString(symbol)}" +
                  $"?period1={period1}" +
                  $"&period2={period2}" +
                  $"&interval={yahooInterval}" +
                  "&events=history";

        Logger.LogDebug("Fetching Yahoo data: {Url}", url);

        var response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        return ParseResponse(json, symbol, interval, adjusted);
    }

    private List<BarEvent> ParseResponse(string json, string symbol, string interval, bool adjusted)
    {
        var bars = new List<BarEvent>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("chart", out var chart) ||
            !chart.TryGetProperty("result", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            Logger.LogWarning("No data returned from Yahoo for {Symbol}", symbol);
            return bars;
        }

        var result = results[0];

        // Get timestamps
        if (!result.TryGetProperty("timestamp", out var timestamps) ||
            timestamps.ValueKind != JsonValueKind.Array)
        {
            return bars;
        }

        // Get indicators
        if (!result.TryGetProperty("indicators", out var indicators) ||
            !indicators.TryGetProperty("quote", out var quotes) ||
            quotes.ValueKind != JsonValueKind.Array ||
            quotes.GetArrayLength() == 0)
        {
            return bars;
        }

        var quote = quotes[0];

        // Get adjusted close if available and requested
        decimal[]? adjClose = null;
        if (adjusted &&
            indicators.TryGetProperty("adjclose", out var adjCloseArray) &&
            adjCloseArray.ValueKind == JsonValueKind.Array &&
            adjCloseArray.GetArrayLength() > 0)
        {
            var adjCloseData = adjCloseArray[0];
            if (adjCloseData.TryGetProperty("adjclose", out var adjCloseValues))
            {
                adjClose = ParseDecimalArray(adjCloseValues);
            }
        }

        // Get OHLCV arrays
        var opens = GetArrayProperty(quote, "open");
        var highs = GetArrayProperty(quote, "high");
        var lows = GetArrayProperty(quote, "low");
        var closes = GetArrayProperty(quote, "close");
        var volumes = GetArrayProperty(quote, "volume");

        // Build bar events
        var timestampArray = timestamps.EnumerateArray().ToArray();
        for (int i = 0; i < timestampArray.Length; i++)
        {
            if (!timestampArray[i].TryGetInt64(out var unixTime))
                continue;

            var open = GetDecimalAt(opens, i);
            var high = GetDecimalAt(highs, i);
            var low = GetDecimalAt(lows, i);
            var close = GetDecimalAt(closes, i);
            var volume = GetDecimalAt(volumes, i);

            // Skip bars with no data
            if (open == 0 && high == 0 && low == 0 && close == 0)
                continue;

            // Use adjusted close if available
            if (adjusted && adjClose != null && i < adjClose.Length && adjClose[i] != 0)
            {
                var adjustmentFactor = adjClose[i] / close;
                open *= adjustmentFactor;
                high *= adjustmentFactor;
                low *= adjustmentFactor;
                close = adjClose[i];
            }

            var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTime);

            bars.Add(new BarEvent
            {
                Symbol = symbol,
                Timestamp = timestamp,
                Source = Id,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Interval = interval,
                IsAdjusted = adjusted
            });
        }

        Logger.LogDebug("Parsed {Count} bars for {Symbol}", bars.Count, symbol);
        return bars;
    }

    private static decimal[]? GetArrayProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return ParseDecimalArray(prop);
    }

    private static decimal[] ParseDecimalArray(JsonElement array)
    {
        return array.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Number ? (decimal)e.GetDouble() : 0m)
            .ToArray();
    }

    private static decimal GetDecimalAt(decimal[]? array, int index)
    {
        if (array == null || index >= array.Length)
            return 0m;
        return array[index];
    }

    #endregion
}
