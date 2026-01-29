using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Models;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Lightweight HTTP server exposing runtime status, metrics (Prometheus format), and a minimal HTML dashboard.
/// Avoids pulling in ASP.NET for small deployments.
/// Enhanced with detailed health check (QW-32), backpressure status (MON-18), and provider latency (PROV-11).
/// </summary>
public sealed class StatusHttpServer : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<StatusHttpServer>();
    private readonly HttpListener _listener = new();
    private readonly Func<MetricsSnapshot> _metricsProvider;
    private readonly Func<PipelineStatistics> _pipelineProvider;
    private readonly Func<IReadOnlyList<DepthIntegrityEvent>> _integrityProvider;
    private readonly Func<ErrorRingBuffer?> _errorBufferProvider;
    private readonly SemaphoreSlim _requestLimiter;
    private readonly string? _accessToken;
    private readonly bool _requireRemoteAuth;
    private readonly CancellationTokenSource _cts = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private Task? _loop;

    // Optional providers for extended functionality (QW-32, MON-18, PROV-11)
    private Func<Task<DetailedHealthReport>>? _detailedHealthProvider;
    private Func<BackpressureStatus>? _backpressureProvider;
    private Func<ProviderLatencySummary>? _providerLatencyProvider;
    private Func<ConnectionHealthSnapshot>? _connectionHealthProvider;

    // Health check thresholds
    private const double HighDropRateThreshold = 5.0; // 5% drop rate is concerning
    private const double CriticalDropRateThreshold = 20.0; // 20% drop rate is critical
    private const int StaleDataThresholdSeconds = 60; // No events for 60s is concerning

    public StatusHttpServer(int port,
        Func<MetricsSnapshot> metricsProvider,
        Func<PipelineStatistics> pipelineProvider,
        Func<IReadOnlyList<DepthIntegrityEvent>> integrityProvider,
        Func<ErrorRingBuffer?>? errorBufferProvider = null,
        string bindAddress = "localhost",
        bool allowRemoteAccess = false,
        string? accessToken = null,
        int maxConcurrentRequests = 16)
    {
        _metricsProvider = metricsProvider;
        _pipelineProvider = pipelineProvider;
        _integrityProvider = integrityProvider;
        _errorBufferProvider = errorBufferProvider ?? (() => null);
        _accessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken;
        _requireRemoteAuth = allowRemoteAccess;
        _requestLimiter = new SemaphoreSlim(Math.Max(1, maxConcurrentRequests));

        var resolvedBindAddress = ResolveBindAddress(bindAddress, allowRemoteAccess);
        if (!allowRemoteAccess && !string.Equals(resolvedBindAddress, bindAddress, StringComparison.OrdinalIgnoreCase))
        {
            _log.Warning("Remote bind address '{BindAddress}' ignored; binding to localhost only.", bindAddress);
        }
        if (allowRemoteAccess && string.IsNullOrWhiteSpace(_accessToken))
        {
            _log.Warning("Remote status access enabled without an access token; remote requests will be rejected.");
        }
        _listener.Prefixes.Add($"http://{resolvedBindAddress}:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(HandleAsync);
        _log.Information("StatusHttpServer started");
    }

    /// <summary>
    /// Registers extended providers for detailed health, backpressure, and provider latency endpoints.
    /// </summary>
    public void RegisterExtendedProviders(
        Func<Task<DetailedHealthReport>>? detailedHealth = null,
        Func<BackpressureStatus>? backpressure = null,
        Func<ProviderLatencySummary>? providerLatency = null,
        Func<ConnectionHealthSnapshot>? connectionHealth = null)
    {
        _detailedHealthProvider = detailedHealth;
        _backpressureProvider = backpressure;
        _providerLatencyProvider = providerLatency;
        _connectionHealthProvider = connectionHealth;
        _log.Debug("Extended providers registered for StatusHttpServer");
    }

    private async Task HandleAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (HttpListenerException) when (_cts.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }

            try
            {
                await _requestLimiter.WaitAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleRequestAsync(ctx);
                }
                finally
                {
                    _requestLimiter.Release();
                }
            }, _cts.Token);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        try
        {
            if (!IsAuthorized(ctx.Request))
            {
                await WriteUnauthorizedAsync(ctx.Response);
                return;
            }

            var path = ctx.Request.Url?.AbsolutePath?.Trim('/')?.ToLowerInvariant() ?? string.Empty;

            // Support both /api/* and /* routes for UWP desktop app compatibility
            if (path.StartsWith("api/"))
                path = path.Substring(4);

            switch (path)
            {
                case "health":
                case "healthz":
                    await WriteHealthCheckAsync(ctx.Response);
                    break;
                case "health/detailed":
                    await WriteDetailedHealthAsync(ctx.Response);
                    break;
                case "ready":
                case "readyz":
                    await WriteReadinessAsync(ctx.Response);
                    break;
                case "live":
                case "livez":
                    await WriteLivenessAsync(ctx.Response);
                    break;
                case "metrics":
                    await WriteMetricsAsync(ctx.Response);
                    break;
                case "status":
                    await WriteStatusAsync(ctx.Response);
                    break;
                case "errors":
                    await WriteErrorsAsync(ctx.Response, ctx.Request.QueryString);
                    break;
                case "backpressure":
                    await WriteBackpressureAsync(ctx.Response);
                    break;
                case "providers/latency":
                    await WriteProviderLatencyAsync(ctx.Response);
                    break;
                case "connections":
                    await WriteConnectionHealthAsync(ctx.Response);
                    break;
                case "backfill/providers":
                    await WriteBackfillProvidersAsync(ctx.Response);
                    break;
                case "backfill/status":
                    await WriteBackfillStatusAsync(ctx.Response);
                    break;
                default:
                    await WriteDashboardAsync(ctx.Response);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error handling HTTP request to {Path}", ctx.Request.Url?.AbsolutePath);
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    /// <summary>
    /// Comprehensive health check endpoint.
    /// Returns 200 OK if healthy, 503 if degraded, with detailed status.
    /// </summary>
    private Task WriteHealthCheckAsync(HttpListenerResponse resp)
    {
        var metrics = _metricsProvider();
        var pipeline = _pipelineProvider();
        var integrity = _integrityProvider();

        var checks = new List<HealthCheckResult>();
        var overallStatus = HealthStatus.Healthy;

        // Check 1: Drop rate
        if (metrics.DropRate >= CriticalDropRateThreshold)
        {
            checks.Add(new HealthCheckResult("drop_rate", HealthStatus.Unhealthy,
                $"Critical drop rate: {metrics.DropRate:F2}%. Events are being lost."));
            overallStatus = HealthStatus.Unhealthy;
        }
        else if (metrics.DropRate >= HighDropRateThreshold)
        {
            checks.Add(new HealthCheckResult("drop_rate", HealthStatus.Degraded,
                $"Elevated drop rate: {metrics.DropRate:F2}%. Consider reducing load."));
            if (overallStatus == HealthStatus.Healthy) overallStatus = HealthStatus.Degraded;
        }
        else
        {
            checks.Add(new HealthCheckResult("drop_rate", HealthStatus.Healthy,
                $"Drop rate: {metrics.DropRate:F2}%"));
        }

        // Check 2: Queue utilization
        if (pipeline.QueueUtilization > 90)
        {
            checks.Add(new HealthCheckResult("queue", HealthStatus.Unhealthy,
                $"Queue near capacity: {pipeline.QueueUtilization:F1}%"));
            overallStatus = HealthStatus.Unhealthy;
        }
        else if (pipeline.QueueUtilization > 70)
        {
            checks.Add(new HealthCheckResult("queue", HealthStatus.Degraded,
                $"Queue filling: {pipeline.QueueUtilization:F1}%"));
            if (overallStatus == HealthStatus.Healthy) overallStatus = HealthStatus.Degraded;
        }
        else
        {
            checks.Add(new HealthCheckResult("queue", HealthStatus.Healthy,
                $"Queue utilization: {pipeline.QueueUtilization:F1}%"));
        }

        // Check 3: Data freshness
        if (metrics.Published > 0 && pipeline.TimeSinceLastFlush.TotalSeconds > StaleDataThresholdSeconds)
        {
            checks.Add(new HealthCheckResult("data_freshness", HealthStatus.Degraded,
                $"No events for {pipeline.TimeSinceLastFlush.TotalSeconds:F0}s"));
            if (overallStatus == HealthStatus.Healthy) overallStatus = HealthStatus.Degraded;
        }
        else
        {
            checks.Add(new HealthCheckResult("data_freshness", HealthStatus.Healthy,
                $"Last flush: {pipeline.TimeSinceLastFlush.TotalSeconds:F0}s ago"));
        }

        // Check 4: Recent integrity issues
        var recentIntegrity = integrity.Count(e => e.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-5));
        if (recentIntegrity > 10)
        {
            checks.Add(new HealthCheckResult("integrity", HealthStatus.Degraded,
                $"{recentIntegrity} integrity events in last 5 minutes"));
            if (overallStatus == HealthStatus.Healthy) overallStatus = HealthStatus.Degraded;
        }
        else
        {
            checks.Add(new HealthCheckResult("integrity", HealthStatus.Healthy,
                $"{recentIntegrity} integrity events in last 5 minutes"));
        }

        // Check 5: Memory usage
        if (metrics.MemoryUsageMb > 1024) // More than 1GB
        {
            checks.Add(new HealthCheckResult("memory", HealthStatus.Degraded,
                $"High memory usage: {metrics.MemoryUsageMb:F0} MB"));
            if (overallStatus == HealthStatus.Healthy) overallStatus = HealthStatus.Degraded;
        }
        else
        {
            checks.Add(new HealthCheckResult("memory", HealthStatus.Healthy,
                $"Memory usage: {metrics.MemoryUsageMb:F0} MB"));
        }

        var response = new
        {
            status = overallStatus.ToString().ToLowerInvariant(),
            timestamp = DateTimeOffset.UtcNow,
            uptime = DateTimeOffset.UtcNow - _startTime,
            checks = checks.Select(c => new { name = c.Name, status = c.Status.ToString().ToLowerInvariant(), message = c.Message })
        };

        resp.StatusCode = overallStatus switch
        {
            HealthStatus.Healthy => 200,
            HealthStatus.Degraded => 200, // Still 200, but status indicates degraded
            HealthStatus.Unhealthy => 503,
            _ => 200
        };

        resp.ContentType = "application/json";
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Kubernetes-style readiness probe.
    /// Returns 200 if ready to receive traffic, 503 otherwise.
    /// </summary>
    private Task WriteReadinessAsync(HttpListenerResponse resp)
    {
        var pipeline = _pipelineProvider();

        // Ready if queue is not overloaded
        var isReady = pipeline.QueueUtilization < 95;

        resp.StatusCode = isReady ? 200 : 503;
        resp.ContentType = "text/plain";
        var message = isReady ? "ready" : "not ready - queue overloaded";
        var bytes = Encoding.UTF8.GetBytes(message);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Kubernetes-style liveness probe.
    /// Returns 200 if the service is alive.
    /// </summary>
    private Task WriteLivenessAsync(HttpListenerResponse resp)
    {
        resp.StatusCode = 200;
        resp.ContentType = "text/plain";
        var bytes = Encoding.UTF8.GetBytes("alive");
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    private enum HealthStatus { Healthy, Degraded, Unhealthy }

    private record HealthCheckResult(string Name, HealthStatus Status, string Message);

    private Task WriteMetricsAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "text/plain; version=0.0.4";
        var m = _metricsProvider();
        var sb = new StringBuilder();
        sb.AppendLine("# HELP mdc_published Total events published");
        sb.AppendLine("# TYPE mdc_published counter");
        sb.AppendLine($"mdc_published {m.Published}");
        sb.AppendLine("# HELP mdc_dropped Total events dropped");
        sb.AppendLine("# TYPE mdc_dropped counter");
        sb.AppendLine($"mdc_dropped {m.Dropped}");
        sb.AppendLine("# HELP mdc_integrity Integrity events");
        sb.AppendLine("# TYPE mdc_integrity counter");
        sb.AppendLine($"mdc_integrity {m.Integrity}");
        sb.AppendLine("# HELP mdc_trades Trades processed");
        sb.AppendLine("# TYPE mdc_trades counter");
        sb.AppendLine($"mdc_trades {m.Trades}");
        sb.AppendLine("# HELP mdc_depth_updates Depth updates processed");
        sb.AppendLine("# TYPE mdc_depth_updates counter");
        sb.AppendLine($"mdc_depth_updates {m.DepthUpdates}");
        sb.AppendLine("# HELP mdc_quotes Quotes processed");
        sb.AppendLine("# TYPE mdc_quotes counter");
        sb.AppendLine($"mdc_quotes {m.Quotes}");
        sb.AppendLine("# HELP mdc_historical_bars Historical bar events processed");
        sb.AppendLine("# TYPE mdc_historical_bars counter");
        sb.AppendLine($"mdc_historical_bars {m.HistoricalBars}");
        sb.AppendLine("# HELP mdc_events_per_second Current event rate");
        sb.AppendLine("# TYPE mdc_events_per_second gauge");
        sb.AppendLine($"mdc_events_per_second {m.EventsPerSecond:F4}");
        sb.AppendLine("# HELP mdc_drop_rate Drop rate percent");
        sb.AppendLine("# TYPE mdc_drop_rate gauge");
        sb.AppendLine($"mdc_drop_rate {m.DropRate:F4}");
        sb.AppendLine("# HELP mdc_historical_bars_per_second Historical bar rate");
        sb.AppendLine("# TYPE mdc_historical_bars_per_second gauge");
        sb.AppendLine($"mdc_historical_bars_per_second {m.HistoricalBarsPerSecond:F4}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    private Task WriteStatusAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";
        var metrics = _metricsProvider();
        var pipeline = _pipelineProvider();

        // Include isConnected for UWP desktop app compatibility
        var payload = new
        {
            isConnected = true, // Service is running and accepting requests
            timestampUtc = DateTimeOffset.UtcNow,
            metrics = new
            {
                published = metrics.Published,
                dropped = metrics.Dropped,
                integrity = metrics.Integrity,
                historicalBars = metrics.HistoricalBars,
                eventsPerSecond = metrics.EventsPerSecond,
                dropRate = metrics.DropRate
            },
            pipeline = pipeline,
            integrity = _integrityProvider()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Returns list of available backfill providers for UWP app.
    /// </summary>
    private Task WriteBackfillProvidersAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";
        var providers = new[]
        {
            new { name = "alpaca", displayName = "Alpaca Markets", description = "Real-time and historical data with adjustments" },
            new { name = "yahoo", displayName = "Yahoo Finance", description = "Free historical data for most US equities" },
            new { name = "stooq", displayName = "Stooq", description = "Free EOD data for global markets" },
            new { name = "nasdaq", displayName = "Nasdaq Data Link", description = "Historical data (API key may be required)" }
        };

        var json = JsonSerializer.Serialize(providers, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Returns current backfill status for UWP app.
    /// </summary>
    private Task WriteBackfillStatusAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";
        // Return empty status when no backfill is running
        var status = new
        {
            success = true,
            provider = (string?)null,
            symbols = Array.Empty<string>(),
            barsWritten = 0,
            startedUtc = (DateTime?)null,
            completedUtc = (DateTime?)null,
            error = (string?)null
        };

        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    private Task WriteDashboardAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "text/html";
        var html = @"<!doctype html>
<html><head><title>MarketDataCollector Status</title>
<style>body{font-family:Arial;margin:20px;} code{background:#f4f4f4;padding:4px;display:block;}
table{border-collapse:collapse;} td,th{border:1px solid #ccc;padding:4px 8px;}</style></head>
<body>
<h2>MarketDataCollector Status</h2>
<p><a href='/metrics'>Prometheus metrics</a> | <a href='/status'>JSON status</a> | <a href='/errors'>Recent errors</a></p>
<pre id='metrics'>Loading metrics...</pre>
<h3>Recent integrity events</h3>
<table id='integrity'><thead><tr><th>Timestamp</th><th>Symbol</th><th>Kind</th><th>Details</th></tr></thead><tbody></tbody></table>
<script>
async function refresh(){
 const status=await fetch('/status').then(r=>r.json());
 document.getElementById('metrics').textContent=JSON.stringify(status.metrics,null,2);
 const tbody=document.querySelector('#integrity tbody');
 tbody.innerHTML='';
 (status.integrity||[]).forEach(ev=>{
  const row=document.createElement('tr');
  row.innerHTML=`<td>${{ev.timestamp}}</td><td>${{ev.symbol}}</td><td>${{ev.kind}}</td><td>${{ev.description||''}}</td>`;
  tbody.appendChild(row);
 });
}
setInterval(refresh,2000);refresh();
</script>
</body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Returns the last N errors endpoint (QW-58).
    /// Supports query parameters: count (default 10), level (warning/error/critical), symbol
    /// </summary>
    private Task WriteErrorsAsync(HttpListenerResponse resp, System.Collections.Specialized.NameValueCollection queryString)
    {
        resp.ContentType = "application/json";

        var errorBuffer = _errorBufferProvider();
        if (errorBuffer == null)
        {
            var emptyResponse = new
            {
                errors = Array.Empty<object>(),
                stats = new { totalErrors = 0, errorsInLastMinute = 0, errorsInLastHour = 0 },
                message = "Error buffer not configured"
            };

            var emptyJson = JsonSerializer.Serialize(emptyResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            var emptyBytes = Encoding.UTF8.GetBytes(emptyJson);
            return resp.OutputStream.WriteAsync(emptyBytes, 0, emptyBytes.Length);
        }

        // Parse query parameters
        var countStr = queryString["count"];
        var count = 10;
        if (!string.IsNullOrEmpty(countStr) && int.TryParse(countStr, out var parsedCount) && parsedCount > 0)
        {
            count = Math.Min(parsedCount, 100); // Cap at 100
        }

        var levelStr = queryString["level"];
        var symbolFilter = queryString["symbol"];

        IReadOnlyList<ErrorEntry> errors;

        // Apply filters
        if (!string.IsNullOrEmpty(symbolFilter))
        {
            errors = errorBuffer.GetBySymbol(symbolFilter, count);
        }
        else if (!string.IsNullOrEmpty(levelStr) && Enum.TryParse<ErrorLevel>(levelStr, ignoreCase: true, out var level))
        {
            errors = errorBuffer.GetByLevel(level, count);
        }
        else
        {
            errors = errorBuffer.GetRecent(count);
        }

        var stats = errorBuffer.GetStats();

        var response = new
        {
            errors = errors.Select(e => new
            {
                id = e.Id,
                timestamp = e.Timestamp,
                level = e.Level.ToString().ToLowerInvariant(),
                source = e.Source,
                message = e.Message,
                exceptionType = e.ExceptionType,
                context = e.Context,
                symbol = e.Symbol,
                provider = e.Provider
            }),
            stats = new
            {
                totalErrors = stats.TotalErrors,
                errorsInLastMinute = stats.ErrorsInLastMinute,
                errorsInLastHour = stats.ErrorsInLastHour,
                warningCount = stats.WarningCount,
                errorCount = stats.ErrorCount,
                criticalCount = stats.CriticalCount,
                lastErrorTime = stats.LastErrorTime
            }
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Detailed health check endpoint (QW-32).
    /// Returns comprehensive health information including dependencies.
    /// </summary>
    private async Task WriteDetailedHealthAsync(HttpListenerResponse resp)
    {
        if (_detailedHealthProvider == null)
        {
            resp.StatusCode = 501;
            resp.ContentType = "application/json";
            var notImpl = JsonSerializer.Serialize(new { error = "Detailed health check not configured" });
            var bytes = Encoding.UTF8.GetBytes(notImpl);
            await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            return;
        }

        try
        {
            var report = await _detailedHealthProvider();

            resp.StatusCode = report.Status switch
            {
                DetailedHealthStatus.Healthy => 200,
                DetailedHealthStatus.Degraded => 200,
                DetailedHealthStatus.Unhealthy => 503,
                _ => 200
            };

            resp.ContentType = "application/json";
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error generating detailed health report");
            resp.StatusCode = 500;
            resp.ContentType = "application/json";
            var error = JsonSerializer.Serialize(new { error = ex.Message });
            var bytes = Encoding.UTF8.GetBytes(error);
            await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        if (request.IsLocal)
        {
            return true;
        }

        if (!_requireRemoteAuth)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return false;
        }

        var headerToken = request.Headers["X-MDC-Status-Token"];
        if (string.IsNullOrWhiteSpace(headerToken))
        {
            var authHeader = request.Headers["Authorization"];
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                headerToken = authHeader.Substring("Bearer ".Length).Trim();
            }
        }

        return string.Equals(headerToken, _accessToken, StringComparison.Ordinal);
    }

    private static string ResolveBindAddress(string bindAddress, bool allowRemoteAccess)
    {
        if (string.IsNullOrWhiteSpace(bindAddress))
        {
            return "localhost";
        }

        if (!allowRemoteAccess && !IsLoopback(bindAddress))
        {
            return "localhost";
        }

        return bindAddress;
    }

    private static bool IsLoopback(string bindAddress)
    {
        return string.Equals(bindAddress, "localhost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(bindAddress, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(bindAddress, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteUnauthorizedAsync(HttpListenerResponse resp)
    {
        resp.StatusCode = 401;
        resp.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error = "Unauthorized" });
        var bytes = Encoding.UTF8.GetBytes(payload);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Backpressure status endpoint (MON-18).
    /// Returns current pipeline backpressure information.
    /// </summary>
    private Task WriteBackpressureAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";

        if (_backpressureProvider == null)
        {
            // Return basic backpressure info from pipeline stats
            var pipeline = _pipelineProvider();
            var dropRate = pipeline.PublishedCount > 0
                ? (double)pipeline.DroppedCount / pipeline.PublishedCount * 100
                : 0;

            var basicStatus = new
            {
                isActive = dropRate > 5 || pipeline.QueueUtilization > 70,
                level = dropRate > 20 || pipeline.QueueUtilization > 90 ? "critical" :
                       dropRate > 5 || pipeline.QueueUtilization > 70 ? "warning" : "none",
                queueUtilization = Math.Round(pipeline.QueueUtilization, 2),
                droppedEvents = pipeline.DroppedCount,
                dropRate = Math.Round(dropRate, 2),
                message = $"Queue: {pipeline.QueueUtilization:F1}%, Drop rate: {dropRate:F2}%"
            };

            var basicJson = JsonSerializer.Serialize(basicStatus, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            var basicBytes = Encoding.UTF8.GetBytes(basicJson);
            return resp.OutputStream.WriteAsync(basicBytes, 0, basicBytes.Length);
        }

        var status = _backpressureProvider();
        var response = new
        {
            isActive = status.IsActive,
            level = status.Level.ToString().ToLowerInvariant(),
            queueUtilization = Math.Round(status.QueueUtilization, 2),
            droppedEvents = status.DroppedEvents,
            dropRate = Math.Round(status.DropRate, 2),
            durationSeconds = status.Duration.TotalSeconds,
            message = status.Message
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Provider latency histogram endpoint (PROV-11).
    /// Returns latency statistics per data provider.
    /// </summary>
    private Task WriteProviderLatencyAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";

        if (_providerLatencyProvider == null)
        {
            var notConfigured = new
            {
                error = "Provider latency tracking not configured",
                providers = Array.Empty<object>()
            };

            var notConfiguredJson = JsonSerializer.Serialize(notConfigured, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            var notConfiguredBytes = Encoding.UTF8.GetBytes(notConfiguredJson);
            return resp.OutputStream.WriteAsync(notConfiguredBytes, 0, notConfiguredBytes.Length);
        }

        var summary = _providerLatencyProvider();

        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Connection health endpoint.
    /// Returns health status of all monitored connections.
    /// </summary>
    private Task WriteConnectionHealthAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";

        if (_connectionHealthProvider == null)
        {
            var notConfigured = new
            {
                error = "Connection health monitoring not configured",
                connections = Array.Empty<object>()
            };

            var notConfiguredJson = JsonSerializer.Serialize(notConfigured, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            var notConfiguredBytes = Encoding.UTF8.GetBytes(notConfiguredJson);
            return resp.OutputStream.WriteAsync(notConfiguredBytes, 0, notConfiguredBytes.Length);
        }

        var snapshot = _connectionHealthProvider();

        var response = new
        {
            timestamp = snapshot.Timestamp,
            totalConnections = snapshot.TotalConnections,
            healthyConnections = snapshot.HealthyConnections,
            unhealthyConnections = snapshot.UnhealthyConnections,
            globalAverageLatencyMs = Math.Round(snapshot.GlobalAverageLatencyMs, 2),
            globalMinLatencyMs = Math.Round(snapshot.GlobalMinLatencyMs, 2),
            globalMaxLatencyMs = Math.Round(snapshot.GlobalMaxLatencyMs, 2),
            connections = snapshot.Connections.Select(c => new
            {
                connectionId = c.ConnectionId,
                providerName = c.ProviderName,
                isConnected = c.IsConnected,
                isHealthy = c.IsHealthy,
                lastHeartbeatTime = c.LastHeartbeatTime,
                missedHeartbeats = c.MissedHeartbeats,
                uptimeSeconds = c.UptimeDuration.TotalSeconds,
                averageLatencyMs = Math.Round(c.AverageLatencyMs, 2)
            })
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Close();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _cts.Dispose();
        _requestLimiter.Dispose();
    }
}
