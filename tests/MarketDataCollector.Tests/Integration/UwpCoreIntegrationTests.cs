using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using Xunit;

namespace MarketDataCollector.Tests.Integration;

/// <summary>
/// Integration tests verifying UWP desktop application can communicate with Core service.
/// These tests validate the HTTP API contract between UWP and the StatusHttpServer.
/// </summary>
public class UwpCoreIntegrationTests : IAsyncLifetime
{
    private StatusHttpServer? _server;
    private HttpClient? _httpClient;
    private int _testPort;

    public Task InitializeAsync()
    {
        // Create metrics provider functions for the status server
        Func<MetricsSnapshot> metricsProvider = () => new MetricsSnapshot(
            Published: 100,
            Dropped: 5,
            Integrity: 2,
            Trades: 80,
            DepthUpdates: 15,
            Quotes: 5,
            HistoricalBars: 50,
            EventsPerSecond: 1000.0,
            TradesPerSecond: 800.0,
            DepthUpdatesPerSecond: 150.0,
            HistoricalBarsPerSecond: 50.0,
            DropRate: 0.05,
            AverageLatencyUs: 100.0,
            MinLatencyUs: 10.0,
            MaxLatencyUs: 500.0,
            LatencySampleCount: 1000,
            Gc0Collections: 0,
            Gc1Collections: 0,
            Gc2Collections: 0,
            Gc0Delta: 0,
            Gc1Delta: 0,
            Gc2Delta: 0,
            MemoryUsageMb: 100.0,
            HeapSizeMb: 50.0,
            Timestamp: DateTimeOffset.UtcNow
        );

        Func<PipelineStatistics> pipelineProvider = () => new PipelineStatistics(
            PublishedCount: 100,
            DroppedCount: 5,
            ConsumedCount: 95,
            CurrentQueueSize: 10,
            PeakQueueSize: 1000,
            QueueCapacity: 100000,
            QueueUtilization: 0.0001,
            AverageProcessingTimeUs: 50.5,
            TimeSinceLastFlush: TimeSpan.FromSeconds(1),
            Timestamp: DateTimeOffset.UtcNow
        );

        Func<IReadOnlyList<DepthIntegrityEvent>> integrityProvider = () => Array.Empty<DepthIntegrityEvent>();

        _testPort = GetFreePort();

        // Start the status server
        _server = new StatusHttpServer(_testPort, metricsProvider, pipelineProvider, integrityProvider);
        _server.Start();

        // Create HTTP client
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{_testPort}"),
            Timeout = TimeSpan.FromSeconds(5)
        };

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_server != null)
        {
            await _server.DisposeAsync();
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// Verify the /api/status endpoint works (UWP uses /api/* prefix).
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_WithApiPrefix_ReturnsValidResponse()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("isConnected");
        content.Should().Contain("timestampUtc");
        content.Should().Contain("metrics");
    }

    /// <summary>
    /// Verify the /status endpoint works (non-prefixed version).
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_WithoutApiPrefix_ReturnsValidResponse()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("isConnected");
    }

    /// <summary>
    /// Verify status response contains isConnected field required by UWP.
    /// </summary>
    [Fact]
    public async Task StatusResponse_ContainsIsConnected_ForUwpCompatibility()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/status");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        json.RootElement.TryGetProperty("isConnected", out var isConnected).Should().BeTrue();
        isConnected.ValueKind.Should().Be(JsonValueKind.True);
    }

    /// <summary>
    /// Verify metrics are properly nested in response.
    /// </summary>
    [Fact]
    public async Task StatusResponse_ContainsMetrics_WithCorrectStructure()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/status");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        json.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("published", out _).Should().BeTrue();
        metrics.TryGetProperty("dropped", out _).Should().BeTrue();
        metrics.TryGetProperty("eventsPerSecond", out _).Should().BeTrue();
        metrics.TryGetProperty("dropRate", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verify /api/health endpoint works for UWP health checks.
    /// </summary>
    [Fact]
    public async Task HealthEndpoint_WithApiPrefix_ReturnsOk()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verify /health endpoint works (non-prefixed version).
    /// </summary>
    [Fact]
    public async Task HealthEndpoint_WithoutApiPrefix_ReturnsOk()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verify /api/metrics endpoint works for Prometheus scraping.
    /// </summary>
    [Fact]
    public async Task MetricsEndpoint_WithApiPrefix_ReturnsPrometheusFormat()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("mdc_events_published");
    }

    /// <summary>
    /// Verify /api/backfill/providers endpoint works for UWP backfill UI.
    /// </summary>
    [Fact]
    public async Task BackfillProvidersEndpoint_ReturnsProviderList()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/backfill/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("alpaca");
        content.Should().Contain("yahoo");
        content.Should().Contain("stooq");
    }

    /// <summary>
    /// Verify /api/backfill/status endpoint works.
    /// </summary>
    [Fact]
    public async Task BackfillStatusEndpoint_ReturnsStatus()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/backfill/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    /// <summary>
    /// Verify Kubernetes readiness probe works.
    /// </summary>
    [Fact]
    public async Task ReadyEndpoint_ReturnsOk()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verify Kubernetes liveness probe works.
    /// </summary>
    [Fact]
    public async Task LiveEndpoint_ReturnsOk()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verify dashboard HTML page is served at root.
    /// </summary>
    [Fact]
    public async Task RootEndpoint_ReturnsDashboardHtml()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("<!DOCTYPE html>");
        content.Should().Contain("Market Data Collector");
    }

    /// <summary>
    /// Verify response timestamps are in valid format.
    /// </summary>
    [Fact]
    public async Task StatusResponse_TimestampUtc_IsValidDateTimeOffset()
    {
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/api/status");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        json.RootElement.TryGetProperty("timestampUtc", out var timestamp).Should().BeTrue();
        var parsed = DateTimeOffset.TryParse(timestamp.GetString(), out var dt);
        parsed.Should().BeTrue();
        dt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Verify concurrent requests are handled correctly.
    /// </summary>
    [Fact]
    public async Task StatusEndpoint_ConcurrentRequests_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _httpClient!.GetAsync("/api/status"))
            .ToArray();

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }
}
