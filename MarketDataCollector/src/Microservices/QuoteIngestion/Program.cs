using DataIngestion.QuoteService.Configuration;
using DataIngestion.QuoteService.Consumers;
using DataIngestion.QuoteService.Services;
using MassTransit;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "DataIngestion.QuoteService")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var serviceConfig = builder.Configuration.GetSection("QuoteService").Get<QuoteServiceConfig>()
    ?? new QuoteServiceConfig();
builder.Services.AddSingleton(serviceConfig);

builder.Services.AddHealthChecks()
    .AddCheck<QuoteServiceHealthCheck>("quote-service");

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<QuoteConsumer>();
    x.AddConsumer<NbboConsumer>();

    var transport = serviceConfig.MessageBus?.Transport?.ToLowerInvariant() ?? "inmemory";
    if (transport == "rabbitmq")
    {
        var cfg = serviceConfig.MessageBus?.RabbitMq;
        x.UsingRabbitMq((context, bus) =>
        {
            bus.Host(cfg?.Host ?? "localhost", (ushort)(cfg?.Port ?? 5672),
                cfg?.VirtualHost ?? "/", h =>
            {
                h.Username(cfg?.Username ?? "guest");
                h.Password(cfg?.Password ?? "guest");
            });
            bus.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
});

builder.Services.AddSingleton<IQuoteProcessor, QuoteProcessor>();
builder.Services.AddSingleton<IQuoteStorage, JsonlQuoteStorage>();
builder.Services.AddSingleton<QuoteMetrics>();
builder.Services.AddHostedService<QuoteFlushService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapHealthChecks("/live", new() { Predicate = _ => false });
app.MapHealthChecks("/ready");
app.UseHttpMetrics();
app.MapMetrics();

app.MapGet("/status", (QuoteMetrics metrics) => Results.Ok(new
{
    service = "DataIngestion.QuoteService",
    status = "running",
    metrics = new
    {
        quotesProcessed = metrics.QuotesProcessed,
        quotesPerSecond = metrics.QuotesPerSecond,
        crossedQuotes = metrics.CrossedQuotes,
        lockedQuotes = metrics.LockedQuotes
    }
}));

app.MapGet("/quotes/{symbol}", (string symbol, IQuoteProcessor processor) =>
{
    var quote = processor.GetLatestQuote(symbol);
    return quote != null ? Results.Ok(quote) : Results.NotFound();
});

Log.Information("Quote Ingestion Service starting on port {Port}", serviceConfig.HttpPort);

try { await app.RunAsync(); }
catch (Exception ex) { Log.Fatal(ex, "Quote Ingestion Service terminated unexpectedly"); }
finally { await Log.CloseAndFlushAsync(); }
