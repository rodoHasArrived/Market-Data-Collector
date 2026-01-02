using DataIngestion.ValidationService.Configuration;
using DataIngestion.ValidationService.Consumers;
using DataIngestion.ValidationService.Services;
using MassTransit;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "DataIngestion.ValidationService")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var config = builder.Configuration.GetSection("ValidationService").Get<ValidationServiceConfig>()
    ?? new ValidationServiceConfig();
builder.Services.AddSingleton(config);

builder.Services.AddHealthChecks();
builder.Services.AddControllers();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<ValidationRequestConsumer>();

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

builder.Services.AddSingleton<IDataValidator, DataValidator>();
builder.Services.AddSingleton<IQualityMetricsAggregator, QualityMetricsAggregator>();
builder.Services.AddSingleton<ValidationMetrics>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.UseHttpMetrics();
app.MapMetrics();
app.MapControllers();

app.MapGet("/status", (ValidationMetrics m) => Results.Ok(new
{
    service = "DataIngestion.ValidationService",
    validationsPerformed = m.ValidationsPerformed,
    validRecords = m.ValidRecords,
    invalidRecords = m.InvalidRecords,
    validityRate = m.ValidityRate
}));

Log.Information("Validation Service starting on port {Port}", config.HttpPort);
try { await app.RunAsync(); }
catch (Exception ex) { Log.Fatal(ex, "Validation Service terminated"); }
finally { await Log.CloseAndFlushAsync(); }
