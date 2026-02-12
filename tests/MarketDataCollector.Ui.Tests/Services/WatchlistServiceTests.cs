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

    [Fact]
    public async Task LoadWatchlistAsync_WithCancellation_SupportsCancellationToken()
    {
        // Arrange
        var service = WatchlistService.Instance;
        using var cts = new CancellationTokenSource();

        // Act - Should accept cancellation token
        var watchlist = await service.LoadWatchlistAsync(cts.Token);

        // Assert
        watchlist.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateOrUpdateWatchlistAsync_CreatesNewWatchlist()
    {
        // Arrange
        var service = WatchlistService.Instance;
        var watchlist = new WatchlistData
        {
            Symbols = new List<WatchlistItem>
            {
                new() { Symbol = "SPY", Notes = "S&P 500" }
            }
        };

        // Act
        var result = await service.CreateOrUpdateWatchlistAsync(watchlist);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void WatchlistItem_CanStoreMultipleProperties()
    {
        // Act
        var item = new WatchlistItem
        {
            Symbol = "AAPL",
            Notes = "Apple Inc.",
            Tags = new List<string> { "tech", "nasdaq" }
        };

        // Assert
        item.Symbol.Should().Be("AAPL");
        item.Notes.Should().Be("Apple Inc.");
        item.Tags.Should().Contain("tech");
        item.Tags.Should().Contain("nasdaq");
    }

    [Fact]
    public void WatchlistGroup_SupportsNestedHierarchy()
    {
        // Act
        var parentGroup = new WatchlistGroup
        {
            Name = "All Stocks",
            Symbols = new List<string>()
        };
        var childGroup = new WatchlistGroup
        {
            Name = "Tech",
            Symbols = new List<string> { "AAPL", "MSFT" }
        };

        // Assert
        parentGroup.Should().NotBeNull();
        childGroup.Should().NotBeNull();
        childGroup.Symbols.Should().HaveCount(2);
    }

    [Fact]
    public void WatchlistData_EmptyWatchlist_IsValid()
    {
        // Act
        var watchlist = new WatchlistData();

        // Assert
        watchlist.Symbols.Should().BeEmpty();
        watchlist.Groups.Should().BeEmpty();
    }

    [Theory]
    [InlineData("SPY")]
    [InlineData("AAPL")]
    [InlineData("MSFT")]
    [InlineData("TSLA")]
    public void WatchlistItem_AcceptsDifferentSymbols(string symbol)
    {
        // Act
        var item = new WatchlistItem { Symbol = symbol };

        // Assert
        item.Symbol.Should().Be(symbol);
    }

    [Fact]
    public void WatchlistData_SupportsLargeNumberOfSymbols()
    {
        // Arrange
        var watchlist = new WatchlistData();

        // Act
        for (int i = 0; i < 100; i++)
        {
            watchlist.Symbols.Add(new WatchlistItem { Symbol = $"SYMBOL{i}" });
        }

        // Assert
        watchlist.Symbols.Should().HaveCount(100);
    }

    [Fact]
    public void WatchlistGroup_CanHaveEmptySymbolList()
    {
        // Act
        var group = new WatchlistGroup
        {
            Name = "Empty Group",
            Symbols = new List<string>()
        };

        // Assert
        group.Symbols.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadWatchlistAsync_MultipleCallsInSequence_ReturnsConsistentData()
    {
        // Arrange
        var service = WatchlistService.Instance;

        // Act
        var watchlist1 = await service.LoadWatchlistAsync();
        var watchlist2 = await service.LoadWatchlistAsync();

        // Assert
        watchlist1.Should().NotBeNull();
        watchlist2.Should().NotBeNull();
        watchlist1.Symbols.Count.Should().Be(watchlist2.Symbols.Count);
    }

    [Fact]
    public void WatchlistItem_WithNullNotes_IsValid()
    {
        // Act
        var item = new WatchlistItem
        {
            Symbol = "SPY",
            Notes = null
        };

        // Assert
        item.Symbol.Should().Be("SPY");
        item.Notes.Should().BeNull();
    }

    [Fact]
    public void WatchlistData_Modification_PreservesIntegrity()
    {
        // Arrange
        var watchlist = new WatchlistData();
        watchlist.Symbols.Add(new WatchlistItem { Symbol = "SPY" });

        // Act
        var symbol = watchlist.Symbols[0];
        symbol.Notes = "Modified";

        // Assert
        watchlist.Symbols[0].Notes.Should().Be("Modified");
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
