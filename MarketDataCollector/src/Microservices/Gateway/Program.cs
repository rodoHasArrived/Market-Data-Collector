using DataIngestion.Gateway.Configuration;
using DataIngestion.Gateway.Middleware;
using DataIngestion.Gateway.Services;
using FluentValidation;
using MassTransit;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "DataIngestion.Gateway")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Load configuration
var gatewayConfig = builder.Configuration.GetSection("Gateway").Get<GatewayConfig>() ?? new GatewayConfig();
builder.Services.AddSingleton(gatewayConfig);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Data Ingestion Gateway API", Version = "v1" });
});

// Add validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<GatewayHealthCheck>("gateway");

// Configure rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    var transport = gatewayConfig.MessageBus?.Transport?.ToLowerInvariant() ?? "inmemory";

    switch (transport)
    {
        case "rabbitmq":
            var rabbitConfig = gatewayConfig.MessageBus?.RabbitMq;
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
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
            break;
    }
});

// Add gateway services
builder.Services.AddSingleton<IDataRouter, DataRouter>();
builder.Services.AddSingleton<IProviderManager, ProviderManager>();
builder.Services.AddSingleton<MetricsCollector>();

// Add HTTP client factory for downstream services
builder.Services.AddHttpClient("TradeIngestion", client =>
{
    client.BaseAddress = new Uri(gatewayConfig.Services?.TradeIngestionUrl ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("OrderBookIngestion", client =>
{
    client.BaseAddress = new Uri(gatewayConfig.Services?.OrderBookIngestionUrl ?? "http://localhost:5002");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("QuoteIngestion", client =>
{
    client.BaseAddress = new Uri(gatewayConfig.Services?.QuoteIngestionUrl ?? "http://localhost:5003");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("HistoricalIngestion", client =>
{
    client.BaseAddress = new Uri(gatewayConfig.Services?.HistoricalIngestionUrl ?? "http://localhost:5004");
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseRouting();
app.UseAuthorization();

// Prometheus metrics
app.UseHttpMetrics();
app.MapMetrics();

// Health endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/live", new()
{
    Predicate = _ => false
});
app.MapHealthChecks("/ready");

app.MapControllers();

Log.Information("Data Ingestion Gateway starting on port {Port}", gatewayConfig.HttpPort);

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
