using System.Text.Json;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataCollector.Ui.Shared.Endpoints;

/// <summary>
/// Extension methods for registering event replay API endpoints.
/// </summary>
public static class ReplayEndpoints
{
    private static readonly Dictionary<string, ReplaySession> s_sessions = new(StringComparer.OrdinalIgnoreCase);

    public static void MapReplayEndpoints(this WebApplication app, JsonSerializerOptions jsonOptions)
    {
        var group = app.MapGroup("").WithTags("Replay");

        // List replay files
        group.MapGet(UiApiRoutes.ReplayFiles, (string? symbol, [FromServices] StorageOptions? storageOptions) =>
        {
            var rootPath = storageOptions?.RootPath ?? "data";
            var files = new List<object>();

            if (Directory.Exists(rootPath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(rootPath, "*.jsonl*", SearchOption.AllDirectories))
                    {
                        var info = new FileInfo(file);
                        if (symbol != null && !info.Name.Contains(symbol, StringComparison.OrdinalIgnoreCase))
                            continue;

                        files.Add(new
                        {
                            path = file,
                            name = info.Name,
                            sizeBytes = info.Length,
                            lastModified = info.LastWriteTimeUtc
                        });
                    }
                }
                catch { /* ignore access errors */ }
            }

            return Results.Json(new { files = files.Take(500), total = files.Count, timestamp = DateTimeOffset.UtcNow }, jsonOptions);
        })
        .WithName("GetReplayFiles")
        .Produces(200);

        // Start replay
        group.MapPost(UiApiRoutes.ReplayStart, (ReplayStartRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.FilePath))
                return Results.BadRequest(new { error = "File path is required" });

            var sessionId = Guid.NewGuid().ToString("N")[..12];
            var session = new ReplaySession(sessionId, req.FilePath, req.SpeedMultiplier ?? 1.0);
            s_sessions[sessionId] = session;

            return Results.Json(new
            {
                sessionId,
                filePath = req.FilePath,
                status = "started",
                speedMultiplier = session.Speed,
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("StartReplay")
        .Produces(200)
        .Produces(400)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Pause replay
        group.MapPost(UiApiRoutes.ReplayPause, (string sessionId) =>
        {
            if (!s_sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = $"Session '{sessionId}' not found" });

            session.Status = "paused";
            return Results.Json(new { sessionId, status = session.Status }, jsonOptions);
        })
        .WithName("PauseReplay")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Resume replay
        group.MapPost(UiApiRoutes.ReplayResume, (string sessionId) =>
        {
            if (!s_sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = $"Session '{sessionId}' not found" });

            session.Status = "running";
            return Results.Json(new { sessionId, status = session.Status }, jsonOptions);
        })
        .WithName("ResumeReplay")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Stop replay
        group.MapPost(UiApiRoutes.ReplayStop, (string sessionId) =>
        {
            if (!s_sessions.Remove(sessionId, out _))
                return Results.NotFound(new { error = $"Session '{sessionId}' not found" });

            return Results.Json(new { sessionId, status = "stopped" }, jsonOptions);
        })
        .WithName("StopReplay")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Seek replay
        group.MapPost(UiApiRoutes.ReplaySeek, (string sessionId, SeekRequest req) =>
        {
            if (!s_sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = $"Session '{sessionId}' not found" });

            return Results.Json(new { sessionId, positionMs = req.PositionMs, status = session.Status }, jsonOptions);
        })
        .WithName("SeekReplay")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Set replay speed
        group.MapPost(UiApiRoutes.ReplaySpeed, (string sessionId, SpeedRequest req) =>
        {
            if (!s_sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = $"Session '{sessionId}' not found" });

            session.Speed = req.SpeedMultiplier;
            return Results.Json(new { sessionId, speedMultiplier = session.Speed, status = session.Status }, jsonOptions);
        })
        .WithName("SetReplaySpeed")
        .Produces(200)
        .Produces(404)
        .RequireRateLimiting(UiEndpoints.MutationRateLimitPolicy);

        // Get replay status
        group.MapGet(UiApiRoutes.ReplayStatus, (string sessionId) =>
        {
            if (!s_sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = $"Session '{sessionId}' not found" });

            return Results.Json(new
            {
                sessionId,
                filePath = session.FilePath,
                status = session.Status,
                speedMultiplier = session.Speed,
                startedAt = session.StartedAt
            }, jsonOptions);
        })
        .WithName("GetReplayStatus")
        .Produces(200)
        .Produces(404);

        // Preview replay events
        group.MapGet(UiApiRoutes.ReplayPreview, (string? filePath, int? limit) =>
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return Results.Json(new { events = Array.Empty<object>(), error = "File not found or path not provided" }, jsonOptions);

            var events = new List<string>();
            try
            {
                using var reader = new StreamReader(filePath);
                for (int i = 0; i < (limit ?? 10) && !reader.EndOfStream; i++)
                {
                    var line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                        events.Add(line);
                }
            }
            catch { /* ignore read errors */ }

            return Results.Json(new { events, total = events.Count, filePath }, jsonOptions);
        })
        .WithName("PreviewReplayEvents")
        .Produces(200);

        // Replay stats
        group.MapGet(UiApiRoutes.ReplayStats, () =>
        {
            return Results.Json(new
            {
                activeSessions = s_sessions.Count,
                sessions = s_sessions.Values.Select(s => new
                {
                    sessionId = s.SessionId,
                    status = s.Status,
                    filePath = s.FilePath,
                    startedAt = s.StartedAt
                }),
                timestamp = DateTimeOffset.UtcNow
            }, jsonOptions);
        })
        .WithName("GetReplayStats")
        .Produces(200);
    }

    private sealed class ReplaySession(string sessionId, string filePath, double speed)
    {
        public string SessionId { get; } = sessionId;
        public string FilePath { get; } = filePath;
        public string Status { get; set; } = "running";
        public double Speed { get; set; } = speed;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }

    private sealed record ReplayStartRequest(string? FilePath, double? SpeedMultiplier);
    private sealed record SeekRequest(long PositionMs);
    private sealed record SpeedRequest(double SpeedMultiplier);
}
