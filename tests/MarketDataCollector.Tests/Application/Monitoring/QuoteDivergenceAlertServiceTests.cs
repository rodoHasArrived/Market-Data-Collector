using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using Xunit;

namespace MarketDataCollector.Tests.Application.Monitoring;

/// <summary>
/// Tests for QuoteDivergenceAlertService (improvement 4.2).
/// Verifies cross-provider mid-price divergence detection and resolution.
/// </summary>
public sealed class QuoteDivergenceAlertServiceTests : IAsyncDisposable
{
    private readonly QuoteDivergenceAlertService _sut;

    public QuoteDivergenceAlertServiceTests()
    {
        _sut = new QuoteDivergenceAlertService(new QuoteDivergenceConfig
        {
            DivergenceThresholdBps = 10m,
            ComparisonWindowSeconds = 60
        });
    }

    public async ValueTask DisposeAsync() => await _sut.DisposeAsync();

    [Fact]
    public void RecordQuote_SingleProvider_NoDivergenceDetected()
    {
        _sut.RecordQuote("AAPL", "Alpaca", 185.00m, 185.10m, DateTimeOffset.UtcNow);

        var divergences = _sut.GetActiveDivergences();
        divergences.Should().BeEmpty("a single provider cannot diverge from itself");
    }

    [Fact]
    public void RecordQuote_TwoProviders_ClosePrices_NoDivergence()
    {
        var now = DateTimeOffset.UtcNow;
        _sut.RecordQuote("AAPL", "Alpaca", 185.00m, 185.10m, now);
        _sut.RecordQuote("AAPL", "Polygon", 185.01m, 185.09m, now);

        var divergences = _sut.GetActiveDivergences();
        divergences.Should().BeEmpty("prices are within the 10 bps threshold");
    }

    [Fact]
    public void RecordQuote_TwoProviders_LargeDivergence_DetectsDivergence()
    {
        QuoteDivergenceEvent? captured = null;
        _sut.OnDivergenceDetected += e => captured = e;

        var now = DateTimeOffset.UtcNow;
        // Provider 1: mid = 185.05
        _sut.RecordQuote("AAPL", "Alpaca", 185.00m, 185.10m, now);
        // Provider 2: mid = 186.05 (100 bps divergence)
        _sut.RecordQuote("AAPL", "Polygon", 186.00m, 186.10m, now);

        var divergences = _sut.GetActiveDivergences();
        divergences.Should().HaveCount(1);
        divergences[0].Symbol.Should().Be("AAPL");
        divergences[0].DivergenceBps.Should().BeGreaterThan(10m);

        captured.Should().NotBeNull();
        captured!.Symbol.Should().Be("AAPL");
        captured.Providers.Should().Contain("Alpaca").And.Contain("Polygon");
    }

    [Fact]
    public void RecordQuote_DivergenceResolves_FiresResolvedEvent()
    {
        QuoteDivergenceResolvedEvent? resolved = null;
        _sut.OnDivergenceResolved += e => resolved = e;

        var now = DateTimeOffset.UtcNow;

        // Create divergence
        _sut.RecordQuote("SPY", "Alpaca", 500.00m, 500.10m, now);
        _sut.RecordQuote("SPY", "Polygon", 502.00m, 502.10m, now);

        _sut.GetActiveDivergences().Should().HaveCount(1);

        // Resolve divergence by converging prices
        _sut.RecordQuote("SPY", "Alpaca", 501.00m, 501.10m, now);
        _sut.RecordQuote("SPY", "Polygon", 501.02m, 501.08m, now);

        _sut.GetActiveDivergences().Should().BeEmpty();
        resolved.Should().NotBeNull();
        resolved!.Symbol.Should().Be("SPY");
    }

    [Fact]
    public void RecordQuote_InvalidPrices_Ignored()
    {
        _sut.RecordQuote("AAPL", "Alpaca", 0m, 185.10m, DateTimeOffset.UtcNow);
        _sut.RecordQuote("AAPL", "Polygon", -1m, 185.10m, DateTimeOffset.UtcNow);

        _sut.GetActiveDivergences().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveDivergences_EmptyByDefault()
    {
        _sut.GetActiveDivergences().Should().BeEmpty();
    }
}
