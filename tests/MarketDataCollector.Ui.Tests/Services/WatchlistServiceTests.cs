using FluentAssertions;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Tests.Services;

/// <summary>
/// Tests for <see cref="WatchlistService"/> functionality.
/// </summary>
public sealed class WatchlistServiceTests
{
    [Fact]
    public void Instance_ReturnsNonNullSingleton()
    {
        // Act
        var instance = WatchlistService.Instance;

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Instance_ReturnsSameInstanceOnMultipleCalls()
    {
        // Act
        var instance1 = WatchlistService.Instance;
        var instance2 = WatchlistService.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public async Task LoadWatchlistAsync_ReturnsEmptyWatchlistByDefault()
    {
        // Arrange
        var service = WatchlistService.Instance;

        // Act
        var watchlist = await service.LoadWatchlistAsync();

        // Assert
        watchlist.Should().NotBeNull();
        watchlist.Symbols.Should().NotBeNull();
        watchlist.Groups.Should().NotBeNull();
    }

    [Fact]
    public void WatchlistData_Initialization_CreatesEmptyCollections()
    {
        // Act
        var watchlist = new WatchlistData();

        // Assert
        watchlist.Symbols.Should().NotBeNull();
        watchlist.Symbols.Should().BeEmpty();
        watchlist.Groups.Should().NotBeNull();
        watchlist.Groups.Should().BeEmpty();
    }

    [Fact]
    public void WatchlistItem_CanStoreSymbolAndNotes()
    {
        // Act
        var item = new WatchlistItem
        {
            Symbol = "SPY",
            Notes = "S&P 500 ETF"
        };

        // Assert
        item.Symbol.Should().Be("SPY");
        item.Notes.Should().Be("S&P 500 ETF");
    }

    [Fact]
    public void WatchlistGroup_CanStoreNameAndSymbols()
    {
        // Act
        var group = new WatchlistGroup
        {
            Name = "Tech Stocks",
            Symbols = new List<string> { "AAPL", "MSFT", "GOOGL" }
        };

        // Assert
        group.Name.Should().Be("Tech Stocks");
        group.Symbols.Should().HaveCount(3);
        group.Symbols.Should().Contain(new[] { "AAPL", "MSFT", "GOOGL" });
    }

    [Fact]
    public void WatchlistData_CanAddMultipleSymbols()
    {
        // Arrange
        var watchlist = new WatchlistData();

        // Act
        watchlist.Symbols.Add(new WatchlistItem { Symbol = "SPY" });
        watchlist.Symbols.Add(new WatchlistItem { Symbol = "AAPL" });
        watchlist.Symbols.Add(new WatchlistItem { Symbol = "MSFT" });

        // Assert
        watchlist.Symbols.Should().HaveCount(3);
    }

    [Fact]
    public void WatchlistData_CanAddMultipleGroups()
    {
        // Arrange
        var watchlist = new WatchlistData();

        // Act
        watchlist.Groups.Add(new WatchlistGroup { Name = "Tech" });
        watchlist.Groups.Add(new WatchlistGroup { Name = "Finance" });

        // Assert
        watchlist.Groups.Should().HaveCount(2);
    }

    [Fact]
    public void Instance_CanBeReplaced()
    {
        // Arrange
        var customService = new CustomWatchlistService();

        // Act
        WatchlistService.Instance = customService;
        var retrievedInstance = WatchlistService.Instance;

        // Assert
        retrievedInstance.Should().BeSameAs(customService);
    }

    private class CustomWatchlistService : WatchlistService
    {
        public override Task<WatchlistData> LoadWatchlistAsync()
        {
            return Task.FromResult(new WatchlistData
            {
                Symbols = new List<WatchlistItem>
                {
                    new WatchlistItem { Symbol = "CUSTOM" }
                }
            });
        }
    }
}
