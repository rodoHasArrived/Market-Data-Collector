using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MarketDataCollector.Tests.Integration.EndpointTests;

/// <summary>
/// Integration tests for provider endpoints including negative-path behavior.
/// Part of B2 improvement: endpoint integration coverage for provider endpoints.
/// </summary>
public sealed class ProviderEndpointNegativeTests : EndpointIntegrationTestBase
{
    public ProviderEndpointNegativeTests(EndpointTestFixture fixture) : base(fixture)
    {
    }

    #region Provider Status

    [Fact]
    public async Task ProviderStatus_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/providers/status");
    }

    [Fact]
    public async Task ProviderMetrics_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/providers/metrics");
    }

    [Fact]
    public async Task ProviderLatency_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/providers/latency");
    }

    [Fact]
    public async Task ProviderCatalog_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/providers/catalog");
    }

    [Fact]
    public async Task ProviderComparison_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/providers/comparison");
    }

    #endregion

    #region Connections

    [Fact]
    public async Task Connections_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/connections");
    }

    #endregion

    #region Failover

    [Fact]
    public async Task FailoverConfig_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/failover/config");
    }

    [Fact]
    public async Task FailoverRules_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/failover/rules");
    }

    [Fact]
    public async Task FailoverHealth_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/failover/health");
    }

    [Fact]
    public async Task ForceFailover_NonExistentRule_HandlesGracefully()
    {
        var response = await Client.PostAsync("/api/failover/force/non-existent-rule", null);

        // Should not crash the server
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Backfill

    [Fact]
    public async Task BackfillProviders_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/backfill/providers");
    }

    [Fact]
    public async Task BackfillStatus_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/backfill/status");
    }

    [Fact]
    public async Task BackfillSchedules_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/backfill/schedules");
    }

    [Fact]
    public async Task BackfillStatistics_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/backfill/statistics");
    }

    #endregion

    #region Non-existent Routes

    [Fact]
    public async Task NonExistentRoute_ReturnsNotFound()
    {
        var response = await GetAsync("/api/this-route-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonExistentApiRoute_ReturnsNotFound()
    {
        var response = await GetAsync("/api/v99/imaginary-endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Response Schema Shape Validation

    [Fact]
    public async Task StatusResponse_HasUptimeField()
    {
        var response = await GetAsync("/api/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);

        json.ValueKind.Should().Be(JsonValueKind.Object);
        json.TryGetProperty("uptime", out _).Should().BeTrue("status response should contain 'uptime' field");
    }

    [Fact]
    public async Task HealthResponse_HasStatusAndChecksFields()
    {
        var response = await GetAsync("/health");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);

        json.ValueKind.Should().Be(JsonValueKind.Object);
        json.TryGetProperty("status", out _).Should().BeTrue("health response should contain 'status' field");
        json.TryGetProperty("checks", out _).Should().BeTrue("health response should contain 'checks' field");
    }

    #endregion
}
