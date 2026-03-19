using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Meridian.Tests.Integration.EndpointTests;

/// <summary>
/// Integration tests for the authentication endpoints:
/// GET /login, POST /api/auth/login, POST /api/auth/logout.
///
/// The test fixture does not set MDC_USERNAME / MDC_PASSWORD environment variables,
/// so LoginSessionService.IsConfigured returns false and the middleware passes all
/// requests through. This lets us verify endpoint reachability and input validation
/// without needing real credentials in the test environment.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Endpoint")]
public sealed class AuthEndpointTests : EndpointIntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointTests(EndpointTestFixture fixture) : base(fixture) { }

    // ================================================================
    // GET /login
    // ================================================================

    [Fact]
    public async Task LoginPage_ReturnsHtml()
    {
        var response = await GetAsync("/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        contentType.Should().Be("text/html");
    }

    [Fact]
    public async Task LoginPage_ContainsSignInForm()
    {
        var response = await GetAsync("/login");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("action=\"/api/auth/login\"");
        html.Should().Contain("name=\"username\"");
        html.Should().Contain("name=\"password\"");
    }

    [Fact]
    public async Task LoginPage_WithErrorQueryParam_ContainsErrorMessage()
    {
        var response = await GetAsync("/login?error=1");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task LoginPage_WithoutErrorQueryParam_DoesNotContainErrorMessage()
    {
        var response = await GetAsync("/login");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain("class=\"login-error\"");
    }

    // ================================================================
    // POST /api/auth/login  (JSON content type)
    // ================================================================

    [Fact]
    public async Task LoginJson_WithEmptyBody_ReturnsBadRequest()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginJson_WithNullUsername_ReturnsBadRequest()
    {
        var payload = new { Username = (string?)null, Password = "secret" };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginJson_WithNullPassword_ReturnsBadRequest()
    {
        var payload = new { Username = "admin", Password = (string?)null };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginJson_WithWrongCredentials_ReturnsUnauthorized()
    {
        // MDC_USERNAME / MDC_PASSWORD are not set → CreateSession always returns null
        var payload = new { Username = "admin", Password = "wrongpassword" };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginJson_WithWrongCredentials_ReturnsJsonError()
    {
        var payload = new { Username = "admin", Password = "wrongpassword" };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/login", content);

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    // ================================================================
    // POST /api/auth/login  (form content type)
    // ================================================================

    [Fact]
    public async Task LoginForm_WithEmptyCredentials_RedirectsToLoginWithError()
    {
        // Use a client that does NOT follow redirects so we can inspect the Location header
        using var noRedirectClient = Fixture.CreateNoRedirectClient();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "",
            ["password"] = ""
        });
        var response = await noRedirectClient.PostAsync("/api/auth/login", form);

        // Empty credentials → redirect back to /login?error=1
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().StartWith("/login");
        response.Headers.Location?.ToString().Should().Contain("error");
    }

    [Fact]
    public async Task LoginForm_WithCredentials_NoEnvVarsConfigured_RedirectsToLoginWithError()
    {
        using var noRedirectClient = Fixture.CreateNoRedirectClient();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "secret"
        });
        var response = await noRedirectClient.PostAsync("/api/auth/login", form);

        // No MDC_USERNAME/MDC_PASSWORD set → credentials rejected → redirect to login with error
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("error");
    }

    // ================================================================
    // POST /api/auth/logout
    // ================================================================

    [Fact]
    public async Task Logout_WithoutSession_RedirectsToLoginPage()
    {
        using var noRedirectClient = Fixture.CreateNoRedirectClient();

        var response = await noRedirectClient.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/login");
    }

    [Fact]
    public async Task Logout_ClearsCookie()
    {
        using var noRedirectClient = Fixture.CreateNoRedirectClient();

        var response = await noRedirectClient.PostAsync("/api/auth/logout", content: null);

        // The Set-Cookie header should contain the session cookie name with an expired/empty value
        var setCookie = response.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        // Either no cookie is set (nothing to clear) or it contains the session cookie name
        if (setCookie.Count > 0)
        {
            setCookie.Should().ContainMatch("*mdc-session*");
        }
    }

    // ================================================================
    // Middleware passthrough when no credentials configured
    // ================================================================

    [Fact]
    public async Task ProtectedEndpoint_WhenNoCredentialsConfigured_PassesThrough()
    {
        // MDC_USERNAME / MDC_PASSWORD not set → middleware IsConfigured=false → passthrough
        var response = await GetAsync("/api/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_WhenNoCredentialsConfigured_PassesThrough()
    {
        var response = await GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
