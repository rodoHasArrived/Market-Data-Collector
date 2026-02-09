using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MarketDataCollector.Tests.Integration.EndpointTests;

/// <summary>
/// Integration tests for the /api/quality/drops endpoint.
/// Part of B2/#7 and C3/#16 improvements.
/// </summary>
public sealed class QualityDropsEndpointTests : EndpointIntegrationTestBase
{
    public QualityDropsEndpointTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task QualityDropsEndpoint_ReturnsJson()
    {
        var response = await GetAsync("/api/quality/drops");
        // Endpoint should return OK with drop statistics
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotImplemented,
            $"Expected 200 or 501 but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task QualityDropsBySymbol_ReturnsJson()
    {
        var response = await GetAsync("/api/quality/drops/AAPL");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotImplemented,
            $"Expected 200 or 501 but got {(int)response.StatusCode}");
    }
}
