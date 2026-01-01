using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Domain.Collectors;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Lightweight HTTP server exposing runtime status, metrics (Prometheus format), and a minimal HTML dashboard.
/// Avoids pulling in ASP.NET for small deployments.
/// </summary>
public sealed class StatusHttpServer : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<StatusHttpServer>();
    private readonly HttpListener _listener = new();
    private readonly Func<MetricsSnapshot> _metricsProvider;
    private readonly Func<PipelineStatistics> _pipelineProvider;
    private readonly Func<IReadOnlyList<DepthIntegrityEvent>> _integrityProvider;
    private readonly CancellationTokenSource _cts = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private Task? _loop;

    // Health check thresholds
    private const double HighDropRateThreshold = 5.0; // 5% drop rate is concerning
    private const double CriticalDropRateThreshold = 20.0; // 20% drop rate is critical
    private const int StaleDataThresholdSeconds = 60; // No events for 60s is concerning

    public StatusHttpServer(int port,
        Func<MetricsSnapshot> metricsProvider,
        Func<PipelineStatistics> pipelineProvider,
        Func<IReadOnlyList<DepthIntegrityEvent>> integrityProvider)
    {
        _metricsProvider = metricsProvider;
        _pipelineProvider = pipelineProvider;
        _integrityProvider = integrityProvider;
        _listener.Prefixes.Add($"http://*:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(HandleAsync);
        _log.Information("StatusHttpServer started");
    }

    private async Task HandleAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (HttpListenerException) when (_cts.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }

            _ = Task.Run(() => HandleRequestAsync(ctx), _cts.Token);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath?.Trim('/')?.ToLowerInvariant() ?? string.Empty;
            switch (path)
            {
                case "health":
                case "healthz":
                    await WriteHealthCheckAsync(ctx.Response);
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
        sb.AppendLine("# HELP mdc_events_per_second Current event rate");
        sb.AppendLine("# TYPE mdc_events_per_second gauge");
        sb.AppendLine($"mdc_events_per_second {m.EventsPerSecond:F4}");
        sb.AppendLine("# HELP mdc_drop_rate Drop rate percent");
        sb.AppendLine("# TYPE mdc_drop_rate gauge");
        sb.AppendLine($"mdc_drop_rate {m.DropRate:F4}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    private Task WriteStatusAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "application/json";
        var payload = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            metrics = _metricsProvider(),
            pipeline = _pipelineProvider(),
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

    private Task WriteDashboardAsync(HttpListenerResponse resp)
    {
        resp.ContentType = "text/html";
        var html = @"<!doctype html>
<html><head><title>MarketDataCollector Status</title>
<style>body{font-family:Arial;margin:20px;} code{background:#f4f4f4;padding:4px;display:block;}
table{border-collapse:collapse;} td,th{border:1px solid #ccc;padding:4px 8px;}</style></head>
<body>
<h2>MarketDataCollector Status</h2>
<p><a href='/metrics'>Prometheus metrics</a> | <a href='/status'>JSON status</a></p>
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
  row.innerHTML=`<td>${ev.timestamp}</td><td>${ev.symbol}</td><td>${ev.kind}</td><td>${ev.description||''}</td>`;
  tbody.appendChild(row);
 });
}
setInterval(refresh,2000);refresh();
</script>
</body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        return resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Close();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch { }
        }
        _cts.Dispose();
    }
}
