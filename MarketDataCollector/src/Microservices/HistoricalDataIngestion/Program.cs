using DataIngestion.HistoricalService.Configuration;
using DataIngestion.HistoricalService.Consumers;
using DataIngestion.HistoricalService.Services;
using MassTransit;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "DataIngestion.HistoricalService")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var config = builder.Configuration.GetSection("HistoricalService").Get<HistoricalServiceConfig>()
    ?? new HistoricalServiceConfig();
builder.Services.AddSingleton(config);

builder.Services.AddHealthChecks();
builder.Services.AddControllers();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<BackfillRequestConsumer>();

    var transport = config.MessageBus?.Transport?.ToLowerInvariant() ?? "inmemory";
    if (transport == "rabbitmq")
    {
        var cfg = config.MessageBus?.RabbitMq;
        x.UsingRabbitMq((ctx, bus) =>
        {
            bus.Host(cfg?.Host ?? "localhost", (ushort)(cfg?.Port ?? 5672),
                cfg?.VirtualHost ?? "/", h =>
            {
                h.Username(cfg?.Username ?? "guest");
                h.Password(cfg?.Password ?? "guest");
            });
            bus.ConfigureEndpoints(ctx);
        });
    }
    else x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
});

builder.Services.AddSingleton<IBackfillJobManager, BackfillJobManager>();
builder.Services.AddSingleton<IHistoricalDataProvider, CompositeHistoricalDataProvider>();
builder.Services.AddSingleton<HistoricalMetrics>();
builder.Services.AddHostedService<BackfillWorkerService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.UseHttpMetrics();
app.MapMetrics();
app.MapControllers();

app.MapGet("/status", (HistoricalMetrics m, IBackfillJobManager mgr) => Results.Ok(new
{
    service = "DataIngestion.HistoricalService",
    activeJobs = mgr.GetActiveJobCount(),
    completedJobs = m.CompletedJobs,
    totalRecords = m.TotalRecordsIngested
}));

Log.Information("Historical Data Service starting on port {Port}", config.HttpPort);
try { await app.RunAsync(); }
catch (Exception ex) { Log.Fatal(ex, "Historical Service terminated"); }
finally { await Log.CloseAndFlushAsync(); }
