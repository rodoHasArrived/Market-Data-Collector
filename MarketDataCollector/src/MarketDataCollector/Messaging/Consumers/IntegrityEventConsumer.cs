using MassTransit;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Sample consumer for data integrity events.
/// Extend this to implement custom alerting and monitoring logic.
/// </summary>
public sealed class IntegrityEventConsumer : IConsumer<IIntegrityEventOccurred>
{
    private readonly ILogger _log;

    public IntegrityEventConsumer()
    {
        _log = Log.ForContext<IntegrityEventConsumer>();
    }

    public Task Consume(ConsumeContext<IIntegrityEventOccurred> context)
    {
        var integrity = context.Message;

        // Log at appropriate level based on severity
        var logLevel = integrity.Severity.ToUpperInvariant() switch
        {
            "ERROR" or "CRITICAL" => Serilog.Events.LogEventLevel.Error,
            "WARNING" => Serilog.Events.LogEventLevel.Warning,
            _ => Serilog.Events.LogEventLevel.Information
        };

        _log.Write(
            logLevel,
            "Integrity Event [{Severity}]: {Symbol} - {Description} (Code: {ErrorCode}) @ {Timestamp}",
            integrity.Severity,
            integrity.Symbol,
            integrity.Description,
            integrity.ErrorCode,
            integrity.Timestamp);

        Metrics.IncIntegrity();

        // TODO: Add custom integrity processing logic here
        // Examples:
        // - Send alerts to monitoring systems (PagerDuty, Slack, etc.)
        // - Trigger reconnection logic
        // - Log to audit trail
        // - Update health dashboards

        return Task.CompletedTask;
    }
}
