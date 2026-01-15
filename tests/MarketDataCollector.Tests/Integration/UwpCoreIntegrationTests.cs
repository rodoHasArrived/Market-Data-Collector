using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MarketDataCollector.Application.Monitoring;
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
    private const int TestPort = 18080;
    private static readonly string BaseUrl = $"http://localhost:{TestPort}";

    public async Task InitializeAsync()
    {
        // Create metrics provider functions for the status server
        var metricsProvider = () => new MetricsSnapshot(
            Published: 100,
            Dropped: 5,
            Integrity: 2,
            HistoricalBars: 50,
            EventsPerSecond: 1000.0,
            DropRate: 0.05
        );

        var pipelineProvider = () => new PipelineSnapshot(
            PublishedCount: 100,
            DroppedCount: 5,
            ConsumedCount: 95,
            CurrentQueueSize: 10,
            PeakQueueSize: 1000,
            QueueCapacity: 100000,
            QueueUtilization: 0.0001,
            AverageProcessingTimeUs: 50.5
        );

        var integrityProvider = () => Array.Empty<object>();

        // Start the status server
        _server = new StatusHttpServer(TestPort, metricsProvider, pipelineProvider, integrityProvider);
        await _server.StartAsync(CancellationToken.None);

        // Create HTTP client
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Wait for server to be ready
        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_server != null)
        {
            await _server.StopAsync();
        }
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

/// <summary>
/// Test helper record for metrics.
/// </summary>
public record MetricsSnapshot(
    long Published,
    long Dropped,
    long Integrity,
    long HistoricalBars,
    double EventsPerSecond,
    double DropRate
);

/// <summary>
/// Test helper record for pipeline data.
/// </summary>
public record PipelineSnapshot(
    long PublishedCount,
    long DroppedCount,
    long ConsumedCount,
    int CurrentQueueSize,
    long PeakQueueSize,
    int QueueCapacity,
    double QueueUtilization,
    double AverageProcessingTimeUs
);
