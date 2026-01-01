using System.Globalization;
using System.Linq;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Pulls free end-of-day historical bars from Stooq (https://stooq.pl).
/// </summary>
public sealed class StooqHistoricalDataProvider : IHistoricalDataProvider
{
    private readonly HttpClient _http;
    private readonly ILogger _log;

    public string Name => "stooq";
    public string DisplayName => "Stooq (free EOD)";
    public string Description => "Free daily OHLCV from stooq.pl (equities/ETFs, US suffix).";

    public StooqHistoricalDataProvider(HttpClient? httpClient = null, ILogger? log = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _log = log ?? LoggingSetup.ForContext<StooqHistoricalDataProvider>();
    }

    public async Task<IReadOnlyList<HistoricalBar>> GetDailyBarsAsync(string symbol, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));

        var normalizedSymbol = Normalize(symbol);
        var url = $"https://stooq.pl/q/d/l/?s={normalizedSymbol}.us&i=d";
        _log.Information("Requesting Stooq history for {Symbol} ({Url})", symbol, url);

        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stooq returned {(int)resp.StatusCode} for symbol {symbol}");

        var csv = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var bars = new List<HistoricalBar>();

        using var reader = new StringReader(csv);
        var header = await reader.ReadLineAsync(); // Skip header row
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
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

            var seq = date.DayNumber;
            bars.Add(new HistoricalBar(symbol.ToUpperInvariant(), date, open, high, low, close, volume, Source: Name, SequenceNumber: seq));
        }

        _log.Information("Fetched {Count} bars for {Symbol} from Stooq", bars.Count, symbol);
        return bars
            .OrderBy(b => b.SessionDate)
            .ToArray();
    }

    private static string Normalize(string symbol)
        => symbol.Replace(".", "-").ToLowerInvariant();
}
