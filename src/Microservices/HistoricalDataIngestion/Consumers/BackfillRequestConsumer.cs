using DataIngestion.Contracts.Messages;
using DataIngestion.Contracts.Services;
using DataIngestion.HistoricalService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.HistoricalService.Consumers;

public sealed class BackfillRequestConsumer : IConsumer<IRequestHistoricalBackfill>
{
    private readonly IBackfillJobManager _jobManager;
    private readonly Serilog.ILogger _log = Log.ForContext<BackfillRequestConsumer>();

    public BackfillRequestConsumer(IBackfillJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    public Task Consume(ConsumeContext<IRequestHistoricalBackfill> context)
    {
        var msg = context.Message;

        _log.Information("Received backfill request for {Symbol} ({DataType}): {Start} to {End}",
            msg.Symbol, msg.DataType, msg.StartDate, msg.EndDate);

        var request = new BackfillRequest(
            msg.Symbol,
            msg.StartDate,
            msg.EndDate,
            msg.DataType.ToString(),
            msg.Source,
            msg.Exchange,
            msg.Timeframe?.ToString(),
            msg.Priority ?? 0
        );

        _jobManager.CreateJob(request);

        return Task.CompletedTask;
    }
}
