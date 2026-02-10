using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// Import extension methods for DTO to domain conversion
using MarketDataCollector.Ui.Shared;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering configuration API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// </summary>
public static class ConfigEndpoints
{
    /// <summary>
    /// Maps all configuration-related API endpoints.
    /// </summary>
    public static void MapConfigEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Configuration");

        // Get full configuration
        group.MapGet(UiApiRoutes.Config, (ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(new
            {
                dataRoot = cfg.DataRoot,
                compress = cfg.Compress,
                dataSource = cfg.DataSource.ToString(),
                alpaca = cfg.Alpaca,
                storage = cfg.Storage,
                symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
                backfill = cfg.Backfill,
                derivatives = cfg.Derivatives
            }, jsonOptions);
        });

        // Update data source
        group.MapPost(UiApiRoutes.ConfigDataSource, async (ConfigStore store, DataSourceRequest req) =>
        {
            var cfg = store.Load();

            if (!Enum.TryParse<DataSourceKind>(req.DataSource, ignoreCase: true, out var ds))
                return Results.BadRequest("Invalid DataSource. Use 'IB' or 'Alpaca'.");

            var next = cfg with { DataSource = ds };
            await store.SaveAsync(next);

            return Results.Ok();
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Update Alpaca settings
        group.MapPost(UiApiRoutes.ConfigAlpaca, async (ConfigStore store, AlpacaOptionsDto alpaca) =>
        {
            var cfg = store.Load();
            var next = cfg with { Alpaca = alpaca.ToDomain() };
            await store.SaveAsync(next);
            return Results.Ok();
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Update storage settings
        group.MapPost(UiApiRoutes.ConfigStorage, async (ConfigStore store, StorageSettingsRequest req) =>
        {
            var cfg = store.Load();
            var storage = new StorageConfig(
                NamingConvention: req.NamingConvention ?? "BySymbol",
                DatePartition: req.DatePartition ?? "Daily",
                IncludeProvider: req.IncludeProvider,
                FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix,
                Profile: string.IsNullOrWhiteSpace(req.Profile) ? null : req.Profile
            );
            var sanitizedRoot = PathValidation.SanitizeDataRoot(req.DataRoot);
            if (sanitizedRoot is null)
                return Results.BadRequest("Invalid DataRoot: must be a relative path without traversal sequences.");

            var next = cfg with
            {
                DataRoot = sanitizedRoot,
                Compress = req.Compress,
                Storage = storage
            };
            await store.SaveAsync(next);
            return Results.Ok();
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Add or update symbol
        group.MapPost(UiApiRoutes.ConfigSymbols, async (ConfigStore store, SymbolConfig symbol) =>
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
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Delete symbol
        group.MapDelete(UiApiRoutes.ConfigSymbols + "/{symbol}", async (ConfigStore store, string symbol) =>
        {
            var cfg = store.Load();
            var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
            list.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            var next = cfg with { Symbols = list.ToArray() };
            await store.SaveAsync(next);
            return Results.Ok();
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Get derivatives configuration
        group.MapGet(UiApiRoutes.ConfigDerivatives, (ConfigStore store) =>
        {
            var cfg = store.Load();
            return Results.Json(cfg.Derivatives ?? new Application.Config.DerivativesConfig(), jsonOptions);
        });

        // Update derivatives configuration
        group.MapPost(UiApiRoutes.ConfigDerivatives, async (ConfigStore store, DerivativesConfigDto derivatives) =>
        {
            var cfg = store.Load();
            var next = cfg with { Derivatives = derivatives.ToDomain() };
            await store.SaveAsync(next);
            return Results.Ok();
        }).RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Get status
        group.MapGet(UiApiRoutes.Status, (ConfigStore store) =>
        {
            var status = store.TryLoadStatusJson();
            return status is null ? Results.NotFound() : Results.Content(status, "application/json");
        });
    }
}
