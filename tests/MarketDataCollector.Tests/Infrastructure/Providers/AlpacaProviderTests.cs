using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

/// <summary>
/// Unit tests for the Alpaca provider family — both historical (AlpacaHistoricalDataProvider)
/// and credential/configuration validation. Covers B3 tranche 2 from the project roadmap.
/// </summary>
public sealed class AlpacaHistoricalProviderTests : IDisposable
{
    private AlpacaHistoricalDataProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
    }

    #region Constructor and Metadata Tests

    [Fact]
    public void Constructor_WithExplicitCredentials_CreatesInstance()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret");

        _provider.Should().NotBeNull();
        _provider.Name.Should().Be("alpaca");
        _provider.DisplayName.Should().Be("Alpaca Markets");
    }

    [Fact]
    public void Name_ReturnsAlpaca()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.Name.Should().Be("alpaca");
    }

    [Fact]
    public void DisplayName_ReturnsAlpacaMarkets()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.DisplayName.Should().Be("Alpaca Markets");
    }

    [Fact]
    public void Description_IsNonEmpty()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Priority_DefaultsTo5()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.Priority.Should().Be(5);
    }

    [Fact]
    public void Priority_UsesCustomValue()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret",
            priority: 15);

        _provider.Priority.Should().Be(15);
    }

    [Fact]
    public void RateLimitDelay_IsPositive()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.RateLimitDelay.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void MaxRequestsPerWindow_DefaultsTo200()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.MaxRequestsPerWindow.Should().Be(200);
    }

    [Fact]
    public void MaxRequestsPerWindow_UsesCustomValue()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret",
            rateLimitPerMinute: 100);

        _provider.MaxRequestsPerWindow.Should().Be(100);
    }

    [Fact]
    public void RateLimitWindow_Is1Minute()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.RateLimitWindow.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Capabilities_IsFullFeatured()
    {
        _provider = CreateProviderWithTestCredentials();
        var caps = _provider.Capabilities;

        caps.Should().NotBeNull();
        caps.SupportsAdjustedPrices.Should().BeTrue();
        caps.SupportsIntraday.Should().BeTrue();
    }

    #endregion

    #region Feed Validation Tests

    [Theory]
    [InlineData("iex")]
    [InlineData("sip")]
    [InlineData("delayed_sip")]
    public void Constructor_WithValidFeed_DoesNotThrow(string feed)
    {
        var act = () => new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret",
            feed: feed);

        act.Should().NotThrow();
        act().Dispose();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("otc")]
    public void Constructor_WithInvalidFeed_ThrowsArgumentException(string feed)
    {
        var act = () => new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret",
            feed: feed);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Adjustment Validation Tests

    [Theory]
    [InlineData("raw")]
    [InlineData("split")]
    [InlineData("dividend")]
    [InlineData("all")]
    public void Constructor_WithValidAdjustment_DoesNotThrow(string adjustment)
    {
        var act = () => new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret",
            adjustment: adjustment);

        act.Should().NotThrow();
        act().Dispose();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("none")]
    public void Constructor_WithInvalidAdjustment_ThrowsArgumentException(string adjustment)
    {
        var act = () => new AlpacaHistoricalDataProvider(
            keyId: "test-key",
            secretKey: "test-secret",
            adjustment: adjustment);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Credential Validation Tests

    [Fact]
    public async Task IsAvailableAsync_WithoutCredentials_ReturnsFalse()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: null,
            secretKey: null);

        var available = await _provider.IsAvailableAsync();

        available.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WithEmptyCredentials_ReturnsFalse()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: "",
            secretKey: "");

        var available = await _provider.IsAvailableAsync();

        available.Should().BeFalse();
    }

    [Fact]
    public async Task GetDailyBarsAsync_WithoutCredentials_ThrowsInvalidOperationException()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: null,
            secretKey: null);

        var act = () => _provider.GetDailyBarsAsync("SPY", null, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public async Task GetAdjustedDailyBarsAsync_WithoutCredentials_ThrowsInvalidOperationException()
    {
        _provider = new AlpacaHistoricalDataProvider(
            keyId: null,
            secretKey: null);

        var act = () => _provider.GetAdjustedDailyBarsAsync("SPY", null, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    #endregion

    #region Symbol Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetDailyBarsAsync_WithEmptySymbol_ThrowsArgumentException(string symbol)
    {
        _provider = CreateProviderWithTestCredentials();

        var act = () => _provider.GetDailyBarsAsync(symbol, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        _provider = CreateProviderWithTestCredentials();

        _provider.Dispose();

        var act = () => _provider.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetDailyBarsAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        _provider = CreateProviderWithTestCredentials();
        _provider.Dispose();

        var act = () => _provider.GetDailyBarsAsync("SPY", null, null);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Helpers

    private static AlpacaHistoricalDataProvider CreateProviderWithTestCredentials()
    {
        return new AlpacaHistoricalDataProvider(
            keyId: "test-key-id",
            secretKey: "test-secret-key");
    }

    #endregion
}
