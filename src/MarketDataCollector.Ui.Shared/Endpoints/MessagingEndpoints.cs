using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering messaging and notification API endpoints.
/// </summary>
public static class MessagingEndpoints
{
    public static void MapMessagingEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Messaging");

        // Messaging config
        group.MapGet(UiApiRoutes.MessagingConfig, () =>
        {
            return Results.Json(new
            {
                enabled = false,
                channels = new[]
                {
                    new { name = "webhook", enabled = false, description = "HTTP webhook notifications" },
                    new { name = "email", enabled = false, description = "Email notifications (SMTP)" },
                    new { name = "slack", enabled = false, description = "Slack integration" }
                },
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingConfig")
        .Produces(200);

        // Messaging status
        group.MapGet(UiApiRoutes.MessagingStatus, () =>
        {
            return Results.Json(new
            {
                running = false,
                queued = 0,
                delivered = 0,
                failed = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingStatus")
        .Produces(200);

        // Messaging stats
        group.MapGet(UiApiRoutes.MessagingStats, () =>
        {
            return Results.Json(new
            {
                totalSent = 0,
                totalFailed = 0,
                totalQueued = 0,
                averageDeliveryMs = 0,
                byChannel = new Dictionary<string, int>(),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingStats")
        .Produces(200);

        // Messaging activity
        group.MapGet(UiApiRoutes.MessagingActivity, (int? limit) =>
        {
            return Results.Json(new
            {
                activity = Array.Empty<object>(),
                total = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingActivity")
        .Produces(200);

        // Messaging consumers
        group.MapGet(UiApiRoutes.MessagingConsumers, () =>
        {
            return Results.Json(new
            {
                consumers = Array.Empty<object>(),
                total = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingConsumers")
        .Produces(200);

        // Messaging endpoints list
        group.MapGet(UiApiRoutes.MessagingEndpoints, () =>
        {
            return Results.Json(new
            {
                endpoints = Array.Empty<object>(),
                total = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingEndpointsList")
        .Produces(200);

        // Test messaging
        group.MapPost(UiApiRoutes.MessagingTest, (MessagingTestRequest? req) =>
        {
            return Results.Json(new
            {
                success = false,
                channel = req?.Channel ?? "webhook",
                message = "Messaging channels are not configured. Set up webhook/email/slack in configuration.",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("TestMessaging")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Publishing stats
        group.MapGet(UiApiRoutes.MessagingPublishing, () =>
        {
            return Results.Json(new
            {
                isPublishing = false,
                messagesPublished = 0,
                messagesFailed = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingPublishing")
        .Produces(200);

        // Purge queue
        group.MapPost(UiApiRoutes.MessagingQueuePurge, (string queueName) =>
        {
            return Results.Json(new
            {
                purged = true,
                queueName,
                messagesRemoved = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("PurgeMessagingQueue")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Messaging errors
        group.MapGet(UiApiRoutes.MessagingErrors, (int? limit) =>
        {
            return Results.Json(new
            {
                errors = Array.Empty<object>(),
                total = 0,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetMessagingErrors")
        .Produces(200);

        // Retry failed message
        group.MapPost(UiApiRoutes.MessagingErrorRetry, (string messageId) =>
        {
            return Results.Json(new
            {
                messageId,
                retried = false,
                message = "Message not found in error queue",
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("RetryMessagingError")
        .Produces(200)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);
    }

    private sealed record MessagingTestRequest(string? Channel, string? Target, string? Message);
}
