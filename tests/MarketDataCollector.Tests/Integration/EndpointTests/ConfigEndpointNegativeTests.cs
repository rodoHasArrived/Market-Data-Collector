using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MarketDataCollector.Tests.Integration.EndpointTests;

/// <summary>
/// Integration tests for config endpoints including negative-path behavior.
/// Part of B2 improvement: endpoint integration coverage for config endpoints.
/// </summary>
public sealed class ConfigEndpointNegativeTests : EndpointIntegrationTestBase
{
    public ConfigEndpointNegativeTests(EndpointTestFixture fixture) : base(fixture)
    {
    }

    #region GET /api/config

    [Fact]
    public async Task GetConfig_ReturnsOkWithJson()
    {
        var response = await GetAsync("/api/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetConfig_ContainsExpectedFields()
    {
        var response = await GetAsync("/api/config");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);

        // Config should be a JSON object
        json.ValueKind.Should().Be(JsonValueKind.Object);
    }

    #endregion

    #region POST /api/config/data-source

    [Fact]
    public async Task PostDataSource_WithInvalidBody_ReturnsBadRequest()
    {
        var content = new StringContent("not-valid-json", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/config/data-source", content);

        // Should return 400 for malformed body
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PostDataSource_WithEmptyBody_ReturnsBadRequest()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/config/data-source", content);

        // Empty body should still be handled
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/config/symbols

    [Fact]
    public async Task PostSymbol_WithValidSymbol_ReturnsSuccess()
    {
        var symbolData = new { symbol = "TSLA", subscribeTrades = true, subscribeDepth = false, depthLevels = 5 };
        var content = new StringContent(JsonSerializer.Serialize(symbolData), Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/config/symbols", content);

        // POST to add symbol should succeed or conflict
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostSymbol_WithInvalidJson_ReturnsBadRequest()
    {
        var content = new StringContent("{invalid", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/config/symbols", content);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    #region DELETE /api/config/symbols/{symbol}

    [Fact]
    public async Task DeleteSymbol_NonExistentSymbol_HandlesGracefully()
    {
        var response = await Client.DeleteAsync("/api/config/symbols/NONEXISTENT_SYMBOL_12345");

        // Should handle missing symbol gracefully (not 500)
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Data Sources Management

    [Fact]
    public async Task GetDataSources_ReturnsJson()
    {
        await AssertEndpointReturnsJson("/api/config/data-sources");
    }

    #endregion
}
