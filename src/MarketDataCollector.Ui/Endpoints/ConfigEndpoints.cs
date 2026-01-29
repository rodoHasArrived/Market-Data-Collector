using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Contracts.Configuration;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Ui.Endpoints;

/// <summary>
/// Extension methods for registering configuration API endpoints.
/// </summary>
public static class ConfigEndpoints
{
    /// <summary>
    /// Maps all configuration-related API endpoints.
    /// </summary>
    public static void MapConfigEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        // Get full configuration
        app.MapGet("/api/config", (ConfigStore store) =>
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
                backfill = cfg.Backfill
            }, jsonOptions);
        });

        // Update data source
        app.MapPost("/api/config/datasource", async (ConfigStore store, DataSourceRequest req) =>
        {
            var cfg = store.Load();

            if (!Enum.TryParse<DataSourceKind>(req.DataSource, ignoreCase: true, out var ds))
                return Results.BadRequest("Invalid DataSource. Use 'IB' or 'Alpaca'.");

            var next = cfg with { DataSource = ds };
            await store.SaveAsync(next);

            return Results.Ok();
        });

        // Update Alpaca settings
        app.MapPost("/api/config/alpaca", async (ConfigStore store, AlpacaOptionsDto alpaca) =>
        {
            var cfg = store.Load();
            var next = cfg with { Alpaca = alpaca.ToDomain() };
            await store.SaveAsync(next);
            return Results.Ok();
        });

        // Update storage settings
        app.MapPost("/api/config/storage", async (ConfigStore store, StorageSettingsRequest req) =>
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
        });

        // Add or update symbol
        app.MapPost("/api/config/symbols", async (ConfigStore store, SymbolConfig symbol) =>
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
        });

        // Delete symbol
        app.MapDelete("/api/config/symbols/{symbol}", async (ConfigStore store, string symbol) =>
        {
            var cfg = store.Load();
            var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
            list.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            var next = cfg with { Symbols = list.ToArray() };
            await store.SaveAsync(next);
            return Results.Ok();
        });

        // Get status
        app.MapGet("/api/status", (ConfigStore store) =>
        {
            var status = store.TryLoadStatusJson();
            return status is null ? Results.NotFound() : Results.Content(status, "application/json");
        });
    }
}
