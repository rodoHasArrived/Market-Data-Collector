using System.Text.Json;
using MarketDataCollector.Application.Subscriptions.Models;
using MarketDataCollector.Application.Subscriptions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Application.UI;

/// <summary>
/// HTTP API endpoints for bulk symbol management: text/CSV import, watchlists,
/// portfolio import, and batch operations (add, delete, toggle, update, copy settings,
/// move to watchlist, filter).
/// Extracted from UiServer.ConfigureBulkSymbolManagementRoutes().
/// </summary>
public static class BulkSymbolManagementEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapBulkSymbolManagementEndpoints(this WebApplication app)
    {
        // ==================== TEXT/CSV IMPORT ====================

        app.MapPost("/api/symbols/import/text", async (
            SymbolImportExportService importExport,
            HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var content = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(content))
                    return Results.BadRequest("Content is required.");

                var options = new BulkImportOptions(
                    SkipExisting: request.Query["skipExisting"] != "false",
                    UpdateExisting: request.Query["updateExisting"] == "true",
                    HasHeader: false,
                    ValidateSymbols: request.Query["validate"] != "false"
                );

                var result = await importExport.ImportFromTextAsync(content, options);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Text import failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/import/auto", async (
            SymbolImportExportService importExport,
            HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var content = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(content))
                    return Results.BadRequest("Content is required.");

                var options = new BulkImportOptions(
                    SkipExisting: request.Query["skipExisting"] != "false",
                    UpdateExisting: request.Query["updateExisting"] == "true",
                    ValidateSymbols: request.Query["validate"] != "false"
                );

                var result = await importExport.ImportAutoDetectAsync(content, options);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Import failed: {ex.Message}");
            }
        });

        // ==================== WATCHLISTS ====================

        app.MapGet("/api/watchlists", async (WatchlistService watchlists) =>
        {
            try
            {
                var all = await watchlists.GetAllWatchlistsAsync();
                return Results.Json(all, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get watchlists: {ex.Message}");
            }
        });

        app.MapGet("/api/watchlists/summaries", async (WatchlistService watchlists) =>
        {
            try
            {
                var summaries = await watchlists.GetWatchlistSummariesAsync();
                return Results.Json(summaries, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get watchlist summaries: {ex.Message}");
            }
        });

        app.MapGet("/api/watchlists/{watchlistId}", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var watchlist = await watchlists.GetWatchlistAsync(watchlistId);
                return watchlist is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Json(watchlist, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get watchlist: {ex.Message}");
            }
        });

        app.MapPost("/api/watchlists", async (WatchlistService watchlists, CreateWatchlistRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Watchlist name is required.");

                var watchlist = await watchlists.CreateWatchlistAsync(request);
                return Results.Json(watchlist, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create watchlist: {ex.Message}");
            }
        });

        app.MapPut("/api/watchlists/{watchlistId}", async (
            WatchlistService watchlists,
            string watchlistId,
            UpdateWatchlistRequest request) =>
        {
            try
            {
                var updated = await watchlists.UpdateWatchlistAsync(watchlistId, request);
                return updated is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Json(updated, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update watchlist: {ex.Message}");
            }
        });

        app.MapDelete("/api/watchlists/{watchlistId}", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var deleted = await watchlists.DeleteWatchlistAsync(watchlistId);
                return deleted ? Results.Ok() : Results.NotFound($"Watchlist '{watchlistId}' not found");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete watchlist: {ex.Message}");
            }
        });

        app.MapPost("/api/watchlists/{watchlistId}/symbols", async (
            WatchlistService watchlists,
            string watchlistId,
            WatchlistSymbolsRequest request) =>
        {
            try
            {
                var result = await watchlists.AddSymbolsAsync(new AddSymbolsToWatchlistRequest(
                    WatchlistId: watchlistId,
                    Symbols: request.Symbols,
                    SubscribeImmediately: request.SubscribeImmediately
                ));
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to add symbols: {ex.Message}");
            }
        });

        app.MapDelete("/api/watchlists/{watchlistId}/symbols", async (
            WatchlistService watchlists,
            string watchlistId,
            WatchlistSymbolsRequest request) =>
        {
            try
            {
                var result = await watchlists.RemoveSymbolsAsync(new RemoveSymbolsFromWatchlistRequest(
                    WatchlistId: watchlistId,
                    Symbols: request.Symbols,
                    UnsubscribeIfOrphaned: request.UnsubscribeIfOrphaned
                ));
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to remove symbols: {ex.Message}");
            }
        });

        app.MapPost("/api/watchlists/{watchlistId}/subscribe", async (
            WatchlistService watchlists,
            string watchlistId,
            WatchlistSubscriptionRequest? request) =>
        {
            try
            {
                var result = await watchlists.SubscribeWatchlistAsync(new WatchlistSubscriptionRequest(
                    WatchlistId: watchlistId,
                    SubscribeTrades: request?.SubscribeTrades,
                    SubscribeDepth: request?.SubscribeDepth,
                    DepthLevels: request?.DepthLevels
                ));
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to subscribe watchlist: {ex.Message}");
            }
        });

        app.MapPost("/api/watchlists/{watchlistId}/unsubscribe", async (
            WatchlistService watchlists,
            string watchlistId) =>
        {
            try
            {
                var result = await watchlists.UnsubscribeWatchlistAsync(watchlistId);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to unsubscribe watchlist: {ex.Message}");
            }
        });

        app.MapGet("/api/watchlists/{watchlistId}/export", async (WatchlistService watchlists, string watchlistId) =>
        {
            try
            {
                var json = await watchlists.ExportWatchlistAsync(watchlistId);
                return json is null
                    ? Results.NotFound($"Watchlist '{watchlistId}' not found")
                    : Results.Text(json, "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to export watchlist: {ex.Message}");
            }
        });

        app.MapPost("/api/watchlists/import", async (WatchlistService watchlists, HttpRequest request) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var json = await reader.ReadToEndAsync();

                var watchlist = await watchlists.ImportWatchlistAsync(json);
                return watchlist is null
                    ? Results.BadRequest("Invalid watchlist JSON")
                    : Results.Json(watchlist, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to import watchlist: {ex.Message}");
            }
        });

        app.MapPost("/api/watchlists/reorder", async (WatchlistService watchlists, string[] watchlistIds) =>
        {
            try
            {
                await watchlists.ReorderWatchlistsAsync(watchlistIds);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to reorder watchlists: {ex.Message}");
            }
        });

        // ==================== PORTFOLIO IMPORT ====================

        app.MapGet("/api/portfolio/brokers", (PortfolioImportService portfolio) =>
        {
            try
            {
                var brokers = portfolio.GetAvailableBrokers();
                return Results.Json(brokers, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get brokers: {ex.Message}");
            }
        });

        app.MapGet("/api/portfolio/{broker}/summary", async (PortfolioImportService portfolio, string broker) =>
        {
            try
            {
                var summary = await portfolio.GetPortfolioSummaryAsync(broker);
                return summary is null
                    ? Results.NotFound($"Broker '{broker}' not configured or not available")
                    : Results.Json(summary, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get portfolio summary: {ex.Message}");
            }
        });

        app.MapPost("/api/portfolio/{broker}/import", async (
            PortfolioImportService portfolio,
            string broker,
            PortfolioImportOptionsDto? options) =>
        {
            try
            {
                var importOptions = new PortfolioImportOptions(
                    MinPositionValue: options?.MinPositionValue,
                    MinQuantity: options?.MinQuantity,
                    AssetClasses: options?.AssetClasses,
                    ExcludeSymbols: options?.ExcludeSymbols,
                    LongOnly: options?.LongOnly ?? false,
                    CreateWatchlist: options?.CreateWatchlist ?? false,
                    WatchlistName: options?.WatchlistName,
                    SubscribeTrades: options?.SubscribeTrades ?? true,
                    SubscribeDepth: options?.SubscribeDepth ?? true,
                    SkipExisting: options?.SkipExisting ?? true
                );

                var result = await portfolio.ImportFromBrokerAsync(new PortfolioImportRequest(broker, importOptions));
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to import from portfolio: {ex.Message}");
            }
        });

        app.MapPost("/api/portfolio/manual/import", async (
            PortfolioImportService portfolio,
            ManualPortfolioImportRequest request) =>
        {
            try
            {
                if (request.Entries is null || request.Entries.Length == 0)
                    return Results.BadRequest("At least one entry is required.");

                var entries = request.Entries.Select(e => new ManualPortfolioEntry(
                    Symbol: e.Symbol,
                    Quantity: e.Quantity,
                    AssetClass: e.AssetClass
                )).ToArray();

                var importOptions = new PortfolioImportOptions(
                    CreateWatchlist: request.CreateWatchlist,
                    WatchlistName: request.WatchlistName,
                    SubscribeTrades: request.SubscribeTrades,
                    SubscribeDepth: request.SubscribeDepth,
                    SkipExisting: request.SkipExisting
                );

                var result = await portfolio.ImportManualAsync(entries, importOptions);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to import manual portfolio: {ex.Message}");
            }
        });

        // ==================== BATCH OPERATIONS ====================

        app.MapPost("/api/symbols/batch/add", async (BatchOperationsService batch, BatchAddRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.AddSymbolsAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch add failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/delete", async (BatchOperationsService batch, BatchDeleteRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DeleteSymbolsAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch delete failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/toggle", async (BatchOperationsService batch, BatchToggleRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.ToggleSubscriptionsAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch toggle failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/update", async (BatchOperationsService batch, BatchUpdateRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.UpdateSymbolsAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Batch update failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/enable-trades", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.EnableTradesAsync(symbols);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Enable trades failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/disable-trades", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DisableTradesAsync(symbols);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Disable trades failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/enable-depth", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.EnableDepthAsync(symbols);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Enable depth failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/disable-depth", async (BatchOperationsService batch, string[] symbols) =>
        {
            try
            {
                if (symbols is null || symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                var result = await batch.DisableDepthAsync(symbols);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Disable depth failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/copy-settings", async (
            BatchOperationsService batch,
            BatchCopySettingsRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.SourceSymbol))
                    return Results.BadRequest("Source symbol is required.");

                if (request.TargetSymbols is null || request.TargetSymbols.Length == 0)
                    return Results.BadRequest("At least one target symbol is required.");

                var result = await batch.CopySettingsAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Copy settings failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/move-to-watchlist", async (
            BatchOperationsService batch,
            BatchMoveToWatchlistRequest request) =>
        {
            try
            {
                if (request.Symbols is null || request.Symbols.Length == 0)
                    return Results.BadRequest("At least one symbol is required.");

                if (string.IsNullOrWhiteSpace(request.TargetWatchlistId))
                    return Results.BadRequest("Target watchlist ID is required.");

                var result = await batch.MoveToWatchlistAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Move to watchlist failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/filter", async (BatchOperationsService batch, BatchFilter filter) =>
        {
            try
            {
                var symbols = await batch.GetFilteredSymbolsAsync(filter);
                return Results.Json(symbols, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Filter failed: {ex.Message}");
            }
        });

        app.MapPost("/api/symbols/batch/filtered-operation", async (
            BatchOperationsService batch,
            BatchFilteredOperationRequest request) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Operation))
                    return Results.BadRequest("Operation is required.");

                var result = await batch.PerformFilteredOperationAsync(request);
                return Results.Json(result, JsonOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Filtered operation failed: {ex.Message}");
            }
        });
    }
}
