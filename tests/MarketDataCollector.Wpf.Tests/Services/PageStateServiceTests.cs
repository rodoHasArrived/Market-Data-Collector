using System.Collections.Generic;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Tests.Services;

/// <summary>
/// Tests for <see cref="PageStateService"/> — per-page filter and sort state management.
/// </summary>
public sealed class PageStateServiceTests
{
    // Helper: return the singleton and reset its in-memory state via a fresh session load
    private static PageStateService CreateService()
    {
        var svc = PageStateService.Instance;
        // Reset to empty by loading an empty session
        svc.LoadFromSession(new SessionState());
        return svc;
    }

    // ── Singleton ────────────────────────────────────────────────────

    [Fact]
    public void Instance_ShouldReturnSingleton()
    {
        var a = PageStateService.Instance;
        var b = PageStateService.Instance;
        a.Should().BeSameAs(b);
    }

    // ── GetFilter / SetFilter ────────────────────────────────────────

    [Fact]
    public void GetFilter_NoValueStored_ReturnsDefaultValue()
    {
        var svc = CreateService();
        svc.GetFilter("Symbols", "searchText").Should().BeNull();
        svc.GetFilter("Symbols", "searchText", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void SetFilter_ThenGetFilter_ReturnsStoredValue()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.GetFilter("Symbols", "searchText").Should().Be("SPY");
    }

    [Fact]
    public void SetFilter_NullValue_RemovesKey()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.SetFilter("Symbols", "searchText", null);
        svc.GetFilter("Symbols", "searchText").Should().BeNull();
    }

    [Fact]
    public void SetFilter_NullValue_WhenKeyNotPresent_DoesNotThrow()
    {
        var svc = CreateService();
        var act = () => svc.SetFilter("NonExistent", "key", null);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetFilter_IsCaseInsensitiveForPageTag()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "AAPL");
        svc.GetFilter("symbols", "searchText").Should().Be("AAPL");
        svc.GetFilter("SYMBOLS", "searchText").Should().Be("AAPL");
    }

    [Fact]
    public void DifferentPages_HaveIsolatedState()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.SetFilter("Backfill", "symbols", "AAPL");

        svc.GetFilter("Symbols", "searchText").Should().Be("SPY");
        svc.GetFilter("Backfill", "symbols").Should().Be("AAPL");
        svc.GetFilter("Symbols", "symbols").Should().BeNull();
        svc.GetFilter("Backfill", "searchText").Should().BeNull();
    }

    // ── ClearPageFilters ─────────────────────────────────────────────

    [Fact]
    public void ClearPageFilters_RemovesAllFiltersForPage()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.SetFilter("Symbols", "filterTag", "Trades");
        svc.SetFilter("Backfill", "symbols", "AAPL");

        svc.ClearPageFilters("Symbols");

        svc.GetFilter("Symbols", "searchText").Should().BeNull();
        svc.GetFilter("Symbols", "filterTag").Should().BeNull();
        // Other pages should not be affected
        svc.GetFilter("Backfill", "symbols").Should().Be("AAPL");
    }

    [Fact]
    public void ClearPageFilters_NonExistentPage_DoesNotThrow()
    {
        var svc = CreateService();
        var act = () => svc.ClearPageFilters("DoesNotExist");
        act.Should().NotThrow();
    }

    // ── GetAllFiltersFlat ────────────────────────────────────────────

    [Fact]
    public void GetAllFiltersFlat_Empty_ReturnsEmptyDictionary()
    {
        var svc = CreateService();
        svc.GetAllFiltersFlat().Should().BeEmpty();
    }

    [Fact]
    public void GetAllFiltersFlat_ReturnsKeyedByPageAndKey()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.SetFilter("Backfill", "granularity", "Daily");

        var flat = svc.GetAllFiltersFlat();

        flat.Should().ContainKey("Symbols:searchText").WhoseValue.Should().Be("SPY");
        flat.Should().ContainKey("Backfill:granularity").WhoseValue.Should().Be("Daily");
        flat.Should().HaveCount(2);
    }

    [Fact]
    public void GetAllFiltersFlat_ExcludesRemovedKeys()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.SetFilter("Symbols", "searchText", null); // remove

        svc.GetAllFiltersFlat().Should().BeEmpty();
    }

    // ── LoadFromSession ──────────────────────────────────────────────

    [Fact]
    public void LoadFromSession_PopulatesFiltersFromFlatDictionary()
    {
        var svc = CreateService();
        var session = new SessionState
        {
            ActiveFilters = new Dictionary<string, string>
            {
                ["Symbols:searchText"] = "MSFT",
                ["Symbols:filterTag"] = "Trades",
                ["Backfill:granularity"] = "1Min"
            }
        };

        svc.LoadFromSession(session);

        svc.GetFilter("Symbols", "searchText").Should().Be("MSFT");
        svc.GetFilter("Symbols", "filterTag").Should().Be("Trades");
        svc.GetFilter("Backfill", "granularity").Should().Be("1Min");
    }

    [Fact]
    public void LoadFromSession_ClearsExistingState()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");

        svc.LoadFromSession(new SessionState
        {
            ActiveFilters = new Dictionary<string, string> { ["Backfill:granularity"] = "Daily" }
        });

        // Previously stored filter should be gone
        svc.GetFilter("Symbols", "searchText").Should().BeNull();
        svc.GetFilter("Backfill", "granularity").Should().Be("Daily");
    }

    [Fact]
    public void LoadFromSession_IgnoresEntriesWithoutColon()
    {
        var svc = CreateService();
        var session = new SessionState
        {
            ActiveFilters = new Dictionary<string, string>
            {
                ["noColonKey"] = "value",      // legacy / malformed — should be silently ignored
                ["Symbols:searchText"] = "SPY"
            }
        };

        svc.LoadFromSession(session);

        svc.GetFilter("Symbols", "searchText").Should().Be("SPY");
    }

    // ── Round-trip: save → load ──────────────────────────────────────

    [Fact]
    public void RoundTrip_GetAllFiltersFlat_ThenLoadFromSession_RestoresState()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "NVDA");
        svc.SetFilter("Symbols", "exchangeTag", "NASDAQ");
        svc.SetFilter("Backfill", "primaryProvider", "yahoo");

        var flat = svc.GetAllFiltersFlat();

        // Simulate a fresh launch
        svc.LoadFromSession(new SessionState { ActiveFilters = flat });

        svc.GetFilter("Symbols", "searchText").Should().Be("NVDA");
        svc.GetFilter("Symbols", "exchangeTag").Should().Be("NASDAQ");
        svc.GetFilter("Backfill", "primaryProvider").Should().Be("yahoo");
    }

    // ── GetPageKeys ──────────────────────────────────────────────────

    [Fact]
    public void GetPageKeys_ReturnsStoredKeys()
    {
        var svc = CreateService();
        svc.SetFilter("Symbols", "searchText", "SPY");
        svc.SetFilter("Symbols", "filterTag", "Trades");

        svc.GetPageKeys("Symbols").Should().BeEquivalentTo(new[] { "searchText", "filterTag" });
    }

    [Fact]
    public void GetPageKeys_NonExistentPage_ReturnsEmpty()
    {
        var svc = CreateService();
        svc.GetPageKeys("NoSuchPage").Should().BeEmpty();
    }
}
