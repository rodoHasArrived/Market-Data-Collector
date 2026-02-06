using System.Globalization;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.ProviderSdk.Attributes;
using MarketDataCollector.ProviderSdk.Http;
using MarketDataCollector.ProviderSdk.Providers;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Providers.FreeData.Stooq;

/// <summary>
/// Free end-of-day historical bar provider using Stooq (https://stooq.pl).
/// No API key required. Provides daily OHLCV for US equities and ETFs.
/// </summary>
[DataSource("stooq-plugin", "Stooq (Plugin)", DataSourceType.Historical, DataSourceCategory.FreeApi, Priority = 15)]
[ImplementsAdr("ADR-001", "Stooq plugin historical data provider")]
public sealed class StooqProvider : BaseHttpProvider, IHistoricalProvider
{
    private const string BaseUrl = "https://stooq.pl/q/d/l";

    public string ProviderId => "stooq-plugin";
    public string DisplayName => "Stooq (Free EOD - Plugin)";
    public string Description => "Free daily OHLCV from stooq.pl (US equities/ETFs). No API key required.";
    public int Priority => 15;

    public ProviderCapabilities Capabilities { get; } = ProviderCapabilities.BackfillBarsOnly with
    {
        SupportedMarkets = new[] { "US" },
        MaxRequestsPerWindow = 100,
        RateLimitWindow = TimeSpan.FromMinutes(10)
    };

    protected override string ProviderName => "stooq";

    public StooqProvider(IHttpClientFactory httpClientFactory, ILogger<StooqProvider> logger)
        : base(httpClientFactory.CreateClient("stooq-historical"), logger)
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

        var normalizedSymbol = NormalizeForStooq(symbol);
        var url = $"{BaseUrl}/?s={normalizedSymbol}.us&i=d";

        var csv = await GetStringAsync(url, symbol, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(csv))
            return Array.Empty<HistoricalBar>();

        var bars = ParseCsvResponse(csv, symbol, from, to);

        Logger.LogInformation("Fetched {Count} bars for {Symbol} from Stooq", bars.Count, symbol);
        return bars.OrderBy(b => b.SessionDate).ToArray();
    }

    private List<HistoricalBar> ParseCsvResponse(string csv, string symbol, DateOnly? from, DateOnly? to)
    {
        var bars = new List<HistoricalBar>();
        using var reader = new StringReader(csv);

        // Skip header row
        reader.ReadLine();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 6) continue;

            if (!DateOnly.TryParse(parts[0], out var date)) continue;
            if (from is not null && date < from.Value) continue;
            if (to is not null && date > to.Value) continue;

            if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var open)) continue;
            if (!decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var high)) continue;
            if (!decimal.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var low)) continue;
            if (!decimal.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var close)) continue;
            if (!long.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var volume)) continue;

            if (!IsValidOhlc(open, high, low, close)) continue;

            bars.Add(new HistoricalBar(
                Symbol: symbol.ToUpperInvariant(),
                SessionDate: date,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: volume,
                Source: "stooq-plugin",
                SequenceNumber: date.DayNumber));
        }

        return bars;
    }

    private static string NormalizeForStooq(string symbol)
    {
        return symbol.ToLowerInvariant().Replace('.', '-');
    }
}
