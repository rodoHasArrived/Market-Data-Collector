using System.Text.Json;
using MarketDataCollector.Application.Composition;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Services;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// Embedded HTTP server for the web dashboard UI.
/// Uses ServiceCompositionRoot for centralized service registration.
/// </summary>
[ImplementsAdr("ADR-001", "UiServer uses centralized composition root")]
public sealed class UiServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly WebApplication _app;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly ILogger<UiServer> _logger;

    /// <summary>
    /// Logs an exception and returns a Problem result with a user-friendly message.
    /// </summary>
    private IResult LogAndProblem(Exception ex, string operation, string? context = null)
    {
        var contextInfo = context is not null ? $" Context: {context}" : "";
        _logger.LogError(ex, "API operation failed: {Operation}.{Context}", operation, contextInfo);
        return Results.Problem($"{operation}. Please check server logs for details.");
    }

    /// <summary>
    /// Creates a new UiServer using the centralized ServiceCompositionRoot.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="port">HTTP port to listen on.</param>
    public UiServer(string configPath, int port = 8080)
    {
        var builder = WebApplication.CreateBuilder();

        // Minimize logging from ASP.NET Core
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Use centralized service composition root
        var compositionOptions = CompositionOptions.WebDashboard with { ConfigPath = configPath };
        builder.Services.AddMarketDataServices(compositionOptions);

        _app = builder.Build();
        _logger = _app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<UiServer>();

        ConfigureRoutes();
    }

    private void ConfigureRoutes()
    {
        // ==================== HEALTH CHECK ENDPOINTS ====================
        // These endpoints support container orchestration (Docker, Kubernetes)

        _app.MapGet("/health", () =>
        {
            var uptime = DateTimeOffset.UtcNow - _startTime;
            return Results.Json(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow,
                uptime = uptime.ToString(),
                version = "1.6.1"
            });
        });

        _app.MapGet("/healthz", () => Results.Ok("healthy"));

        _app.MapGet("/ready", () => Results.Ok("ready"));

        _app.MapGet("/readyz", () => Results.Ok("ready"));

        _app.MapGet("/live", () => Results.Ok("alive"));

        _app.MapGet("/livez", () => Results.Ok("alive"));

        _app.MapGet("/", (ConfigStore store) =>
        {
            var html = HtmlTemplateManager.Index(
                store.ConfigPath,
                store.GetStatusPath(),
                store.GetBackfillStatusPath());
            return Results.Content(html, "text/html");
        });

        _app.MapGet("/api/config", (ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(new
            {
                dataRoot = cfg.DataRoot,
                compress = cfg.Compress ?? false,
                dataSource = cfg.DataSource.ToString(),
                alpaca = cfg.Alpaca,
                storage = cfg.Storage,
                symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
                backfill = cfg.Backfill
            }, s_jsonOptions);
        });

        _app.MapPost("/api/config/datasource", async (ConfigStore store, DataSourceRequest req) =>
        {
            try
            {
                var cfg = store.Load();

                if (!Enum.TryParse<DataSourceKind>(req.DataSource, ignoreCase: true, out var ds))
                    return Results.BadRequest("Invalid DataSource. Use 'IB', 'Alpaca', or 'Polygon'.");

                var next = cfg with { DataSource = ds };
                await store.SaveAsync(next);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to update data source", $"DataSource={req.DataSource}");
            }
        });

        _app.MapPost("/api/config/alpaca", async (ConfigStore store, AlpacaOptions alpaca) =>
        {
            try
            {
                var cfg = store.Load();
                var next = cfg with { Alpaca = alpaca };
                await store.SaveAsync(next);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to save Alpaca settings");
            }
        });

        _app.MapPost("/api/config/storage", async (ConfigStore store, StorageSettingsRequest req) =>
        {
            try
            {
                var cfg = store.Load();
                var storage = new StorageConfig(
                    NamingConvention: req.NamingConvention ?? "BySymbol",
                    DatePartition: req.DatePartition ?? "Daily",
                    IncludeProvider: req.IncludeProvider,
                    FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix,
                    Profile: string.IsNullOrWhiteSpace(req.Profile) ? null : req.Profile
                );
                var next = cfg with
                {
                    DataRoot = string.IsNullOrWhiteSpace(req.DataRoot) ? "data" : req.DataRoot,
                    Compress = req.Compress,
                    Storage = storage
                };
                await store.SaveAsync(next);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to save storage settings");
            }
        });

        _app.MapGet("/api/storage/profiles", () =>
        {
            var profiles = StorageProfilePresets.GetPresets()
                .Select(p => new StorageProfileResponse(p.Id, p.Label, p.Description))
                .ToArray();
            return Results.Json(profiles, s_jsonOptions);
        });

        _app.MapPost("/api/config/symbols", async (ConfigStore store, SymbolConfig symbol) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol.Symbol))
                    return Results.BadRequest("Symbol is required.");

                var cfg = store.Load();

                var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
                var idx = list.FindIndex(s => string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) list[idx] = symbol;
                else list.Add(symbol);

                var next = cfg with { Symbols = list.ToArray() };
                await store.SaveAsync(next);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to add symbol", $"Symbol={symbol.Symbol}");
            }
        });

        _app.MapDelete("/api/config/symbols/{symbol}", async (ConfigStore store, string symbol) =>
        {
            try
            {
                var cfg = store.Load();
                var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
                list.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
                var next = cfg with { Symbols = list.ToArray() };
                await store.SaveAsync(next);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to delete symbol", $"Symbol={symbol}");
            }
        });

        _app.MapGet("/api/status", (ConfigStore store) =>
        {
            try
            {
                var status = store.TryLoadStatusJson();
                return status is null ? Results.NotFound() : Results.Content(status, "application/json");
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to load status");
            }
        });

        _app.MapGet("/api/backfill/providers", (BackfillCoordinator backfill) =>
        {
            try
            {
                var providers = backfill.DescribeProviders();
                return Results.Json(providers, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to get providers");
            }
        });

        _app.MapGet("/api/backfill/status", (BackfillCoordinator backfill) =>
        {
            try
            {
                var status = backfill.TryReadLast();
                return status is null
                    ? Results.NotFound()
                    : Results.Json(status, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Failed to load backfill status");
            }
        });

        _app.MapPost("/api/backfill/run", async (BackfillCoordinator backfill, BackfillRequestDto req, CancellationToken ct) =>
        {
            try
            {
                if (req.Symbols is null || req.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var request = new Application.Backfill.BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "composite" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request, ct);
                return Results.Json(result, s_jsonOptions);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Backfill failed");
            }
        });

        _app.MapGet("/api/backfill/health", async (BackfillCoordinator backfill, CancellationToken ct) =>
        {
            try
            {
                var health = await backfill.CheckProviderHealthAsync(ct);
                return Results.Json(health, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Health check failed");
            }
        });

        _app.MapGet("/api/backfill/resolve/{symbol}", async (BackfillCoordinator backfill, string symbol, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                    return Results.BadRequest("Symbol is required.");

                var resolution = await backfill.ResolveSymbolAsync(symbol, ct);
                if (resolution is null)
                    return Results.NotFound($"Symbol '{symbol}' not found.");

                return Results.Json(resolution, s_jsonOptions);
            }
            catch (Exception ex)
            {
                return LogAndProblem(ex, "Symbol resolution failed");
            }
        });

        // Storage organization: catalog, search, health, quality, tiers, sources
        _app.MapStorageOrganizationEndpoints();

        // Symbol management: bulk import/export, templates, schedules, metadata, search, indices
        _app.MapSymbolManagementEndpoints();

        // New features: historical query, diagnostics, sample data, config templates, dry-run, API docs
        _app.MapNewFeatureEndpoints();

        // Credential management: testing, OAuth, provider config, self-healing
        _app.MapCredentialManagementEndpoints();

        // Scheduled backfill endpoints
        _app.MapScheduledBackfillEndpoints();

        // Bulk symbol management: import, watchlists, portfolio, batch operations
        _app.MapBulkSymbolManagementEndpoints();

        // Packaging endpoints
        var config = _app.Services.GetRequiredService<ConfigStore>().Load();
        _app.MapPackagingEndpoints(config.DataRoot);

        // Archive maintenance endpoints
        _app.MapArchiveMaintenanceEndpoints();
    }

    // NOTE: The following endpoint groups have been extracted to separate files
    // in Http/Endpoints/ for better navigability:
    //   - StorageOrganizationEndpoints.cs (catalog, search, health, quality, tiers, sources)
    //   - SymbolManagementEndpoints.cs (bulk import/export, templates, schedules, metadata, search)
    //   - NewFeatureEndpoints.cs (historical query, diagnostics, sample data, config templates)
    //   - CredentialManagementEndpoints.cs (testing, OAuth, provider config, self-healing)
    //   - BulkSymbolManagementEndpoints.cs (import, watchlists, portfolio, batch operations)

    // Previously extracted:
    //   - ScheduledBackfillEndpoints.cs
    //   - PackagingEndpoints.cs
    //   - ArchiveMaintenanceEndpoints.cs

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _app.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _app.StopAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}
