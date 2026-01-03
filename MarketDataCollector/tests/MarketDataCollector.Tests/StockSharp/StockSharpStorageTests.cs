using FluentAssertions;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Storage.StockSharp;
using Xunit;

namespace MarketDataCollector.Tests.StockSharp;

/// <summary>
/// Tests for StockSharp storage components.
/// Note: Full functionality requires StockSharp NuGet packages.
/// These tests validate the API surface and stub behavior when packages are not installed.
/// </summary>
public class StockSharpStorageTests : IDisposable
{
    private readonly string _tempPath;

    public StockSharpStorageTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "mdc-ss-test-" + Guid.NewGuid().ToString("N")[..8]);
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
    public void StockSharpStorageSink_Constructor_CreatesDirectory()
    {
        // Arrange
        var sinkPath = Path.Combine(_tempPath, "storage-sink-test");

        // Act
        // Note: Without StockSharp packages, the sink will log a warning but still create the directory
        var sink = new StockSharpStorageSink(sinkPath, useBinaryFormat: true);

        // Assert
        Directory.Exists(sinkPath).Should().BeTrue();
    }

    [Fact]
    public async Task StockSharpStorageSink_EventsWritten_StartsAtZero()
    {
        // Arrange
        var sinkPath = Path.Combine(_tempPath, "events-test");
        await using var sink = new StockSharpStorageSink(sinkPath);

        // Assert
        sink.EventsWritten.Should().Be(0);
    }

    [Fact]
    public async Task StockSharpStorageSink_FlushAsync_CompletesSuccessfully()
    {
        // Arrange
        var sinkPath = Path.Combine(_tempPath, "flush-test");
        await using var sink = new StockSharpStorageSink(sinkPath);

        // Act & Assert (should not throw)
        await sink.FlushAsync();
    }

    [Fact]
    public void StockSharpStorageReader_Constructor_DoesNotThrow()
    {
        // Arrange & Act
        var reader = new StockSharpStorageReader(_tempPath);

        // Assert - just verify it doesn't throw
        reader.Should().NotBeNull();
    }

    [Fact]
    public async Task StockSharpStorageReader_ReadTradesAsync_ReturnsEmptyWithoutStockSharpPackages()
    {
        // Arrange
        var reader = new StockSharpStorageReader(_tempPath);
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;

        // Act
        var trades = new List<Trade>();
        await foreach (var trade in reader.ReadTradesAsync("SPY", from, to))
        {
            trades.Add(trade);
        }

        // Assert
        // Without StockSharp packages, this returns empty
        trades.Should().BeEmpty();
    }

    [Fact]
    public async Task StockSharpStorageReader_ReadDepthAsync_ReturnsEmptyWithoutStockSharpPackages()
    {
        // Arrange
        var reader = new StockSharpStorageReader(_tempPath);
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;

        // Act
        var snapshots = new List<LOBSnapshot>();
        await foreach (var snap in reader.ReadDepthAsync("SPY", from, to))
        {
            snapshots.Add(snap);
        }

        // Assert
        snapshots.Should().BeEmpty();
    }

    [Fact]
    public async Task StockSharpStorageReader_ReadCandlesAsync_ReturnsEmptyWithoutStockSharpPackages()
    {
        // Arrange
        var reader = new StockSharpStorageReader(_tempPath);
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        // Act
        var candles = new List<HistoricalBar>();
        await foreach (var candle in reader.ReadCandlesAsync("SPY", from, to))
        {
            candles.Add(candle);
        }

        // Assert
        candles.Should().BeEmpty();
    }

    [Fact]
    public void StockSharpStorageReader_GetTradesDateRange_ReturnsNullWithoutData()
    {
        // Arrange
        var reader = new StockSharpStorageReader(_tempPath);

        // Act
        var (first, last) = reader.GetTradesDateRange("SPY");

        // Assert
        first.Should().BeNull();
        last.Should().BeNull();
    }

    [Fact]
    public void StockSharpStorageReader_GetDepthDateRange_ReturnsNullWithoutData()
    {
        // Arrange
        var reader = new StockSharpStorageReader(_tempPath);

        // Act
        var (first, last) = reader.GetDepthDateRange("SPY");

        // Assert
        first.Should().BeNull();
        last.Should().BeNull();
    }
}
