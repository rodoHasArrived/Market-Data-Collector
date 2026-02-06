using System.Globalization;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.ProviderSdk.Attributes;
using MarketDataCollector.ProviderSdk.Http;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Providers.FreeData.YahooFinance;

/// <summary>
/// Historical data provider using Yahoo Finance (unofficial API).
/// No API key required. Provides daily OHLCV for global equities.
/// </summary>
[DataSource("yahoo-plugin", "Yahoo Finance (Plugin)", DataSourceType.Historical, DataSourceCategory.FreeApi, Priority = 20)]
[ImplementsAdr("ADR-001", "Yahoo Finance plugin historical data provider")]
public sealed class YahooFinanceProvider : BaseHttpProvider, IHistoricalProvider
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v7/finance/download";

    public string ProviderId => "yahoo-plugin";
    public string DisplayName => "Yahoo Finance (Free - Plugin)";
    public string Description => "Free daily OHLCV from Yahoo Finance (unofficial API). No API key required.";
    public int Priority => 20;

    public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.BackfillBarsOnly with
    {
        SupportedMarkets = new[] { "US", "UK", "EU", "APAC" }
    };

    protected override string ProviderName => "yahoo";

    public YahooFinanceProvider(IHttpClientFactory httpClientFactory, ILogger<YahooFinanceProvider> logger)
        : base(httpClientFactory.CreateClient("yahoo-historical"), logger)
    {
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ValidateSymbol(symbol);

        var period1 = from.HasValue
            ? new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds()
            : new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        var period2 = to.HasValue
            ? new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var url = $"{BaseUrl}/{Uri.EscapeDataString(symbol)}?period1={period1}&period2={period2}&interval=1d&events=history";

        var csv = await GetStringAsync(url, symbol, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(csv))
            return Array.Empty<HistoricalBar>();

        var bars = ParseCsvResponse(csv, symbol, from, to);

        Logger.LogInformation("Fetched {Count} bars for {Symbol} from Yahoo Finance", bars.Count, symbol);
        return bars.OrderBy(b => b.SessionDate).ToArray();
    }

    private List<HistoricalBar> ParseCsvResponse(string csv, string symbol, DateOnly? from, DateOnly? to)
    {
        var bars = new List<HistoricalBar>();
        using var reader = new StringReader(csv);

        // Skip header: Date,Open,High,Low,Close,Adj Close,Volume
        reader.ReadLine();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 7) continue;

            if (!DateOnly.TryParse(parts[0], out var date)) continue;
            if (from is not null && date < from.Value) continue;
            if (to is not null && date > to.Value) continue;

            if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var open)) continue;
            if (!decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var high)) continue;
            if (!decimal.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var low)) continue;
            // Use Adj Close (column 5) instead of Close (column 4) for adjusted data
            if (!decimal.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var adjClose)) continue;
            if (!long.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var volume)) continue;

            if (!IsValidOhlc(open, high, low, adjClose)) continue;

            bars.Add(new HistoricalBar(
                Symbol: symbol.ToUpperInvariant(),
                SessionDate: date,
                Open: open,
                High: high,
                Low: low,
                Close: adjClose,
                Volume: volume,
                Source: "yahoo-plugin",
                SequenceNumber: date.DayNumber));
        }

        return bars;
    }
}
