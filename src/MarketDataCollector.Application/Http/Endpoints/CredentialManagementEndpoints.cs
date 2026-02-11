using System.Text.Json;
using MarketDataCollector.Application.Config.Credentials;
using MarketDataCollector.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// HTTP API endpoints for credential management: credential testing, OAuth token
/// management, consolidated configuration service endpoints, self-healing,
/// validation, and the credentials dashboard.
/// Extracted from UiServer.ConfigureCredentialManagementRoutes().
/// </summary>
public static class CredentialManagementEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapCredentialManagementEndpoints(this WebApplication app)
    {
        // ==================== CREDENTIAL TESTING ====================

        // Test credentials for a specific provider
        app.MapPost("/api/credentials/test", async (
            CredentialTestingService credentialService,
            CredentialTestRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Provider))
                    return Results.BadRequest("Provider name is required");

                var result = await credentialService.TestCredentialAsync(
                    req.Provider,
                    req.ApiKey,
                    req.ApiSecret,
                    req.CredentialSource);

                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Credential test failed: {ex.Message}");
            }
        });

        // Test all configured credentials
        app.MapPost("/api/credentials/test-all", async (
            CredentialTestingService credentialService,
            ConfigurationService configService,
            ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var summary = await credentialService.TestAllCredentialsAsync(config);
                return Results.Json(summary, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Credential test failed: {ex.Message}");
            }
        });

        // Get all credential statuses (cached)
        app.MapGet("/api/credentials/status", (CredentialTestingService credentialService) =>
        {
            try
            {
                var statuses = credentialService.GetAllCachedStatuses();

                var response = statuses.Select(kvp => new
                {
                    provider = kvp.Key,
                    lastSuccessfulAuth = kvp.Value.LastSuccessfulAuth,
                    lastTestResult = kvp.Value.LastTestResult.ToString(),
                    lastTestedAt = kvp.Value.LastTestedAt,
                    consecutiveFailures = kvp.Value.ConsecutiveFailures,
                    expiresAt = kvp.Value.ExpiresAt,
                    isExpiringSoon = kvp.Value.ExpiresAt.HasValue &&
                        (kvp.Value.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays <= 7,
                    daysUntilExpiration = kvp.Value.ExpiresAt.HasValue
                        ? (kvp.Value.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays
                        : (double?)null
                }).ToList();

                return Results.Json(response, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get credential status: {ex.Message}");
            }
        });

        // Get credential status for a specific provider
        app.MapGet("/api/credentials/status/{provider}", (
            CredentialTestingService credentialService,
            string provider) =>
        {
            try
            {
                var status = credentialService.GetCachedStatus(provider);
                if (status == null)
                    return Results.NotFound($"No status found for provider: {provider}");

                var response = new
                {
                    provider = status.ProviderName,
                    lastSuccessfulAuth = status.LastSuccessfulAuth,
                    lastTestResult = status.LastTestResult.ToString(),
                    lastTestedAt = status.LastTestedAt,
                    consecutiveFailures = status.ConsecutiveFailures,
                    expiresAt = status.ExpiresAt,
                    isExpiringSoon = status.ExpiresAt.HasValue &&
                        (status.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays <= 7,
                    daysUntilExpiration = status.ExpiresAt.HasValue
                        ? (status.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays
                        : (double?)null
                };

                return Results.Json(response, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get credential status: {ex.Message}");
            }
        });

        // ==================== OAUTH TOKEN MANAGEMENT ====================

        // Get all OAuth token statuses
        app.MapGet("/api/credentials/oauth/tokens", (OAuthTokenRefreshService oauthService) =>
        {
            try
            {
                var tokens = oauthService.GetAllTokens();

                var response = tokens.Select(kvp => new
                {
                    provider = kvp.Key,
                    status = kvp.Value.Status.ToString(),
                    expiresAt = kvp.Value.Token.ExpiresAt,
                    isExpired = kvp.Value.Token.IsExpired,
                    isExpiringSoon = kvp.Value.Token.IsExpiringSoon,
                    canRefresh = kvp.Value.Token.CanRefresh,
                    lifetimeRemainingPercent = kvp.Value.Token.LifetimeRemainingPercent,
                    timeUntilExpiration = kvp.Value.Token.TimeUntilExpiration.ToString()
                }).ToList();

                return Results.Json(response, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get OAuth tokens: {ex.Message}");
            }
        });

        // Manually refresh OAuth token for a provider
        app.MapPost("/api/credentials/oauth/refresh/{provider}", async (
            OAuthTokenRefreshService oauthService,
            string provider) =>
        {
            try
            {
                var result = await oauthService.RefreshTokenAsync(provider);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Token refresh failed: {ex.Message}");
            }
        });

        // Store OAuth token for a provider
        app.MapPost("/api/credentials/oauth/store", async (
            OAuthTokenRefreshService oauthService,
            OAuthTokenStoreRequest req) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Provider))
                    return Results.BadRequest("Provider name is required");

                if (string.IsNullOrWhiteSpace(req.AccessToken))
                    return Results.BadRequest("Access token is required");

                var token = new OAuthToken(
                    AccessToken: req.AccessToken,
                    TokenType: req.TokenType ?? "Bearer",
                    ExpiresAt: req.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
                    RefreshToken: req.RefreshToken,
                    RefreshTokenExpiresAt: req.RefreshTokenExpiresAt,
                    Scope: req.Scope,
                    IssuedAt: DateTimeOffset.UtcNow
                );

                await oauthService.StoreTokenAsync(req.Provider, token);
                return Results.Ok(new { message = "Token stored successfully" });
            }
            catch (Exception ex)
            {
                // Log full exception server-side but return generic error to client
                // to avoid leaking sensitive details (token fragments, paths, etc.)
                Log.Error(ex, "Failed to store OAuth token for provider {Provider}", req.Provider);
                return Results.Problem("Failed to store OAuth token. Check server logs for details.");
            }
        });

        // Remove OAuth token for a provider
        app.MapDelete("/api/credentials/oauth/{provider}", async (
            OAuthTokenRefreshService oauthService,
            string provider) =>
        {
            try
            {
                await oauthService.RemoveTokenAsync(provider);
                return Results.Ok(new { message = $"Token removed for {provider}" });
            }
            catch (Exception ex)
            {
                // Log full exception server-side but return generic error to client
                Log.Error(ex, "Failed to remove OAuth token for provider {Provider}", provider);
                return Results.Problem("Failed to remove OAuth token. Check server logs for details.");
            }
        });

        // ==================== CONSOLIDATED CONFIGURATION SERVICE ENDPOINTS ====================
        // These endpoints route through ConfigurationService for unified configuration operations

        // Get detected providers via ConfigurationService
        app.MapGet("/api/config/providers", (ConfigurationService configService) =>
        {
            try
            {
                var providers = configService.DetectProviders();
                return Results.Json(providers, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to detect providers: {ex.Message}");
            }
        });

        // Get provider credential status summary
        app.MapGet("/api/config/providers/status", (ConfigurationService configService) =>
        {
            try
            {
                var status = configService.GetCredentialStatus();
                return Results.Json(status, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get credential status: {ex.Message}");
            }
        });

        // Get best real-time provider
        app.MapGet("/api/config/providers/best-realtime", (ConfigurationService configService) =>
        {
            try
            {
                var provider = configService.GetBestRealTimeProvider();
                if (provider == null)
                    return Results.NotFound(new { message = "No real-time providers with credentials configured" });

                return Results.Json(provider, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get best real-time provider: {ex.Message}");
            }
        });

        // Get historical providers
        app.MapGet("/api/config/providers/historical", (ConfigurationService configService) =>
        {
            try
            {
                var providers = configService.GetHistoricalProviders();
                return Results.Json(providers, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get historical providers: {ex.Message}");
            }
        });

        // Apply self-healing fixes to configuration
        app.MapPost("/api/config/self-healing", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath, applySelfHealing: false);
                var (_, appliedFixes, warnings) = configService.ApplySelfHealingFixes(config);

                return Results.Json(new
                {
                    success = true,
                    appliedFixes = appliedFixes,
                    warnings = warnings,
                    configChanged = appliedFixes.Count > 0
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to apply self-healing fixes: {ex.Message}");
            }
        });

        // Apply and save self-healing fixes
        app.MapPost("/api/config/self-healing/apply", async (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath, applySelfHealing: false);
                var (fixedConfig, appliedFixes, warnings) = configService.ApplySelfHealingFixes(config);

                if (appliedFixes.Count > 0)
                {
                    await store.SaveAsync(fixedConfig);
                }

                return Results.Json(new
                {
                    success = true,
                    appliedFixes = appliedFixes,
                    warnings = warnings,
                    configSaved = appliedFixes.Count > 0
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to apply and save self-healing fixes: {ex.Message}");
            }
        });

        // Validate configuration via ConfigurationService
        app.MapPost("/api/config/validate", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var isValid = configService.ValidateConfig(config, out var errors);

                return Results.Json(new
                {
                    isValid = isValid,
                    errors = errors
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to validate configuration: {ex.Message}");
            }
        });

        // Perform quick check via ConfigurationService
        app.MapGet("/api/config/quick-check", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var config = configService.LoadAndPrepareConfig(store.ConfigPath);
                var result = configService.PerformQuickCheck(config);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to perform quick check: {ex.Message}");
            }
        });

        // Resolve all credentials for configuration
        app.MapPost("/api/config/resolve-credentials", (ConfigurationService configService, ConfigStore store) =>
        {
            try
            {
                var resolvedConfig = configService.LoadAndPrepareConfig(store.ConfigPath, applySelfHealing: false);

                // Don't return sensitive data - just indicate which credentials were resolved
                var resolvedProviders = new List<string>();

                if (!string.IsNullOrEmpty(resolvedConfig.Alpaca?.KeyId))
                    resolvedProviders.Add("Alpaca");
                if (!string.IsNullOrEmpty(resolvedConfig.Polygon?.ApiKey))
                    resolvedProviders.Add("Polygon");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.Tiingo?.ApiToken))
                    resolvedProviders.Add("Tiingo");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.Finnhub?.ApiKey))
                    resolvedProviders.Add("Finnhub");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.AlphaVantage?.ApiKey))
                    resolvedProviders.Add("AlphaVantage");
                if (!string.IsNullOrEmpty(resolvedConfig.Backfill?.Providers?.Polygon?.ApiKey))
                    resolvedProviders.Add("Polygon (Backfill)");

                return Results.Json(new
                {
                    resolvedProviders = resolvedProviders,
                    message = $"Resolved credentials for {resolvedProviders.Count} provider(s)"
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to resolve credentials: {ex.Message}");
            }
        });

        // Check if IB Gateway is available
        app.MapGet("/api/config/ib-gateway/status", (ConfigurationService configService) =>
        {
            try
            {
                var available = configService.IsIBGatewayAvailable();
                return Results.Json(new
                {
                    available = available,
                    checkedPorts = new[] { 7496, 7497, 4001, 4002 },
                    message = available ? "IB Gateway/TWS is running" : "IB Gateway/TWS not detected"
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to check IB Gateway status: {ex.Message}");
            }
        });

        // Get environment name
        app.MapGet("/api/config/environment", () =>
        {
            try
            {
                var envName = ConfigurationService.GetEnvironmentName();
                return Results.Json(new
                {
                    environment = envName ?? "default",
                    isConfigured = envName != null,
                    envVars = new
                    {
                        MDC_ENVIRONMENT = Environment.GetEnvironmentVariable("MDC_ENVIRONMENT"),
                        DOTNET_ENVIRONMENT = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                    }
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get environment: {ex.Message}");
            }
        });

        // ==================== CREDENTIAL MANAGEMENT UI ====================

        // Get credentials dashboard HTML
        app.MapGet("/credentials", (ConfigStore store, CredentialTestingService credentialService) =>
        {
            try
            {
                var config = store.Load();
                var statuses = credentialService.GetAllCachedStatuses();
                var html = HtmlTemplateManager.CredentialsDashboard(config, statuses);
                return Results.Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to render credentials dashboard: {ex.Message}");
            }
        });
    }
}
