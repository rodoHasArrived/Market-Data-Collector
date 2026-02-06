using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Infrastructure.Providers.PluginAdapters;
using MarketDataCollector.ProviderSdk.Providers;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for plugin adapter classes that bridge SDK interfaces to internal interfaces.
/// </summary>
public sealed class PluginAdapterTests
{
    #region PluginHistoricalProviderAdapter

    [Fact]
    public void HistoricalAdapter_BridgesIdentityProperties()
    {
        // Arrange
        var mockProvider = new Mock<IHistoricalProvider>();
        mockProvider.Setup(p => p.ProviderId).Returns("test-provider");
        mockProvider.Setup(p => p.DisplayName).Returns("Test Provider");
        mockProvider.Setup(p => p.Description).Returns("A test provider");
        mockProvider.Setup(p => p.Priority).Returns(50);
        mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.BackfillBarsOnly);

        // Act
        var adapter = new PluginHistoricalProviderAdapter(mockProvider.Object);

        // Assert
        adapter.Name.Should().Be("test-provider");
        adapter.DisplayName.Should().Be("Test Provider");
        adapter.Description.Should().Be("A test provider");
        adapter.Priority.Should().Be(50);
        adapter.ProviderId.Should().Be("test-provider");
    }

    [Fact]
    public void HistoricalAdapter_MapsCapabilitiesCorrectly()
    {
        // Arrange
        var caps = ProviderCapabilities.BackfillFullFeatured with
        {
            SupportedMarkets = new[] { "US", "UK" }
        };
        var mockProvider = new Mock<IHistoricalProvider>();
        mockProvider.Setup(p => p.Capabilities).Returns(caps);

        // Act
        var adapter = new PluginHistoricalProviderAdapter(mockProvider.Object);
        var internalCaps = adapter.Capabilities;

        // Assert
        internalCaps.AdjustedPrices.Should().BeTrue();
        internalCaps.Intraday.Should().BeTrue();
        internalCaps.Dividends.Should().BeTrue();
        internalCaps.Splits.Should().BeTrue();
        internalCaps.Quotes.Should().BeTrue();
        internalCaps.Trades.Should().BeTrue();
        internalCaps.Auctions.Should().BeTrue();
        internalCaps.SupportedMarkets.Should().Contain("US").And.Contain("UK");
    }

    [Fact]
    public async Task HistoricalAdapter_DelegatesGetDailyBarsAsync()
    {
        // Arrange
        var expectedBars = new List<HistoricalBar>
        {
            new("SPY", new DateOnly(2024, 1, 2), 470m, 475m, 468m, 473m, 100000, "test", 1)
        };
        var mockProvider = new Mock<IHistoricalProvider>();
        mockProvider.Setup(p => p.GetDailyBarsAsync("SPY", It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBars);
        mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.BackfillBarsOnly);

        var adapter = new PluginHistoricalProviderAdapter(mockProvider.Object);

        // Act
        var bars = await adapter.GetDailyBarsAsync("SPY", null, null);

        // Assert
        bars.Should().HaveCount(1);
        bars[0].Symbol.Should().Be("SPY");
        bars[0].Close.Should().Be(473m);
    }

    [Fact]
    public async Task HistoricalAdapter_DelegatesIsAvailableAsync()
    {
        // Arrange
        var mockProvider = new Mock<IHistoricalProvider>();
        mockProvider.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.BackfillBarsOnly);

        var adapter = new PluginHistoricalProviderAdapter(mockProvider.Object);

        // Act
        var available = await adapter.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public void HistoricalAdapter_DelegatesDispose()
    {
        // Arrange
        var mockProvider = new Mock<IHistoricalProvider>();
        mockProvider.Setup(p => p.Capabilities).Returns(ProviderCapabilities.BackfillBarsOnly);
        var adapter = new PluginHistoricalProviderAdapter(mockProvider.Object);

        // Act
        adapter.Dispose();

        // Assert
        mockProvider.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public void HistoricalAdapter_ThrowsOnNullProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PluginHistoricalProviderAdapter(null!));
    }

    #endregion

    #region PluginStreamingProviderAdapter

    [Fact]
    public void StreamingAdapter_BridgesIdentityProperties()
    {
        // Arrange
        var mockProvider = new Mock<IStreamingProvider>();
        mockProvider.Setup(p => p.ProviderId).Returns("test-stream");
        mockProvider.Setup(p => p.DisplayName).Returns("Test Stream");
        mockProvider.Setup(p => p.Description).Returns("A test streaming provider");
        mockProvider.Setup(p => p.Priority).Returns(10);
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Capabilities).Returns(
            ProviderCapabilities.Streaming(trades: true, quotes: true, depth: true, maxDepthLevels: 5));

        // Act
        var adapter = new PluginStreamingProviderAdapter(mockProvider.Object);

        // Assert
        adapter.ProviderId.Should().Be("test-stream");
        adapter.ProviderDisplayName.Should().Be("Test Stream");
        adapter.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void StreamingAdapter_MapsStreamingCapabilities()
    {
        // Arrange
        var caps = ProviderCapabilities.Streaming(trades: true, quotes: true, depth: true, maxDepthLevels: 5);
        var mockProvider = new Mock<IStreamingProvider>();
        mockProvider.Setup(p => p.Capabilities).Returns(caps);

        // Act
        var adapter = new PluginStreamingProviderAdapter(mockProvider.Object);
        var internalCaps = adapter.ProviderCapabilities;

        // Assert
        internalCaps.SupportsStreaming.Should().BeTrue();
        internalCaps.SupportsRealtimeTrades.Should().BeTrue();
        internalCaps.SupportsRealtimeQuotes.Should().BeTrue();
        internalCaps.SupportsMarketDepth.Should().BeTrue();
        internalCaps.MaxDepthLevels.Should().Be(5);
    }

    [Fact]
    public void StreamingAdapter_ThrowsOnNullProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PluginStreamingProviderAdapter(null!));
    }

    #endregion

    #region PluginSymbolSearchProviderAdapter

    [Fact]
    public void SymbolSearchAdapter_BridgesIdentityProperties()
    {
        // Arrange
        var mockProvider = new Mock<ProviderSdk.Providers.ISymbolSearchProvider>();
        mockProvider.Setup(p => p.ProviderId).Returns("test-search");
        mockProvider.Setup(p => p.DisplayName).Returns("Test Search");
        mockProvider.Setup(p => p.Description).Returns("A test search provider");
        mockProvider.Setup(p => p.Priority).Returns(5);

        // Act
        var adapter = new PluginSymbolSearchProviderAdapter(mockProvider.Object);

        // Assert
        adapter.Name.Should().Be("test-search");
        adapter.DisplayName.Should().Be("Test Search");
        adapter.ProviderId.Should().Be("test-search");
        adapter.ProviderPriority.Should().Be(5);
    }

    [Fact]
    public async Task SymbolSearchAdapter_MapsSearchResults()
    {
        // Arrange
        var sdkResults = new List<ProviderSdk.Providers.SymbolSearchResult>
        {
            new("AAPL", "Apple Inc.", Exchange: "NASDAQ", AssetType: "Stock", Currency: "USD", Region: "US")
        };
        var mockProvider = new Mock<ProviderSdk.Providers.ISymbolSearchProvider>();
        mockProvider.Setup(p => p.SearchAsync("AAPL", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sdkResults);

        var adapter = new PluginSymbolSearchProviderAdapter(mockProvider.Object);

        // Act
        var results = await adapter.SearchAsync("AAPL", 10);

        // Assert
        results.Should().HaveCount(1);
        results[0].Symbol.Should().Be("AAPL");
        results[0].Name.Should().Be("Apple Inc.");
        results[0].Exchange.Should().Be("NASDAQ");
        results[0].Country.Should().Be("US"); // SDK Region → internal Country
    }

    [Fact]
    public async Task SymbolSearchAdapter_MapsDetails()
    {
        // Arrange
        var sdkDetails = new ProviderSdk.Providers.SymbolDetails(
            Symbol: "AAPL",
            Name: "Apple Inc.",
            Exchange: "NASDAQ",
            AssetType: "Stock",
            Currency: "USD",
            Description: "Consumer electronics company",
            Sector: "Technology",
            Industry: "Consumer Electronics",
            Country: "US");

        var mockProvider = new Mock<ProviderSdk.Providers.ISymbolSearchProvider>();
        mockProvider.Setup(p => p.GetDetailsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sdkDetails);

        var adapter = new PluginSymbolSearchProviderAdapter(mockProvider.Object);

        // Act
        var details = await adapter.GetDetailsAsync("AAPL");

        // Assert
        details.Should().NotBeNull();
        details!.Symbol.Should().Be("AAPL");
        details.Name.Should().Be("Apple Inc.");
        details.Sector.Should().Be("Technology");
        details.Country.Should().Be("US");
    }

    [Fact]
    public async Task SymbolSearchAdapter_ReturnsNullForMissingDetails()
    {
        // Arrange
        var mockProvider = new Mock<ProviderSdk.Providers.ISymbolSearchProvider>();
        mockProvider.Setup(p => p.GetDetailsAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderSdk.Providers.SymbolDetails?)null);

        var adapter = new PluginSymbolSearchProviderAdapter(mockProvider.Object);

        // Act
        var details = await adapter.GetDetailsAsync("UNKNOWN");

        // Assert
        details.Should().BeNull();
    }

    [Fact]
    public void SymbolSearchAdapter_ThrowsOnNullProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PluginSymbolSearchProviderAdapter(null!));
    }

    #endregion
}
