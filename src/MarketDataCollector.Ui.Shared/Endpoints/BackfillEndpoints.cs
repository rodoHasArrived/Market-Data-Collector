using System.Text.Json;
using System.Text.RegularExpressions;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Ui.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using BackfillRequest = MarketDataCollector.Application.Backfill.BackfillRequest;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering backfill-related API endpoints.
/// Shared between web dashboard and desktop application hosts.
/// </summary>
public static class BackfillEndpoints
{
    // Matches 1-10 uppercase letters, digits, dots, hyphens, or forward slashes (e.g. SPY, AAPL, BRK.B, BTC/USD)
    private static readonly Regex SymbolPattern = new(@"^[A-Z0-9][A-Z0-9.\-/]{0,19}$", RegexOptions.Compiled);

    /// <summary>
    /// Maps all backfill API endpoints.
    /// </summary>
    public static void MapBackfillEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions, JsonSerializerOptions jsonOptionsIndented)
    {
        // Get available providers
        app.MapGet(UiApiRoutes.BackfillProviders, (BackfillCoordinator backfill) =>
        {
            var providers = backfill.DescribeProviders();
            return Results.Json(providers, jsonOptions);
        });

        // Get last backfill status
        app.MapGet(UiApiRoutes.BackfillStatus, (BackfillCoordinator backfill) =>
        {
            var status = backfill.TryReadLast();
            return status is null
                ? Results.NotFound()
                : Results.Json(status, jsonOptionsIndented);
        });

        // Preview backfill (dry run - shows what would be fetched)
        app.MapPost(UiApiRoutes.BackfillRun + "/preview", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            var validationError = ValidateBackfillRequest(req);
            if (validationError is not null)
                return validationError;

            try
            {
                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var preview = await backfill.PreviewAsync(request);
                return Results.Json(preview, jsonOptionsIndented);
            }
            catch (Exception ex)
            {
                return Results.Json(
                    ErrorResponse.Validation(SanitizeErrorMessage(ex.Message)),
                    statusCode: 400);
            }
        });

        // Run backfill
        app.MapPost(UiApiRoutes.BackfillRun, async (BackfillCoordinator backfill, BackfillRequestDto req) =>
        {
            var validationError = ValidateBackfillRequest(req);
            if (validationError is not null)
                return validationError;

            try
            {
                var request = new BackfillRequest(
                    string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
                    req.Symbols,
                    req.From,
                    req.To);

                var result = await backfill.RunAsync(request);
                return Results.Json(result, jsonOptionsIndented);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(
                    ErrorResponse.Validation(SanitizeErrorMessage(ex.Message)),
                    statusCode: 400);
            }
            catch (Exception)
            {
                return Results.Json(
                    ErrorResponse.InternalError("An unexpected error occurred while running the backfill."),
                    statusCode: 500);
            }
        });
    }

    private static IResult? ValidateBackfillRequest(BackfillRequestDto req)
    {
        var fieldErrors = new List<FieldError>();

        if (req.Symbols is null || req.Symbols.Length == 0)
        {
            fieldErrors.Add(new FieldError("symbols", "At least one symbol is required."));
        }
        else
        {
            foreach (var symbol in req.Symbols)
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    fieldErrors.Add(new FieldError("symbols", "Symbol entries must not be empty."));
                    break;
                }

                if (!SymbolPattern.IsMatch(symbol.ToUpperInvariant()))
                {
                    fieldErrors.Add(new FieldError("symbols",
                        $"Invalid symbol format: '{symbol}'. Symbols must be 1-20 uppercase alphanumeric characters, dots, hyphens, or forward slashes.",
                        AttemptedValue: symbol));
                }
            }
        }

        if (req.From.HasValue && req.To.HasValue && req.From.Value > req.To.Value)
        {
            fieldErrors.Add(new FieldError("from",
                $"'From' date ({req.From.Value}) must not be after 'To' date ({req.To.Value}).",
                AttemptedValue: req.From.Value.ToString("yyyy-MM-dd")));
        }

        if (fieldErrors.Count > 0)
        {
            return Results.Json(
                ErrorResponse.Validation("One or more validation errors occurred.", fieldErrors),
                statusCode: 400);
        }

        return null;
    }

    /// <summary>
    /// Sanitizes exception messages to avoid leaking internal details like file paths or stack traces.
    /// </summary>
    internal static string SanitizeErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "An error occurred.";

        // Strip file system paths (Unix and Windows)
        var sanitized = Regex.Replace(message, @"[A-Za-z]:\\[^\s""']+|/(?:home|usr|var|tmp|etc|opt|root|mnt|data)[^\s""']*", "[path]");

        // Strip stack trace fragments
        sanitized = Regex.Replace(sanitized, @"\s+at\s+\S+\.\S+\(.*?\)", string.Empty);

        // Truncate overly long messages
        if (sanitized.Length > 500)
            sanitized = sanitized[..500] + "...";

        return sanitized;
    }
}
