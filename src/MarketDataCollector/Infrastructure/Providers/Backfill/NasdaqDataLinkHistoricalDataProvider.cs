using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Http;
using MarketDataCollector.Infrastructure.Utilities;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.Backfill;

/// <summary>
/// Historical data provider using Nasdaq Data Link (formerly Quandl).
/// Free tier: 50 calls/day, 300 calls/10 seconds.
/// Provides access to various datasets including WIKI (end-of-life) and premium datasets.
/// </summary>
[ImplementsAdr("ADR-001", "Nasdaq Data Link historical data provider implementation")]
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public sealed class NasdaqDataLinkHistoricalDataProvider : IHistoricalDataProvider, IDisposable
{
    private const string BaseUrl = "https://data.nasdaq.com/api/v3";

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly HttpResponseHandler _responseHandler;
    private readonly string? _apiKey;
    private readonly string _database;
    private readonly ILogger _log;
    private bool _disposed;

    public string Name => "nasdaq";
    public string DisplayName => "Nasdaq Data Link (Quandl)";
    public string Description => "Alternative and financial datasets from Nasdaq Data Link (formerly Quandl).";

    public int Priority => 30;
    public TimeSpan RateLimitDelay => TimeSpan.FromMilliseconds(100);
    public int MaxRequestsPerWindow => 50;
    public TimeSpan RateLimitWindow => TimeSpan.FromDays(1);

    public bool SupportsAdjustedPrices => true;
    public bool SupportsIntraday => false;
    public bool SupportsDividends => true;
    public bool SupportsSplits => true;
    public IReadOnlyList<string> SupportedMarkets => new[] { "US" };

    /// <summary>
    /// Create a Nasdaq Data Link provider.
    /// </summary>
    /// <param name="apiKey">API key from data.nasdaq.com (optional but recommended)</param>
    /// <param name="database">Database to query (default: "WIKI" for legacy wiki prices, or use "EOD" for end-of-day)</param>
    /// <param name="httpClient">Optional HTTP client</param>
    /// <param name="log">Optional logger</param>
    public NasdaqDataLinkHistoricalDataProvider(
        string? apiKey = null,
        string database = "WIKI",
        HttpClient? httpClient = null,
        ILogger? log = null)
    {
        _apiKey = apiKey;
        _database = database;
        _log = log ?? LoggingSetup.ForContext<NasdaqDataLinkHistoricalDataProvider>();

        // TD-10: Use HttpClientFactory instead of creating new HttpClient instances
        _http = httpClient ?? HttpClientFactoryProvider.CreateClient(HttpClientNames.NasdaqDataLinkHistorical);

        // Free tier: 50 calls/day without key, 300/10sec burst limit
        _rateLimiter = new RateLimiter(MaxRequestsPerWindow, RateLimitWindow, RateLimitDelay, _log);
        _responseHandler = new HttpResponseHandler(Name, _log);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/datasets/{_database}/AAPL/metadata.json";
            if (!string.IsNullOrEmpty(_apiKey))
                url += $"?api_key={_apiKey}";

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

        await _rateLimiter.WaitForSlotAsync(ct).ConfigureAwait(false);

        var normalizedSymbol = SymbolNormalization.NormalizeForNasdaqDataLink(symbol);

        // Build URL
        var url = $"{BaseUrl}/datasets/{_database}/{normalizedSymbol}.json";
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(_apiKey))
            queryParams.Add($"api_key={_apiKey}");

        if (from.HasValue)
            queryParams.Add($"start_date={from.Value:yyyy-MM-dd}");

        if (to.HasValue)
            queryParams.Add($"end_date={to.Value:yyyy-MM-dd}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        _log.Information("Requesting Nasdaq Data Link history for {Symbol} ({Url})", symbol, url);

        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            var httpResult = await _responseHandler.HandleResponseAsync(response, symbol, "daily bars", ct: ct).ConfigureAwait(false);
            if (httpResult.IsNotFound)
            {
                _log.Warning("Nasdaq Data Link: Symbol {Symbol} not found", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<QuandlDatasetResponse>(json);

            if (result?.Dataset?.Data is null)
            {
                _log.Warning("No data returned from Nasdaq Data Link for {Symbol}", symbol);
                return Array.Empty<AdjustedHistoricalBar>();
            }

            var columns = result.Dataset.ColumnNames ?? Array.Empty<string>();
            var columnIndex = BuildColumnIndex(columns);

            var bars = new List<AdjustedHistoricalBar>();

            foreach (var row in result.Dataset.Data)
            {
                var bar = ParseRow(row, columnIndex, symbol);
                if (bar is not null)
                {
                    // Apply date filter
                    if (from.HasValue && bar.SessionDate < from.Value) continue;
                    if (to.HasValue && bar.SessionDate > to.Value) continue;

                    bars.Add(bar);
                }
            }

            _log.Information("Fetched {Count} bars for {Symbol} from Nasdaq Data Link", bars.Count, symbol);
            return bars.OrderBy(b => b.SessionDate).ToList();
        }
        catch (JsonException ex)
        {
            _log.Error(ex, "Failed to parse Nasdaq Data Link response for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to parse Nasdaq Data Link data for {symbol}", ex);
        }
    }

    private static Dictionary<string, int> BuildColumnIndex(string[] columns)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Length; i++)
        {
            index[columns[i]] = i;
        }
        return index;
    }

    private AdjustedHistoricalBar? ParseRow(JsonElement[] row, Dictionary<string, int> columns, string symbol)
    {
        try
        {
            // Standard WIKI columns: Date, Open, High, Low, Close, Volume, Ex-Dividend, Split Ratio, Adj. Open, Adj. High, Adj. Low, Adj. Close, Adj. Volume
            if (!TryGetValue(row, columns, "Date", out string? dateStr) || !DateOnly.TryParse(dateStr, out var date))
                return null;

            var open = GetDecimalValue(row, columns, "Open") ?? GetDecimalValue(row, columns, "Adj. Open");
            var high = GetDecimalValue(row, columns, "High") ?? GetDecimalValue(row, columns, "Adj. High");
            var low = GetDecimalValue(row, columns, "Low") ?? GetDecimalValue(row, columns, "Adj. Low");
            var close = GetDecimalValue(row, columns, "Close") ?? GetDecimalValue(row, columns, "Adj. Close");
            var volume = GetLongValue(row, columns, "Volume") ?? GetLongValue(row, columns, "Adj. Volume");

            if (open is null || high is null || low is null || close is null)
                return null;

            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                return null;

            var adjOpen = GetDecimalValue(row, columns, "Adj. Open");
            var adjHigh = GetDecimalValue(row, columns, "Adj. High");
            var adjLow = GetDecimalValue(row, columns, "Adj. Low");
            var adjClose = GetDecimalValue(row, columns, "Adj. Close");
            var adjVolume = GetLongValue(row, columns, "Adj. Volume");

            var dividend = GetDecimalValue(row, columns, "Ex-Dividend");
            var splitRatio = GetDecimalValue(row, columns, "Split Ratio");

            return new AdjustedHistoricalBar(
                Symbol: symbol.ToUpperInvariant(),
                SessionDate: date,
                Open: open.Value,
                High: high.Value,
                Low: low.Value,
                Close: close.Value,
                Volume: volume ?? 0,
                Source: Name,
                SequenceNumber: date.DayNumber,
                AdjustedOpen: adjOpen,
                AdjustedHigh: adjHigh,
                AdjustedLow: adjLow,
                AdjustedClose: adjClose,
                AdjustedVolume: adjVolume,
                SplitFactor: splitRatio != 1m ? splitRatio : null,
                DividendAmount: dividend > 0 ? dividend : null
            );
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Failed to parse row for {Symbol}", symbol);
            return null;
        }
    }

    private static bool TryGetValue(JsonElement[] row, Dictionary<string, int> columns, string column, out string? value)
    {
        value = null;
        if (!columns.TryGetValue(column, out var index) || index >= row.Length)
            return false;

        value = row[index].GetString();
        return value is not null;
    }

    private static decimal? GetDecimalValue(JsonElement[] row, Dictionary<string, int> columns, string column)
    {
        if (!columns.TryGetValue(column, out var index) || index >= row.Length)
            return null;

        var element = row[index];
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
            _ => null
        };
    }

    private static long? GetLongValue(JsonElement[] row, Dictionary<string, int> columns, string column)
    {
        if (!columns.TryGetValue(column, out var index) || index >= row.Length)
            return null;

        var element = row[index];
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.String when long.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
        _http.Dispose();
    }

    #region Nasdaq Data Link API Models

    private sealed class QuandlDatasetResponse
    {
        [JsonPropertyName("dataset")]
        public QuandlDataset? Dataset { get; set; }
    }

    private sealed class QuandlDataset
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("dataset_code")]
        public string? DatasetCode { get; set; }

        [JsonPropertyName("database_code")]
        public string? DatabaseCode { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("column_names")]
        public string[]? ColumnNames { get; set; }

        [JsonPropertyName("data")]
        public JsonElement[][]? Data { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("frequency")]
        public string? Frequency { get; set; }
    }

    #endregion
}
