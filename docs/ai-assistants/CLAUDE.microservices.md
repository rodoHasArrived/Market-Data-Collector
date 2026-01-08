# CLAUDE.microservices.md - Microservices Architecture Guide

This document provides guidance for AI assistants working with the microservices architecture in Market Data Collector.

---

## Architecture Overview

For high-throughput deployments, the system can be deployed as a set of microservices communicating via MassTransit message bus.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            API Gateway (Port 5000)                       │
│  ├── Request routing and rate limiting                                  │
│  ├── Provider connection management                                     │
│  └── Subscription management                                            │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │ MassTransit
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
┌───────▼───────┐         ┌─────────▼─────────┐       ┌─────────▼─────────┐
│ Trade Service │         │ OrderBook Service │       │ Quote Service     │
│ (Port 5001)   │         │ (Port 5002)       │       │ (Port 5003)       │
└───────────────┘         └───────────────────┘       └───────────────────┘
        │                           │                           │
        └───────────────────────────┼───────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
┌───────▼───────────┐     ┌─────────▼─────────┐     ┌───────────▼───────┐
│ Historical Service│     │ Validation Service│     │     Storage       │
│ (Port 5004)       │     │ (Port 5005)       │     │   (JSONL/DB)      │
└───────────────────┘     └───────────────────┘     └───────────────────┘
```

---

## Service Projects

| Service | Port | Project Location | Purpose |
|---------|------|------------------|---------|
| Gateway | 5000 | `src/Microservices/Gateway/` | API entry, routing, rate limiting |
| Trade | 5001 | `src/Microservices/TradeIngestion/` | Trade processing, sequence validation |
| Quote | 5002 | `src/Microservices/QuoteIngestion/` | BBO/NBBO, spread calculation |
| OrderBook | 5003 | `src/Microservices/OrderBookIngestion/` | L2 depth, integrity checking |
| Historical | 5004 | `src/Microservices/HistoricalDataIngestion/` | Backfill, gap repair |
| Validation | 5005 | `src/Microservices/DataValidation/` | Quality rules, alerting |
| Shared | - | `src/Microservices/Shared/` | Shared contracts |

---

## Shared Contracts

### Location
`src/Microservices/Shared/DataIngestion.Contracts.csproj`

### Message Contracts

```csharp
// Trade messages
public interface ITradeReceived
{
    string Symbol { get; }
    decimal Price { get; }
    long Size { get; }
    int AggressorSide { get; }
    long SequenceNumber { get; }
    DateTimeOffset Timestamp { get; }
    string? StreamId { get; }
    string? Venue { get; }
}

public interface ITradeValidated
{
    string Symbol { get; }
    decimal Price { get; }
    long Size { get; }
    int AggressorSide { get; }
    long SequenceNumber { get; }
    DateTimeOffset Timestamp { get; }
    bool IsValid { get; }
    IReadOnlyList<string>? ValidationErrors { get; }
}

// Quote messages
public interface IQuoteReceived
{
    string Symbol { get; }
    decimal BidPrice { get; }
    long BidSize { get; }
    decimal AskPrice { get; }
    long AskSize { get; }
    DateTimeOffset Timestamp { get; }
}

public interface IQuoteEnriched
{
    string Symbol { get; }
    decimal BidPrice { get; }
    long BidSize { get; }
    decimal AskPrice { get; }
    long AskSize { get; }
    decimal? MidPrice { get; }
    decimal? Spread { get; }
    decimal? SpreadBps { get; }
    decimal? Imbalance { get; }
    DateTimeOffset Timestamp { get; }
}

// Order book messages
public interface IDepthUpdateReceived
{
    string Symbol { get; }
    int Side { get; }
    int Level { get; }
    int Operation { get; }
    decimal Price { get; }
    long Size { get; }
    DateTimeOffset Timestamp { get; }
}

public interface IOrderBookSnapshot
{
    string Symbol { get; }
    IReadOnlyList<OrderBookLevel> Bids { get; }
    IReadOnlyList<OrderBookLevel> Asks { get; }
    decimal? MidPrice { get; }
    decimal? Microprice { get; }
    DateTimeOffset Timestamp { get; }
}

// Integrity messages
public interface IIntegrityEventOccurred
{
    string Symbol { get; }
    string EventType { get; }
    string Severity { get; }
    string Description { get; }
    DateTimeOffset Timestamp { get; }
}

// Backfill messages
public interface IBackfillRequested
{
    string Symbol { get; }
    DateTime StartDate { get; }
    DateTime EndDate { get; }
    string Provider { get; }
    int Priority { get; }
}

public interface IBackfillCompleted
{
    string Symbol { get; }
    DateTime StartDate { get; }
    DateTime EndDate { get; }
    string Provider { get; }
    int RecordCount { get; }
    bool Success { get; }
    string? ErrorMessage { get; }
}
```

---

## Gateway Service

### Purpose
- Entry point for all external requests
- Provider connection management
- Request routing and rate limiting
- Subscription management

### Key Files
```
src/Microservices/Gateway/
├── Program.cs
├── DataIngestion.Gateway.csproj
├── Controllers/
│   ├── SubscriptionController.cs
│   ├── BackfillController.cs
│   └── StatusController.cs
├── Services/
│   ├── ConnectionManager.cs
│   └── SubscriptionManager.cs
└── appsettings.json
```

### Example Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISubscriptionManager _subscriptionManager;

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        await _subscriptionManager.SubscribeAsync(request.Symbol, request.DataTypes);

        await _publishEndpoint.Publish<ISubscriptionChanged>(new
        {
            Symbol = request.Symbol,
            Action = "subscribe",
            DataTypes = request.DataTypes,
            Timestamp = DateTimeOffset.UtcNow
        });

        return Ok(new { message = $"Subscribed to {request.Symbol}" });
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        await _subscriptionManager.UnsubscribeAsync(request.Symbol);
        return Ok();
    }
}
```

---

## Trade Service

### Purpose
- High-throughput trade processing
- Sequence validation
- Order flow statistics
- Aggressor inference

### Key Files
```
src/Microservices/TradeIngestion/
├── Program.cs
├── DataIngestion.TradeService.csproj
├── Consumers/
│   └── TradeReceivedConsumer.cs
├── Services/
│   ├── TradeProcessor.cs
│   └── SequenceValidator.cs
└── appsettings.json
```

### Consumer Example

```csharp
public class TradeReceivedConsumer : IConsumer<ITradeReceived>
{
    private readonly ITradeProcessor _processor;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<TradeReceivedConsumer> _logger;

    public async Task Consume(ConsumeContext<ITradeReceived> context)
    {
        var trade = context.Message;

        _logger.LogDebug("Processing trade for {Symbol} at {Price}",
            trade.Symbol, trade.Price);

        // Validate sequence
        var validation = await _processor.ValidateAsync(trade);

        if (!validation.IsValid)
        {
            await _publishEndpoint.Publish<IIntegrityEventOccurred>(new
            {
                Symbol = trade.Symbol,
                EventType = validation.ErrorType,
                Severity = "Warning",
                Description = validation.ErrorMessage,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        // Publish validated trade
        await _publishEndpoint.Publish<ITradeValidated>(new
        {
            trade.Symbol,
            trade.Price,
            trade.Size,
            trade.AggressorSide,
            trade.SequenceNumber,
            trade.Timestamp,
            IsValid = validation.IsValid,
            ValidationErrors = validation.Errors
        });
    }
}
```

---

## Quote Service

### Purpose
- BBO/NBBO quote tracking
- Spread calculation
- Crossed/locked market detection
- Quote state caching

### Consumer Example

```csharp
public class QuoteReceivedConsumer : IConsumer<IQuoteReceived>
{
    private readonly IQuoteStateStore _stateStore;
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task Consume(ConsumeContext<IQuoteReceived> context)
    {
        var quote = context.Message;

        // Detect crossed market
        if (quote.BidPrice >= quote.AskPrice)
        {
            await _publishEndpoint.Publish<IIntegrityEventOccurred>(new
            {
                Symbol = quote.Symbol,
                EventType = "CrossedMarket",
                Severity = "Warning",
                Description = $"Bid {quote.BidPrice} >= Ask {quote.AskPrice}",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        // Calculate enriched fields
        var midPrice = (quote.BidPrice + quote.AskPrice) / 2;
        var spread = quote.AskPrice - quote.BidPrice;
        var spreadBps = spread / midPrice * 10000m;
        var imbalance = (decimal)(quote.BidSize - quote.AskSize) /
                        (quote.BidSize + quote.AskSize);

        // Update state store
        await _stateStore.UpdateAsync(quote.Symbol, quote);

        // Publish enriched quote
        await _publishEndpoint.Publish<IQuoteEnriched>(new
        {
            quote.Symbol,
            quote.BidPrice,
            quote.BidSize,
            quote.AskPrice,
            quote.AskSize,
            MidPrice = midPrice,
            Spread = spread,
            SpreadBps = spreadBps,
            Imbalance = imbalance,
            quote.Timestamp
        });
    }
}
```

---

## OrderBook Service

### Purpose
- L2 order book state management
- Snapshot/delta processing
- Integrity checking
- Book freeze on violations

### Consumer Example

```csharp
public class DepthUpdateConsumer : IConsumer<IDepthUpdateReceived>
{
    private readonly IOrderBookManager _bookManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task Consume(ConsumeContext<IDepthUpdateReceived> context)
    {
        var update = context.Message;

        try
        {
            // Apply update to order book
            var result = await _bookManager.ApplyUpdateAsync(
                update.Symbol,
                update.Side,
                update.Level,
                update.Operation,
                update.Price,
                update.Size);

            if (result.IntegrityViolation)
            {
                await _publishEndpoint.Publish<IIntegrityEventOccurred>(new
                {
                    Symbol = update.Symbol,
                    EventType = "BookIntegrityViolation",
                    Severity = "Error",
                    Description = result.ViolationDescription,
                    Timestamp = DateTimeOffset.UtcNow
                });

                // Freeze the book - requires manual reset
                await _bookManager.FreezeAsync(update.Symbol);
            }

            // Publish snapshot periodically or on significant changes
            if (result.ShouldPublishSnapshot)
            {
                var snapshot = await _bookManager.GetSnapshotAsync(update.Symbol);
                await _publishEndpoint.Publish<IOrderBookSnapshot>(snapshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing depth update for {Symbol}", update.Symbol);
            throw;
        }
    }
}
```

---

## Historical Service

### Purpose
- Backfill job management
- Multi-provider coordination
- Gap detection and repair
- Progress tracking

### Consumer Example

```csharp
public class BackfillRequestedConsumer : IConsumer<IBackfillRequested>
{
    private readonly IBackfillJobManager _jobManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task Consume(ConsumeContext<IBackfillRequested> context)
    {
        var request = context.Message;

        var jobId = await _jobManager.EnqueueAsync(new BackfillJob
        {
            Symbol = request.Symbol,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Provider = request.Provider,
            Priority = request.Priority
        });

        _logger.LogInformation(
            "Enqueued backfill job {JobId} for {Symbol} from {Start} to {End}",
            jobId, request.Symbol, request.StartDate, request.EndDate);

        // Job completion is published by BackfillWorkerService
    }
}

// Background worker that processes the queue
public class BackfillWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(ct);
            if (job == null) continue;

            try
            {
                var provider = _providerFactory.GetProvider(job.Provider);
                var bars = await provider.GetHistoricalBarsAsync(
                    job.Symbol, job.StartDate, job.EndDate, BarTimeframe.Daily, ct);

                await _storage.WriteBarsAsync(bars, ct);

                await _publishEndpoint.Publish<IBackfillCompleted>(new
                {
                    job.Symbol,
                    job.StartDate,
                    job.EndDate,
                    job.Provider,
                    RecordCount = bars.Count,
                    Success = true,
                    ErrorMessage = (string?)null
                });
            }
            catch (Exception ex)
            {
                await _publishEndpoint.Publish<IBackfillCompleted>(new
                {
                    job.Symbol,
                    job.StartDate,
                    job.EndDate,
                    job.Provider,
                    RecordCount = 0,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
```

---

## Validation Service

### Purpose
- Data quality rules
- Metrics aggregation
- Alert generation
- Quality scoring

### Consumer Example

```csharp
public class TradeValidatedConsumer : IConsumer<ITradeValidated>
{
    private readonly IQualityMetrics _metrics;
    private readonly IAlertManager _alertManager;

    public async Task Consume(ConsumeContext<ITradeValidated> context)
    {
        var trade = context.Message;

        // Update metrics
        _metrics.RecordTrade(trade.Symbol, trade.IsValid);

        if (!trade.IsValid)
        {
            _metrics.RecordValidationError(trade.Symbol, trade.ValidationErrors);

            // Check if error rate exceeds threshold
            var errorRate = _metrics.GetErrorRate(trade.Symbol, TimeSpan.FromMinutes(5));
            if (errorRate > 0.05)  // 5% threshold
            {
                await _alertManager.RaiseAlertAsync(new Alert
                {
                    Symbol = trade.Symbol,
                    AlertType = "HighErrorRate",
                    Severity = "Warning",
                    Message = $"Error rate {errorRate:P2} exceeds 5% threshold",
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
        }
    }
}
```

---

## MassTransit Configuration

### Basic Setup

```csharp
// In Program.cs
builder.Services.AddMassTransit(x =>
{
    // Add consumers from this assembly
    x.AddConsumers(typeof(Program).Assembly);

    // Configure transport
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Configure endpoints
        cfg.ConfigureEndpoints(context);
    });
});
```

### With Azure Service Bus

```csharp
x.UsingAzureServiceBus((context, cfg) =>
{
    cfg.Host(connectionString);
    cfg.ConfigureEndpoints(context);
});
```

### Retry Configuration

```csharp
cfg.UseMessageRetry(r =>
{
    r.Exponential(
        retryLimit: 5,
        minInterval: TimeSpan.FromSeconds(1),
        maxInterval: TimeSpan.FromSeconds(30),
        intervalDelta: TimeSpan.FromSeconds(2));

    r.Ignore<ValidationException>();
});
```

### Circuit Breaker

```csharp
cfg.UseCircuitBreaker(cb =>
{
    cb.TripThreshold = 15;
    cb.ActiveThreshold = 10;
    cb.ResetInterval = TimeSpan.FromMinutes(5);
});
```

---

## Running Microservices

### Development (Single Machine)

```bash
# Start RabbitMQ
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management

# Start each service (in separate terminals)
dotnet run --project src/Microservices/Gateway
dotnet run --project src/Microservices/TradeIngestion
dotnet run --project src/Microservices/QuoteIngestion
dotnet run --project src/Microservices/OrderBookIngestion
dotnet run --project src/Microservices/HistoricalDataIngestion
dotnet run --project src/Microservices/DataValidation
```

### Docker Compose

```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"

  gateway:
    build:
      context: .
      dockerfile: src/Microservices/Gateway/Dockerfile
    ports:
      - "5000:5000"
    depends_on:
      - rabbitmq
    environment:
      - RabbitMQ__Host=rabbitmq

  trade-service:
    build:
      context: .
      dockerfile: src/Microservices/TradeIngestion/Dockerfile
    depends_on:
      - rabbitmq
    environment:
      - RabbitMQ__Host=rabbitmq

  # ... other services
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: trade-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: trade-service
  template:
    metadata:
      labels:
        app: trade-service
    spec:
      containers:
      - name: trade-service
        image: marketdatacollector/trade-service:latest
        ports:
        - containerPort: 5001
        env:
        - name: RabbitMQ__Host
          value: rabbitmq-service
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5001
          initialDelaySeconds: 10
          periodSeconds: 5
```

---

## Monitoring

### Health Endpoints

Each service exposes:
- `/health` - Overall health
- `/ready` - Readiness check
- `/live` - Liveness check

### Metrics

```csharp
// In each service
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Metrics exposed:
// - masstransit_receive_total
// - masstransit_receive_fault_total
// - masstransit_consume_total
// - masstransit_consume_fault_total
// - masstransit_publish_total
// - masstransit_send_total
```

### Distributed Tracing

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddMassTransitInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

---

## Testing Microservices

### Integration Tests

```csharp
public class TradeServiceIntegrationTests : IAsyncLifetime
{
    private readonly ITestHarness _harness;

    public async Task InitializeAsync()
    {
        _harness = new InMemoryTestHarness();
        _harness.Consumer<TradeReceivedConsumer>();
        await _harness.Start();
    }

    [Fact]
    public async Task TradeReceived_PublishesValidatedTrade()
    {
        // Arrange
        await _harness.InputQueueSendEndpoint.Send<ITradeReceived>(new
        {
            Symbol = "AAPL",
            Price = 150.0m,
            Size = 100L,
            AggressorSide = 1,
            SequenceNumber = 1L,
            Timestamp = DateTimeOffset.UtcNow
        });

        // Assert
        Assert.True(await _harness.Consumed.Any<ITradeReceived>());
        Assert.True(await _harness.Published.Any<ITradeValidated>());
    }

    public Task DisposeAsync() => _harness.Stop();
}
```

---

## Related Documentation

- [docs/architecture/overview.md](MarketDataCollector/docs/architecture/overview.md) - System architecture
- [MassTransit Documentation](https://masstransit.io/documentation/concepts) - Message bus docs
- [docs/guides/operator-runbook.md](MarketDataCollector/docs/guides/operator-runbook.md) - Operations guide

---

*Last Updated: 2026-01-08*
