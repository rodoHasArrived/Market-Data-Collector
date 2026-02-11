using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Xunit;
using Xunit.Abstractions;

namespace MarketDataCollector.Tests.Integration;

/// <summary>
/// Integration test that fetches Yahoo Finance historical data for configurable ticker symbols.
///
/// Reads symbols from the YAHOO_TICKER_SYMBOLS environment variable (comma-separated).
/// Defaults to "SPY" if the variable is not set.
///
/// Outputs are written as JSON files to the ArtifactOutput directory for CI artifact upload.
///
/// Run locally:
///   YAHOO_TICKER_SYMBOLS=SPY,AAPL dotnet test --filter "FullyQualifiedName~ConfigurableTickerDataCollectionTests"
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "TickerArtifact")]
public sealed class ConfigurableTickerDataCollectionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly YahooFinanceHistoricalDataProvider _provider;
    private readonly string _outputDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ConfigurableTickerDataCollectionTests(ITestOutputHelper output)
    {
        _output = output;
        _provider = new YahooFinanceHistoricalDataProvider();
        _outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ArtifactOutput");
        Directory.CreateDirectory(_outputDir);
    }

    private static string[] GetConfiguredSymbols()
    {
        var envValue = Environment.GetEnvironmentVariable("YAHOO_TICKER_SYMBOLS");
        if (string.IsNullOrWhiteSpace(envValue))
            return ["SPY"];

        return envValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToUpperInvariant())
            .ToArray();
    }

    [Fact]
    public async Task FetchAndExport_ConfigurableTickerData()
    {
        // Check if Yahoo Finance is available before proceeding
        var isAvailable = await _provider.IsAvailableAsync();
        if (!isAvailable)
        {
            _output.WriteLine("⚠️  Yahoo Finance is not available (network connectivity issue)");
            _output.WriteLine("Skipping data collection. This is expected in restricted environments.");
            _output.WriteLine("");
            _output.WriteLine("To run this test successfully, ensure:");
            _output.WriteLine("  1. Internet connectivity is available");
            _output.WriteLine("  2. DNS can resolve query1.finance.yahoo.com");
            _output.WriteLine("  3. HTTPS access to Yahoo Finance is not blocked");

            // Write a summary file even when skipping, so the workflow can report it
            var skipSummary = new List<string>
            {
                "# Yahoo Finance Data Collection Report",
                $"Run Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                "",
                "Status: SKIPPED",
                "Reason: Yahoo Finance is not available (network connectivity issue)",
                "",
                "This is expected in restricted network environments where:",
                "  - DNS resolution for query1.finance.yahoo.com may fail",
                "  - External API access may be blocked",
                "  - Internet connectivity is not available",
                "",
                "To collect data successfully, ensure network connectivity and DNS resolution work properly.",
            };

            var skipSummaryPath = Path.Combine(_outputDir, "collection_summary.txt");
            await File.WriteAllTextAsync(skipSummaryPath, string.Join("\n", skipSummary));
            _output.WriteLine($"Summary written to: {skipSummaryPath}");

            // Skip the test instead of failing
            return;
        }

        var symbols = GetConfiguredSymbols();
        _output.WriteLine($"Configured symbols: {string.Join(", ", symbols)}");
        _output.WriteLine($"Output directory: {_outputDir}");
        _output.WriteLine("");

        var summaryLines = new List<string>
        {
            "# Yahoo Finance Data Collection Report",
            $"Run Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            $"Symbols: {string.Join(", ", symbols)}",
            "",
            $"{"Symbol",-12} {"AdjBars",10} {"RawBars",10} {"From",14} {"To",14} {"Status",-20}",
            new string('-', 82),
        };

        var successCount = 0;

        foreach (var symbol in symbols)
        {
            _output.WriteLine($"=== Fetching data for {symbol} ===");

            var adjustedBarCount = 0;
            var rawBarCount = 0;
            string dateFrom = "N/A";
            string dateTo = "N/A";
            string status;

            try
            {
                // Fetch adjusted bars (no OHLC validation, captures raw provider data)
                var adjustedBars = await _provider.GetAdjustedDailyBarsAsync(
                    symbol, from: null, to: null);
                adjustedBarCount = adjustedBars.Count;

                if (adjustedBars.Count > 0)
                {
                    dateFrom = adjustedBars.First().SessionDate.ToString("yyyy-MM-dd");
                    dateTo = adjustedBars.Last().SessionDate.ToString("yyyy-MM-dd");
                }

                // Write adjusted bars to JSON
                var adjustedPath = Path.Combine(_outputDir, $"{symbol}_adjusted_bars.json");
                var adjustedJson = JsonSerializer.Serialize(
                    adjustedBars.Select(b => new
                    {
                        b.Symbol,
                        SessionDate = b.SessionDate.ToString("yyyy-MM-dd"),
                        b.Open,
                        b.High,
                        b.Low,
                        b.Close,
                        b.Volume,
                        b.AdjustedOpen,
                        b.AdjustedHigh,
                        b.AdjustedLow,
                        b.AdjustedClose,
                        b.AdjustedVolume,
                        b.SplitFactor,
                        b.DividendAmount,
                        b.Source,
                    }),
                    JsonOptions);
                await File.WriteAllTextAsync(adjustedPath, adjustedJson);

                _output.WriteLine($"  Adjusted bars: {adjustedBarCount}");
                _output.WriteLine($"  Written to: {adjustedPath}");

                // Attempt raw bars (may fail due to OHLC validation on some symbols)
                try
                {
                    var rawBars = await _provider.GetDailyBarsAsync(
                        symbol, from: null, to: null);
                    rawBarCount = rawBars.Count;

                    var rawPath = Path.Combine(_outputDir, $"{symbol}_daily_bars.json");
                    var rawJson = JsonSerializer.Serialize(
                        rawBars.Select(b => new
                        {
                            b.Symbol,
                            SessionDate = b.SessionDate.ToString("yyyy-MM-dd"),
                            b.Open,
                            b.High,
                            b.Low,
                            b.Close,
                            b.Volume,
                            b.Source,
                            b.Range,
                            b.BodySize,
                            b.IsBullish,
                            b.ChangePercent,
                            b.TypicalPrice,
                        }),
                        JsonOptions);
                    await File.WriteAllTextAsync(rawPath, rawJson);

                    _output.WriteLine($"  Raw bars: {rawBarCount}");
                    _output.WriteLine($"  Written to: {rawPath}");
                    status = "OK";
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Raw bars FAILED: {ex.Message}");
                    status = $"Adjusted OK, Raw failed";

                    // Write error details
                    var errorPath = Path.Combine(_outputDir, $"{symbol}_raw_bars_error.txt");
                    await File.WriteAllTextAsync(errorPath, $"Error fetching raw bars for {symbol}:\n{ex}");
                }

                successCount++;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  FAILED: {ex.Message}");
                status = $"FAILED: {ex.Message}";

                var errorPath = Path.Combine(_outputDir, $"{symbol}_error.txt");
                await File.WriteAllTextAsync(errorPath, $"Error fetching data for {symbol}:\n{ex}");
            }

            summaryLines.Add(
                $"{symbol,-12} {adjustedBarCount,10} {rawBarCount,10} {dateFrom,14} {dateTo,14} {status,-20}");

            _output.WriteLine("");
        }

        // Write summary report
        summaryLines.Add("");
        summaryLines.Add($"Total symbols: {symbols.Length}");
        summaryLines.Add($"Successful: {successCount}");
        summaryLines.Add($"Failed: {symbols.Length - successCount}");

        var summaryPath = Path.Combine(_outputDir, "collection_summary.txt");
        await File.WriteAllTextAsync(summaryPath, string.Join("\n", summaryLines));
        _output.WriteLine($"Summary written to: {summaryPath}");

        foreach (var line in summaryLines)
        {
            _output.WriteLine(line);
        }

        // Report the outcome without failing the test
        // This allows the workflow to collect whatever data is available
        // and surface it as an artifact for inspection
        if (successCount == 0)
        {
            _output.WriteLine("");
            _output.WriteLine("⚠️  WARNING: No symbols were successfully fetched.");
            _output.WriteLine("This may indicate:");
            _output.WriteLine("  - Yahoo Finance API changes or rate limiting");
            _output.WriteLine("  - Network connectivity issues");
            _output.WriteLine("  - Symbol availability issues");
            _output.WriteLine("");
            _output.WriteLine("Check the error files in ArtifactOutput for details.");
        }
        else if (successCount < symbols.Length)
        {
            _output.WriteLine("");
            _output.WriteLine($"⚠️  WARNING: Only {successCount}/{symbols.Length} symbols were successfully fetched.");
        }
        else
        {
            _output.WriteLine("");
            _output.WriteLine($"✅ SUCCESS: All {successCount} symbols were successfully fetched.");
        }
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
