using System.Text;
using System.Text.Json;
using FluentAssertions;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Crystallized;
using MarketDataCollector.Storage.Export;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

public class CrystallizedCsvExporterTests : IDisposable
{
    private readonly string _testDataRoot;
    private readonly string _testOutputDir;
    private readonly CrystallizedStorageFormat _format;
    private readonly CrystallizedCsvExporter _exporter;

    public CrystallizedCsvExporterTests()
    {
        _testDataRoot = Path.Combine(Path.GetTempPath(), $"mdc_crystallized_test_{Guid.NewGuid():N}");
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"mdc_crystallized_export_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_testDataRoot);
        Directory.CreateDirectory(_testOutputDir);

        var options = new CrystallizedStorageOptions { RootPath = _testDataRoot };
        _format = new CrystallizedStorageFormat(options);
        _exporter = new CrystallizedCsvExporter(_format);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataRoot))
            Directory.Delete(_testDataRoot, recursive: true);
        if (Directory.Exists(_testOutputDir))
            Directory.Delete(_testOutputDir, recursive: true);
    }

    [Fact]
    public async Task ExportSymbolBarsAsync_WithValidJsonlData_ShouldExportToCsv()
    {
        // Arrange
        var provider = "alpaca";
        var symbol = "AAPL";
        var granularity = TimeGranularity.Daily;

        await CreateTestBarDataAsync(provider, symbol, granularity, new[]
        {
            new { symbol = "AAPL", sessionDate = "2024-01-02", open = 185.50m, high = 186.50m, low = 184.50m, close = 186.00m, volume = 1000000L, source = "alpaca" },
            new { symbol = "AAPL", sessionDate = "2024-01-03", open = 186.00m, high = 187.00m, low = 185.00m, close = 186.50m, volume = 1100000L, source = "alpaca" },
            new { symbol = "AAPL", sessionDate = "2024-01-04", open = 186.50m, high = 188.00m, low = 186.00m, close = 187.50m, volume = 1200000L, source = "alpaca" }
        });

        var outputPath = Path.Combine(_testOutputDir, "AAPL_bars.csv");

        // Act
        await _exporter.ExportSymbolBarsAsync(provider, symbol, granularity, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(outputPath);
        lines.Length.Should().Be(4); // 1 header + 3 data rows
        lines[0].Should().Be("date,open,high,low,close,volume");
        lines[1].Should().Contain("2024-01-02");
        lines[2].Should().Contain("2024-01-03");
        lines[3].Should().Contain("2024-01-04");
    }

    [Fact]
    public async Task ExportSymbolBarsAsync_WithDateFilter_ShouldFilterResults()
    {
        // Arrange
        var provider = "alpaca";
        var symbol = "AAPL";
        var granularity = TimeGranularity.Daily;

        await CreateTestBarDataAsync(provider, symbol, granularity, new[]
        {
            new { symbol = "AAPL", sessionDate = "2024-01-02", open = 185.50m, high = 186.50m, low = 184.50m, close = 186.00m, volume = 1000000L, source = "alpaca" },
            new { symbol = "AAPL", sessionDate = "2024-01-03", open = 186.00m, high = 187.00m, low = 185.00m, close = 186.50m, volume = 1100000L, source = "alpaca" },
            new { symbol = "AAPL", sessionDate = "2024-01-04", open = 186.50m, high = 188.00m, low = 186.00m, close = 187.50m, volume = 1200000L, source = "alpaca" }
        });

        var outputPath = Path.Combine(_testOutputDir, "AAPL_filtered.csv");

        // Act
        await _exporter.ExportSymbolBarsAsync(
            provider, symbol, granularity, outputPath,
            fromDate: new DateOnly(2024, 1, 3),
            toDate: new DateOnly(2024, 1, 3));

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(outputPath);
        lines.Length.Should().Be(2); // 1 header + 1 filtered data row
        lines[1].Should().Contain("2024-01-03");
    }

    [Fact]
    public async Task ExportMultipleSymbolsAsync_ShouldCombineMultipleSymbols()
    {
        // Arrange
        var provider = "alpaca";
        var granularity = TimeGranularity.Daily;

        await CreateTestBarDataAsync(provider, "AAPL", granularity, new[]
        {
            new { symbol = "AAPL", sessionDate = "2024-01-02", open = 185.50m, high = 186.50m, low = 184.50m, close = 186.00m, volume = 1000000L, source = "alpaca" },
            new { symbol = "AAPL", sessionDate = "2024-01-03", open = 186.00m, high = 187.00m, low = 185.00m, close = 186.50m, volume = 1100000L, source = "alpaca" }
        });

        await CreateTestBarDataAsync(provider, "MSFT", granularity, new[]
        {
            new { symbol = "MSFT", sessionDate = "2024-01-02", open = 375.50m, high = 377.50m, low = 374.50m, close = 376.00m, volume = 2000000L, source = "alpaca" },
            new { symbol = "MSFT", sessionDate = "2024-01-03", open = 376.00m, high = 378.00m, low = 375.00m, close = 377.50m, volume = 2100000L, source = "alpaca" }
        });

        var outputPath = Path.Combine(_testOutputDir, "combined_bars.csv");

        // Act
        await _exporter.ExportMultipleSymbolsAsync(provider, new[] { "AAPL", "MSFT" }, granularity, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(outputPath);
        lines.Length.Should().Be(5); // 1 header + 4 data rows (2 per symbol)
        lines[0].Should().StartWith("symbol,"); // Symbol column should be included
        lines.Count(l => l.Contains("AAPL")).Should().Be(2);
        lines.Count(l => l.Contains("MSFT")).Should().Be(2);
    }

    [Fact]
    public async Task ExportSymbolBarsAsync_WithNoData_ShouldCreateEmptyFile()
    {
        // Arrange
        var provider = "alpaca";
        var symbol = "NONEXISTENT";
        var granularity = TimeGranularity.Daily;
        var outputPath = Path.Combine(_testOutputDir, "empty.csv");

        // Act
        await _exporter.ExportSymbolBarsAsync(provider, symbol, granularity, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(outputPath);
        lines.Length.Should().Be(1); // Just the header
    }

    [Fact]
    public async Task ExportSymbolBarsAsync_WithInvalidProvider_ShouldThrow()
    {
        // Arrange
        var outputPath = Path.Combine(_testOutputDir, "invalid.csv");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exporter.ExportSymbolBarsAsync("", "AAPL", TimeGranularity.Daily, outputPath));
    }

    [Fact]
    public async Task ExportSymbolBarsAsync_WithInvalidSymbol_ShouldThrow()
    {
        // Arrange
        var outputPath = Path.Combine(_testOutputDir, "invalid.csv");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exporter.ExportSymbolBarsAsync("alpaca", "", TimeGranularity.Daily, outputPath));
    }

    [Fact]
    public async Task ExportMultipleSymbolsAsync_WithEmptySymbolList_ShouldThrow()
    {
        // Arrange
        var outputPath = Path.Combine(_testOutputDir, "invalid.csv");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exporter.ExportMultipleSymbolsAsync("alpaca", Array.Empty<string>(), TimeGranularity.Daily, outputPath));
    }

    private async Task CreateTestBarDataAsync<T>(string provider, string symbol, TimeGranularity granularity, T[] bars)
    {
        // Create the crystallized storage directory structure
        var manifestPath = _format.GetSymbolManifestPath(provider, symbol);
        var symbolDir = Path.GetDirectoryName(manifestPath)!;
        var barsDir = Path.Combine(symbolDir, "bars", granularity.ToFileSuffix());

        Directory.CreateDirectory(barsDir);

        // Create JSONL file with test data
        var filePath = Path.Combine(barsDir, "2024-01.jsonl");
        await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

        foreach (var bar in bars)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(bar));
        }
    }
}
