using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketDataCollector.Application.UI;

public sealed class UiServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _configPath;

    public UiServer(string configPath, int port = 8080)
    {
        _configPath = configPath;

        var builder = WebApplication.CreateBuilder();

        // Minimize logging from ASP.NET Core
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);

        builder.WebHost.UseUrls($"http://localhost:{port}");

        var store = new ConfigStore(configPath);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<BackfillCoordinator>();

        _app = builder.Build();

        ConfigureRoutes();
    }

    private void ConfigureRoutes()
    {
        _app.MapGet("/", (ConfigStore store) =>
        {
            var html = HtmlTemplates.Index(
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
                compress = cfg.Compress,
                dataSource = cfg.DataSource.ToString(),
                alpaca = cfg.Alpaca,
                storage = cfg.Storage,
                symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
                backfill = cfg.Backfill
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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
                return Results.Problem($"Failed to update data source: {ex.Message}");
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
                return Results.Problem($"Failed to save Alpaca settings: {ex.Message}");
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
                    FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix
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
                return Results.Problem($"Failed to save storage settings: {ex.Message}");
            }
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
                return Results.Problem($"Failed to add symbol: {ex.Message}");
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
                return Results.Problem($"Failed to delete symbol: {ex.Message}");
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
                return Results.Problem($"Failed to load status: {ex.Message}");
            }
        });

        _app.MapGet("/api/backfill/providers", (BackfillCoordinator backfill) =>
        {
            try
            {
                var providers = backfill.DescribeProviders();
                return Results.Json(providers, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get providers: {ex.Message}");
            }
        });

        _app.MapGet("/api/backfill/status", (BackfillCoordinator backfill) =>
        {
            try
            {
                var status = backfill.TryReadLast();
                return status is null
                    ? Results.NotFound()
                    : Results.Json(status, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to load backfill status: {ex.Message}");
            }
        });

        _app.MapPost("/api/backfill/run", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            try
            {
                if (req.Symbols is null || req.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request);
                return Results.Json(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Backfill failed: {ex.Message}");
            }
        });
    }

    public async Task StartAsync()
    {
        await _app.StartAsync();
    }

    public async Task StopAsync()
    {
        await _app.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}

public record DataSourceRequest(string DataSource);
public record StorageSettingsRequest(string? DataRoot, bool Compress, string? NamingConvention, string? DatePartition, bool IncludeProvider, string? FilePrefix);
public record BackfillRequestDto(string? Provider, string[] Symbols, DateOnly? From, DateOnly? To);
