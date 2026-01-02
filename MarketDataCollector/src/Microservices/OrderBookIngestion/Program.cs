using DataIngestion.OrderBookService.Configuration;
using DataIngestion.OrderBookService.Consumers;
using DataIngestion.OrderBookService.Services;
using MassTransit;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "DataIngestion.OrderBookService")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Load configuration
var serviceConfig = builder.Configuration.GetSection("OrderBookService").Get<OrderBookServiceConfig>()
    ?? new OrderBookServiceConfig();
builder.Services.AddSingleton(serviceConfig);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<OrderBookServiceHealthCheck>("orderbook-service");

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<OrderBookSnapshotConsumer>();
    x.AddConsumer<OrderBookUpdateConsumer>();

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
                cfg.ConfigureEndpoints(context);
            });
            break;

        default:
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            break;
    }
});

// Add services
builder.Services.AddSingleton<IOrderBookManager, OrderBookManager>();
builder.Services.AddSingleton<IOrderBookStorage, JsonlOrderBookStorage>();
builder.Services.AddSingleton<OrderBookMetrics>();
builder.Services.AddHostedService<OrderBookSnapshotService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapHealthChecks("/live", new() { Predicate = _ => false });
app.MapHealthChecks("/ready");
app.UseHttpMetrics();
app.MapMetrics();

app.MapGet("/status", (OrderBookMetrics metrics, IOrderBookManager manager) => Results.Ok(new
{
    service = "DataIngestion.OrderBookService",
    status = "running",
    metrics = new
    {
        snapshotsProcessed = metrics.SnapshotsProcessed,
        updatesProcessed = metrics.UpdatesProcessed,
        activeBooks = manager.GetActiveBookCount(),
        integrityErrors = metrics.IntegrityErrors
    }
}));

app.MapGet("/books/{symbol}", (string symbol, IOrderBookManager manager) =>
{
    var book = manager.GetOrderBook(symbol);
    return book != null ? Results.Ok(book) : Results.NotFound();
});

Log.Information("OrderBook Ingestion Service starting on port {Port}", serviceConfig.HttpPort);

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderBook Ingestion Service terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
