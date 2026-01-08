# ADR-003: Microservices Decomposition

**Status:** Accepted
**Date:** 2024-09-10
**Deciders:** Core Team

## Context

As the Market Data Collector matured, scaling challenges emerged:

1. **Resource contention**: Trade processing and quote processing compete for CPU
2. **Deployment coupling**: Bug in order book logic requires full redeployment
3. **Scaling granularity**: Cannot scale quote ingestion independently from trades
4. **Team ownership**: Multiple teams want to own different data streams

The monolithic architecture, while simpler, cannot meet these requirements.

## Decision

Decompose the data ingestion layer into specialized microservices while keeping the monolith available for simpler deployments:

| Service | Port | Responsibility |
|---------|------|----------------|
| Gateway | 5000 | API routing, auth, rate limiting |
| TradeIngestion | 5001 | Tick-by-tick trade processing |
| QuoteIngestion | 5002 | BBO quote processing |
| OrderBookIngestion | 5003 | L2 order book maintenance |
| HistoricalDataIngestion | 5004 | Backfill orchestration |
| DataValidation | 5005 | Cross-stream validation |

Services communicate via MassTransit (RabbitMQ/Azure Service Bus).

## Implementation Links

<!-- These links are verified by the build process -->

| Component | Location | Purpose |
|-----------|----------|---------|
| Gateway Service | `src/Microservices/Gateway/` | API entry point |
| Trade Service | `src/Microservices/TradeIngestion/` | Trade processing |
| Quote Service | `src/Microservices/QuoteIngestion/` | Quote processing |
| OrderBook Service | `src/Microservices/OrderBookIngestion/` | Order book maintenance |
| Historical Service | `src/Microservices/HistoricalDataIngestion/` | Backfill service |
| Validation Service | `src/Microservices/DataValidation/` | Data quality |
| Shared Contracts | `src/Microservices/Shared/Contracts/` | Message contracts |
| MassTransit Config | `src/Microservices/Shared/Messaging/` | Bus configuration |
| Docker Compose | `deploy/docker/docker-compose.microservices.yml` | Orchestration |
| Service Tests | `tests/MarketDataCollector.Tests/Microservices/` | Integration tests |

## Rationale

### Bounded Contexts
Each service owns a specific data domain:
- **TradeIngestion**: Trade execution data, sequence validation
- **QuoteIngestion**: Best bid/offer, spread calculations
- **OrderBookIngestion**: Full L2 depth, imbalance metrics
- **HistoricalDataIngestion**: Backfill coordination, gap detection

### Communication Patterns
- **Commands**: Request/response for backfill triggers
- **Events**: Pub/sub for data flow (trade arrived, quote updated)
- **Sagas**: Long-running backfill orchestration

### Deployment Flexibility
```
┌─────────────────────────────────────────────────┐
│  Deployment Option 1: Monolith                  │
│  dotnet run --project MarketDataCollector       │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  Deployment Option 2: Microservices             │
│  docker-compose -f docker-compose.microservices │
└─────────────────────────────────────────────────┘
```

## Alternatives Considered

### Alternative 1: Modular Monolith

Logical separation within single process.

**Pros:**
- Simpler deployment
- No network overhead
- Easier debugging

**Cons:**
- Cannot scale independently
- Resource contention remains
- Single point of failure

**Why rejected:** Does not address scaling requirements.

### Alternative 2: Function-as-a-Service

Serverless functions per data type.

**Pros:**
- Maximum scalability
- Pay-per-use
- Zero ops

**Cons:**
- Cold start latency
- State management complexity
- Vendor lock-in

**Why rejected:** Latency-sensitive real-time data doesn't fit serverless model.

## Consequences

### Positive

- Independent scaling per data type
- Isolated failures (quote service down doesn't affect trades)
- Team autonomy
- Technology flexibility per service

### Negative

- Operational complexity
- Network latency overhead
- Distributed tracing required
- Message schema versioning

### Neutral

- Can run monolith or microservices based on needs
- Requires container orchestration for production

## Compliance

### Code Contracts

```csharp
// All services must implement health checks
public interface IHealthCheck
{
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);
}

// Message contracts are immutable records
public sealed record TradeArrived(
    string Symbol,
    decimal Price,
    decimal Volume,
    DateTime Timestamp,
    Guid CorrelationId);
```

### Service Requirements

- Health endpoint at `/health`
- Prometheus metrics at `/metrics`
- Structured logging with correlation IDs
- Graceful shutdown handling

### Runtime Verification

- `[ImplementsAdr("ADR-003")]` on service entry points
- Health check validation at startup
- Contract verification via MassTransit test harness

## References

- [Microservices Architecture](../architecture/overview.md#microservices)
- [CLAUDE.microservices.md](../ai-assistants/CLAUDE.microservices.md)
- [Docker Deployment](../../deploy/docker/README.md)

---

*Last Updated: 2026-01-08*
