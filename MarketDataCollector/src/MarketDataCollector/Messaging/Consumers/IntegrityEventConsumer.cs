using MassTransit;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Messaging.Contracts;
using Serilog;

namespace MarketDataCollector.Messaging.Consumers;

/// <summary>
/// Consumer for data integrity events. Logs events at appropriate severity levels and updates metrics.
/// </summary>
public sealed class IntegrityEventConsumer : IConsumer<IIntegrityEventOccurred>
{
    private readonly ILogger _log;

    public IntegrityEventConsumer()
    {
        _log = LoggingSetup.ForContext<IntegrityEventConsumer>();
    }

    public Task Consume(ConsumeContext<IIntegrityEventOccurred> context)
    {
        var integrity = context.Message;

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

        return Task.CompletedTask;
    }
}
