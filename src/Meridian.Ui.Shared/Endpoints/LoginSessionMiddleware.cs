using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Meridian.Ui.Shared.Endpoints;

/// <summary>
/// Middleware that enforces session-based authentication when MDC_USERNAME and
/// MDC_PASSWORD environment variables are both configured.
/// <list type="bullet">
///   <item>Health probes (/healthz, /readyz, /livez) are always exempt.</item>
///   <item>The login page (/login) and auth API endpoints (/api/auth/*) are always exempt.</item>
///   <item>Unauthenticated API requests receive a 401 JSON response.</item>
///   <item>Unauthenticated browser (non-/api) requests are redirected to /login.</item>
///   <item>When no credentials are configured all requests pass through (backward-compatible).</item>
/// </list>
/// </summary>
public sealed class LoginSessionMiddleware
{
    /// <summary>Name of the HTTP-only session cookie set after successful login.</summary>
    public const string SessionCookieName = "mdc-session";

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/healthz",
        "/readyz",
        "/livez"
    };

    private readonly RequestDelegate _next;

    public LoginSessionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, LoginSessionService sessionService)
    {
        // If no credentials are configured, allow all requests (backward-compatible)
        if (!sessionService.IsConfigured)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        var trimmedPath = path.TrimEnd('/');

        // Exempt health probes
        if (ExemptPaths.Contains(trimmedPath))
        {
            await _next(context);
            return;
        }

        // Exempt the login page and all auth API endpoints
        if (trimmedPath.Equals("/login", StringComparison.OrdinalIgnoreCase) ||
            trimmedPath.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Validate session cookie
        var token = context.Request.Cookies[SessionCookieName];
        if (!string.IsNullOrWhiteSpace(token) && sessionService.ValidateSession(token))
        {
            await _next(context);
            return;
        }

        // Unauthenticated request — differentiate API from browser
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized. Please sign in via the login page."}""");
        }
        else
        {
            var returnUrl = path + context.Request.QueryString.ToString();
            context.Response.Redirect(
                $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
    }
}

/// <summary>
/// Extension methods for registering the login session middleware.
/// </summary>
public static class LoginSessionMiddlewareExtensions
{
    /// <summary>
    /// Adds session-based authentication middleware.
    /// Authentication is only enforced when MDC_USERNAME and MDC_PASSWORD are configured.
    /// </summary>
    public static IApplicationBuilder UseLoginSessionAuthentication(this IApplicationBuilder app)
        => app.UseMiddleware<LoginSessionMiddleware>();
}
