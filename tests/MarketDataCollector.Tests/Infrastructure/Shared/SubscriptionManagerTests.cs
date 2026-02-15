using FluentAssertions;
using MarketDataCollector.Infrastructure.Shared;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Shared;

/// <summary>
/// Unit tests for SubscriptionManager — the thread-safe subscription tracking layer
/// used by streaming providers (Alpaca, Polygon, etc.).
/// Covers B3 tranche 2 — provider infrastructure tests.
/// </summary>
public sealed class SubscriptionManagerTests : IDisposable
{
    private SubscriptionManager? _manager;

    public void Dispose()
    {
        _manager?.Dispose();
    }

    #region Subscribe Tests

    [Fact]
    public void Subscribe_ValidSymbol_ReturnsPositiveId()
    {
        _manager = new SubscriptionManager();

        var id = _manager.Subscribe("SPY", "trades");

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Subscribe_MultipleSymbols_ReturnsUniqueIds()
    {
        _manager = new SubscriptionManager();

        var id1 = _manager.Subscribe("SPY", "trades");
        var id2 = _manager.Subscribe("AAPL", "trades");
        var id3 = _manager.Subscribe("MSFT", "quotes");

        new[] { id1, id2, id3 }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Subscribe_EmptySymbol_ReturnsNegativeOne()
    {
        _manager = new SubscriptionManager();

        var id = _manager.Subscribe("", "trades");

        id.Should().Be(-1);
    }

    [Fact]
    public void Subscribe_WhitespaceSymbol_ReturnsNegativeOne()
    {
        _manager = new SubscriptionManager();

        var id = _manager.Subscribe("   ", "trades");

        id.Should().Be(-1);
    }

    [Fact]
    public void Subscribe_IncrementsCount()
    {
        _manager = new SubscriptionManager();

        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "trades");

        _manager.Count.Should().Be(2);
        _manager.ActiveSubscriptionCount.Should().Be(2);
    }

    [Fact]
    public void Subscribe_CustomStartingId_StartsFromConfiguredValue()
    {
        _manager = new SubscriptionManager(startingId: 5000);

        var id = _manager.Subscribe("SPY", "trades");

        id.Should().BeGreaterThan(5000);
    }

    #endregion

    #region Unsubscribe Tests

    [Fact]
    public void Unsubscribe_ExistingSubscription_ReturnsSubscription()
    {
        _manager = new SubscriptionManager();
        var id = _manager.Subscribe("SPY", "trades");

        var removed = _manager.Unsubscribe(id);

        removed.Should().NotBeNull();
        removed!.Symbol.Should().Be("SPY");
        removed.Kind.Should().Be("trades");
    }

    [Fact]
    public void Unsubscribe_NonexistentId_ReturnsNull()
    {
        _manager = new SubscriptionManager();

        var removed = _manager.Unsubscribe(99999);

        removed.Should().BeNull();
    }

    [Fact]
    public void Unsubscribe_DecrementsCount()
    {
        _manager = new SubscriptionManager();
        var id = _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "trades");

        _manager.Unsubscribe(id);

        _manager.Count.Should().Be(1);
    }

    [Fact]
    public void Unsubscribe_RemovesFromSymbolsByKind()
    {
        _manager = new SubscriptionManager();
        var id = _manager.Subscribe("SPY", "trades");

        _manager.Unsubscribe(id);

        _manager.GetSymbolsByKind("trades").Should().NotContain("SPY");
    }

    [Fact]
    public void Unsubscribe_SameSymbolDifferentKinds_OnlyRemovesSpecificKind()
    {
        _manager = new SubscriptionManager();
        var tradeId = _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("SPY", "quotes");

        _manager.Unsubscribe(tradeId);

        _manager.GetSymbolsByKind("trades").Should().NotContain("SPY");
        _manager.GetSymbolsByKind("quotes").Should().Contain("SPY");
    }

    #endregion

    #region GetSymbolsByKind Tests

    [Fact]
    public void GetSymbolsByKind_ReturnsAllSymbolsForKind()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "trades");
        _manager.Subscribe("MSFT", "quotes");

        var tradeSymbols = _manager.GetSymbolsByKind("trades");
        var quoteSymbols = _manager.GetSymbolsByKind("quotes");

        tradeSymbols.Should().HaveCount(2);
        tradeSymbols.Should().Contain(new[] { "SPY", "AAPL" });
        quoteSymbols.Should().HaveCount(1);
        quoteSymbols.Should().Contain("MSFT");
    }

    [Fact]
    public void GetSymbolsByKind_UnknownKind_ReturnsEmpty()
    {
        _manager = new SubscriptionManager();

        var symbols = _manager.GetSymbolsByKind("unknown");

        symbols.Should().BeEmpty();
    }

    #endregion

    #region HasSubscription Tests

    [Fact]
    public void HasSubscription_ExistingSubscription_ReturnsTrue()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");

        _manager.HasSubscription("SPY", "trades").Should().BeTrue();
    }

    [Fact]
    public void HasSubscription_WrongKind_ReturnsFalse()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");

        _manager.HasSubscription("SPY", "quotes").Should().BeFalse();
    }

    [Fact]
    public void HasSubscription_AfterUnsubscribe_ReturnsFalse()
    {
        _manager = new SubscriptionManager();
        var id = _manager.Subscribe("SPY", "trades");
        _manager.Unsubscribe(id);

        _manager.HasSubscription("SPY", "trades").Should().BeFalse();
    }

    #endregion

    #region GetAllSubscriptions Tests

    [Fact]
    public void GetAllSubscriptions_ReturnsAllActive()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "quotes");

        var all = _manager.GetAllSubscriptions();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void GetAllSubscriptions_Empty_ReturnsEmpty()
    {
        _manager = new SubscriptionManager();

        var all = _manager.GetAllSubscriptions();

        all.Should().BeEmpty();
    }

    #endregion

    #region GetActiveKinds Tests

    [Fact]
    public void GetActiveKinds_ReturnsAllActiveKinds()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "quotes");
        _manager.Subscribe("MSFT", "depth");

        var kinds = _manager.GetActiveKinds();

        kinds.Should().HaveCount(3);
        kinds.Should().Contain(new[] { "trades", "quotes", "depth" });
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllSubscriptions()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "quotes");

        var cleared = _manager.Clear();

        cleared.Should().HaveCount(2);
        _manager.Count.Should().Be(0);
        _manager.GetAllSubscriptions().Should().BeEmpty();
    }

    #endregion

    #region GetSnapshot Tests

    [Fact]
    public void GetSnapshot_ReturnsCurrentState()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "trades");
        _manager.Subscribe("MSFT", "quotes");

        var snapshot = _manager.GetSnapshot();

        snapshot.TotalSubscriptions.Should().Be(3);
        snapshot.SymbolsByKind.Should().HaveCount(2);
        snapshot.SymbolsByKind["trades"].Should().HaveCount(2);
        snapshot.SymbolsByKind["quotes"].Should().HaveCount(1);
        snapshot.Subscriptions.Should().HaveCount(3);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ClearsAllSubscriptions()
    {
        _manager = new SubscriptionManager();
        _manager.Subscribe("SPY", "trades");
        _manager.Subscribe("AAPL", "trades");

        _manager.Dispose();

        _manager.Count.Should().Be(0);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        _manager = new SubscriptionManager();

        _manager.Dispose();
        var act = () => _manager.Dispose();
        act.Should().NotThrow();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ConcurrentSubscribeAndUnsubscribe_DoesNotThrow()
    {
        _manager = new SubscriptionManager();

        var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
        var subscribeTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var id = _manager.Subscribe($"SYM{i}", "trades");
                if (id > 0) ids.Add(id);
            }
        });

        var unsubscribeTask = Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                if (ids.TryTake(out var id))
                    _manager.Unsubscribe(id);
            }
        });

        var queryTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                _ = _manager.Count;
                _ = _manager.GetSymbolsByKind("trades");
            }
        });

        var act = () => Task.WhenAll(subscribeTask, unsubscribeTask, queryTask);
        act.Should().NotThrow();
    }

    #endregion
}
