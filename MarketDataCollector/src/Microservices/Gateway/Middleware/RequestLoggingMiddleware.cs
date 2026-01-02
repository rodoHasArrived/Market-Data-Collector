using System.Diagnostics;
using DataIngestion.Gateway.Services;
using Serilog;

namespace DataIngestion.Gateway.Middleware;

/// <summary>
/// Middleware for logging HTTP requests with timing information.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _log = Log.ForContext<RequestLoggingMiddleware>();

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, MetricsCollector metrics)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        try
        {
            await _next(context);
            sw.Stop();

            var statusCode = context.Response.StatusCode;
            metrics.RecordRequest(method, path, statusCode);

            if (statusCode >= 400)
            {
                _log.Warning("{Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                    method, path, statusCode, sw.ElapsedMilliseconds);
            }
            else
            {
                _log.Debug("{Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                    method, path, statusCode, sw.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            metrics.RecordRequest(method, path, 500);
            _log.Error(ex, "{Method} {Path} threw exception after {ElapsedMs}ms",
                method, path, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
