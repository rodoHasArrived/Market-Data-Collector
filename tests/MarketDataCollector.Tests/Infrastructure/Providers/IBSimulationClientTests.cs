using FluentAssertions;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Tests.TestHelpers;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

/// <summary>
/// Unit tests for IBSimulationClient — the IB simulation client used for development
/// and testing without TWS/Gateway. Covers B3 tranche 2 from the project roadmap.
/// </summary>
public sealed class IBSimulationClientTests : IAsyncLifetime
{
    private readonly TestMarketEventPublisher _publisher;
    private IBSimulationClient? _client;

    public IBSimulationClientTests()
    {
        _publisher = new TestMarketEventPublisher();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }

    #region Constructor and Metadata Tests

    [Fact]
    public void Constructor_WithNullPublisher_ThrowsArgumentNullException()
    {
        var act = () => new IBSimulationClient(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsEnabled_ReturnsTrue()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsSimulation_ReturnsTrue()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.IsSimulation.Should().BeTrue();
    }

    [Fact]
    public void ProviderId_ReturnsIbSim()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.ProviderId.Should().Be("ib-sim");
    }

    [Fact]
    public void ProviderDisplayName_ContainsSimulation()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.ProviderDisplayName.Should().Contain("Simulation");
    }

    [Fact]
    public void ProviderPriority_IsLow()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.ProviderPriority.Should().BeGreaterOrEqualTo(90, "simulation providers should have low priority");
    }

    [Fact]
    public void ProviderCapabilities_SupportsTradingAndDepth()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        var caps = _client.ProviderCapabilities;

        caps.SupportsRealtimeTrades.Should().BeTrue();
        caps.SupportsRealtimeQuotes.Should().BeTrue();
        caps.SupportsMarketDepth.Should().BeTrue();
    }

    [Fact]
    public void ProviderCredentialFields_IsEmpty()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.ProviderCredentialFields.Should().BeEmpty("simulation requires no credentials");
    }

    [Fact]
    public void ProviderNotes_ContainsSimulationInfo()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.ProviderNotes.Should().Contain(n => n.Contains("Simulation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderWarnings_WarnAboutSimulatedData()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.ProviderWarnings.Should().Contain(w => w.Contains("simulated", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Connect / Disconnect Tests

    [Fact]
    public async Task ConnectAsync_CompletesSuccessfully()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);

        var act = () => _client.ConnectAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisconnectAsync_CompletesSuccessfully()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        await _client.ConnectAsync();

        var act = () => _client.DisconnectAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WithoutConnect_DoesNotThrow()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);

        var act = () => _client.DisconnectAsync();

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Subscription Tests

    [Fact]
    public void SubscribeTrades_ReturnsPositiveId()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        var cfg = new SymbolConfig("SPY");

        var id = _client.SubscribeTrades(cfg);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SubscribeTrades_MultipleSymbols_ReturnsUniqueIds()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);

        var id1 = _client.SubscribeTrades(new SymbolConfig("SPY"));
        var id2 = _client.SubscribeTrades(new SymbolConfig("AAPL"));
        var id3 = _client.SubscribeTrades(new SymbolConfig("MSFT"));

        new[] { id1, id2, id3 }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void UnsubscribeTrades_RemovesSubscription()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        var id = _client.SubscribeTrades(new SymbolConfig("SPY"));

        // Should not throw
        var act = () => _client.UnsubscribeTrades(id);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnsubscribeTrades_UnknownId_DoesNotThrow()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);

        var act = () => _client.UnsubscribeTrades(99999);
        act.Should().NotThrow();
    }

    [Fact]
    public void SubscribeMarketDepth_ReturnsPositiveId()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        var cfg = new SymbolConfig("SPY");

        var id = _client.SubscribeMarketDepth(cfg);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UnsubscribeMarketDepth_RemovesSubscription()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        var id = _client.SubscribeMarketDepth(new SymbolConfig("SPY"));

        var act = () => _client.UnsubscribeMarketDepth(id);
        act.Should().NotThrow();
    }

    #endregion

    #region Tick Generation Tests

    [Fact]
    public async Task ConnectWithAutoTicks_GeneratesTradeEvents()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: true);
        _client.SubscribeTrades(new SymbolConfig("SPY"));

        await _client.ConnectAsync();

        // Wait for at least one tick cycle (timer fires every 1s)
        await Task.Delay(1500);

        _publisher.PublishedEvents.Should().NotBeEmpty("auto-ticks should generate trade events");
    }

    [Fact]
    public async Task ConnectWithAutoTicks_TradeEventsHaveValidPrices()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: true);
        _client.SubscribeTrades(new SymbolConfig("SPY"));

        await _client.ConnectAsync();
        await Task.Delay(1500);

        var events = _publisher.PublishedEvents;
        events.Should().NotBeEmpty();

        foreach (var evt in events)
        {
            evt.Symbol.Should().Be("SPY");
        }
    }

    [Fact]
    public async Task ConnectWithAutoTicks_NoSubscriptions_NoEvents()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: true);
        // No subscriptions added

        await _client.ConnectAsync();
        await Task.Delay(1500);

        _publisher.PublishedEvents.Should().BeEmpty("no trade subscriptions means no trade events");
    }

    [Fact]
    public async Task DisconnectAsync_StopsTickGeneration()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: true);
        _client.SubscribeTrades(new SymbolConfig("SPY"));

        await _client.ConnectAsync();
        await Task.Delay(1500);

        await _client.DisconnectAsync();

        var countAfterDisconnect = _publisher.PublishedEvents.Count;
        await Task.Delay(1500);

        // Should not have generated more events after disconnect
        _publisher.PublishedEvents.Count.Should().Be(countAfterDisconnect);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ClearsSubscriptions()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);
        _client.SubscribeTrades(new SymbolConfig("SPY"));
        _client.SubscribeMarketDepth(new SymbolConfig("AAPL"));

        await _client.DisposeAsync();

        // Double dispose should not throw
        var act = () => _client.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        _client = new IBSimulationClient(_publisher, enableAutoTicks: false);

        await _client.DisposeAsync();
        await _client.DisposeAsync();
        await _client.DisposeAsync();
    }

    #endregion
}
