using FluentAssertions;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Services;
using Xunit;

namespace MarketDataCollector.Tests.Application.Services;

/// <summary>
/// Tests for ConnectivityTestService focusing on result construction,
/// summary generation, and provider test configuration.
/// </summary>
public sealed class ConnectivityTestServiceTests : IAsyncDisposable
{
    private readonly ConnectivityTestService _sut;

    public ConnectivityTestServiceTests()
    {
        _sut = new ConnectivityTestService(new AppConfig());
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    #region ConnectivityTestResult

    [Fact]
    public void ConnectivityTestResult_Construction_SetsAllProperties()
    {
        var result = new ConnectivityTestResult(
            Provider: "TestProvider",
            IsReachable: true,
            ResponseTime: TimeSpan.FromMilliseconds(150),
            ErrorMessage: null,
            Suggestion: null);

        result.Provider.Should().Be("TestProvider");
        result.IsReachable.Should().BeTrue();
        result.ResponseTime.Should().Be(TimeSpan.FromMilliseconds(150));
        result.ErrorMessage.Should().BeNull();
        result.Suggestion.Should().BeNull();
    }

    [Fact]
    public void ConnectivityTestResult_WithError_HasErrorDetails()
    {
        var result = new ConnectivityTestResult(
            Provider: "FailedProvider",
            IsReachable: false,
            ResponseTime: TimeSpan.Zero,
            ErrorMessage: "Connection refused",
            Suggestion: "Check if the service is running");

        result.IsReachable.Should().BeFalse();
        result.ErrorMessage.Should().Be("Connection refused");
        result.Suggestion.Should().NotBeNull();
    }

    #endregion

    #region ConnectivitySummary

    [Fact]
    public void ConnectivitySummary_AllReachable_ReturnsTrue()
    {
        var results = new[]
        {
            new ConnectivityTestResult("Provider1", true, TimeSpan.FromMilliseconds(100), null, null),
            new ConnectivityTestResult("Provider2", true, TimeSpan.FromMilliseconds(200), null, null)
        };

        var summary = new ConnectivitySummary(results, true, Array.Empty<string>());

        summary.AllReachable.Should().BeTrue();
        summary.Results.Should().HaveCount(2);
        summary.NetworkIssues.Should().BeEmpty();
    }

    [Fact]
    public void ConnectivitySummary_WithFailures_ReportsNotAllReachable()
    {
        var results = new[]
        {
            new ConnectivityTestResult("Provider1", true, TimeSpan.FromMilliseconds(100), null, null),
            new ConnectivityTestResult("Provider2", false, TimeSpan.Zero, "Timeout", "Check network")
        };

        var summary = new ConnectivitySummary(results, false, new[] { "DNS resolution failed" });

        summary.AllReachable.Should().BeFalse();
        summary.NetworkIssues.Should().NotBeEmpty();
        summary.NetworkIssues.Should().Contain("DNS resolution failed");
    }

    [Fact]
    public void ConnectivitySummary_EmptyResults_HasNoIssues()
    {
        var summary = new ConnectivitySummary(
            Array.Empty<ConnectivityTestResult>(),
            true,
            Array.Empty<string>());

        summary.Results.Should().BeEmpty();
        summary.AllReachable.Should().BeTrue();
    }

    #endregion

    #region Service Construction

    [Fact]
    public void Constructor_WithDefaultConfig_DoesNotThrow()
    {
        var config = new AppConfig();

        var action = () => new ConnectivityTestService(config);

        action.Should().NotThrow("construction with default config should succeed");
    }

    [Fact]
    public void Constructor_WithAlpacaConfig_DoesNotThrow()
    {
        var alpacaConfig = new AlpacaOptions(
            KeyId: "test-key",
            SecretKey: "test-secret");
        var config = new AppConfig(Alpaca: alpacaConfig);

        var action = () => new ConnectivityTestService(config);

        action.Should().NotThrow("construction with Alpaca config should succeed");
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        var service = new ConnectivityTestService(new AppConfig());

        await service.DisposeAsync();
        var action = async () => await service.DisposeAsync();

        await action.Should().NotThrowAsync("double dispose should be safe");
    }

    #endregion
}
