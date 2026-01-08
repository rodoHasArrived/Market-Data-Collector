using DataIngestion.TradeService.Configuration;
using DataIngestion.TradeService.Consumers;
using DataIngestion.TradeService.Services;
using MassTransit;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "DataIngestion.TradeService")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Load configuration
var serviceConfig = builder.Configuration.GetSection("TradeService").Get<TradeServiceConfig>()
    ?? new TradeServiceConfig();
builder.Services.AddSingleton(serviceConfig);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<TradeServiceHealthCheck>("trade-service");

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Register consumers
    x.AddConsumer<RawTradeConsumer>();
    x.AddConsumer<TradesBatchConsumer>();

    var transport = serviceConfig.MessageBus?.Transport?.ToLowerInvariant() ?? "inmemory";

    switch (transport)
    {
        case "rabbitmq":
            var rabbitConfig = serviceConfig.MessageBus?.RabbitMq;
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig?.Host ?? "localhost", (ushort)(rabbitConfig?.Port ?? 5672),
                    rabbitConfig?.VirtualHost ?? "/", h =>
                {
                    h.Username(rabbitConfig?.Username ?? "guest");
                    h.Password(rabbitConfig?.Password ?? "guest");
                });

                cfg.PrefetchCount = serviceConfig.MessageBus?.PrefetchCount ?? 32;
                cfg.ConfigureEndpoints(context);
            });
            break;

        default:
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
            break;
    }
});

// Add services
builder.Services.AddSingleton<ITradeProcessor, TradeProcessor>();
builder.Services.AddSingleton<ITradeStorage, JsonlTradeStorage>();
builder.Services.AddSingleton<ITradeValidator, TradeValidator>();
builder.Services.AddSingleton<TradeMetrics>();
builder.Services.AddHostedService<TradeFlushService>();

var app = builder.Build();

// Configure middleware
app.UseSerilogRequestLogging();

// Health endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/live", new() { Predicate = _ => false });
app.MapHealthChecks("/ready");

// Prometheus metrics
app.UseHttpMetrics();
app.MapMetrics();

// Status endpoint
app.MapGet("/status", (TradeMetrics metrics) => Results.Ok(new
{
    service = "DataIngestion.TradeService",
    status = "running",
    metrics = new
    {
        tradesProcessed = metrics.TradesProcessed,
        tradesPerSecond = metrics.TradesPerSecond,
        averageLatencyMs = metrics.AverageLatencyMs,
        errorCount = metrics.ErrorCount
    }
}));

Log.Information("Trade Ingestion Service starting on port {Port}", serviceConfig.HttpPort);

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Trade Ingestion Service terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
