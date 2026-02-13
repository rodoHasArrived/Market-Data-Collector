using FluentAssertions;
using MarketDataCollector.Application.Services;
using Xunit;

namespace MarketDataCollector.Tests.Application.Services;

/// <summary>
/// Tests for <see cref="ConnectivityTestService"/> — connectivity testing
/// for data providers. Focuses on record types, summary logic, and lifecycle.
/// </summary>
public sealed class ConnectivityTestServiceTests
{
    #region ConnectivityTestResult record

    [Fact]
    public void ConnectivityTestResult_ConstructsCorrectly()
    {
        var result = new ConnectivityTestService.ConnectivityTestResult(
            Provider: "Alpaca",
            IsReachable: true,
            ResponseTime: TimeSpan.FromMilliseconds(150),
            Error: null,
            Suggestion: null);

        result.Provider.Should().Be("Alpaca");
        result.IsReachable.Should().BeTrue();
        result.ResponseTime.Should().Be(TimeSpan.FromMilliseconds(150));
        result.Error.Should().BeNull();
        result.Suggestion.Should().BeNull();
    }

    [Fact]
    public void ConnectivityTestResult_WithError_CapturesDetails()
    {
        var result = new ConnectivityTestService.ConnectivityTestResult(
            Provider: "Polygon",
            IsReachable: false,
            ResponseTime: TimeSpan.FromSeconds(5),
            Error: "Connection timed out",
            Suggestion: "Check firewall settings");

        result.IsReachable.Should().BeFalse();
        result.Error.Should().Be("Connection timed out");
        result.Suggestion.Should().Be("Check firewall settings");
    }

    [Fact]
    public void ConnectivityTestResult_SupportsRecordEquality()
    {
        var result1 = new ConnectivityTestService.ConnectivityTestResult(
            "Test", true, TimeSpan.FromMilliseconds(100), null, null);
        var result2 = new ConnectivityTestService.ConnectivityTestResult(
            "Test", true, TimeSpan.FromMilliseconds(100), null, null);

        result1.Should().Be(result2);
    }

    [Fact]
    public void ConnectivityTestResult_DifferentProviders_AreNotEqual()
    {
        var result1 = new ConnectivityTestService.ConnectivityTestResult(
            "Provider1", true, TimeSpan.Zero, null, null);
        var result2 = new ConnectivityTestService.ConnectivityTestResult(
            "Provider2", true, TimeSpan.Zero, null, null);

        result1.Should().NotBe(result2);
    }

    #endregion

    #region ConnectivitySummary record

    [Fact]
    public void ConnectivitySummary_AllReachable_WhenAllProvidersSucceed()
    {
        var results = new List<ConnectivityTestService.ConnectivityTestResult>
        {
            new("Alpaca", true, TimeSpan.FromMilliseconds(100), null, null),
            new("Polygon", true, TimeSpan.FromMilliseconds(200), null, null),
        };

        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: results,
            AllReachable: results.All(r => r.IsReachable),
            NetworkIssues: Array.Empty<string>());

        summary.AllReachable.Should().BeTrue();
        summary.Results.Should().HaveCount(2);
        summary.NetworkIssues.Should().BeEmpty();
    }

    [Fact]
    public void ConnectivitySummary_NotAllReachable_WhenProviderFails()
    {
        var results = new List<ConnectivityTestService.ConnectivityTestResult>
        {
            new("Alpaca", true, TimeSpan.FromMilliseconds(100), null, null),
            new("Polygon", false, TimeSpan.FromSeconds(5), "Timeout", "Check network"),
        };

        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: results,
            AllReachable: false,
            NetworkIssues: new[] { "Some providers unreachable" });

        summary.AllReachable.Should().BeFalse();
        summary.NetworkIssues.Should().ContainSingle();
    }

    [Fact]
    public void ConnectivitySummary_EmptyResults()
    {
        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: Array.Empty<ConnectivityTestService.ConnectivityTestResult>(),
            AllReachable: true,
            NetworkIssues: Array.Empty<string>());

        summary.Results.Should().BeEmpty();
        summary.AllReachable.Should().BeTrue();
    }

    [Fact]
    public void ConnectivitySummary_WithNetworkIssues()
    {
        var issues = new[] { "No internet connectivity detected", "DNS resolution issues" };
        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: Array.Empty<ConnectivityTestService.ConnectivityTestResult>(),
            AllReachable: false,
            NetworkIssues: issues);

        summary.NetworkIssues.Should().HaveCount(2);
        summary.NetworkIssues.Should().Contain("No internet connectivity detected");
        summary.NetworkIssues.Should().Contain("DNS resolution issues");
    }

    #endregion

    #region DisplaySummary

    [Fact]
    public async Task DisplaySummary_WithAllReachable_DoesNotThrow()
    {
        await using var service = CreateService();
        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: new[]
            {
                new ConnectivityTestService.ConnectivityTestResult("Alpaca", true, TimeSpan.FromMilliseconds(50), null, null),
            },
            AllReachable: true,
            NetworkIssues: Array.Empty<string>());

        var output = CaptureConsoleOutput(() => service.DisplaySummary(summary));

        output.Should().Contain("1/1 providers reachable");
        output.Should().NotContain("Troubleshooting Tips");
    }

    [Fact]
    public async Task DisplaySummary_WithFailures_ShowsTroubleshootingTips()
    {
        await using var service = CreateService();
        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: new[]
            {
                new ConnectivityTestService.ConnectivityTestResult("Alpaca", true, TimeSpan.FromMilliseconds(50), null, null),
                new ConnectivityTestService.ConnectivityTestResult("Polygon", false, TimeSpan.FromSeconds(5), "Timeout", null),
            },
            AllReachable: false,
            NetworkIssues: Array.Empty<string>());

        var output = CaptureConsoleOutput(() => service.DisplaySummary(summary));

        output.Should().Contain("1/2 providers reachable");
        output.Should().Contain("Troubleshooting Tips");
    }

    [Fact]
    public async Task DisplaySummary_WithNetworkIssues_ShowsIssues()
    {
        await using var service = CreateService();
        var summary = new ConnectivityTestService.ConnectivitySummary(
            Results: Array.Empty<ConnectivityTestService.ConnectivityTestResult>(),
            AllReachable: false,
            NetworkIssues: new[] { "No internet connectivity detected" });

        var output = CaptureConsoleOutput(() => service.DisplaySummary(summary));

        output.Should().Contain("No internet connectivity detected");
    }

    #endregion

    #region Dispose

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var service = CreateService();
        await service.DisposeAsync();
        // Second dispose should not throw
        var action = async () => await service.DisposeAsync();
        await action.Should().NotThrowAsync();
    }

    #endregion

    #region Helpers

    private static ConnectivityTestService CreateService()
    {
        return new ConnectivityTestService();
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion
}
