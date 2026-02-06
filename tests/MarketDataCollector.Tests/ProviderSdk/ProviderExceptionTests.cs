using FluentAssertions;
using MarketDataCollector.ProviderSdk.Exceptions;
using Xunit;

namespace MarketDataCollector.Tests.ProviderSdk;

/// <summary>
/// Tests for the SDK exception hierarchy.
/// </summary>
public sealed class ProviderExceptionTests
{
    [Fact]
    public void ProviderException_StoresProviderIdAndSymbol()
    {
        // Arrange & Act
        var ex = new ProviderException("test error", providerId: "stooq", symbol: "AAPL");

        // Assert
        ex.Message.Should().Be("test error");
        ex.ProviderId.Should().Be("stooq");
        ex.Symbol.Should().Be("AAPL");
    }

    [Fact]
    public void ProviderRateLimitException_StoresRetryAfter()
    {
        // Arrange
        var retryAfter = TimeSpan.FromSeconds(30);

        // Act
        var ex = new ProviderRateLimitException(
            "rate limit exceeded",
            providerId: "yahoo",
            symbol: "SPY",
            retryAfter: retryAfter);

        // Assert
        ex.RetryAfter.Should().Be(retryAfter);
        ex.ProviderId.Should().Be("yahoo");
        ex.Symbol.Should().Be("SPY");
        ex.Should().BeAssignableTo<ProviderException>();
    }

    [Fact]
    public void ProviderConnectionException_StoresHost()
    {
        // Act
        var ex = new ProviderConnectionException(
            "connection failed",
            providerId: "tiingo",
            host: "api.tiingo.com");

        // Assert
        ex.Host.Should().Be("api.tiingo.com");
        ex.ProviderId.Should().Be("tiingo");
        ex.Should().BeAssignableTo<ProviderException>();
    }

    [Fact]
    public void ProviderException_WrapsInnerException()
    {
        // Arrange
        var inner = new HttpRequestException("network error");

        // Act
        var ex = new ProviderException("request failed", inner);

        // Assert
        ex.InnerException.Should().BeSameAs(inner);
    }
}
