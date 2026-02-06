using FluentAssertions;
using MarketDataCollector.ProviderSdk.Providers;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for the Provider SDK <see cref="ProviderCapabilities"/> record
/// and its factory methods.
/// </summary>
public sealed class ProviderCapabilitiesTests
{
    [Fact]
    public void None_ReturnsEmptyCapabilities()
    {
        // Act
        var caps = ProviderCapabilities.None;

        // Assert
        caps.SupportsStreaming.Should().BeFalse();
        caps.SupportsBackfill.Should().BeFalse();
        caps.SupportsSymbolSearch.Should().BeFalse();
        caps.SupportedMarkets.Should().ContainSingle().Which.Should().Be("US");
    }

    [Fact]
    public void BackfillBarsOnly_SetsCorrectFlags()
    {
        // Act
        var caps = ProviderCapabilities.BackfillBarsOnly;

        // Assert
        caps.SupportsBackfill.Should().BeTrue();
        caps.SupportsAdjustedPrices.Should().BeTrue();
        caps.SupportsDividends.Should().BeTrue();
        caps.SupportsSplits.Should().BeTrue();
        caps.SupportsStreaming.Should().BeFalse();
        caps.SupportsIntraday.Should().BeFalse();
    }

    [Fact]
    public void BackfillFullFeatured_SetsAllBackfillFlags()
    {
        // Act
        var caps = ProviderCapabilities.BackfillFullFeatured;

        // Assert
        caps.SupportsBackfill.Should().BeTrue();
        caps.SupportsAdjustedPrices.Should().BeTrue();
        caps.SupportsIntraday.Should().BeTrue();
        caps.SupportsHistoricalQuotes.Should().BeTrue();
        caps.SupportsHistoricalTrades.Should().BeTrue();
        caps.SupportsHistoricalAuctions.Should().BeTrue();
        caps.HasTickData.Should().BeTrue();
        caps.HasCorporateActions.Should().BeTrue();
    }

    [Fact]
    public void Streaming_SetsCorrectFlags()
    {
        // Act
        var caps = ProviderCapabilities.Streaming(trades: true, quotes: true, depth: true, maxDepthLevels: 10);

        // Assert
        caps.SupportsStreaming.Should().BeTrue();
        caps.SupportsRealtimeTrades.Should().BeTrue();
        caps.SupportsRealtimeQuotes.Should().BeTrue();
        caps.SupportsMarketDepth.Should().BeTrue();
        caps.MaxDepthLevels.Should().Be(10);
        caps.SupportsBackfill.Should().BeFalse();
    }

    [Fact]
    public void Hybrid_SetsStreamingAndBackfillFlags()
    {
        // Act
        var caps = ProviderCapabilities.Hybrid(depth: true, intraday: true);

        // Assert
        caps.SupportsStreaming.Should().BeTrue();
        caps.SupportsBackfill.Should().BeTrue();
        caps.SupportsMarketDepth.Should().BeTrue();
        caps.SupportsIntraday.Should().BeTrue();
    }

    [Fact]
    public void SymbolSearchOnly_SetsCorrectFlags()
    {
        // Act
        var caps = ProviderCapabilities.SymbolSearchOnly;

        // Assert
        caps.SupportsSymbolSearch.Should().BeTrue();
        caps.SupportsStreaming.Should().BeFalse();
        caps.SupportsBackfill.Should().BeFalse();
    }

    [Fact]
    public void SupportsMarket_CaseInsensitiveComparison()
    {
        // Arrange
        var caps = ProviderCapabilities.BackfillBarsOnly with
        {
            SupportedMarkets = new[] { "US", "UK", "EU" }
        };

        // Assert
        caps.SupportsMarket("US").Should().BeTrue();
        caps.SupportsMarket("us").Should().BeTrue();
        caps.SupportsMarket("Uk").Should().BeTrue();
        caps.SupportsMarket("APAC").Should().BeFalse();
    }

    [Fact]
    public void WithExpression_OverridesSupportedMarkets()
    {
        // Arrange
        var caps = ProviderCapabilities.BackfillBarsOnly with
        {
            SupportedMarkets = new[] { "US", "UK", "EU", "APAC" }
        };

        // Assert
        caps.SupportedMarkets.Should().HaveCount(4);
        caps.SupportsMarket("APAC").Should().BeTrue();
    }

    [Fact]
    public void RateLimitProperties_DefaultToNull()
    {
        // Act
        var caps = ProviderCapabilities.None;

        // Assert
        caps.MaxRequestsPerWindow.Should().BeNull();
        caps.RateLimitWindow.Should().BeNull();
        caps.MinRequestDelay.Should().BeNull();
    }

    [Fact]
    public void RateLimitProperties_CanBeSet()
    {
        // Arrange
        var caps = ProviderCapabilities.BackfillBarsOnly with
        {
            MaxRequestsPerWindow = 100,
            RateLimitWindow = TimeSpan.FromMinutes(1),
            MinRequestDelay = TimeSpan.FromMilliseconds(500)
        };

        // Assert
        caps.MaxRequestsPerWindow.Should().Be(100);
        caps.RateLimitWindow.Should().Be(TimeSpan.FromMinutes(1));
        caps.MinRequestDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }
}
