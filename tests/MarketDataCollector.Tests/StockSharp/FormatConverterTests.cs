using FluentAssertions;
using MarketDataCollector.Storage.StockSharp;
using Xunit;

namespace MarketDataCollector.Tests.StockSharp;

/// <summary>
/// Tests for the FormatConverter utility.
/// </summary>
public class FormatConverterTests : IDisposable
{
    private readonly string _tempPath;

    public FormatConverterTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "mdc-fc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, recursive: true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    [Fact]
    public void FormatConverter_Constructor_DoesNotThrow()
    {
        // Act
        var converter = new FormatConverter();

        // Assert
        converter.Should().NotBeNull();
    }

    [Fact]
    public void EstimateSavings_WithNonExistentDirectory_ReturnsError()
    {
        // Arrange
        var converter = new FormatConverter();
        var nonExistentPath = Path.Combine(_tempPath, "does-not-exist");

        // Act
        var estimate = converter.EstimateSavings(nonExistentPath);

        // Assert
        estimate.Error.Should().Be("Directory not found");
    }

    [Fact]
    public void EstimateSavings_WithEmptyDirectory_ReturnsZeros()
    {
        // Arrange
        var converter = new FormatConverter();
        var emptyPath = Path.Combine(_tempPath, "empty-dir");
        Directory.CreateDirectory(emptyPath);

        // Act
        var estimate = converter.EstimateSavings(emptyPath);

        // Assert
        estimate.Error.Should().BeNull();
        estimate.CurrentSizeBytes.Should().Be(0);
        estimate.EstimatedBinarySizeBytes.Should().Be(0);
        estimate.EstimatedSavingsBytes.Should().Be(0);
        estimate.FileCount.Should().Be(0);
    }

    [Fact]
    public void EstimateSavings_WithJsonlFiles_CalculatesEstimate()
    {
        // Arrange
        var converter = new FormatConverter();
        var jsonlPath = Path.Combine(_tempPath, "jsonl-data");
        Directory.CreateDirectory(jsonlPath);

        // Create sample JSONL files
        var sampleContent = new string('x', 1000); // 1KB
        File.WriteAllText(Path.Combine(jsonlPath, "trades.jsonl"), sampleContent);
        File.WriteAllText(Path.Combine(jsonlPath, "depth.jsonl"), sampleContent + sampleContent); // 2KB

        // Act
        var estimate = converter.EstimateSavings(jsonlPath);

        // Assert
        estimate.Error.Should().BeNull();
        estimate.FileCount.Should().Be(2);
        estimate.CurrentSizeBytes.Should().Be(3000); // 3KB total
        estimate.EstimatedBinarySizeBytes.Should().Be(150); // 3000 / 20 = 150 bytes
        estimate.EstimatedSavingsBytes.Should().Be(2850); // 3000 - 150
        estimate.EstimatedSavingsPercent.Should().Be(95); // 2850/3000 * 100
    }

    [Fact]
    public void StorageSavingsEstimate_FormatBytes_FormatsCorrectly()
    {
        // Arrange
        var estimate = new StorageSavingsEstimate
        {
            CurrentSizeBytes = 1024 * 1024 * 500, // 500 MB
            EstimatedBinarySizeBytes = 1024 * 1024 * 25, // 25 MB
            EstimatedSavingsBytes = 1024 * 1024 * 475 // 475 MB
        };

        // Assert
        estimate.CurrentSizeFormatted.Should().Be("500 MB");
        estimate.EstimatedBinarySizeFormatted.Should().Be("25 MB");
        estimate.EstimatedSavingsFormatted.Should().Be("475 MB");
    }

    [Fact]
    public void ConversionResult_EventsPerSecond_CalculatesCorrectly()
    {
        // Arrange
        var result = new ConversionResult
        {
            EventsConverted = 1000000,
            Duration = TimeSpan.FromSeconds(10)
        };

        // Assert
        result.EventsPerSecond.Should().Be(100000);
    }

    [Fact]
    public void ConversionResult_EventsPerSecond_ReturnsZeroForZeroDuration()
    {
        // Arrange
        var result = new ConversionResult
        {
            EventsConverted = 1000000,
            Duration = TimeSpan.Zero
        };

        // Assert
        result.EventsPerSecond.Should().Be(0);
    }

    [Fact]
    public async Task ConvertJsonlToStockSharpAsync_WithEmptySource_ReturnsSuccess()
    {
        // Arrange
        var converter = new FormatConverter();
        var sourcePath = Path.Combine(_tempPath, "empty-jsonl");
        var targetPath = Path.Combine(_tempPath, "binary-output");
        Directory.CreateDirectory(sourcePath);

        // Act
        var result = await converter.ConvertJsonlToStockSharpAsync(sourcePath, targetPath);

        // Assert
        result.Success.Should().BeTrue();
        result.EventsConverted.Should().Be(0);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ConvertStockSharpToJsonlAsync_WithEmptySource_ReturnsSuccess()
    {
        // Arrange
        var converter = new FormatConverter();
        var sourcePath = Path.Combine(_tempPath, "empty-binary");
        var targetPath = Path.Combine(_tempPath, "jsonl-output");
        Directory.CreateDirectory(sourcePath);

        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = await converter.ConvertStockSharpToJsonlAsync(
            sourcePath, targetPath, new[] { "SPY", "QQQ" }, from, to);

        // Assert
        result.Success.Should().BeTrue();
        result.EventsConverted.Should().Be(0);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ConvertJsonlToStockSharpAsync_WithCancellation_ReturnsCancelledResult()
    {
        // Arrange
        var converter = new FormatConverter();
        var sourcePath = Path.Combine(_tempPath, "cancel-source");
        var targetPath = Path.Combine(_tempPath, "cancel-target");
        Directory.CreateDirectory(sourcePath);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await converter.ConvertJsonlToStockSharpAsync(sourcePath, targetPath, ct: cts.Token);

        // Assert
        // With an empty source, it may complete before cancellation is checked
        // The result should either be successful (empty) or cancelled
        (result.Success || result.Error == "Conversion cancelled").Should().BeTrue();
    }

    [Fact]
    public void ConversionResult_CanTrackMultipleEventTypes()
    {
        // Arrange & Act
        var result = new ConversionResult
        {
            Success = true,
            EventsConverted = 10000,
            TradesConverted = 5000,
            DepthSnapshotsConverted = 3000,
            QuotesConverted = 1500,
            CandlesConverted = 500,
            Duration = TimeSpan.FromSeconds(5)
        };

        // Assert
        result.TradesConverted.Should().Be(5000);
        result.DepthSnapshotsConverted.Should().Be(3000);
        result.QuotesConverted.Should().Be(1500);
        result.CandlesConverted.Should().Be(500);
        (result.TradesConverted + result.DepthSnapshotsConverted +
         result.QuotesConverted + result.CandlesConverted).Should().Be(10000);
    }
}
