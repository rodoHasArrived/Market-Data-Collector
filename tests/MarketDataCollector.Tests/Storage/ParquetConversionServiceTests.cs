using System.Text;
using System.Text.Json;
using FluentAssertions;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Services;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

public class ParquetConversionServiceTests : IDisposable
{
    private readonly string _testDataRoot;
    private readonly ParquetConversionService _service;

    public ParquetConversionServiceTests()
    {
        _testDataRoot = Path.Combine(Path.GetTempPath(), $"mdc_pq_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataRoot);

        var options = new StorageOptions { RootPath = _testDataRoot };
        _service = new ParquetConversionService(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataRoot))
            Directory.Delete(_testDataRoot, recursive: true);
    }

    [Fact]
    public async Task ConvertCompletedDaysAsync_WithNoFiles_ShouldReturnZeroCounts()
    {
        // Act
        var result = await _service.ConvertCompletedDaysAsync();

        // Assert
        result.FilesConverted.Should().Be(0);
        result.RecordsConverted.Should().Be(0);
        result.Errors.Should().Be(0);
    }

    [Fact]
    public async Task ConvertCompletedDaysAsync_ShouldSkipTodaysFiles()
    {
        // Arrange - create a file with today's date in the name
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fileName = $"AAPL.Trade.{today:yyyy-MM-dd}.jsonl";
        await CreateTestJsonlFileAsync(fileName, new[]
        {
            new { Timestamp = DateTime.UtcNow.ToString("o"), Symbol = "AAPL", Price = 185.5, Size = 100 }
        });

        // Act
        var result = await _service.ConvertCompletedDaysAsync();

        // Assert - should not convert today's files
        result.FilesConverted.Should().Be(0);
    }

    [Fact]
    public async Task ConvertCompletedDaysAsync_WithYesterdaysFiles_ShouldConvert()
    {
        // Arrange - create a file with yesterday's date in the name
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var fileName = $"AAPL.Trade.{yesterday:yyyy-MM-dd}.jsonl";
        await CreateTestJsonlFileAsync(fileName, new[]
        {
            new { Timestamp = DateTime.UtcNow.AddDays(-1).ToString("o"), Symbol = "AAPL", Price = 185.5, Size = 100 },
            new { Timestamp = DateTime.UtcNow.AddDays(-1).ToString("o"), Symbol = "AAPL", Price = 185.6, Size = 200 }
        });

        // Act
        var result = await _service.ConvertCompletedDaysAsync();

        // Assert
        result.FilesConverted.Should().Be(1);
        result.RecordsConverted.Should().Be(2);
        result.Errors.Should().Be(0);

        // Verify Parquet file was created
        var parquetDir = Path.Combine(_testDataRoot, "_parquet");
        Directory.Exists(parquetDir).Should().BeTrue();
        Directory.GetFiles(parquetDir, "*.parquet", SearchOption.AllDirectories)
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task ConvertCompletedDaysAsync_ShouldSkipAlreadyConvertedFiles()
    {
        // Arrange - create a JSONL file and run conversion once
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var fileName = $"SPY.Trade.{yesterday:yyyy-MM-dd}.jsonl";
        await CreateTestJsonlFileAsync(fileName, new[]
        {
            new { Timestamp = DateTime.UtcNow.AddDays(-1).ToString("o"), Symbol = "SPY", Price = 450.0, Size = 100 }
        });

        // First conversion
        await _service.ConvertCompletedDaysAsync();

        // Act - run again
        var result = await _service.ConvertCompletedDaysAsync();

        // Assert - should skip already converted
        result.FilesConverted.Should().Be(0);
        result.SkippedAlreadyConverted.Should().Be(1);
    }

    private async Task CreateTestJsonlFileAsync<T>(string fileName, T[] records)
    {
        var filePath = Path.Combine(_testDataRoot, fileName);
        var sb = new StringBuilder();
        foreach (var record in records)
        {
            sb.AppendLine(JsonSerializer.Serialize(record));
        }
        await File.WriteAllTextAsync(filePath, sb.ToString());
    }
}
