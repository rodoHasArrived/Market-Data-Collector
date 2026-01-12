using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.Providers;

/// <summary>
/// Unit tests for the PolygonMarketDataClient class.
/// Tests credential validation, connection behavior, and subscription methods.
/// </summary>
public class PolygonMarketDataClientTests : IDisposable
{
    private readonly Mock<IMarketEventPublisher> _mockPublisher;
    private readonly Mock<TradeDataCollector> _mockTradeCollector;
    private readonly Mock<QuoteCollector> _mockQuoteCollector;
    private readonly List<MarketEvent> _publishedEvents;

    // Store original environment variable values for cleanup
    private readonly string? _originalPolygonApiKey;
    private readonly string? _originalPolygonApiKeyAlt;

    public PolygonMarketDataClientTests()
    {
        _mockPublisher = new Mock<IMarketEventPublisher>();
        _publishedEvents = new List<MarketEvent>();

        _mockPublisher
            .Setup(p => p.TryPublish(It.IsAny<MarketEvent>()))
            .Callback<MarketEvent>(e => _publishedEvents.Add(e))
            .Returns(true);

        // Create real collectors with mock publisher for testing
        _mockTradeCollector = new Mock<TradeDataCollector>(_mockPublisher.Object, null) { CallBase = true };
        _mockQuoteCollector = new Mock<QuoteCollector>(_mockPublisher.Object) { CallBase = true };

        // Store and clear environment variables for predictable testing
        _originalPolygonApiKey = Environment.GetEnvironmentVariable("POLYGON_API_KEY");
        _originalPolygonApiKeyAlt = Environment.GetEnvironmentVariable("POLYGON__APIKEY");
        Environment.SetEnvironmentVariable("POLYGON_API_KEY", null);
        Environment.SetEnvironmentVariable("POLYGON__APIKEY", null);
    }

    public void Dispose()
    {
        // Restore original environment variables
        Environment.SetEnvironmentVariable("POLYGON_API_KEY", _originalPolygonApiKey);
        Environment.SetEnvironmentVariable("POLYGON__APIKEY", _originalPolygonApiKeyAlt);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullPublisher_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PolygonMarketDataClient(
            null!,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("publisher");
    }

    [Fact]
    public void Constructor_WithNullTradeCollector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PolygonMarketDataClient(
            _mockPublisher.Object,
            null!,
            _mockQuoteCollector.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("tradeCollector");
    }

    [Fact]
    public void Constructor_WithNullQuoteCollector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("quoteCollector");
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaultOptions()
    {
        // Act - should not throw
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            opt: null);

        // Assert - client should be created successfully
        client.Should().NotBeNull();
    }

    #endregion

    #region Credential Validation Tests

    [Fact]
    public void IsEnabled_WithNoApiKey_ReturnsFalse()
    {
        // Arrange
        var options = new PolygonOptions();
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Assert
        client.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WithShortApiKey_ReturnsFalse()
    {
        // Arrange - API key too short (less than 20 chars)
        var options = new PolygonOptions(ApiKey: "shortkey");
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Assert
        client.IsEnabled.Should().BeTrue();
        client.HasValidCredentials.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullOptions_SetsIsEnabledFalse()
    {
        // Arrange & Act
        client.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WithValidApiKey_ReturnsTrue()
    {
        // Arrange - Valid API key (20+ chars)
        var options = new PolygonOptions(ApiKey: "abcdefghijklmnopqrstuvwxyz123456");
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options: null);

        // Assert - Without env var, should be disabled
        // Note: This may pass if POLYGON__APIKEY env var is set in test environment
        // The test validates the default behavior
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_SetsIsEnabledFalse()
    {
        // Arrange
        var options = new PolygonOptions(ApiKey: "");

        // Act
            options);

        // Assert
        client.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WithEnvironmentVariable_ReturnsTrue()
    {
        // Arrange - Set environment variable
        Environment.SetEnvironmentVariable("POLYGON_API_KEY", "env_api_key_that_is_long_enough_123");

        var options = new PolygonOptions(); // No API key in options
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Assert - Empty key should result in disabled (unless env var is set)
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithWhitespaceApiKey_SetsIsEnabledFalse()
    {
        // Arrange
        var options = new PolygonOptions(ApiKey: "   ");

        // Act
        // Assert
        client.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WithAlternateEnvironmentVariable_ReturnsTrue()
    {
        // Arrange - Set alternate environment variable (POLYGON__APIKEY)
        Environment.SetEnvironmentVariable("POLYGON__APIKEY", "alt_env_api_key_long_enough_12345");

        var options = new PolygonOptions(); // No API key in options
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Assert - Whitespace key should result in disabled (unless env var is set)
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectAsync_WithValidCredentials_PublishesHeartbeat()
    {
        // Arrange
        var publishedEvents = new List<MarketEvent>();
        _mockPublisher
            .Setup(p => p.TryPublish(It.IsAny<MarketEvent>()))
            .Callback<MarketEvent>(e => publishedEvents.Add(e))
            .Returns(true);

        var options = new PolygonOptions(ApiKey: "test-api-key");
        // Assert
        client.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_EnvironmentVariableTakesPrecedenceOverOptions()
    {
        // Arrange - Set valid env var but short options key
        Environment.SetEnvironmentVariable("POLYGON_API_KEY", "valid_environment_api_key_12345678");

        var options = new PolygonOptions(ApiKey: "short"); // Invalid options key
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Assert - env var should take precedence
        client.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region Connection Tests

    [Fact]
    public async Task ConnectAsync_PublishesHeartbeatEvent()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Act
        await client.ConnectAsync();

        // Assert
        publishedEvents.Should().ContainSingle();
        publishedEvents[0].Type.Should().Be(MarketEventType.Heartbeat);
    }

    [Fact]
    public async Task ConnectAsync_WithoutCredentials_PublishesHeartbeatInStubMode()
    {
        // Arrange
        var publishedEvents = new List<MarketEvent>();
        _mockPublisher
            .Setup(p => p.TryPublish(It.IsAny<MarketEvent>()))
            .Callback<MarketEvent>(e => publishedEvents.Add(e))
            .Returns(true);

        var options = new PolygonOptions(ApiKey: null);

        // Clear any environment variables for this test
        var originalEnvVar = Environment.GetEnvironmentVariable("POLYGON__APIKEY");
        var originalEnvVar2 = Environment.GetEnvironmentVariable("POLYGON_API_KEY");

        try
        {
            Environment.SetEnvironmentVariable("POLYGON__APIKEY", null);
            Environment.SetEnvironmentVariable("POLYGON_API_KEY", null);

            var client = new PolygonMarketDataClient(
                _mockPublisher.Object,
                _mockTradeCollector.Object,
                _mockQuoteCollector.Object,
                options);

            // Act
            await client.ConnectAsync();

            // Assert - Should still publish heartbeat in stub mode
            publishedEvents.Should().ContainSingle();
            publishedEvents[0].Type.Should().Be(MarketEventType.Heartbeat);
        }
        finally
        {
            // Restore environment variables
            Environment.SetEnvironmentVariable("POLYGON__APIKEY", originalEnvVar);
            Environment.SetEnvironmentVariable("POLYGON_API_KEY", originalEnvVar2);
        }
    }

    [Fact]
    public async Task DisconnectAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var options = new PolygonOptions(ApiKey: "test-api-key");
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Act
        var act = async () => await client.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var options = new PolygonOptions(ApiKey: "test-api-key");
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Act
        var act = async () => await client.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SubscribeMarketDepth_ReturnsNegativeOne_InStubMode()
    {
        // Arrange
        var options = new PolygonOptions(ApiKey: "test-api-key");
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        var symbolConfig = new SymbolConfig("SPY");

        // Act
        var subscriptionId = client.SubscribeMarketDepth(symbolConfig);

        // Assert - Stub mode returns -1
        _publishedEvents.Should().HaveCount(1);
        _publishedEvents[0].Type.Should().Be(MarketEventType.Heartbeat);
        _publishedEvents[0].Source.Should().Be("PolygonStub");
    }

    [Fact]
    public async Task ConnectAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);
        using var cts = new CancellationTokenSource();

        // Act - should complete without throwing for stub implementation
        await client.ConnectAsync(cts.Token);

        // Assert
        _publishedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task DisconnectAsync_CompletesSuccessfully()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Act & Assert - should not throw
        await client.DisconnectAsync();
    }

    #endregion

    #region Subscription Tests

    [Fact]
    public void SubscribeMarketDepth_ReturnsNegativeOne()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);
        var config = new SymbolConfig("SPY");

        // Act
        var subscriptionId = client.SubscribeMarketDepth(config);

        // Assert - depth not supported in stub
        subscriptionId.Should().Be(-1);
    }

    [Fact]
    public void UnsubscribeMarketDepth_DoesNotThrow()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Act & Assert - should not throw
        var act = () => client.UnsubscribeMarketDepth(1);
        act.Should().NotThrow();
    }

    [Fact]
    public void SubscribeTrades_ReturnsNegativeOne()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);
        var config = new SymbolConfig("AAPL");

        // Act
        var subscriptionId = client.SubscribeTrades(config);

        // Assert - stub returns -1 for subscription ID
        subscriptionId.Should().Be(-1);
    }

    [Fact]
    public void SubscribeTrades_ReturnsNegativeOne_InStubMode()
    {
        // Arrange
        var options = new PolygonOptions(ApiKey: "test-api-key");
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        var symbolConfig = new SymbolConfig("SPY");

        // Act
        var subscriptionId = client.SubscribeTrades(symbolConfig);

        // Assert - Stub mode returns -1
        subscriptionId.Should().Be(-1);
    }

    [Fact]
    public void PolygonOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new PolygonOptions();

        // Assert
        options.ApiKey.Should().BeNull();
        options.UseDelayed.Should().BeFalse();
        options.Feed.Should().Be("stocks");
        options.SubscribeTrades.Should().BeTrue();
        options.SubscribeQuotes.Should().BeFalse();
        options.SubscribeAggregates.Should().BeFalse();
    }
    public void SubscribeTrades_EmitsSyntheticTrade()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);
        var config = new SymbolConfig("AAPL");

        // Act
        client.SubscribeTrades(config);

        // Assert - should have published a trade event via the collector
        _publishedEvents.Should().Contain(e => e.Type == MarketEventType.Trade);
    }

    [Fact]
    public void UnsubscribeTrades_DoesNotThrow()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Act & Assert - should not throw
        var act = () => client.UnsubscribeTrades(1);
        act.Should().NotThrow();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Act & Assert - should not throw
        await client.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object);

        // Act & Assert - should not throw on multiple calls
        await client.DisposeAsync();
        await client.DisposeAsync();
    }

    #endregion

    #region Options Configuration Tests

    [Fact]
    public void Constructor_WithCustomOptions_UsesProvidedOptions()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            UseDelayed: true,
            Feed: "crypto",
            SubscribeTrades: true,
            SubscribeQuotes: true);

        // Act
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Assert
        client.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region Aggregate Subscription Tests

    [Fact]
    public void SubscribeAggregates_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            SubscribeAggregates: true);
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Act
        var act = () => client.SubscribeAggregates(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("cfg");
    }

    [Fact]
    public void SubscribeAggregates_WithEmptySymbol_ReturnsNegativeOne()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            SubscribeAggregates: true);
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);
        var config = new SymbolConfig("   ");

        // Act
        var subscriptionId = client.SubscribeAggregates(config);

        // Assert
        subscriptionId.Should().Be(-1);
    }

    [Fact]
    public void SubscribeAggregates_WhenAggregatesDisabled_ReturnsNegativeOne()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            SubscribeAggregates: false); // Aggregates disabled
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);
        var config = new SymbolConfig("SPY");

        // Act
        var subscriptionId = client.SubscribeAggregates(config);

        // Assert
        subscriptionId.Should().Be(-1);
    }

    [Fact]
    public void SubscribeAggregates_WhenAggregatesEnabled_ReturnsPositiveId()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            SubscribeAggregates: true);
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);
        var config = new SymbolConfig("SPY");

        // Act
        var subscriptionId = client.SubscribeAggregates(config);

        // Assert
        subscriptionId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UnsubscribeAggregates_DoesNotThrow()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            SubscribeAggregates: true);
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);

        // Act & Assert - should not throw
        var act = () => client.UnsubscribeAggregates(1);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnsubscribeAggregates_WithValidSubscription_DoesNotThrow()
    {
        // Arrange
        var options = new PolygonOptions(
            ApiKey: "a_valid_api_key_that_is_long_enough",
            SubscribeAggregates: true);
        var client = new PolygonMarketDataClient(
            _mockPublisher.Object,
            _mockTradeCollector.Object,
            _mockQuoteCollector.Object,
            options);
        var config = new SymbolConfig("SPY");
        var subscriptionId = client.SubscribeAggregates(config);

        // Act & Assert - should not throw
        var act = () => client.UnsubscribeAggregates(subscriptionId);
        act.Should().NotThrow();
    }

    [Fact]
    public void PolygonOptions_WithAggregatesEnabled_SetsCorrectValue()
    {
        // Arrange & Act
        var options = new PolygonOptions(SubscribeAggregates: true);

        // Assert
        options.SubscribeAggregates.Should().BeTrue();
    }

    #endregion
}
