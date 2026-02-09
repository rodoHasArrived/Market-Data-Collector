using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MarketDataCollector.Tests.Integration.EndpointTests;

/// <summary>
/// Base class for HTTP endpoint integration tests.
/// Uses WebApplicationFactory for in-process testing without real network calls.
/// Implements improvement B2/#7 from the structural improvements analysis.
/// </summary>
public abstract class EndpointIntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly HttpClient Client;
    protected readonly WebApplicationFactory<Program> Factory;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected EndpointIntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected async Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct = default)
    {
        return await Client.GetAsync(url, ct);
    }

    protected async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct = default)
    {
        var response = await Client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    protected async Task AssertEndpointReturnsOk(string url)
    {
        var response = await GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    protected async Task AssertEndpointReturnsJson(string url)
    {
        var response = await GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
    }

    protected async Task AssertEndpointReturnsNotImplemented(string url)
    {
        var response = await GetAsync(url);
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
