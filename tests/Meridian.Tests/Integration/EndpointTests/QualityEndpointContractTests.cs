using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Meridian.Application.Monitoring.DataQuality;
using Meridian.Contracts.Api;
using Meridian.Contracts.Api.Quality;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Meridian.Tests.Integration.EndpointTests;

/// <summary>
/// Locks the quality endpoint payloads that the desktop dashboard currently consumes.
/// </summary>
public sealed class QualityEndpointContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task QualityDashboard_ReturnsStableDashboardContract()
    {
        await using var host = await CreateHostAsync(out _, out _);
        using var client = host.GetTestClient();

        var response = await client.GetAsync(UiApiRoutes.QualityDashboard);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QualityDashboardResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.RealTimeMetrics.SymbolHealth.Should().NotBeEmpty();
        payload.RecentGaps.Should().NotBeEmpty();
        payload.RecentAnomalies.Should().NotBeEmpty();
        payload.AnomalyStats.UnacknowledgedCount.Should().BeGreaterThan(0);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("realTimeMetrics", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("recentGaps", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("recentAnomalies", out _).Should().BeTrue();
    }

    [Fact]
    public async Task QualityGaps_ReturnsStableGapContract()
    {
        await using var host = await CreateHostAsync(out _, out _);
        using var client = host.GetTestClient();

        var response = await client.GetAsync($"{UiApiRoutes.QualityGaps}?count=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<QualityGapResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Should().ContainSingle();
        payload[0].Symbol.Should().Be("AAPL");
        payload[0].Duration.Should().BeGreaterThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task QualityAnomalies_ReturnsStableAnomalyContract()
    {
        await using var host = await CreateHostAsync(out _, out _);
        using var client = host.GetTestClient();

        var response = await client.GetAsync($"{UiApiRoutes.QualityAnomalies}?count=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<QualityAnomalyResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Should().ContainSingle();
        payload[0].Symbol.Should().Be("AAPL");
        payload[0].IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task QualityLatencyStatistics_ReturnsStableLatencyContract()
    {
        await using var host = await CreateHostAsync(out _, out _);
        using var client = host.GetTestClient();

        var response = await client.GetAsync(UiApiRoutes.QualityLatencyStatistics);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QualityLatencyStatisticsResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.SymbolsTracked.Should().BeGreaterThan(0);
        payload.TotalSamples.Should().BeGreaterThan(0);
        payload.DistributionsBySymbol.Should().ContainKey("AAPL");
    }

    [Fact]
    public async Task QualityComparison_ReturnsStableComparisonContract()
    {
        await using var host = await CreateHostAsync(out _, out _);
        using var client = host.GetTestClient();

        var route = UiApiRoutes.WithParam(UiApiRoutes.QualityComparison, "symbol", "AAPL");
        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QualityComparisonResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Symbol.Should().Be("AAPL");
        payload.Providers.Should().HaveCount(2);
        payload.RecommendedProvider.Should().Be("Provider1");
    }

    [Fact]
    public async Task QualityAnomalyAcknowledgement_ReturnsStableAcknowledgementContract()
    {
        await using var host = await CreateHostAsync(out var qualityService, out var anomalyId);
        using var client = host.GetTestClient();

        var route = UiApiRoutes.WithParam(UiApiRoutes.QualityAnomaliesAcknowledge, "anomalyId", anomalyId);
        var response = await client.PostAsync(route, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QualityAnomalyAcknowledgementResponse>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Acknowledged.Should().BeTrue();
        qualityService.AnomalyDetector.GetRecentAnomalies(10).Single().IsAcknowledged.Should().BeTrue();
    }

    private static async Task<WebApplication> CreateHostAsync(
        out DataQualityMonitoringService qualityService,
        out string anomalyId)
    {
        qualityService = CreateSeededQualityService();
        anomalyId = qualityService.AnomalyDetector.GetRecentAnomalies(10).Single().Id;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(qualityService);

        var app = builder.Build();
        app.MapDataQualityEndpoints(app.Services.GetRequiredService<DataQualityMonitoringService>());
        await app.StartAsync();
        return app;
    }

    private static DataQualityMonitoringService CreateSeededQualityService()
    {
        var service = new DataQualityMonitoringService(new DataQualityMonitoringConfig
        {
            GapAnalyzerConfig = new GapAnalyzerConfig
            {
                GapThresholdSeconds = 60,
                ExpectedEventsPerHour = 1000
            }
        });

        var baseTime = new DateTimeOffset(2026, 03, 20, 14, 00, 00, TimeSpan.Zero);

        service.ProcessTrade("AAPL", baseTime, 150.00m, 100m, sequence: 1, provider: "Provider1", latencyMs: 8);
        service.ProcessTrade("AAPL", baseTime.AddMinutes(2), 151.00m, 100m, sequence: 2, provider: "Provider1", latencyMs: 12);

        service.ProcessTrade("AAPL", baseTime, 150.00m, 100m, sequence: 1, provider: "Provider2", latencyMs: 18);
        service.ProcessTrade("AAPL", baseTime.AddMinutes(1), 150.50m, 100m, sequence: 2, provider: "Provider2", latencyMs: 24);

        service.ProcessQuote("AAPL", baseTime.AddMinutes(3), 151.00m, 150.00m, 10m, 10m, provider: "Provider1", latencyMs: 30);

        return service;
    }
}
