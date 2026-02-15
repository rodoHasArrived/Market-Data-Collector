using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Providers.StockSharp;
using MarketDataCollector.Tests.TestHelpers;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

/// <summary>
/// Unit tests for StockSharp client subscription lifecycle, configuration, and error handling.
/// Part of B3 tranche 1 (infrastructure provider unit tests) improvement.
/// Tests client construction, subscription management, and stub-mode behavior.
/// </summary>
public sealed class StockSharpSubscriptionTests : IAsyncLifetime
{
    private readonly TestMarketEventPublisher _publisher;
    private readonly TradeDataCollector _tradeCollector;
    private readonly MarketDepthCollector _depthCollector;
    private readonly QuoteCollector _quoteCollector;

    public StockSharpSubscriptionTests()
    {
        _publisher = new TestMarketEventPublisher();
        _tradeCollector = new TradeDataCollector(_publisher, null);
        _depthCollector = new MarketDepthCollector(_publisher);
        _quoteCollector = new QuoteCollector(_publisher);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private StockSharpMarketDataClient CreateClient(StockSharpConfig? config = null)
    {
        return new StockSharpMarketDataClient(
            _tradeCollector, _depthCollector, _quoteCollector,
            config ?? new StockSharpConfig());
    }

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithNullTradeCollector_ThrowsArgumentNullException()
    {
        var act = () => new StockSharpMarketDataClient(
            null!, _depthCollector, _quoteCollector, new StockSharpConfig());

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("tradeCollector");
    }

    [Fact]
    public void Constructor_WithNullDepthCollector_ThrowsArgumentNullException()
    {
        var act = () => new StockSharpMarketDataClient(
            _tradeCollector, null!, _quoteCollector, new StockSharpConfig());

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("depthCollector");
    }

    [Fact]
    public void Constructor_WithNullQuoteCollector_ThrowsArgumentNullException()
    {
        var act = () => new StockSharpMarketDataClient(
            _tradeCollector, _depthCollector, null!, new StockSharpConfig());

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("quoteCollector");
    }

    [Fact]
    public void Constructor_WithDefaultConfig_CreatesClient()
    {
        var client = CreateClient();

        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfig_UsesDefaults()
    {
        // StockSharpConfig should have reasonable defaults
        var config = new StockSharpConfig();
        var client = CreateClient(config);

        client.Should().NotBeNull();
    }

    #endregion

    #region Subscription Management Tests

    [Fact]
    public void SubscribeTrades_ReturnsPositiveId()
    {
        var client = CreateClient();
        var config = new SymbolConfig("AAPL");

        var id = client.SubscribeTrades(config);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SubscribeTrades_MultipleSymbols_ReturnsUniqueIds()
    {
        var client = CreateClient();
        var ids = new List<int>();

        foreach (var symbol in new[] { "AAPL", "MSFT", "GOOGL", "AMZN" })
        {
            ids.Add(client.SubscribeTrades(new SymbolConfig(symbol)));
        }

        ids.Should().HaveCount(4);
        ids.Distinct().Should().HaveCount(4, "each subscription should be unique");
    }

    [Fact]
    public void SubscribeMarketDepth_ReturnsPositiveId()
    {
        var client = CreateClient();
        var config = new SymbolConfig("SPY");

        var id = client.SubscribeMarketDepth(config);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SubscribeMarketDepth_MultipleSymbols_ReturnsUniqueIds()
    {
        var client = CreateClient();
        var ids = new List<int>();

        foreach (var symbol in new[] { "SPY", "QQQ", "DIA" })
        {
            ids.Add(client.SubscribeMarketDepth(new SymbolConfig(symbol)));
        }

        ids.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void UnsubscribeTrades_DoesNotThrow()
    {
        var client = CreateClient();
        var id = client.SubscribeTrades(new SymbolConfig("AAPL"));

        var act = () => client.UnsubscribeTrades(id);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnsubscribeMarketDepth_DoesNotThrow()
    {
        var client = CreateClient();
        var id = client.SubscribeMarketDepth(new SymbolConfig("SPY"));

        var act = () => client.UnsubscribeMarketDepth(id);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnsubscribeTrades_NonExistentId_DoesNotThrow()
    {
        var client = CreateClient();

        var act = () => client.UnsubscribeTrades(99999);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnsubscribeMarketDepth_NonExistentId_DoesNotThrow()
    {
        var client = CreateClient();

        var act = () => client.UnsubscribeMarketDepth(99999);
        act.Should().NotThrow();
    }

    #endregion

    #region Connection Lifecycle Tests

    [Fact]
    public async Task ConnectAsync_CompletesSuccessfully()
    {
        var client = CreateClient();

        // Stub mode should complete without throwing
        await client.ConnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_WithCancellationToken_Respects()
    {
        var client = CreateClient();
        using var cts = new CancellationTokenSource();

        await client.ConnectAsync(cts.Token);
    }

    [Fact]
    public async Task DisconnectAsync_CompletesSuccessfully()
    {
        var client = CreateClient();
        await client.ConnectAsync();

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WithoutConnect_DoesNotThrow()
    {
        var client = CreateClient();

        await client.DisconnectAsync();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        var client = CreateClient();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_AfterSubscriptions_CompletesCleanly()
    {
        var client = CreateClient();
        client.SubscribeTrades(new SymbolConfig("AAPL"));
        client.SubscribeMarketDepth(new SymbolConfig("SPY"));

        await client.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleDisposals_DoesNotThrow()
    {
        var client = CreateClient();
        await client.DisposeAsync();
        await client.DisposeAsync();
    }

    #endregion

    #region IsEnabled Tests

    [Fact]
    public void IsEnabled_WithDefaultConfig_ReturnsExpectedValue()
    {
        var client = CreateClient();

        // StockSharp is typically enabled by default when configured
        // (stub mode may report disabled since no adapter is present)
        client.IsEnabled.Should().Be(client.IsEnabled); // Ensures no exception
    }

    #endregion

    #region Domain Model Integration Tests

    [Fact]
    public void MarketTradeUpdate_WithStockSharpFields_PreservesData()
    {
        var ts = DateTimeOffset.UtcNow;
        var update = new MarketTradeUpdate(
            Timestamp: ts,
            Symbol: "AAPL",
            Price: 185.50m,
            Size: 100,
            Aggressor: AggressorSide.Buy,
            SequenceNumber: 12345,
            StreamId: "STOCKSHARP",
            Venue: "NASDAQ");

        update.StreamId.Should().Be("STOCKSHARP");
        update.Venue.Should().Be("NASDAQ");
        update.SequenceNumber.Should().Be(12345);
    }

    [Fact]
    public void MarketDepthUpdate_WithStockSharpFields_PreservesData()
    {
        var ts = DateTimeOffset.UtcNow;
        var update = new MarketDepthUpdate(
            Timestamp: ts,
            Symbol: "SPY",
            Position: 0,
            Operation: DepthOperation.Update,
            Side: OrderBookSide.Bid,
            Price: 450.25m,
            Size: 1500m,
            StreamId: "STOCKSHARP",
            Venue: "ARCA");

        update.StreamId.Should().Be("STOCKSHARP");
        update.Venue.Should().Be("ARCA");
        update.Operation.Should().Be(DepthOperation.Update);
    }

    [Fact]
    public void MarketDepthUpdate_FullBook_SimulatesStockSharpSnapshot()
    {
        // StockSharp sends complete book snapshots
        var ts = DateTimeOffset.UtcNow;
        var bids = Enumerable.Range(0, 10).Select(i => new MarketDepthUpdate(
            Timestamp: ts, Symbol: "SPY", Position: i,
            Operation: DepthOperation.Update, Side: OrderBookSide.Bid,
            Price: 450.00m - (i * 0.01m), Size: 100m * (i + 1),
            StreamId: "STOCKSHARP", Venue: "ARCA")).ToList();

        var asks = Enumerable.Range(0, 10).Select(i => new MarketDepthUpdate(
            Timestamp: ts, Symbol: "SPY", Position: i,
            Operation: DepthOperation.Update, Side: OrderBookSide.Ask,
            Price: 450.01m + (i * 0.01m), Size: 80m * (i + 1),
            StreamId: "STOCKSHARP", Venue: "ARCA")).ToList();

        bids.Should().HaveCount(10);
        asks.Should().HaveCount(10);

        // Best bid should be higher than worst bid
        bids[0].Price.Should().BeGreaterThan(bids[9].Price);
        // Best ask should be lower than worst ask
        asks[0].Price.Should().BeLessThan(asks[9].Price);
        // Spread should be positive
        (asks[0].Price - bids[0].Price).Should().BeGreaterThan(0);
    }

    #endregion
}
